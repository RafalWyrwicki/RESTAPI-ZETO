#Requires -Version 5.1

[CmdletBinding()]
param(
    [switch]$ValidatePackageOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$PackageRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$SourceApp = Join-Path $PackageRoot 'App'
$ConfigPath = Join-Path $PackageRoot 'konfiguracja.json'
$DotNetFileName = 'NDP48-x86-x64-AllOS-ENU.exe'
$LocalDotNetInstaller = Join-Path $PackageRoot ('Prerequisites\' + $DotNetFileName)
$DotNetDownloadUrl = 'https://go.microsoft.com/fwlink/?LinkId=2088631'
$MinimumDotNetRelease = 461808 # .NET Framework 4.7.2
$script:RestartRequired = $false
$script:TranscriptStarted = $false

function Write-Step([string]$Message) {
    Write-Host ''
    Write-Host ('==> ' + $Message) -ForegroundColor Cyan
}

function Test-IsAdministrator {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Resolve-PackageLayout {
    $roots = New-Object System.Collections.Generic.List[string]
    $roots.Add($PackageRoot)
    foreach ($levelOne in @(Get-ChildItem -LiteralPath $PackageRoot -Directory -ErrorAction SilentlyContinue)) {
        $roots.Add($levelOne.FullName)
        foreach ($levelTwo in @(Get-ChildItem -LiteralPath $levelOne.FullName -Directory -ErrorAction SilentlyContinue)) {
            $roots.Add($levelTwo.FullName)
        }
    }

    $matches = @($roots | Where-Object {
        (Test-Path -LiteralPath (Join-Path $_ 'konfiguracja.json') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $_ 'App\Web.config') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $_ 'App\Global.asax') -PathType Leaf) -and
        (Test-Path -LiteralPath (Join-Path $_ 'App\bin\REST_API.dll') -PathType Leaf)
    } | Select-Object -Unique)

    if ($matches.Count -eq 0) {
        throw @"
Nie znaleziono kompletnego katalogu App obok instalatora.
Nie uruchamiaj skryptu bezpośrednio z archiwum ZIP i nie kopiuj samych plików
URUCHOM_INSTALACJE.cmd oraz Install-REST_API.ps1.

Użyj opcji "Wyodrębnij wszystkie". Oczekiwana struktura:
  KATALOG_PACZKI\Install-REST_API.ps1
  KATALOG_PACZKI\konfiguracja.json
  KATALOG_PACZKI\App\Web.config
  KATALOG_PACZKI\App\bin\REST_API.dll
"@
    }
    if ($matches.Count -gt 1) {
        throw "Znaleziono więcej niż jedną kompletną zawartość paczki: $($matches -join ', '). Pozostaw tylko jedną wersję."
    }

    $contentRoot = [System.IO.Path]::GetFullPath($matches[0])
    $script:SourceApp = Join-Path $contentRoot 'App'
    $script:ConfigPath = Join-Path $contentRoot 'konfiguracja.json'
    $script:LocalDotNetInstaller = Join-Path $contentRoot ('Prerequisites\' + $DotNetFileName)
    if ($contentRoot -ne $PackageRoot) {
        Write-Host "Znaleziono zawartość paczki w zagnieżdżonym katalogu: $contentRoot" -ForegroundColor Yellow
    }
}

function Get-DotNetRelease {
    $path = 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'
    try { return [int](Get-ItemPropertyValue -LiteralPath $path -Name Release -ErrorAction Stop) }
    catch { return 0 }
}

function New-RandomApiToken {
    $bytes = New-Object byte[] 32
    $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

function Read-RequiredValue([string]$CurrentValue, [string]$Prompt) {
    if (-not [string]::IsNullOrWhiteSpace($CurrentValue) -and $CurrentValue -notlike 'UZUPELNIJ*') {
        return $CurrentValue.Trim()
    }
    do { $value = Read-Host $Prompt } while ([string]::IsNullOrWhiteSpace($value))
    return $value.Trim()
}

function Read-PasswordValue([string]$CurrentValue) {
    if (-not [string]::IsNullOrWhiteSpace($CurrentValue) -and $CurrentValue -notlike 'UZUPELNIJ*') {
        return $CurrentValue
    }
    $secure = Read-Host 'Haslo użytkownika SQL' -AsSecureString
    $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    try { return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer) }
    finally { [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer) }
}

function Assert-SafeInstallRoot([string]$Path) {
    if ([string]::IsNullOrWhiteSpace($Path)) { throw 'InstallRoot nie może być pusty.' }
    $full = [System.IO.Path]::GetFullPath($Path)
    $driveRoot = [System.IO.Path]::GetPathRoot($full)
    $windowsPath = [System.IO.Path]::GetFullPath($env:SystemRoot)
    if ($full -eq $driveRoot -or $full.TrimEnd('\') -eq $windowsPath.TrimEnd('\')) {
        throw "Niedozwolony katalog instalacji: $full"
    }
    return $full.TrimEnd('\')
}

function Test-PackageFiles {
    $required = @(
        $ConfigPath,
        (Join-Path $SourceApp 'Web.config'),
        (Join-Path $SourceApp 'Global.asax'),
        (Join-Path $SourceApp 'bin\REST_API.dll'),
        (Join-Path $SourceApp 'Templates\etykieta.zpl')
    )
    $missing = @($required | Where-Object { -not (Test-Path -LiteralPath $_ -PathType Leaf) })
    if ($missing.Count -gt 0) { throw "Niekompletna paczka. Brakuje:`r`n$($missing -join "`r`n")" }

    [xml]$xml = Get-Content -LiteralPath (Join-Path $SourceApp 'Web.config')
    $token = @($xml.configuration.appSettings.add | Where-Object { $_.key -eq 'ApiToken' })[0].value
    $connection = @($xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'SQLServer' })[0].connectionString
    if ($token -notlike 'GENERUJ*' -and $token -notlike 'UZUPELNIJ*') {
        throw 'Paczka źródłowa zawiera skonfigurowany token API. Utwórz paczkę bez sekretów.'
    }
    if ($connection -notlike '*UZUPELNIJ*') {
        throw 'Paczka źródłowa zawiera skonfigurowane dane SQL. Utwórz paczkę bez sekretów.'
    }
}

function Install-DotNetIfRequired([object]$Config) {
    $release = Get-DotNetRelease
    Write-Host ".NET Framework Release: $release"
    if ($release -ge $MinimumDotNetRelease) {
        Write-Host '.NET Framework 4.7.2 lub nowszy jest już zainstalowany.' -ForegroundColor Green
        return
    }

    $installer = $LocalDotNetInstaller
    if (-not (Test-Path -LiteralPath $installer -PathType Leaf)) {
        if (-not [bool]$Config.DownloadDotNetIfMissing) {
            throw "Brak $DotNetFileName w katalogu Prerequisites."
        }
        $downloadRoot = Join-Path $env:ProgramData 'SIETOM\REST_API\Downloads'
        New-Item -ItemType Directory -Path $downloadRoot -Force | Out-Null
        $installer = Join-Path $downloadRoot $DotNetFileName
        Write-Host 'Brak lokalnego instalatora. Pobieranie z oficjalnej strony Microsoft...'
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        $client = New-Object Net.WebClient
        try { $client.DownloadFile($DotNetDownloadUrl, $installer) } finally { $client.Dispose() }
    }

    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
        $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
        throw "Nieprawidłowy podpis cyfrowy instalatora .NET: $($signature.Status)"
    }

    Write-Host 'Instalowanie .NET Framework 4.8...'
    $process = Start-Process -FilePath $installer -ArgumentList '/q /norestart' -Wait -PassThru
    if ($process.ExitCode -notin @(0, 1641, 3010)) {
        throw "Instalator .NET zakończył się kodem $($process.ExitCode)."
    }
    if ($process.ExitCode -in @(1641, 3010)) { $script:RestartRequired = $true }
    if ((Get-DotNetRelease) -lt $MinimumDotNetRelease) {
        throw 'Po instalacji nadal nie wykryto .NET Framework 4.7.2 lub nowszego.'
    }
}

function Resolve-SupportedOperatingSystem([int]$ProductType, [int]$BuildNumber) {
    if ($ProductType -eq 1) {
        if ($BuildNumber -ge 22000) { return 'Windows 11' }
        if ($BuildNumber -ge 10240) { return 'Windows 10' }
        throw "Nieobsługiwany system kliencki Windows (build $BuildNumber). Wymagany Windows 10 lub Windows 11."
    }
    if ($ProductType -in @(2, 3)) {
        if ($BuildNumber -ge 14393) { return 'Windows Server' }
        throw "Nieobsługiwany Windows Server (build $BuildNumber). Wymagany Windows Server 2016 lub nowszy."
    }
    throw "Nieobsługiwany typ systemu Windows: $ProductType."
}

function Get-SupportedOperatingSystem {
    $system = Get-CimInstance Win32_OperatingSystem
    $name = Resolve-SupportedOperatingSystem ([int]$system.ProductType) ([int]$system.BuildNumber)
    return [pscustomobject]@{
        Name = $name
        Caption = [string]$system.Caption
        BuildNumber = [int]$system.BuildNumber
        ProductType = [int]$system.ProductType
    }
}

function Test-OperatingSystemRecognition {
    if ((Resolve-SupportedOperatingSystem 1 19045) -ne 'Windows 10') { throw 'Test Windows 10 nie powiódł się.' }
    if ((Resolve-SupportedOperatingSystem 1 22631) -ne 'Windows 11') { throw 'Test Windows 11 nie powiódł się.' }
    if ((Resolve-SupportedOperatingSystem 3 20348) -ne 'Windows Server') { throw 'Test Windows Server nie powiódł się.' }
}

function Enable-RequiredIisFeatures([object]$OperatingSystem) {
    Write-Host ("Wykryto: {0}; build {1}." -f $OperatingSystem.Caption, $OperatingSystem.BuildNumber)
    if ($OperatingSystem.ProductType -eq 1) {
        $features = @(
            'IIS-WebServerRole', 'IIS-WebServer', 'IIS-CommonHttpFeatures',
            'IIS-DefaultDocument', 'IIS-StaticContent', 'IIS-HttpErrors',
            'IIS-ApplicationDevelopment', 'IIS-NetFxExtensibility45', 'IIS-ASPNET45',
            'IIS-ISAPIExtensions', 'IIS-ISAPIFilter', 'IIS-Security',
            'IIS-RequestFiltering', 'IIS-HttpCompressionDynamic', 'IIS-ManagementConsole'
        )
        foreach ($feature in $features) {
            $state = Get-WindowsOptionalFeature -Online -FeatureName $feature -ErrorAction Stop
            if ($state.State -ne 'Enabled') {
                Write-Host "Włączanie funkcji: $feature"
                $result = Enable-WindowsOptionalFeature -Online -FeatureName $feature -All -NoRestart
                if ($result.RestartNeeded) { $script:RestartRequired = $true }
            }
        }
    } else {
        Write-Host 'Wykryto Windows Server.'
        Import-Module ServerManager -ErrorAction Stop
        $features = @('Web-Server', 'Web-Asp-Net45', 'Web-Net-Ext45', 'Web-ISAPI-Ext', 'Web-ISAPI-Filter', 'Web-Dyn-Compression', 'Web-Mgmt-Console')
        $missing = @($features | Where-Object { -not (Get-WindowsFeature -Name $_).Installed })
        if ($missing.Count -gt 0) {
            Write-Host ('Instalowanie funkcji: ' + ($missing -join ', '))
            $result = Install-WindowsFeature -Name $missing -IncludeManagementTools
            if (-not $result.Success) { throw 'Nie udało się zainstalować wymaganych funkcji IIS.' }
            if ($result.RestartNeeded -eq 'Yes') { $script:RestartRequired = $true }
        }
    }
}

function New-SqlConnectionString([object]$Config, [string]$SqlPassword) {
    Add-Type -AssemblyName System.Data
    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder
    # Windows PowerShell 5.1 może potraktować zapis .DataSource jak klucz
    # "DataSource", którego SqlConnectionStringBuilder nie obsługuje.
    # Indeksator z kanonicznymi nazwami działa jednakowo na Windows 10 i Server.
    $builder['Data Source'] = if ([int]$Config.SqlPort -eq 1433) { [string]$Config.SqlServer } else { "$($Config.SqlServer),$($Config.SqlPort)" }
    $builder['Initial Catalog'] = [string]$Config.SqlDatabase
    $builder['Pooling'] = $true
    $builder['Min Pool Size'] = 0
    $builder['Max Pool Size'] = 100
    $builder['Connect Timeout'] = 5
    $builder['Application Name'] = 'REST_API'
    if ([bool]$Config.UseWindowsAuthentication) {
        $builder['Integrated Security'] = $true
    } else {
        $builder['User ID'] = [string]$Config.SqlUser
        $builder['Password'] = $SqlPassword
    }
    return $builder.ConnectionString
}

function Test-SqlConnectionStringBuilder {
    $testConfig = [pscustomobject]@{
        SqlServer = '127.0.0.1'
        SqlPort = 1433
        SqlDatabase = 'SietomSoft'
        UseWindowsAuthentication = $false
        SqlUser = 'test'
    }
    $value = New-SqlConnectionString $testConfig 'test-password'
    $parsed = New-Object System.Data.SqlClient.SqlConnectionStringBuilder -ArgumentList $value
    if ($parsed.DataSource -ne '127.0.0.1' -or $parsed.InitialCatalog -ne 'SietomSoft') {
        throw 'Test budowania connection stringa SQL nie powiódł się.'
    }
}

function Set-ApplicationConfiguration([string]$WebConfigPath, [object]$Config, [string]$ApiToken, [string]$SqlPassword) {
    $connectionString = New-SqlConnectionString $Config $SqlPassword

    [xml]$xml = Get-Content -LiteralPath $WebConfigPath
    $tokenNode = @($xml.configuration.appSettings.add | Where-Object { $_.key -eq 'ApiToken' })[0]
    $tokenNode.value = $ApiToken
    $printerPortNode = @($xml.configuration.appSettings.add | Where-Object { $_.key -eq 'PrinterPort' })[0]
    $printerPortNode.value = [string]$Config.PrinterPort
    $connectionNode = @($xml.configuration.connectionStrings.add | Where-Object { $_.name -eq 'SQLServer' })[0]
    $connectionNode.connectionString = $connectionString
    $xml.Save($WebConfigPath)
}

function Test-TcpPort([string]$HostName, [int]$Port, [int]$TimeoutMilliseconds = 3000) {
    $client = New-Object Net.Sockets.TcpClient
    try {
        $task = $client.ConnectAsync($HostName, $Port)
        if (-not $task.Wait($TimeoutMilliseconds)) { return $false }
        return $client.Connected
    } catch { return $false } finally { $client.Dispose() }
}

function Deploy-RestApi([object]$Config, [string]$ApiToken, [string]$SqlPassword) {
    Import-Module WebAdministration -ErrorAction Stop
    $installRoot = Assert-SafeInstallRoot ([string]$Config.InstallRoot)
    $releaseName = Get-Date -Format 'yyyyMMdd_HHmmss'
    $releasePath = Join-Path (Join-Path $installRoot 'releases') $releaseName
    New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
    Get-ChildItem -LiteralPath $SourceApp -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $releasePath -Recurse -Force
    }
    Set-ApplicationConfiguration (Join-Path $releasePath 'Web.config') $Config $ApiToken $SqlPassword

    $siteName = [string]$Config.SiteName
    $poolName = [string]$Config.AppPoolName
    $port = [int]$Config.Port
    $hostHeader = [string]$Config.HostHeader
    $poolPath = "IIS:\AppPools\$poolName"
    if (-not (Test-Path -LiteralPath $poolPath)) { New-WebAppPool -Name $poolName | Out-Null }
    Set-ItemProperty -LiteralPath $poolPath -Name managedRuntimeVersion -Value 'v4.0'
    Set-ItemProperty -LiteralPath $poolPath -Name managedPipelineMode -Value 'Integrated'
    Set-ItemProperty -LiteralPath $poolPath -Name startMode -Value 'AlwaysRunning'
    Set-ItemProperty -LiteralPath $poolPath -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
    Set-ItemProperty -LiteralPath $poolPath -Name enable32BitAppOnWin64 -Value $false

    $sitePath = "IIS:\Sites\$siteName"
    if (Test-Path -LiteralPath $sitePath) {
        Set-ItemProperty -LiteralPath $sitePath -Name physicalPath -Value $releasePath
        Set-ItemProperty -LiteralPath $sitePath -Name applicationPool -Value $poolName
        $bindingValue = "*:${port}:${hostHeader}"
        $hasBinding = @(Get-WebBinding -Name $siteName -Protocol http | Where-Object { $_.bindingInformation -eq $bindingValue }).Count -gt 0
        if (-not $hasBinding) { New-WebBinding -Name $siteName -Protocol http -Port $port -HostHeader $hostHeader | Out-Null }
    } else {
        New-Website -Name $siteName -PhysicalPath $releasePath -ApplicationPool $poolName -Port $port -HostHeader $hostHeader | Out-Null
    }

    $identity = "IIS AppPool\$poolName"
    & (Join-Path $env:SystemRoot 'System32\icacls.exe') $releasePath /grant "${identity}:(OI)(CI)(RX)" /T /C | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Nie udało się nadać praw katalogu dla $identity." }

    if ([bool]$Config.OpenFirewall) {
        $ruleName = "SIETOM REST API TCP $port"
        $rule = Get-NetFirewallRule -DisplayName $ruleName -ErrorAction SilentlyContinue
        if ($null -eq $rule) {
            New-NetFirewallRule -DisplayName $ruleName -Direction Inbound -Action Allow -Protocol TCP -LocalPort $port -Profile Domain,Private,Public | Out-Null
        } else {
            $rule | Set-NetFirewallRule -Enabled True -Action Allow -Profile Domain,Private,Public | Out-Null
        }
    }

    Start-WebAppPool -Name $poolName
    Start-Website -Name $siteName
    return $releasePath
}

function Show-NetworkRequirements([object]$Config) {
    Write-Host 'Reguły wymagane na zewnętrznych firewallach/routerach sądu:' -ForegroundColor Yellow
    Write-Host ("  1. Systemy źródłowe  -> ten serwer API : TCP {0} (ruch przychodzący)." -f $Config.Port)
    Write-Host ("  2. Ten serwer API    -> {0} : TCP {1} (SQL Server)." -f $Config.SqlServer, $Config.SqlPort)
    Write-Host ("  3. Ten serwer API    -> adresy IP drukarek : TCP {0} (RAW/ZPL)." -f $Config.PrinterPort)
    Write-Host '  4. DNS, jeśli używane są nazwy zamiast IP: UDP/TCP 53 do serwerów DNS.'
    Write-Host 'Nie należy wystawiać portu API bezpośrednio do Internetu. Dopuszczać tylko wymagane podsieci sądu.'
    Write-Host 'Przykładowa kontrola: Test-NetConnection ADRES -Port NUMER_PORTU'
}

try {
    Write-Step 'Kontrola kompletności paczki'
    Resolve-PackageLayout
    Test-PackageFiles
    [object]$config = Get-Content -LiteralPath $ConfigPath -Raw | ConvertFrom-Json
    [void](Assert-SafeInstallRoot ([string]$config.InstallRoot))
    if ([int]$config.Port -lt 1 -or [int]$config.Port -gt 65535) { throw 'Port HTTP jest poza zakresem 1-65535.' }
    if ([int]$config.SqlPort -lt 1 -or [int]$config.SqlPort -gt 65535) { throw 'Port SQL jest poza zakresem 1-65535.' }
    if ($ValidatePackageOnly) {
        Test-SqlConnectionStringBuilder
        Test-OperatingSystemRecognition
        Write-Host 'Paczka jest kompletna i nie zawiera skonfigurowanych sekretów.' -ForegroundColor Green
        exit 0
    }

    if (-not (Test-IsAdministrator)) { throw 'Uruchom instalator jako administrator.' }
    $logRoot = Join-Path $env:ProgramData 'SIETOM\REST_API\Logs'
    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
    $logPath = Join-Path $logRoot ("install_{0}.log" -f (Get-Date -Format 'yyyyMMdd_HHmmss'))
    Start-Transcript -LiteralPath $logPath -Force | Out-Null
    $script:TranscriptStarted = $true

    Write-Step 'Odczyt konfiguracji środowiska'
    $config.SqlServer = Read-RequiredValue ([string]$config.SqlServer) 'Adres lub nazwa SQL Server'
    $config.SqlDatabase = Read-RequiredValue ([string]$config.SqlDatabase) 'Nazwa bazy danych'
    $sqlPassword = ''
    if (-not [bool]$config.UseWindowsAuthentication) {
        $config.SqlUser = Read-RequiredValue ([string]$config.SqlUser) 'Użytkownik SQL'
        $sqlPassword = Read-PasswordValue ([string]$config.SqlPassword)
        if ([string]::IsNullOrEmpty($sqlPassword)) { throw 'Hasło SQL nie może być puste.' }
    }
    $apiToken = New-RandomApiToken

    Write-Step 'Sprawdzenie i instalacja .NET Framework'
    Install-DotNetIfRequired $config
    Write-Step 'Sprawdzenie i instalacja wymaganych funkcji IIS'
    $operatingSystem = Get-SupportedOperatingSystem
    Enable-RequiredIisFeatures $operatingSystem
    Write-Step 'Instalacja aplikacji REST API w IIS'
    $releasePath = Deploy-RestApi $config $apiToken $sqlPassword

    Write-Step 'Test połączeń'
    if (Test-TcpPort ([string]$config.SqlServer) ([int]$config.SqlPort)) {
        Write-Host 'Połączenie TCP z SQL Server działa.' -ForegroundColor Green
    } else { Write-Warning 'Nie udało się połączyć z portem SQL Server.' }
    if (-not [string]::IsNullOrWhiteSpace([string]$config.PrinterTestAddress)) {
        if (Test-TcpPort ([string]$config.PrinterTestAddress) ([int]$config.PrinterPort)) {
            Write-Host 'Połączenie TCP z drukarką działa.' -ForegroundColor Green
        } else { Write-Warning 'Nie udało się połączyć z portem drukarki.' }
    }
    if (-not $script:RestartRequired) {
        try {
            $url = "http://localhost:$($config.Port)/"
            $response = Invoke-WebRequest -Uri $url -UseBasicParsing -TimeoutSec 15
            if ($response.StatusCode -eq 200) { Write-Host "API odpowiada pod $url" -ForegroundColor Green }
        } catch { Write-Warning ("Lokalny test HTTP nie powiódł się: " + $_.Exception.Message) }
    }

    Write-Step 'Instalacja zakończona'
    Write-Host "Katalog wersji: $releasePath"
    Write-Host "Adres: http://localhost:$($config.Port)/"
    Write-Host "Token API zapisano wyłącznie w: $releasePath\Web.config"
    Write-Host "Log instalacji: $logPath"
    Write-Step 'Informacja dla administratora sieci'
    Show-NetworkRequirements $config
    if ($script:RestartRequired) { Write-Warning 'Uruchom ponownie Windows przed użyciem API.' }
    exit 0
} catch {
    Write-Host ''
    Write-Host ('BŁĄD: ' + $_.Exception.Message) -ForegroundColor Red
    Write-Host $_.ScriptStackTrace -ForegroundColor DarkGray
    exit 1
} finally {
    if ($script:TranscriptStarted) { try { Stop-Transcript | Out-Null } catch { } }
    if (-not $ValidatePackageOnly) {
        Write-Host ''
        [void](Read-Host 'Naciśnij Enter, aby zamknąć instalator')
    }
}
