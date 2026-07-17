param(
    [string]$EnvFile = ".env.dev",
    [string]$Urls = "",
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [string]$ProjectPath = "./sekura/sekura.csproj"
)

$ErrorActionPreference = "Stop"

# Env file lines are KEY=VALUE and read literally (no expansion), so '$' in
# password hashes needs no quoting or escaping.
if (-not (Test-Path $EnvFile)) {
    Write-Error "Env file '$EnvFile' not found. Create it from the template first: Copy-Item .env.template $EnvFile"
}

Write-Host "Loading environment from $EnvFile" -ForegroundColor Cyan
foreach ($line in Get-Content -Path $EnvFile) {
    $trimmed = $line.Trim()
    if ($trimmed -eq "" -or $trimmed.StartsWith("#")) {
        continue
    }

    $separatorIndex = $trimmed.IndexOf("=")
    if ($separatorIndex -lt 1) {
        Write-Warning "Ignoring malformed line in $EnvFile (expected KEY=VALUE)"
        continue
    }

    $key = $trimmed.Substring(0, $separatorIndex).Trim()
    $value = $trimmed.Substring($separatorIndex + 1)
    if (($value.StartsWith('"') -and $value.EndsWith('"') -and $value.Length -ge 2) -or
        ($value.StartsWith("'") -and $value.EndsWith("'") -and $value.Length -ge 2)) {
        $value = $value.Substring(1, $value.Length - 2)
    }

    [Environment]::SetEnvironmentVariable($key, $value, "Process")
}

if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_ENVIRONMENT)) {
    $env:ASPNETCORE_ENVIRONMENT = "Development"
}
Write-Host "ASPNETCORE_ENVIRONMENT=$env:ASPNETCORE_ENVIRONMENT" -ForegroundColor DarkGray

Write-Host "Restoring dependencies..." -ForegroundColor Cyan
dotnet restore

Write-Host "Building project ($Configuration)..." -ForegroundColor Cyan
dotnet build $ProjectPath -c $Configuration

if ([string]::IsNullOrWhiteSpace($Urls)) {
    $urlInfo = if ([string]::IsNullOrWhiteSpace($env:ASPNETCORE_URLS)) { "<kestrel default>" } else { $env:ASPNETCORE_URLS }
    Write-Host "Starting sekura using URLs from $EnvFile (ASPNETCORE_URLS=$urlInfo)" -ForegroundColor Green
    Write-Host "Use -Urls to override (example: -Urls https://localhost:7099)" -ForegroundColor DarkGray
    Write-Host "Press Ctrl+C to stop." -ForegroundColor Yellow
    dotnet run --project $ProjectPath -c $Configuration --no-launch-profile
    exit $LASTEXITCODE
}

Write-Host "Starting sekura on $Urls" -ForegroundColor Green
Write-Host "Press Ctrl+C to stop." -ForegroundColor Yellow

dotnet run --project $ProjectPath -c $Configuration --no-launch-profile --urls $Urls
