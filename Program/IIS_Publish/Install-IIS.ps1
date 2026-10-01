#Requires -RunAsAdministrator

[CmdletBinding()]
param(
    [string]$SiteName = "REST_API",
    [string]$AppPoolName = "REST_API",
    [string]$PhysicalPath = (Join-Path $PSScriptRoot "App"),
    [ValidateRange(1, 65535)]
    [int]$Port = 8080,
    [string]$HostHeader = "",
    [switch]$UpdateExisting
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$resolvedPath = (Resolve-Path -LiteralPath $PhysicalPath).Path
$webConfigPath = Join-Path $resolvedPath "Web.config"
$applicationDllPath = Join-Path $resolvedPath "bin\REST_API.dll"

if (-not (Test-Path -LiteralPath $webConfigPath)) {
    throw "Nie znaleziono pliku Web.config w: $resolvedPath"
}

if (-not (Test-Path -LiteralPath $applicationDllPath)) {
    throw "Nie znaleziono pliku bin\REST_API.dll w: $resolvedPath"
}

Import-Module WebAdministration -ErrorAction Stop

$appPoolPath = "IIS:\AppPools\$AppPoolName"
if (-not (Test-Path -LiteralPath $appPoolPath)) {
    New-WebAppPool -Name $AppPoolName | Out-Null
}

Set-ItemProperty -LiteralPath $appPoolPath -Name managedRuntimeVersion -Value "v4.0"
Set-ItemProperty -LiteralPath $appPoolPath -Name managedPipelineMode -Value "Integrated"
Set-ItemProperty -LiteralPath $appPoolPath -Name startMode -Value "AlwaysRunning"
Set-ItemProperty -LiteralPath $appPoolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)

$sitePath = "IIS:\Sites\$SiteName"
if (Test-Path -LiteralPath $sitePath) {
    if (-not $UpdateExisting) {
        throw "Witryna '$SiteName' już istnieje. Użyj -UpdateExisting, aby zaktualizować jej katalog i pulę aplikacji."
    }

    Set-ItemProperty -LiteralPath $sitePath -Name physicalPath -Value $resolvedPath
    Set-ItemProperty -LiteralPath $sitePath -Name applicationPool -Value $AppPoolName
} else {
    New-Website `
        -Name $SiteName `
        -PhysicalPath $resolvedPath `
        -ApplicationPool $AppPoolName `
        -Port $Port `
        -HostHeader $HostHeader | Out-Null
}

$appPoolIdentity = "IIS AppPool\$AppPoolName"
$icaclsPath = Join-Path $env:SystemRoot "System32\icacls.exe"
& $icaclsPath $resolvedPath /grant "${appPoolIdentity}:(OI)(CI)(RX)" /T /C | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw "Nie udało się nadać praw odczytu katalogu dla $appPoolIdentity."
}

Start-WebAppPool -Name $AppPoolName
Start-Website -Name $SiteName

Write-Host "Witryna IIS została przygotowana." -ForegroundColor Green
Write-Host "Nazwa witryny: $SiteName"
Write-Host "Pula aplikacji: $AppPoolName (.NET CLR v4.0, Integrated)"
Write-Host "Katalog: $resolvedPath"
Write-Host "Adres HTTP: http://localhost:$Port/"
Write-Warning "Przed użyciem produkcyjnym skonfiguruj HTTPS oraz zmień token API i dane logowania SQL w Web.config."
