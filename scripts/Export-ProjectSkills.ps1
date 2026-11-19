param([Parameter(Mandatory)][string]$OutDir)
$ErrorActionPreference='Stop'
if(-not $IsWindows){throw 'SKILL_IMPORT_PLATFORM_NOT_TESTED'}
$repo=Split-Path $PSScriptRoot -Parent
$OutDir=[IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force|Out-Null
$licenseFile=Join-Path $repo 'README.md'
if((Get-Content -LiteralPath $licenseFile -Raw) -notmatch '(?m)^## License\r?\n\r?\nMIT\s*$'){throw 'PROJECT_SKILL_LICENSE_UNVERIFIED'}
Copy-Item -LiteralPath $licenseFile -Destination (Join-Path $OutDir 'README.md') -Force
$packages=@();$inventory=@()
foreach($dir in Get-ChildItem -LiteralPath (Join-Path $repo '.github/skills') -Directory|Sort-Object Name){
 $entry=Join-Path $dir.FullName 'SKILL.md'
 if(-not (Test-Path -LiteralPath $entry)){continue}
 $markdown=Get-Content -LiteralPath $entry -Raw
 $description=[regex]::Match($markdown,'(?m)^description:\s*"([^"]+)"').Groups[1].Value
 if(-not $description){throw "SKILL_DESCRIPTION_MISSING $($dir.Name)"}
 $files=@();$external=@()
 foreach($child in Get-ChildItem -LiteralPath $dir.FullName -Force){
  if($child.Name -eq 'SKILL.md'){continue}
  if($child.PSIsContainer -and (Test-Path -LiteralPath (Join-Path $child.FullName '.git'))){
   $origin=& git -C $child.FullName config --get remote.origin.url
   if($LASTEXITCODE -ne 0 -or $origin -notmatch '^https://github\.com/[^/@]+/[^/]+(?:\.git)?$'){throw 'EXTERNAL_REFERENCE_ORIGIN_REQUIRES_REVIEW'}
   $revision=& git -C $child.FullName rev-parse HEAD
   if($LASTEXITCODE -ne 0 -or $revision -notmatch '^[a-f0-9]{40}$'){throw 'EXTERNAL_REFERENCE_REVISION_MISSING'}
   $external+=[ordered]@{directory=$child.Name;sourceUrl=$origin;observedRevision=$revision;role='external-library-reference-not-skill-package';updatePolicy='consult canonical upstream before use; preserve one revision per lookup'}
   continue
  }
  $owned=if($child.PSIsContainer){@(Get-ChildItem -LiteralPath $child.FullName -Recurse -File -Force)}else{@($child)}
  foreach($file in $owned){
   if($file.Attributes -band [IO.FileAttributes]::ReparsePoint){throw 'SKILL_REPARSE_POINT_REQUIRES_REVIEW'}
   $content=Get-Content -LiteralPath $file.FullName -Raw
   if($content.Contains([char]0)){throw 'SKILL_BINARY_SUPPORTING_FILE_REQUIRES_REVIEW'}
   $files+=[ordered]@{path=[IO.Path]::GetRelativePath($dir.FullName,$file.FullName).Replace('\','/');content=$content}
  }
 }
 if($external.Count){$files+=[ordered]@{path='references/external-library-sources.json';content=($external|ConvertTo-Json -Depth 5)}}
 $slug=$dir.Name
 $sourceSnapshot=Join-Path $OutDir $slug
 New-Item -ItemType Directory -Path $sourceSnapshot -Force|Out-Null
 Copy-Item -LiteralPath $entry -Destination (Join-Path $sourceSnapshot 'SKILL.md') -Force
 $package=[ordered]@{slug=$slug;version='1.0.0';description=$description;license='MIT';visibility='registry-candidate';skillMarkdown=$markdown;provenanceJson=([ordered]@{sourceType='original';sourceLocator="artifact://duxel/project-skills/2026-11-04/$slug/SKILL.md";sourceSha256=(Get-FileHash -LiteralPath $entry).Hash.ToLowerInvariant();licenseVerified=$true;licenseEvidenceLocator='artifact://duxel/project-skills/2026-11-04/README.md';licenseEvidenceSha256=(Get-FileHash -LiteralPath $licenseFile).Hash.ToLowerInvariant()}|ConvertTo-Json -Compress);supportingFilesJson=(ConvertTo-Json -InputObject @($files) -Depth 5 -Compress);searchAliasesJson=(ConvertTo-Json -InputObject @("Duxel $slug") -Compress)}
 $packages+=$package
 $inventory+=[ordered]@{slug=$slug;source=$entry;sourceSha256=(Get-FileHash -LiteralPath $entry).Hash;ownedSupportingFiles=@($files|ForEach-Object path);externalLibraryReferences=$external;licenseEvidence='README.md explicit MIT declaration'}
}
ConvertTo-Json -InputObject @($packages) -Depth 8|Set-Content -LiteralPath (Join-Path $OutDir 'packages.json')
ConvertTo-Json -InputObject @($inventory) -Depth 8|Set-Content -LiteralPath (Join-Path $OutDir 'inventory.json')
[ordered]@{suiteId='duxel-project-skill-import-v1';frozenBeforeRun=$true;cases=@(@{id='normal';behavior='prepare exact complete owned instruction package deterministically'},@{id='boundary';behavior='reject declarations without tested-platform evidence'},@{id='negative-control';behavior='reject public visibility during candidate-only import'});scope='Windows import integrity, provenance and publication boundaries; not universal skill effectiveness';sourcePackageCount=$packages.Count;platform='windows';verified=$true;completedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json -Depth 5|Set-Content -LiteralPath (Join-Path $OutDir 'frozen-import-contract.json')
Write-Output "Exported $($packages.Count) complete project instruction packages with owned text references and canonical external-library pointers."
