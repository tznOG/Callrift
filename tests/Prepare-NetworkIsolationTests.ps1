param([string]$ApplicationDirectory = (Join-Path $PSScriptRoot '..\dist\Callrift-review'))
$ErrorActionPreference = 'Stop'
$taskOutput = Join-Path $PSScriptRoot 'output\network-isolation'
$taskFramework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
New-Item -ItemType Directory -Force -Path $taskOutput,(Join-Path $taskOutput 'engine') | Out-Null
Copy-Item -LiteralPath (Join-Path $ApplicationDirectory 'Callrift.exe'),(Join-Path $ApplicationDirectory 'Callrift.exe.config') -Destination $taskOutput -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Read-NetworkIsolationDiagnostics.ps1') -Destination $taskOutput -Force
foreach ($taskFile in @('sing-box.exe','wintun.dll','ProtonVPN.CalloutDriver.sys')) { Copy-Item -LiteralPath (Join-Path $ApplicationDirectory "engine\$taskFile") -Destination (Join-Path $taskOutput 'engine') -Force }
& (Join-Path $taskFramework 'csc.exe') /nologo /target:exe /platform:x64 /codepage:65001 ('/reference:' + (Join-Path $taskOutput 'Callrift.exe')) /reference:System.dll /reference:System.Core.dll /reference:System.Security.dll /reference:System.Web.Extensions.dll ('/out:' + (Join-Path $taskOutput 'NetworkIsolationTests.exe')) (Join-Path $PSScriptRoot 'NetworkIsolationTests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Integration test compilation failed.' }
Copy-Item -LiteralPath (Join-Path $taskOutput 'NetworkIsolationTests.exe') -Destination (Join-Path $taskOutput 'DirectProbe.exe') -Force
Copy-Item -LiteralPath (Join-Path $taskOutput 'NetworkIsolationTests.exe') -Destination (Join-Path $taskOutput 'SelectedProbe.exe') -Force
Write-Output "Prepared only; no driver, tunnel or network test was started: $taskOutput"
