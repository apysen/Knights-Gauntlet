param([string]$ManifestName = 'candidates.json', [string]$ResultName = 'result.json')
$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assetsRoot = [IO.Path]::GetFullPath((Join-Path $projectRoot 'Assets'))
$assetsPrefix = $assetsRoot + [IO.Path]::DirectorySeparatorChar
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot $ManifestName) -Raw | ConvertFrom-Json
$targets = [Collections.Generic.List[string]]::new()
foreach ($entry in $manifest) {
    $target = [IO.Path]::GetFullPath((Join-Path $projectRoot $entry.path))
    if (!$target.StartsWith($assetsPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw "Target outside Assets: $target" }
    $item = Get-Item -LiteralPath $target
    if ($item.PSIsContainer -or ($item.Attributes -band [IO.FileAttributes]::ReparsePoint)) { throw "Expected ordinary file: $target" }
    if ($item.Length -ne $entry.bytes) { throw "File changed since audit: $target" }
    $targets.Add($target)
    if (Test-Path -LiteralPath ($target + '.meta')) { $targets.Add($target + '.meta') }
}
$folders = @(Get-ChildItem -LiteralPath $assetsRoot -Directory -Recurse -Force)
foreach ($folder in $folders) {
    if ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint) { throw "Unexpected linked directory: $($folder.FullName)" }
    if (!$folder.FullName.StartsWith($assetsPrefix, [StringComparison]::OrdinalIgnoreCase)) { throw 'Directory escaped Assets' }
}

# Store a recovery archive outside both Assets and the repository.
Add-Type -AssemblyName System.IO.Compression
$backup = Join-Path ([IO.Path]::GetTempPath()) ('KnightsGauntlet-unused-assets-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
$archiveFiles = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
foreach ($file in $targets) { [void]$archiveFiles.Add($file) }
foreach ($folder in $folders) { if (Test-Path -LiteralPath ($folder.FullName+'.meta')) { [void]$archiveFiles.Add($folder.FullName+'.meta') } }
$zip = [IO.Compression.ZipFile]::Open($backup, [IO.Compression.ZipArchiveMode]::Create)
try {
    foreach ($file in $archiveFiles) {
        $relative = [IO.Path]::GetRelativePath($projectRoot,$file).Replace('\','/')
        [void][IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$file,$relative,[IO.Compression.CompressionLevel]::Fastest)
    }
} finally { $zip.Dispose() }
$check = [IO.Compression.ZipFile]::OpenRead($backup)
try {
    if ($check.Entries.Count -ne $archiveFiles.Count) { throw 'Backup verification failed' }
    foreach ($entry in $check.Entries) {
        $source = Join-Path $projectRoot $entry.FullName
        if ($entry.Length -ne (Get-Item -LiteralPath $source).Length) { throw "Archive length mismatch: $source" }
    }
} finally { $check.Dispose() }

# Keep license/source notices available without importing them into Unity.
foreach ($entry in $manifest) {
    if ([IO.Path]::GetFileName($entry.path) -match '(?i)license|licence|copyright|notice|readme|credits') {
        $notice = Join-Path (Join-Path $PSScriptRoot 'Notices') $entry.path
        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($notice))
        Copy-Item -LiteralPath (Join-Path $projectRoot $entry.path) -Destination $notice
    }
}

foreach ($file in $targets) { Remove-Item -LiteralPath $file -Force }
$removedFolders = 0
foreach ($folder in ($folders | Sort-Object { $_.FullName.Length } -Descending)) {
    if ([IO.Directory]::GetFileSystemEntries($folder.FullName).Length -eq 0) {
        Remove-Item -LiteralPath $folder.FullName -Force
        if (Test-Path -LiteralPath ($folder.FullName+'.meta')) { Remove-Item -LiteralPath ($folder.FullName+'.meta') -Force }
        $removedFolders++
    }
}
$result = [ordered]@{ removedAssets=$manifest.Count; removedAssetBytes=($manifest | Measure-Object bytes -Sum).Sum; removedFolders=$removedFolders; backup=$backup; backupEntries=$archiveFiles.Count }
$result | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $PSScriptRoot $ResultName)
$result | ConvertTo-Json
