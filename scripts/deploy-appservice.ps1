param(
	[Parameter(Mandatory = $true)]
	[string]$SubscriptionId,

	[Parameter(Mandatory = $true)]
	[string]$ResourceGroupName,

	[Parameter(Mandatory = $true)]
	[string]$Location,

	[Parameter(Mandatory = $true)]
	[string]$AppServicePlanName,

	[Parameter(Mandatory = $true)]
	[string]$WebAppName,

	[string]$EnvFile = "./.env.prod",
	[string]$ProjectPath = "./sekura/sekura.csproj",
	[string]$Configuration = "Release",
	[string]$OutputDirectory = "./artifacts/deploy/appservice",
	[string]$Sku = "B1",
	[string]$Runtime = "DOTNETCORE:10.0",
	[string]$AppEnvironment = "Production",
	[int]$AppServicePort = 8080,
	[string]$StartupCommand = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Az {
	param(
		[Parameter(Mandatory = $true)]
		[string[]]$Args
	)

	$output = & az @Args
	$code = $LASTEXITCODE
	if ($code -ne 0) {
		throw "Azure CLI failed (exit $code): az $($Args -join ' ')"
	}

	return $output
}

function Invoke-CommandStrict {
	param(
		[Parameter(Mandatory = $true)]
		[string]$FileName,

		[Parameter(Mandatory = $true)]
		[string[]]$Arguments
	)

	& $FileName @Arguments
	$code = $LASTEXITCODE
	if ($code -ne 0) {
		throw "Command failed (exit $code): $FileName $($Arguments -join ' ')"
	}
}

# Env file lines are KEY=VALUE and read literally (no expansion), so '$' in
# password hashes needs no quoting or escaping. Optional surrounding single
# or double quotes are stripped.
function ConvertFrom-DotEnvFile {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path
	)

	$settings = [ordered]@{}
	foreach ($line in Get-Content -Path $Path) {
		$trimmed = $line.Trim()
		if ($trimmed -eq "" -or $trimmed.StartsWith("#")) {
			continue
		}

		$separatorIndex = $trimmed.IndexOf("=")
		if ($separatorIndex -lt 1) {
			Write-Warning "Ignoring malformed line in '$Path' (expected KEY=VALUE): $trimmed"
			continue
		}

		$key = $trimmed.Substring(0, $separatorIndex).Trim()
		$value = $trimmed.Substring($separatorIndex + 1)
		if (($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) -or
			($value.StartsWith("'") -and $value.EndsWith("'") -and $value.Length -ge 2)) {
			$value = $value.Substring(1, $value.Length - 2)
		}

		$settings[$key] = $value
	}

	return $settings
}

function Get-ProjectAssemblyName {
	param(
		[Parameter(Mandatory = $true)]
		[string]$ProjectFile
	)

	[xml]$projectXml = Get-Content -Path $ProjectFile -Raw
	foreach ($propertyGroup in @($projectXml.Project.PropertyGroup)) {
		$assemblyNameNode = $propertyGroup.SelectSingleNode("AssemblyName")
		if ($null -eq $assemblyNameNode) {
			continue
		}

		$assemblyName = [string]$assemblyNameNode.InnerText
		if (-not [string]::IsNullOrWhiteSpace($assemblyName)) {
			return $assemblyName
		}
	}

	return [IO.Path]::GetFileNameWithoutExtension($ProjectFile)
}

function Get-AppServiceAppSettings {
	param(
		[Parameter(Mandatory = $true)]
		[string]$ResourceGroupName,

		[Parameter(Mandatory = $true)]
		[string]$WebAppName
	)

	$json = Invoke-Az -Args @(
		"webapp", "config", "appsettings", "list",
		"--resource-group", $ResourceGroupName,
		"--name", $WebAppName,
		"--output", "json"
	)

	$result = @{}
	$items = ($json -join "`n") | ConvertFrom-Json
	if ($null -eq $items) {
		return $result
	}

	foreach ($item in @($items)) {
		if ($null -eq $item) {
			continue
		}

		$result[[string]$item.name] = [string]$item.value
	}

	return $result
}

if (-not (Get-Command az -ErrorAction SilentlyContinue)) {
	throw "Azure CLI is not installed. Install it first: https://aka.ms/installazurecliwindows"
}

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
	throw "dotnet SDK is not installed or not available in PATH."
}

if ($AppServicePort -le 0) {
	throw "AppServicePort must be greater than 0."
}

if ([string]::IsNullOrWhiteSpace($EnvFile)) {
	throw "EnvFile is required."
}

