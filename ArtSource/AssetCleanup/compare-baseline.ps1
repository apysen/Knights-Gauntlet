param([ValidateSet('Restore','Delete')][string]$Mode)
$ErrorActionPreference='Stop'
$projectRoot=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$assetsRoot=Join-Path $projectRoot 'Assets'
$prefix=$assetsRoot+[IO.Path]::DirectorySeparatorChar
Add-Type -AssemblyName System.IO.Compression
$pairs=@(@('candidates.json','result.json'),@('additional.json','additional-result.json'))
foreach($pair in $pairs){
    $manifest=Get-Content -LiteralPath (Join-Path $PSScriptRoot $pair[0]) -Raw | ConvertFrom-Json
    $result=Get-Content -LiteralPath (Join-Path $PSScriptRoot $pair[1]) -Raw | ConvertFrom-Json
    $zip=[IO.Compression.ZipFile]::OpenRead($result.backup)
    try{
        foreach($item in $manifest){
            foreach($relative in @($item.path,($item.path+'.meta'))){
                $target=[IO.Path]::GetFullPath((Join-Path $projectRoot $relative))
                if(!$target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Path escaped Assets'}
                $entry=$zip.GetEntry($relative)
                if($Mode -eq 'Restore'){
                    if($null -ne $entry -and !(Test-Path -LiteralPath $target)){
                        [void][IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
                        [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
                    }
                }elseif(Test-Path -LiteralPath $target){
                    if((Get-Item -LiteralPath $target).Length -ne $entry.Length){throw "File changed since baseline restore: $target"}
                    Remove-Item -LiteralPath $target -Force
                }
            }
        }
        if($Mode -eq 'Restore'){
            # Restore original folder GUIDs before Unity imports the comparison.
            foreach($entry in $zip.Entries){
                if(!$entry.FullName.EndsWith('.meta')){continue}
                $target=[IO.Path]::GetFullPath((Join-Path $projectRoot $entry.FullName))
                if(!$target.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)){throw 'Archive path escaped Assets'}
                if(!(Test-Path -LiteralPath $target) -and (Test-Path -LiteralPath $target.Substring(0,$target.Length-5) -PathType Container)){
                    [IO.Compression.ZipFileExtensions]::ExtractToFile($entry,$target,$false)
                }
            }
        }
    }finally{$zip.Dispose()}
}
if($Mode -eq 'Delete'){
    $folders=Get-ChildItem -LiteralPath $assetsRoot -Directory -Recurse -Force | Sort-Object {$_.FullName.Length} -Descending
    foreach($folder in $folders){
        if(!$folder.FullName.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or ($folder.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Unexpected directory path'}
        if([IO.Directory]::GetFileSystemEntries($folder.FullName).Length -eq 0){
            Remove-Item -LiteralPath $folder.FullName -Force
            if(Test-Path -LiteralPath ($folder.FullName+'.meta')){Remove-Item -LiteralPath ($folder.FullName+'.meta') -Force}
        }
    }
}
Write-Output "Baseline comparison asset operation complete: $Mode"
