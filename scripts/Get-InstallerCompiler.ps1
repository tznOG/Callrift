$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path $PSScriptRoot -Parent
$compilerRoot = Join-Path $taskRoot 'tools\innosetup-7.1.1'
$setup = Join-Path $taskRoot 'tools\innosetup-7.1.1-x64.exe'
New-Item -ItemType Directory -Force -Path (Split-Path $setup -Parent) | Out-Null
if (!(Test-Path -LiteralPath $setup)) {
    Invoke-WebRequest -Uri 'https://github.com/jrsoftware/issrc/releases/download/is-7_1_1/innosetup-7.1.1-x64.exe' -OutFile $setup -UseBasicParsing
}
if ((Get-FileHash -LiteralPath $setup -Algorithm SHA256).Hash -ne '8a5a6711c3766a6e5fb74f280ba0e3551e719ce94538d901fdbd1f8f9920b3f0') { throw 'Unexpected Inno Setup download hash.' }
$compiler = Join-Path $compilerRoot 'ISCC.exe'
if (!(Test-Path -LiteralPath $compiler)) {
    # The official compiler's portable mode creates no uninstaller or installed-app entry.
    $process = Start-Process -FilePath $setup -ArgumentList @('/PORTABLE=1','/CURRENTUSER','/VERYSILENT','/SUPPRESSMSGBOXES','/SP-','/NORESTART',('/DIR="' + $compilerRoot + '"')) -WindowStyle Hidden -Wait -PassThru
    if ($process.ExitCode -ne 0 -or !(Test-Path -LiteralPath $compiler)) { throw 'Could not extract portable Inno Setup compiler.' }
}
Write-Output $compiler
