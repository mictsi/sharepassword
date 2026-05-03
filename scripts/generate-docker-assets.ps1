param(
	[string]$SettingsFile = "./sharepassword/appsettings.json",
	[string]$OutputDirectory = "./artifacts/docker",
	[string]$ServiceName = "sharepassword",
	[string]$ContainerName = "sharepassword",
	[string]$ImageName = "sharepassword:local",
	[string]$AppEnvironment = "Production",
	[int]$ContainerPort = 8080,
	[int]$HostPort = 8080,
	[string]$SqliteContainerConnectionString = "Data Source=/app/data/sharepassword.db"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $PSCommandPath
$repoRoot = [IO.Path]::GetFullPath((Join-Path $scriptRoot ".."))

function Get-RepoPath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path,

		[switch]$AllowMissing
	)

	if ([string]::IsNullOrWhiteSpace($Path)) {
		throw "Path is required."
	}

	$resolvedPath = if ([IO.Path]::IsPathRooted($Path)) {
		[IO.Path]::GetFullPath($Path)
	}
	else {
		[IO.Path]::GetFullPath((Join-Path $repoRoot $Path))
	}

	if (-not $AllowMissing -and -not (Test-Path $resolvedPath)) {
		throw "Path '$Path' was not found. Resolved to '$resolvedPath'."
	}

	return $resolvedPath
}

function ConvertTo-AppSettingValue {
	param(
		[AllowNull()]
		[object]$Value
	)

	if ($null -eq $Value) {
		return ""
	}

	if ($Value -is [bool]) {
		if ($Value) {
			return "true"
		}

		return "false"
	}

	if ($Value -is [string]) {
		return $Value
	}

	if ($Value -is [ValueType]) {
		return [Convert]::ToString($Value, [Globalization.CultureInfo]::InvariantCulture)
	}

	return ($Value | ConvertTo-Json -Compress -Depth 64)
}

function Add-FlattenedJsonSettings {
	param(
		[AllowNull()]
		[object]$Source,

		[string]$Prefix = "",

		[Parameter(Mandatory = $true)]
		[System.Collections.Specialized.OrderedDictionary]$Settings
	)

	if ($null -eq $Source) {
		if (-not [string]::IsNullOrWhiteSpace($Prefix)) {
			$Settings[$Prefix] = ""
		}

		return
	}

	if ($Source -is [pscustomobject]) {
		foreach ($property in $Source.PSObject.Properties) {
			$key = if ([string]::IsNullOrWhiteSpace($Prefix)) { $property.Name } else { "${Prefix}__$($property.Name)" }
			Add-FlattenedJsonSettings -Source $property.Value -Prefix $key -Settings $Settings
		}

		return
	}

	if (($Source -is [System.Collections.IEnumerable]) -and -not ($Source -is [string])) {
		$index = 0
		foreach ($item in $Source) {
			$key = "${Prefix}__$index"
			Add-FlattenedJsonSettings -Source $item -Prefix $key -Settings $Settings
			$index++
		}

		return
	}

	if ([string]::IsNullOrWhiteSpace($Prefix)) {
		throw "The settings file root must be a JSON object."
	}

	$Settings[$Prefix] = ConvertTo-AppSettingValue -Value $Source
}

function ConvertTo-YamlSingleQuotedScalar {
	param(
		[AllowNull()]
		[string]$Value
	)

	if ($null -eq $Value) {
		return "''"
	}

	return "'" + $Value.Replace("'", "''") + "'"
}

function ConvertTo-BashSingleQuotedLiteral {
	param(
		[AllowNull()]
		[string]$Value
	)

	if ($null -eq $Value) {
		return "''"
	}

	$singleQuoteEscape = "'" + '"' + "'" + '"' + "'"
	return "'" + $Value.Replace("'", $singleQuoteEscape) + "'"
}

function ConvertTo-ComposePath {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path
	)

	if ([string]::IsNullOrWhiteSpace($Path)) {
		return "."
	}

	return $Path.Replace("\", "/")
}

function Write-GeneratedTextFile {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Path,

		[Parameter(Mandatory = $true)]
		[AllowEmptyString()]
		[string[]]$Lines
	)

	$content = (($Lines | ForEach-Object { $_ -replace "`r?`n", "" }) -join "`n") + "`n"
	$encoding = [System.Text.UTF8Encoding]::new($false)
	[System.IO.File]::WriteAllText($Path, $content, $encoding)
}

function Get-DockerSafeName {
	param(
		[Parameter(Mandatory = $true)]
		[string]$Value
	)

	$sanitizedValue = $Value -replace "[^a-zA-Z0-9_.-]", "-"
	if ([string]::IsNullOrWhiteSpace($sanitizedValue)) {
		return "sharepassword"
	}

	return $sanitizedValue
}

