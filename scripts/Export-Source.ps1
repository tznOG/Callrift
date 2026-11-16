param([string]$Version)
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskRoot = Split-Path $PSScriptRoot -Parent
if (!$Version) { $Version = [Diagnostics.FileVersionInfo]::GetVersionInfo((Join-Path $taskRoot 'dist\Callrift\Callrift.exe')).ProductVersion }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid source export version.' }
$files = @()
foreach ($name in @('.gitignore','LICENSE','THIRD-PARTY-NOTICES.md','README.md','build.ps1','release-config.json','CHANGELOG.md','CONTRIBUTING.md','SECURITY.md')) { $files += Get-Item -LiteralPath (Join-Path $taskRoot $name) }
foreach ($directory in @('src','assets','scripts','docs','.github','licenses','installer')) { $files += Get-ChildItem -LiteralPath (Join-Path $taskRoot $directory) -File -Recurse }
$files += Get-ChildItem -LiteralPath (Join-Path $taskRoot 'tests') -File | Where-Object Extension -in @('.cs','.ps1')
$destination = Join-Path $taskRoot ('dist\Callrift-' + $Version + '-source.zip')
New-Item -ItemType Directory -Path (Split-Path $destination -Parent) -Force | Out-Null
$stream = [IO.File]::Open($destination,[IO.FileMode]::Create)
$archive = New-Object IO.Compression.ZipArchive $stream,([IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $files | Sort-Object FullName -Unique) {
        $relative = $file.FullName.Substring($taskRoot.Length+1).Replace('\','/')
        if ($relative -match '(^|/)(\.git|tools|dist|upstream)(/|$)' -or $relative -match '\.(conf|profile|key|pem|log)$') { throw 'Unexpected source export file.' }
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive,$file.FullName,('callrift/' + $relative),[IO.Compression.CompressionLevel]::Optimal) | Out-Null
    }
} finally { $archive.Dispose(); $stream.Dispose() }
Write-Output $destination
