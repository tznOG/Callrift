param([switch]$Network)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$output = Join-Path $PSScriptRoot 'output'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$application = Join-Path $taskRoot 'dist\Callrift'
Copy-Item -LiteralPath (Join-Path $application 'Callrift.exe'),(Join-Path $application 'Callrift.exe.config'),(Join-Path $application 'ActivityHost.exe') -Destination $output -Force
New-Item -ItemType Directory -Force -Path (Join-Path $output 'engine') | Out-Null
Copy-Item -LiteralPath (Join-Path $application 'engine\sing-box.exe'),(Join-Path $application 'engine\wintun.dll') -Destination (Join-Path $output 'engine') -Force
$refs = @('System.dll','System.Core.dll','System.Security.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
& (Join-Path $framework 'csc.exe') /nologo /target:exe /platform:x64 /codepage:65001 ('/reference:' + (Join-Path $output 'Callrift.exe')) @refs ('/out:' + (Join-Path $output 'Tests.exe')) (Join-Path $PSScriptRoot 'Tests.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
$arguments = @((Join-Path $application 'engine\sing-box.exe')); if ($Network) { $arguments += '--network' }
& (Join-Path $output 'Tests.exe') @arguments | Tee-Object -FilePath (Join-Path $output 'results.txt')
if ($LASTEXITCODE -ne 0) { throw 'Falha na validação.' }