if ($ContainerPort -le 0) {
	throw "ContainerPort must be greater than 0."
}

if ($HostPort -le 0) {
	throw "HostPort must be greater than 0."
}

$settingsPath = Get-RepoPath -Path $SettingsFile
$outputPath = Get-RepoPath -Path $OutputDirectory -AllowMissing

Write-Host "Loading and flattening settings from '$settingsPath'..." -ForegroundColor Cyan
$settingsJson = Get-Content -Path $settingsPath -Raw | ConvertFrom-Json
$flattenedSettings = [ordered]@{}
Add-FlattenedJsonSettings -Source $settingsJson -Settings $flattenedSettings

$dockerSettings = [ordered]@{}
foreach ($entry in $flattenedSettings.GetEnumerator()) {
	$dockerSettings[[string]$entry.Key] = [string]$entry.Value
}

$dockerSettings["Application__EnableHttpsRedirection"] = "false"
$dockerSettings["ASPNETCORE_ENVIRONMENT"] = $AppEnvironment
$dockerSettings["ASPNETCORE_URLS"] = "http://+:$ContainerPort"
$dockerSettings["Kestrel__Endpoints__Http__Url"] = "http://+:$ContainerPort"

$storageBackend = ""
if ($dockerSettings.Contains("Storage__Backend")) {
	$storageBackend = [string]$dockerSettings["Storage__Backend"]
}

$useSqliteVolume = $storageBackend.Equals("sqlite", [StringComparison]::OrdinalIgnoreCase)
if ($useSqliteVolume) {
	$dockerSettings["SqliteStorage__ConnectionString"] = $SqliteContainerConnectionString
}

$httpsEndpointKeys = @(
	foreach ($key in $dockerSettings.Keys) {
		if ([string]$key -like "Kestrel__Endpoints__Https__*") {
			[string]$key
		}
	}
)

if ($httpsEndpointKeys.Count -gt 0) {
	Write-Warning "HTTPS Kestrel endpoint settings were preserved from appsettings.json. If the container is not mounting certificates, remove or override those settings before using the generated compose file."
}

New-Item -Path $outputPath -ItemType Directory -Force | Out-Null

$startScriptPath = Join-Path $outputPath "start.sh"
$composeFilePath = Join-Path $outputPath "docker-compose.generated.yml"
$legacyEnvScriptPath = Join-Path $outputPath "set-docker-env.generated.ps1"

$startScriptRepoRelativePath = [IO.Path]::GetRelativePath($repoRoot, $startScriptPath)
$composeFileRepoRelativePath = [IO.Path]::GetRelativePath($repoRoot, $composeFilePath)
$composeBuildContext = ConvertTo-ComposePath -Path ([IO.Path]::GetRelativePath($outputPath, $repoRoot))
$volumeName = (Get-DockerSafeName -Value "$ServiceName-data")
$generatedTimestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ssK"

$startScriptLines = [System.Collections.Generic.List[string]]::new()
$startScriptLines.Add("#!/usr/bin/env bash")
$startScriptLines.Add("set -euo pipefail")
$startScriptLines.Add("")
$startScriptLines.Add("# Generated from $settingsPath on $generatedTimestamp.")
$startScriptLines.Add("# Contains literal values from appsettings.json. Review before sharing.")
$startScriptLines.Add('SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"')
$startScriptLines.Add('REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"')
$startScriptLines.Add("")
$startScriptLines.Add(('IMAGE_NAME=${{IMAGE_NAME:-{0}}}' -f (ConvertTo-BashSingleQuotedLiteral -Value $ImageName)))
$startScriptLines.Add(('CONTAINER_NAME=${{CONTAINER_NAME:-{0}}}' -f (ConvertTo-BashSingleQuotedLiteral -Value $ContainerName)))
$startScriptLines.Add(('HOST_PORT=${{HOST_PORT:-{0}}}' -f (ConvertTo-BashSingleQuotedLiteral -Value ([string]$HostPort))))
$startScriptLines.Add(('CONTAINER_PORT=${{CONTAINER_PORT:-{0}}}' -f (ConvertTo-BashSingleQuotedLiteral -Value ([string]$ContainerPort))))
if ($useSqliteVolume) {
	$startScriptLines.Add(('VOLUME_NAME=${{VOLUME_NAME:-{0}}}' -f (ConvertTo-BashSingleQuotedLiteral -Value $volumeName)))
}
$startScriptLines.Add("")
$startScriptLines.Add('echo "Building $IMAGE_NAME from $REPO_ROOT/Dockerfile..."')
$startScriptLines.Add('docker build --tag "$IMAGE_NAME" --file "$REPO_ROOT/Dockerfile" "$REPO_ROOT"')
$startScriptLines.Add("")
$startScriptLines.Add('docker_args=(')
$startScriptLines.Add('  run')
$startScriptLines.Add('  --publish "${HOST_PORT}:${CONTAINER_PORT}"')
$startScriptLines.Add('  --restart unless-stopped')
$startScriptLines.Add(')')
$startScriptLines.Add("")
$startScriptLines.Add('if [[ -n "$CONTAINER_NAME" ]]; then')
$startScriptLines.Add('  docker_args+=(--name "$CONTAINER_NAME")')
$startScriptLines.Add('fi')
if ($useSqliteVolume) {
	$startScriptLines.Add("")
	$startScriptLines.Add('if [[ -n "${VOLUME_NAME:-}" ]]; then')
	$startScriptLines.Add('  docker_args+=(--volume "${VOLUME_NAME}:/app/data")')
	$startScriptLines.Add('fi')
}
$startScriptLines.Add("")
foreach ($entry in $dockerSettings.GetEnumerator()) {
	$startScriptLines.Add(("docker_args+=(--env {0})" -f (ConvertTo-BashSingleQuotedLiteral -Value ("{0}={1}" -f ([string]$entry.Key), ([string]$entry.Value)))))
}
$startScriptLines.Add("")
$startScriptLines.Add('docker_args+=("$IMAGE_NAME")')
$startScriptLines.Add("")
$startScriptLines.Add('echo "Starting container from $IMAGE_NAME on host port $HOST_PORT..."')
$startScriptLines.Add('echo "Press Ctrl+C to stop. If the container name already exists, remove it first."')
$startScriptLines.Add('exec docker "${docker_args[@]}"')

