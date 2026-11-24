param([string]$PackageDirectory, [string]$OutputDirectory, [string]$Identity, [string]$Version, [string]$DisplayName, [string]$SessionMutex)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$PackageDirectory) { $PackageDirectory = Join-Path $taskRoot 'dist\Callrift' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $taskRoot 'dist' }
$PackageDirectory = [IO.Path]::GetFullPath($PackageDirectory)
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
if (!(Test-Path -LiteralPath (Join-Path $PackageDirectory 'Callrift.exe'))) { throw 'Build the app before creating the installer.' }
$compiler = & (Join-Path $PSScriptRoot 'Get-InstallerCompiler.ps1')
$arguments = @('/Qp',('/DPackageDir=' + $PackageDirectory),('/DOutputDir=' + $OutputDirectory))
if ($Identity) { $arguments += '/DAppIdentity=' + $Identity }
if (!$Version) { $Version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $PackageDirectory 'Callrift.exe')).ProductVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+(-preview\.[1-9][0-9]*)?$') { throw 'Invalid version.' }
$arguments += '/DAppVersion=' + $Version
if ($SessionMutex) { if ($SessionMutex -notmatch '^Local\\Callrift-Packaging-Test-[a-f0-9]{32}$') { throw 'Invalid test mutex.' }; $arguments += '/DAppSessionMutex=' + $SessionMutex }
if ($DisplayName) { $arguments += '/DAppName=' + $DisplayName }
& $compiler @arguments (Join-Path $taskRoot 'installer\Callrift.iss')
if ($LASTEXITCODE -ne 0) { throw 'Failed to build the installer.' }
