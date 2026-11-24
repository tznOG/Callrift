param([Parameter(Mandatory=$true)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
$taskRoot = Split-Path $PSScriptRoot -Parent
$taskManifestPath = Join-Path $PSScriptRoot 'isolation-driver-source.json'
$taskManifest = Get-Content -LiteralPath $taskManifestPath -Raw -Encoding UTF8 | ConvertFrom-Json
$taskCommit = 'a4e78deae2e123ee0ac1c48cc69a66647c5228da'
if ($taskManifest.Commit -cne $taskCommit) { throw 'Unexpected source revision.' }
$taskCache = Join-Path $taskRoot ('tools\proton-driver-source-' + $taskCommit)
$taskSources = Join-Path (Split-Path ([IO.Path]::GetFullPath($OutputDirectory)) -Parent) 'sources'
New-Item -ItemType Directory -Force -Path $taskCache,$taskSources,$OutputDirectory | Out-Null
$taskSeen = @{}
foreach ($taskFile in $taskManifest.Files) {
    if ($taskFile.Path -notmatch '^(LICENSE|README\.md|BUILD\.md|CONTRIBUTING\.md|src/ProtonVPN\.CalloutDriver/[A-Za-z0-9_./-]+)$' -or
        $taskFile.Path.Contains('..') -or $taskFile.Sha256 -notmatch '^[a-f0-9]{64}$' -or $taskSeen.ContainsKey($taskFile.Path)) { throw 'Invalid source manifest entry.' }
    $taskSeen[$taskFile.Path] = $true
    $taskLocal = Join-Path $taskCache $taskFile.Path
    New-Item -ItemType Directory -Force -Path (Split-Path $taskLocal -Parent) | Out-Null
    if (!(Test-Path -LiteralPath $taskLocal)) {
        Invoke-WebRequest -UseBasicParsing -Uri ('https://raw.githubusercontent.com/ProtonVPN/win-app/' + $taskCommit + '/' + $taskFile.Path) -OutFile $taskLocal
    }
    if ((Get-FileHash -LiteralPath $taskLocal -Algorithm SHA256).Hash -ine $taskFile.Sha256) { throw ('Source checksum mismatch: ' + $taskFile.Path) }
}
foreach ($taskRequired in @('LICENSE','README.md','src/ProtonVPN.CalloutDriver/Callout.cpp','src/ProtonVPN.CalloutDriver/Driver.cpp','src/ProtonVPN.CalloutDriver/ProtonVPN.CalloutDriver.vcxproj')) {
    if (!$taskSeen.ContainsKey($taskRequired)) { throw ('Missing source: ' + $taskRequired) }
}
Copy-Item -LiteralPath (Join-Path $taskCache 'LICENSE') -Destination (Join-Path $OutputDirectory 'ProtonVPN-CalloutDriver-LICENSE.txt') -Force
$taskZip = Join-Path $taskSources ('ProtonVPN-CalloutDriver-' + $taskCommit + '.zip')
$taskStream = [IO.File]::Open($taskZip,[IO.FileMode]::Create)
$taskArchive = New-Object IO.Compression.ZipArchive $taskStream,([IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($taskFile in $taskManifest.Files | Sort-Object Path) {
        $taskEntry = $taskArchive.CreateEntry(('proton-driver/' + $taskFile.Path),[IO.Compression.CompressionLevel]::Optimal)
        $taskEntry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
        $taskInput = [IO.File]::OpenRead((Join-Path $taskCache $taskFile.Path))
        $taskOutput = $taskEntry.Open()
        try { $taskInput.CopyTo($taskOutput) } finally { $taskOutput.Dispose(); $taskInput.Dispose() }
    }
    foreach ($taskExtra in @(@($taskManifestPath,'SOURCE-SHA256.json'),@((Join-Path $taskRoot 'docs\DRIVER-SOURCE.md'),'BUILD-AND-PROVENANCE.md'))) {
        $taskEntry = $taskArchive.CreateEntry(('proton-driver/' + $taskExtra[1]),[IO.Compression.CompressionLevel]::Optimal)
        $taskEntry.LastWriteTime = [DateTimeOffset]::Parse('2000-01-01T00:00:00Z')
        $taskInput = [IO.File]::OpenRead($taskExtra[0]); $taskOutput = $taskEntry.Open()
        try { $taskInput.CopyTo($taskOutput) } finally { $taskOutput.Dispose(); $taskInput.Dispose() }
    }
} finally { $taskArchive.Dispose(); $taskStream.Dispose() }
Write-Output 'Pinned driver sources and original license prepared.'