if (-not (Test-Path $EnvFile)) {
	throw "Env file '$EnvFile' was not found. Create it from the template first: Copy-Item .env.template $EnvFile"
}

$settingsPath = (Resolve-Path $EnvFile).Path
Write-Host "Loading settings from '$settingsPath'..." -ForegroundColor Cyan
$settingsObject = ConvertFrom-DotEnvFile -Path $settingsPath

try {
	Invoke-Az -Args @("account", "show", "--output", "none") | Out-Null
}
catch {
	throw "Azure CLI is not authenticated. Run 'az login' first."
}

Invoke-Az -Args @("account", "set", "--subscription", $SubscriptionId) | Out-Null

$projectFile = (Resolve-Path $ProjectPath).Path
$assemblyName = Get-ProjectAssemblyName -ProjectFile $projectFile
if ([string]::IsNullOrWhiteSpace($StartupCommand)) {
	$StartupCommand = "dotnet $assemblyName.dll"
}

$outputRoot = (Resolve-Path ".").Path
$deployRoot = Join-Path $outputRoot $OutputDirectory
$publishDir = Join-Path $deployRoot "publish"
$zipPath = Join-Path $deployRoot "$WebAppName.zip"

if (Test-Path $deployRoot) {
	Remove-Item -Path $deployRoot -Recurse -Force
}

New-Item -Path $deployRoot -ItemType Directory -Force | Out-Null

Write-Host "Creating/updating resource group '$ResourceGroupName' in '$Location'..." -ForegroundColor Cyan
Invoke-Az -Args @("group", "create", "--name", $ResourceGroupName, "--location", $Location, "--output", "none") | Out-Null

Write-Host "Creating/updating App Service plan '$AppServicePlanName' (SKU: $Sku, Linux)..." -ForegroundColor Cyan
Invoke-Az -Args @(
	"appservice", "plan", "create",
	"--name", $AppServicePlanName,
	"--resource-group", $ResourceGroupName,
	"--location", $Location,
	"--sku", $Sku,
	"--is-linux",
	"--output", "none"
) | Out-Null

$webAppExists = $false
try {
	$existingName = (Invoke-Az -Args @("webapp", "show", "--resource-group", $ResourceGroupName, "--name", $WebAppName, "--query", "name", "--output", "tsv")).Trim()
	$webAppExists = -not [string]::IsNullOrWhiteSpace($existingName)
}
catch {
	$webAppExists = $false
}

if (-not $webAppExists) {
	Write-Host "Creating web app '$WebAppName'..." -ForegroundColor Cyan
	Invoke-Az -Args @(
		"webapp", "create",
		"--resource-group", $ResourceGroupName,
		"--plan", $AppServicePlanName,
		"--name", $WebAppName,
		"--runtime", $Runtime,
		"--https-only", "true",
		"--output", "none"
	) | Out-Null
}
else {
	Write-Host "Web app '$WebAppName' already exists. Reusing existing app." -ForegroundColor Yellow
}

Write-Host "Applying secure web app defaults..." -ForegroundColor Cyan
Invoke-Az -Args @(
	"webapp", "config", "set",
	"--resource-group", $ResourceGroupName,
	"--name", $WebAppName,
	"--always-on", "true",
	"--http20-enabled", "true",
	"--min-tls-version", "1.2",
	"--ftps-state", "Disabled",
	"--startup-file", $StartupCommand,
	"--output", "none"
) | Out-Null

Write-Host "Configuring App Service application settings..." -ForegroundColor Cyan
$managedSettingExactNames = @(
	"ASPNETCORE_ENVIRONMENT",
	"ASPNETCORE_URLS",
	"WEBSITES_PORT",
	"Kestrel__Endpoints__Http__Url",
	"Logging__LogLevel__Microsoft__AspNetCore"
)
$managedSettingPrefixes = @("Kestrel__Endpoints__")

foreach ($key in @($settingsObject.Keys)) {
	$separatorIndex = ([string]$key).IndexOf("__")
	if ($separatorIndex -gt 0) {
		$managedSettingPrefixes += ([string]$key).Substring(0, $separatorIndex) + "__"
	}
	else {
		$managedSettingExactNames += [string]$key
	}
}
$managedSettingPrefixes = @($managedSettingPrefixes | Select-Object -Unique)

foreach ($key in @($settingsObject.Keys)) {
	if ([string]$key -like "Kestrel__Endpoints__*") {
		$settingsObject.Remove($key)
	}
}

