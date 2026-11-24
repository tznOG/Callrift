param([string]$OutputDirectory = (Join-Path $PSScriptRoot '..\dist\Callrift-review\engine'))
$ErrorActionPreference = 'Stop'
$taskCommit = 'a4e78deae2e123ee0ac1c48cc69a66647c5228da'
$taskHash = 'ba788ffdc1bd95dd4aa0eccb1b43a5e512eae09fb2d8d623e7266bde65d79e00'
$taskBase = "https://raw.githubusercontent.com/ProtonVPN/win-app/$taskCommit/"
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$taskDriver = Join-Path $OutputDirectory 'ProtonVPN.CalloutDriver.sys'
if (!(Test-Path -LiteralPath $taskDriver) -or (Get-FileHash -LiteralPath $taskDriver -Algorithm SHA256).Hash -ne $taskHash) {
    Invoke-WebRequest ($taskBase + 'Setup/Native/x64/ProtonVPN.CalloutDriver.sys') -UseBasicParsing -OutFile $taskDriver
}
if ((Get-FileHash -LiteralPath $taskDriver -Algorithm SHA256).Hash -ne $taskHash) { throw 'Unexpected isolation driver hash.' }
$taskSignature = Get-AuthenticodeSignature -LiteralPath $taskDriver
if ($taskSignature.Status -ne 'Valid' -or $taskSignature.SignerCertificate.Subject -notmatch 'CN=Proton AG,') { throw 'The Proton driver signature could not be validated.' }
& (Join-Path $PSScriptRoot 'Get-IsolationDriverSource.ps1') -OutputDirectory $OutputDirectory
Write-Output 'Isolation driver 1.0.5.0: official hash and Proton AG signature verified. Nothing installed or started.'
