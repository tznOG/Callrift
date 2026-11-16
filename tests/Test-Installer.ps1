$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$fixtureId = [Guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path $PSScriptRoot ('output\installer-' + $fixtureId)
$application = Join-Path $fixtureRoot 'app'
$output = Join-Path $fixtureRoot 'packages'
$identity = 'Callrift-Packaging-Test-' + $fixtureId
$displayName = 'Callrift Packaging Test ' + $fixtureId
$key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\' + $identity + '_is1'
New-Item -ItemType Directory -Force -Path $fixtureRoot,$output | Out-Null
$results = New-Object 'Collections.Generic.List[string]'
function Assert-Installer([bool]$condition,[string]$message) {
    if (!$condition) { throw $message }
    $results.Add('PASS ' + $message)
}
function Invoke-Setup([string]$path,[string[]]$options) {
    $process = Start-Process -FilePath $path -ArgumentList $options -WindowStyle Hidden -PassThru -Wait
    return $process.ExitCode
}
$options = @('/VERYSILENT','/SUPPRESSMSGBOXES','/SP-','/NORESTART',('/DIR="' + $application + '"'),'/MERGETASKS=desktopicon')
$desktop = Join-Path ([Environment]::GetFolderPath('DesktopDirectory')) ($displayName + '.lnk')
$menu = Join-Path ([Environment]::GetFolderPath('StartMenu')) ('Programs\' + $displayName + '\' + $displayName + '.lnk')
$uninstaller = Join-Path $application 'unins000.exe'
$currentVersion = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskRoot 'dist\Callrift\Callrift.exe')).ProductVersion
try {
    & (Join-Path $taskRoot 'scripts\Build-Installer.ps1') -OutputDirectory $output -Identity $identity -DisplayName $displayName -Version '0.0.1'
    & (Join-Path $taskRoot 'scripts\Build-Installer.ps1') -OutputDirectory $output -Identity $identity -DisplayName $displayName -Version $currentVersion
    $oldSetup = Join-Path $output 'Callrift-Setup-0.0.1.exe'
    $newSetup = Join-Path $output ('Callrift-Setup-' + $currentVersion + '.exe')
    Assert-Installer ((Invoke-Setup $oldSetup $options) -eq 0) 'Installer completes for current user in an isolated path'
    Assert-Installer ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq '0.0.1') 'Installed version appears in Windows uninstall registration'
    Assert-Installer ((Test-Path -LiteralPath (Join-Path $application 'installed.marker')) -and (Test-Path -LiteralPath (Join-Path $application 'engine\sing-box.exe'))) 'Installed package contains distribution marker and VPN components'
    Assert-Installer ((Test-Path -LiteralPath $desktop) -and (Test-Path -LiteralPath $menu)) 'Start menu and optional desktop shortcuts are created'
    Assert-Installer (!(Test-Path -LiteralPath (Join-Path $taskRoot 'dist\Callrift\installed.marker'))) 'Portable distribution remains separate from installed distribution'
    $data = Join-Path $fixtureRoot 'user-data'; New-Item -ItemType Directory -Path $data | Out-Null
    [IO.File]::WriteAllText((Join-Path $data 'favorites.json'),'["fixture"]')
    [IO.File]::WriteAllText((Join-Path $application 'user-note.txt'),'keep this user file')
    $favoriteHash = (Get-FileHash -LiteralPath (Join-Path $data 'favorites.json')).Hash
    [IO.File]::WriteAllText((Join-Path $application 'engine\sing-box.exe'),'fixture overwritten before upgrade')
    Assert-Installer ((Invoke-Setup $newSetup $options) -eq 0) 'New installer updates the existing installation in place'
    Assert-Installer ((Get-ItemProperty -LiteralPath $key).DisplayVersion -eq $currentVersion) 'Upgrade changes version without creating another uninstall entry'
    Assert-Installer ((Get-FileHash -LiteralPath (Join-Path $application 'engine\sing-box.exe')).Hash -eq '8f30682ee198855440d2a7fe070d5d3653793b2d01bb8c85eea88f93bbe7c8b9') 'Upgrade restores the expected engine executable'
    Assert-Installer (((Get-FileHash -LiteralPath (Join-Path $data 'favorites.json')).Hash -eq $favoriteHash) -and (Test-Path -LiteralPath (Join-Path $application 'user-note.txt'))) 'Upgrade preserves fixture user data and unknown user files'
    $mutex = New-Object Threading.Mutex $false,'Local\Callrift-Installer'
    try {
        Assert-Installer ((Invoke-Setup $newSetup $options) -ne 0) 'Installer refuses to replace a running Callrift session'
        Assert-Installer ((Invoke-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')) -ne 0) 'Uninstaller refuses to remove a running Callrift session'
        Assert-Installer (Test-Path -LiteralPath (Join-Path $application 'Callrift.exe')) 'Blocked uninstall preserves the installation'
    } finally { $mutex.Dispose() }
    Assert-Installer ((Invoke-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')) -eq 0) 'Uninstaller completes after the application is closed'
    Assert-Installer (!(Test-Path -LiteralPath $key) -and !(Test-Path -LiteralPath (Join-Path $application 'Callrift.exe'))) 'Uninstall removes registration and application files'
    Assert-Installer (!(Test-Path -LiteralPath $desktop) -and !(Test-Path -LiteralPath $menu)) 'Uninstall removes its shortcuts'
    Assert-Installer (((Get-FileHash -LiteralPath (Join-Path $data 'favorites.json')).Hash -eq $favoriteHash) -and (Test-Path -LiteralPath (Join-Path $application 'user-note.txt'))) 'Uninstall preserves fixture data and unknown user files'
} finally {
    # Only our unique test registration and path may be cleaned up; never a user's installation.
    if ((Test-Path -LiteralPath $key) -and (Test-Path -LiteralPath $uninstaller)) { Invoke-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART') | Out-Null }
    $results | Set-Content -LiteralPath (Join-Path $fixtureRoot 'results.txt') -Encoding UTF8
}
$results
Write-Output ('RESULT ' + $results.Count + '/' + $results.Count + ' installer checks passed')