$settingsObject["ASPNETCORE_ENVIRONMENT"] = $AppEnvironment
$settingsObject["ASPNETCORE_URLS"] = "http://+:$AppServicePort"
$settingsObject["WEBSITES_PORT"] = [string]$AppServicePort
$settingsObject["Kestrel__Endpoints__Http__Url"] = "http://+:$AppServicePort"

$settingsFilePath = Join-Path $deployRoot "appsettings.deploy.json"
$settingsFileItems = @(
	foreach ($entry in $settingsObject.GetEnumerator()) {
		[ordered]@{
			name = [string]$entry.Key
			value = [string]$entry.Value
			slotSetting = $false
		}
	}
)
$settingsFileItems | ConvertTo-Json -Depth 4 | Set-Content -Path $settingsFilePath -Encoding utf8

$existingSettings = Get-AppServiceAppSettings -ResourceGroupName $ResourceGroupName -WebAppName $WebAppName
$staleManagedSettingNames = @()
foreach ($existingSettingName in $existingSettings.Keys) {
	$isManagedSetting = $managedSettingExactNames -contains $existingSettingName
	foreach ($managedSettingPrefix in $managedSettingPrefixes) {
		if ($existingSettingName.StartsWith($managedSettingPrefix, [StringComparison]::Ordinal)) {
			$isManagedSetting = $true
			break
		}
	}

	if (-not $settingsObject.Contains($existingSettingName) -and $isManagedSetting) {
		$staleManagedSettingNames += $existingSettingName
	}
}

if ($staleManagedSettingNames.Count -gt 0) {
	Write-Host "Removing stale script-managed App Service application settings..." -ForegroundColor Cyan
	Invoke-Az -Args (@(
		"webapp", "config", "appsettings", "delete",
		"--resource-group", $ResourceGroupName,
		"--name", $WebAppName,
		"--setting-names"
	) + $staleManagedSettingNames + @("--output", "none")) | Out-Null
}

Invoke-Az -Args @(
	"webapp", "config", "appsettings", "set",
	"--resource-group", $ResourceGroupName,
	"--name", $WebAppName,
	"--settings", "@$settingsFilePath",
	"--output", "none"
) | Out-Null

Write-Host "Verifying App Service application settings..." -ForegroundColor Cyan
$appliedSettings = Get-AppServiceAppSettings -ResourceGroupName $ResourceGroupName -WebAppName $WebAppName
$settingsVerificationFailures = @()
foreach ($entry in $settingsObject.GetEnumerator()) {
	$key = [string]$entry.Key
	$expectedValue = [string]$entry.Value

	if (-not $appliedSettings.ContainsKey($key)) {
		$settingsVerificationFailures += "$key (missing)"
		continue
	}

	if ([string]$appliedSettings[$key] -ne $expectedValue) {
		$settingsVerificationFailures += "$key (mismatch)"
	}
}

if ($settingsVerificationFailures.Count -gt 0) {
	throw "App Service application settings verification failed. Missing or mismatched settings: $($settingsVerificationFailures -join ', ')"
}

Write-Host "Publishing app from '$projectFile' ($Configuration)..." -ForegroundColor Cyan
Invoke-CommandStrict -FileName "dotnet" -Arguments @(
	"publish", $projectFile,
	"-c", $Configuration,
	"-o", $publishDir,
	"--self-contained", "false",
	"-p:UseAppHost=false",
	"--nologo"
)

Write-Host "Packaging deployment artifact..." -ForegroundColor Cyan
Compress-Archive -Path (Join-Path $publishDir "*") -DestinationPath $zipPath -Force

Write-Host "Deploying package to App Service..." -ForegroundColor Cyan
Invoke-Az -Args @(
	"webapp", "deploy",
	"--resource-group", $ResourceGroupName,
	"--name", $WebAppName,
	"--src-path", $zipPath,
	"--type", "zip",
	"--clean", "true",
	"--output", "none"
) | Out-Null

$defaultHostName = (Invoke-Az -Args @("webapp", "show", "--resource-group", $ResourceGroupName, "--name", $WebAppName, "--query", "defaultHostName", "--output", "tsv")).Trim()
$appUrl = "https://$defaultHostName"
$portalUrl = "https://portal.azure.com/#view/WebsitesExtension/WebsiteOverviewBlade/id/subscriptions/$SubscriptionId/resourceGroups/$ResourceGroupName/providers/Microsoft.Web/sites/$WebAppName"

Write-Host "Deployment completed successfully." -ForegroundColor Green
Write-Host "App URL: $appUrl" -ForegroundColor Green
Write-Host "Azure Portal: $portalUrl" -ForegroundColor Green
