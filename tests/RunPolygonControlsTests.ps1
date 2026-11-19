param([Parameter(Mandatory)][string]$OutDir)
$ErrorActionPreference='Stop'
$repo=Split-Path $PSScriptRoot -Parent
$OutDir=[IO.Path]::GetFullPath($OutDir)
New-Item -ItemType Directory -Path $OutDir -Force|Out-Null
$resultPath=Join-Path $OutDir 'controls.json'
if(Test-Path -LiteralPath $resultPath){throw 'CONTROL_RESULT_ALREADY_EXISTS'}
$original=Join-Path $repo 'samples/fba/Duxel_perf_test_fba.cs'
$source=(Get-Content -LiteralPath $original -Raw).Replace("`r`n","`n")
$originalHash=(Get-FileHash -LiteralPath $original).Hash
$source=$source.Replace('#:property TargetFramework=net10.0',"#:property TargetFramework=net10.0`n#:property AllowUnsafeBlocks=true")
$source=$source.Replace('Title = "Duxel Performance Test (FBA)"','Title = $"Duxel Controls Validation {Environment.ProcessId}"')
$markers=@{
 'Dynamic Canvas Layer'='        ui.Checkbox("Dynamic Canvas Layer", ref _dynamicCanvas);'
 'VSync'="        if (ui.Checkbox(`"VSync`", ref vsync))`n        {`n            ui.SetVSync(vsync);`n        }"
 'Global Static Cache'="        if (ui.Checkbox(`"Global Static Cache`", ref globalStaticCache))`n        {`n            _enableGlobalStaticCache = globalStaticCache;`n`n        }"
 'Render Profile'="        if (ui.Checkbox(`"Render Profile`", ref renderProfile))`n        {`n            ApplyProfile(ui, renderProfile ? DuxelPerformanceProfile.Render : DuxelPerformanceProfile.Display);`n        }"
}
foreach($label in $markers.Keys){
 $marker=$markers[$label]
 if([regex]::Matches($source,[regex]::Escape($marker)).Count -ne 1){throw "CONTROL_FIXTURE_MARKER_MISMATCH $label"}
 $source=$source.Replace($marker,$marker+"`n"+('        PerfControlProbe.Record(ui,"{0}");' -f $label))
}
foreach($label in @('Add','Remove','Reset','Clear')){
 $pattern='(?s)        if \(ui.Button\("'+$label+'"\)\)\s*\{.*?\n        \}'
 $matches=[regex]::Matches($source,$pattern)
 if($matches.Count -ne 1){throw "CONTROL_FIXTURE_BUTTON_MISMATCH $label"}
 $source=$source.Replace($matches[0].Value,$matches[0].Value+"`n"+('        PerfControlProbe.Record(ui,"{0}");' -f $label))
}
$renderMarker='        DrawControls(ui, bounds, renderBounds);'
if([regex]::Matches($source,[regex]::Escape($renderMarker)).Count -ne 1){throw 'CONTROL_FIXTURE_RENDER_MISMATCH'}
$source=$source.Replace($renderMarker,$renderMarker+"`n        PerfControlProbe.Tick(ui,_polygons.Count,_gpuParticles,_dynamicCanvas,_enableGlobalStaticCache,_activeProfile == DuxelPerformanceProfile.Render);")
$source+="`n"+(Get-Content -LiteralPath (Join-Path $PSScriptRoot 'PolygonControlsProbe.cs.txt') -Raw)
$fixture=Join-Path $OutDir 'perf_controls_validation.cs'
Set-Content -LiteralPath $fixture -Value $source
$selector=Join-Path $repo 'tests/runtime-net11'
$build=@{Path=$fixture;DotnetPath='C:/Program Files/dotnet/dotnet.exe';TargetFramework='net11.0-windows7.0';NoCache=$true;NoLaunch=$true;ExtraArgs=@('-p:OptimizationPreference=Speed')}
Push-Location -LiteralPath $selector
try {& (Join-Path $repo 'run-fba.ps1') @build 1>(Join-Path $OutDir 'build.stdout.log') 2>(Join-Path $OutDir 'build.stderr.log')}
finally {Pop-Location}
$publish=Join-Path $OutDir 'artifacts/perf_controls_validation/net11.0-windows7.0'
$exe=Join-Path $publish 'perf_controls_validation.exe'
if(-not (Test-Path -LiteralPath $exe)){throw 'CONTROL_NATIVE_EXE_MISSING'}
$variables=@('DUXEL_CONTROLS_PROBE_OUT','DUXEL_PERF_BENCH_SECONDS','DUXEL_PERF_BENCH_OUT','DUXEL_PERF_SIMULATION','DUXEL_PERF_INITIAL_POLYGONS','DUXEL_PERF_INITIAL_SIZE','DUXEL_PERF_PAUSED','DUXEL_PERF_PROFILE','DUXEL_PERF_DYNAMIC_CANVAS','DUXEL_PERF_GLOBAL_STATIC_CACHE','DUXEL_PERF_PIPELINE','DUXEL_VK_PROFILE','DUXEL_VK_GPU_PROFILE','DUXEL_VK_PROFILE_EVERY')
$saved=@{}
foreach($name in $variables){$saved[$name]=[Environment]::GetEnvironmentVariable($name);[Environment]::SetEnvironmentVariable($name,$null)}
$process=$null
try {
 $env:DUXEL_CONTROLS_PROBE_OUT=$resultPath;$env:DUXEL_VK_PROFILE='1';$env:DUXEL_VK_PROFILE_EVERY='1000000'
 $process=Start-Process -FilePath $exe -WorkingDirectory $publish -WindowStyle Normal -PassThru -RedirectStandardOutput (Join-Path $OutDir 'run.stdout.log') -RedirectStandardError (Join-Path $OutDir 'run.stderr.log')
 $watch=[Diagnostics.Stopwatch]::StartNew();$visible=$false
 while(-not $process.HasExited){$process.Refresh();if($process.MainWindowHandle -ne 0){$visible=$true};if($watch.Elapsed.TotalSeconds -gt 20){$null=$process.CloseMainWindow();throw 'CONTROL_NATIVE_TIMEOUT'};Start-Sleep -Milliseconds 100}
 $process.WaitForExit()
 $result=Get-Content -LiteralPath $resultPath -Raw|ConvertFrom-Json
 if($process.ExitCode -ne 0 -or -not $visible -or -not $result.complete -or $result.stage -ne 13){throw 'CONTROL_NATIVE_INTERACTION_FAILED'}
 if((Get-FileHash -LiteralPath $original).Hash -ne $originalHash){throw 'CONTROL_SOURCE_CHANGED_DURING_VALIDATION'}
 $nativeHash=(Get-FileHash -LiteralPath $exe).Hash
 [ordered]@{complete=$true;source=$original;sourceSha256=$originalHash;fixture=$fixture;fixtureSha256=(Get-FileHash -LiteralPath $fixture).Hash;executableSha256=$nativeHash;visible=$visible;exitCode=$process.ExitCode;checks=$result.passed;completedUtc=[DateTime]::UtcNow.ToString('o')}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutDir 'execution-record.json')
 # Delete only this completed fixture's owned binary directory after preserving all file hashes.
 $recordPath=@(Get-ChildItem -LiteralPath (Join-Path $OutDir 'artifacts/perf_controls_validation/.build-records') -File|Where-Object Name -Like 'publish-*.json')
 if($recordPath.Count -ne 1){throw 'CONTROL_FIXTURE_OWNERSHIP_AMBIGUOUS'}
 $record=Get-Content -LiteralPath $recordPath[0].FullName -Raw|ConvertFrom-Json
 if($record.status -ne 'completed' -or [IO.Path]::GetFullPath($record.directory) -ne [IO.Path]::GetFullPath($publish) -or -not [IO.Path]::GetFullPath($publish).StartsWith($OutDir+[IO.Path]::DirectorySeparatorChar,[StringComparison]::OrdinalIgnoreCase)){throw 'CONTROL_FIXTURE_CLEANUP_BOUNDARY'}
 foreach($file in $record.snapshot.files){if((Get-FileHash -LiteralPath (Join-Path $publish $file.path)).Hash -ne $file.sha256){throw 'CONTROL_FIXTURE_HASH_CHANGED'}}
 Copy-Item -LiteralPath $recordPath[0].FullName -Destination (Join-Path $OutDir 'publish-ownership.json')
 Remove-Item -LiteralPath $publish -Recurse -Force
 Write-Output "PASS NativeAOT controls $($result.passed) checks; original source unchanged; fixture binaries removed."
} finally {
 foreach($name in $variables){[Environment]::SetEnvironmentVariable($name,$saved[$name])}
 if($null -ne $process -and -not $process.HasExited){$null=$process.CloseMainWindow();$null=$process.WaitForExit(3000)}
}