$composeLines = [System.Collections.Generic.List[string]]::new()
$composeLines.Add("# Generated from $settingsPath on $generatedTimestamp.")
$composeLines.Add("# Contains literal values from appsettings.json. Review before sharing.")
$composeLines.Add(("# Runtime environment variables are intentionally omitted here. Use ./{0} to run the container with flattened settings from appsettings.json." -f (ConvertTo-ComposePath -Path $startScriptRepoRelativePath)))
$composeLines.Add("services:")
$composeLines.Add(("  {0}:" -f $ServiceName))
if (-not [string]::IsNullOrWhiteSpace($ContainerName)) {
	$composeLines.Add("    container_name: $(ConvertTo-YamlSingleQuotedScalar -Value $ContainerName)")
}
$composeLines.Add("    image: $(ConvertTo-YamlSingleQuotedScalar -Value $ImageName)")
$composeLines.Add("    build:")
$composeLines.Add("      context: $(ConvertTo-YamlSingleQuotedScalar -Value $composeBuildContext)")
$composeLines.Add("      dockerfile: 'Dockerfile'")
$composeLines.Add("    ports:")
$composeLines.Add(("      - {0}" -f (ConvertTo-YamlSingleQuotedScalar -Value ("{0}:{1}" -f $HostPort, $ContainerPort))))
$composeLines.Add("    restart: unless-stopped")

if ($useSqliteVolume) {
	$composeLines.Add("    volumes:")
	$composeLines.Add(("      - {0}" -f (ConvertTo-YamlSingleQuotedScalar -Value ("{0}:/app/data" -f $volumeName))))
	$composeLines.Add("")
	$composeLines.Add("volumes:")
	$composeLines.Add(("  {0}:" -f $volumeName))
}

if (Test-Path $legacyEnvScriptPath) {
	Remove-Item -Path $legacyEnvScriptPath -Force
}

Write-GeneratedTextFile -Path $startScriptPath -Lines ($startScriptLines.ToArray())
Write-GeneratedTextFile -Path $composeFilePath -Lines ($composeLines.ToArray())

Write-Host "Generated Docker start script: '$startScriptPath'" -ForegroundColor Green
Write-Host "Generated Docker Compose file: '$composeFilePath'" -ForegroundColor Green
Write-Host ("Flattened environment variable count: {0}" -f $dockerSettings.Count) -ForegroundColor Green
Write-Host ("Start command: bash ./{0}" -f (ConvertTo-ComposePath -Path $startScriptRepoRelativePath)) -ForegroundColor DarkGray
Write-Host ("Compose file generated without inline environment values: ./{0}" -f (ConvertTo-ComposePath -Path $composeFileRepoRelativePath)) -ForegroundColor DarkGray

if ($useSqliteVolume) {
	Write-Host ("SQLite backend detected. The start script mounts the '{0}' named volume and maps the database to '{1}'." -f $volumeName, $SqliteContainerConnectionString) -ForegroundColor DarkGray
}