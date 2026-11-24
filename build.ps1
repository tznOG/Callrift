param([switch]$SkipEngine, [string]$Repository, [string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
$output = if ($OutputDirectory) { [IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $taskRoot 'dist\Callrift' }
New-Item -ItemType Directory -Force -Path $output,(Join-Path $output 'engine'),(Join-Path $output 'licenses') | Out-Null
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.8 x64 é necessário para compilar.' }
$refs = @('System.dll','System.Core.dll','System.Security.dll','System.Web.Extensions.dll','System.Net.Http.dll','System.Xml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $framework $_) }
$releaseConfig = Get-Content -LiteralPath (Join-Path $taskRoot 'release-config.json') -Raw | ConvertFrom-Json
if ($PSBoundParameters.ContainsKey('Repository')) { $releaseConfig.Repository = $Repository }
if ($releaseConfig.Repository -notmatch '^[A-Za-z0-9][A-Za-z0-9-]{0,38}/[A-Za-z0-9_.-]{1,100}$') { throw 'Configure a valid GitHub owner/repository in release-config.json.' }
$compiledConfig = Join-Path $taskRoot 'tools\release-build.json'
New-Item -ItemType Directory -Force -Path (Split-Path $compiledConfig -Parent) | Out-Null
[IO.File]::WriteAllText($compiledConfig, ($releaseConfig | ConvertTo-Json), (New-Object Text.UTF8Encoding($false)))
$sources = Get-ChildItem (Join-Path $taskRoot 'src') -Filter '*.cs' | Where-Object Name -ne 'ActivityHost.cs' | ForEach-Object FullName
$icon = Join-Path $taskRoot 'assets\callrift.ico'
$iconArgs = @(); if (Test-Path -LiteralPath $icon) { $iconArgs = @('/win32icon:' + $icon) }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /codepage:65001 ('/out:' + (Join-Path $output 'Callrift.exe')) ('/win32manifest:' + (Join-Path $taskRoot 'src\app.manifest')) ('/resource:' + (Join-Path $taskRoot 'src\MainWindow.xaml') + ',MainWindow.xaml') ('/resource:' + (Join-Path $taskRoot 'src\Controls.xaml') + ',Controls.xaml') ('/resource:' + (Join-Path $taskRoot 'assets\brand\callrift-mark-128.png') + ',BrandMark.png') ('/resource:' + $compiledConfig + ',ReleaseConfig.json') @iconArgs @refs @sources
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar Callrift.' }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output /codepage:65001 ('/out:' + (Join-Path $output 'ActivityHost.exe')) (Join-Path $taskRoot 'src\ActivityHost.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar ActivityHost.' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'src\Callrift.exe.config') -Destination $output -Force
if (!$SkipEngine) { & (Join-Path $taskRoot 'scripts\Get-Engine.ps1') -OutputDirectory (Join-Path $output 'engine') }
& (Join-Path $taskRoot 'scripts\Get-IsolationDriver.ps1') -OutputDirectory (Join-Path $output 'engine')
Copy-Item -LiteralPath (Join-Path $taskRoot 'LICENSE'),(Join-Path $taskRoot 'THIRD-PARTY-NOTICES.md') -Destination (Join-Path $output 'licenses') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'licenses\Disactivity-MIT.txt') -Destination (Join-Path $output 'licenses\Disactivity-MIT.txt') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'licenses\Inno-Setup-LICENSE.txt') -Destination (Join-Path $output 'licenses') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'docs\USAGE.md') -Destination (Join-Path $output 'LEIA-ME.md') -Force
Write-Output "Compilado: $output\Callrift.exe"
