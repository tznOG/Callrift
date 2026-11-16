param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$cache = Join-Path $taskRoot 'tools'
New-Item -ItemType Directory -Force -Path $cache,$OutputDirectory | Out-Null
$zip = Join-Path $cache 'sing-box.zip'
$url = 'https://github.com/SagerNet/sing-box/releases/download/v1.14.4/sing-box-1.14.4-windows-amd64.zip'
if (!(Test-Path -LiteralPath $zip)) { Invoke-WebRequest -Uri $url -OutFile $zip -UseBasicParsing }
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne '99185d75d314a07dbba4a130c8ad986cbeb629a07927975636b341ca61f991df') { throw 'SHA256 do pacote sing-box não confere.' }
Expand-Archive -LiteralPath $zip -DestinationPath $cache -Force
$binary = Join-Path $cache 'sing-box-1.14.4-windows-amd64\sing-box.exe'
if ((Get-FileHash -LiteralPath $binary -Algorithm SHA256).Hash -ne '8f30682ee198855440d2a7fe070d5d3653793b2d01bb8c85eea88f93bbe7c8b9') { throw 'SHA256 do sing-box.exe não confere.' }
Copy-Item -LiteralPath $binary -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $cache 'sing-box-1.14.4-windows-amd64\LICENSE') -Destination (Join-Path $OutputDirectory 'sing-box-LICENSE.txt') -Force
$gpl = Join-Path $cache 'GPL-3.0.txt'
if (!(Test-Path -LiteralPath $gpl)) { Invoke-WebRequest -Uri 'https://www.gnu.org/licenses/gpl-3.0.txt' -OutFile $gpl -UseBasicParsing }
Copy-Item -LiteralPath $gpl -Destination $OutputDirectory -Force
# Wintun's signed redistributable includes its own permissive binary distribution license.
$wintun = Join-Path $cache 'wintun.zip'
if (!(Test-Path -LiteralPath $wintun)) { Invoke-WebRequest -Uri 'https://www.wintun.net/builds/wintun-0.14.1.zip' -OutFile $wintun -UseBasicParsing }
if ((Get-FileHash -LiteralPath $wintun -Algorithm SHA256).Hash -ne '07c256185d6ee3652e09fa55c0b673e2624b565e02c4b9091c79ca7d2f24ef51') { throw 'SHA256 do Wintun não confere.' }
Expand-Archive -LiteralPath $wintun -DestinationPath $cache -Force
Copy-Item -LiteralPath (Join-Path $cache 'wintun\bin\amd64\wintun.dll') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $cache 'wintun\LICENSE.txt') -Destination (Join-Path $OutputDirectory 'Wintun-COPYING.txt') -Force
$sourceDirectory = Join-Path (Split-Path $OutputDirectory -Parent) 'sources'
New-Item -ItemType Directory -Force -Path $sourceDirectory | Out-Null
$source = Join-Path $sourceDirectory 'sing-box-v1.14.4.tar.gz'
if (!(Test-Path -LiteralPath $source)) { Invoke-WebRequest -Uri 'https://codeload.github.com/SagerNet/sing-box/tar.gz/refs/tags/v1.14.4' -OutFile $source -UseBasicParsing }
Write-Output 'Motor sing-box 1.14.4 e Wintun 0.14.1 verificados e preparados.'
