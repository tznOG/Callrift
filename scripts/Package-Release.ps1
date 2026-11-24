param([switch]$SkipBuild, [switch]$SkipTests, [string]$Repository, [string]$ApplicationDirectory, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$application = if ($ApplicationDirectory) { [IO.Path]::GetFullPath($ApplicationDirectory) } else { Join-Path $taskRoot 'dist\Callrift' }
$destination = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'dist' }
New-Item -ItemType Directory -Force -Path $destination | Out-Null
if (!$SkipBuild) {
    $buildArguments = @{}
    if ($PSBoundParameters.ContainsKey('Repository')) { $buildArguments.Repository = $Repository }
    $buildArguments.OutputDirectory = $application
    & (Join-Path $taskRoot 'build.ps1') @buildArguments
}
if (!$SkipTests) { & (Join-Path $taskRoot 'tests\Run-Tests.ps1') -ApplicationDirectory $application }
$required = @('Callrift.exe','Callrift.exe.config','ActivityHost.exe','LEIA-ME.md','licenses\LICENSE','licenses\THIRD-PARTY-NOTICES.md','licenses\Disactivity-MIT.txt','licenses\Inno-Setup-LICENSE.txt','engine\sing-box.exe','engine\wintun.dll','engine\sing-box-LICENSE.txt','engine\GPL-3.0.txt','engine\Wintun-COPYING.txt','sources\sing-box-v1.14.4.tar.gz')
$required += @('engine\ProtonVPN.CalloutDriver.sys','engine\ProtonVPN-CalloutDriver-LICENSE.txt','sources\ProtonVPN-CalloutDriver-a4e78deae2e123ee0ac1c48cc69a66647c5228da.zip')
foreach ($file in $required) {
    if (!(Test-Path -LiteralPath (Join-Path $application $file) -PathType Leaf)) { throw "Missing package file: $file" }
}
if ((Get-FileHash -LiteralPath (Join-Path $application 'engine\sing-box.exe') -Algorithm SHA256).Hash -ne '8f30682ee198855440d2a7fe070d5d3653793b2d01bb8c85eea88f93bbe7c8b9') { throw 'Unexpected sing-box executable.' }
if ((Get-FileHash -LiteralPath (Join-Path $application 'engine\wintun.dll') -Algorithm SHA256).Hash -ne 'e5da8447dc2c320edc0fc52fa01885c103de8c118481f683643cacc3220dafce') { throw 'Unexpected Wintun DLL.' }
if ((Get-FileHash -LiteralPath (Join-Path $application 'engine\ProtonVPN.CalloutDriver.sys') -Algorithm SHA256).Hash -ne 'ba788ffdc1bd95dd4aa0eccb1b43a5e512eae09fb2d8d623e7266bde65d79e00') { throw 'Unexpected isolation driver.' }
$privateFiles = Get-ChildItem -LiteralPath $application -File -Recurse | Where-Object { $_.Extension -in @('.conf','.profile','.key','.pem','.pfx','.p12','.log') -or $_.Name -in @('settings.json','favorites.json','profile-tests.json','games-cache.json','last-error.txt','.env') }
if ($privateFiles) { throw 'User data found in the package directory. Remove it before packaging.' }
if (Test-Path -LiteralPath (Join-Path $application 'installed.marker')) { throw 'The portable directory contains an installer marker.' }
$version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $application 'Callrift.exe')).ProductVersion
if ($version -notmatch '^\d+\.\d+\.\d+(-preview\.[1-9][0-9]*)?$') { throw 'Invalid application version.' }
$archive = Join-Path $destination ('Callrift-' + $version + '-Windows-x64.zip')
Compress-Archive -LiteralPath $application -DestinationPath $archive -CompressionLevel Optimal -Force
& (Join-Path $PSScriptRoot 'Build-Installer.ps1') -PackageDirectory $application -OutputDirectory $destination
$installer = Join-Path $destination ('Callrift-Setup-' + $version + '.exe')
if (!(Test-Path -LiteralPath $installer)) { throw 'Installer was not generated.' }
& (Join-Path $PSScriptRoot 'Export-Source.ps1') -Version $version -OutputDirectory $destination
$sourceArchive = Join-Path $destination ('Callrift-' + $version + '-source.zip')
$checksums = @($archive,$installer,$sourceArchive) | ForEach-Object { (Get-FileHash -LiteralPath $_ -Algorithm SHA256).Hash.ToLowerInvariant() + '  ' + [IO.Path]::GetFileName($_) }
[IO.File]::WriteAllText((Join-Path $destination 'SHA256SUMS.txt'), ($checksums -join "`n") + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Output $archive
Write-Output $installer
Write-Output $checksums
