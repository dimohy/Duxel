[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$GraphicsBinary,
    [Parameter(Mandatory)][string]$ControlBinary,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repo = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$runner = Join-Path $repo 'scripts/Test-WidgetContracts.ps1'
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh runner-test output directory.' }
New-Item -ItemType Directory -Path $output | Out-Null
$base = @{GraphicsBinary=$GraphicsBinary;ControlBinary=$ControlBinary;Suite=@('control-contracts')}
$checks = [Collections.Generic.List[object]]::new()
function Reject-BeforeStart([string]$Id,[hashtable]$Parameters,[string]$Expected,[string]$Script) {
    $message = $null
    try { & $Script @Parameters | Out-Null } catch { $message = $_.Exception.Message }
    if (!$message -or !$message.StartsWith($Expected,[StringComparison]::Ordinal)) { throw ("Unexpected rejection : $Id : $message") }
    if (Test-Path -LiteralPath $Parameters.OutputDirectory) { throw ("Preflight created output : $Id") }
    $checks.Add(@{id=$Id;passed=$true;failure=$message;processesStarted=0})
}
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'positive with spaces'
& $runner @p | Out-Null
$positive = Get-Content -LiteralPath (Join-Path $p.OutputDirectory 'summary.json') -Raw | ConvertFrom-Json
if ($positive.status -ne 'passed' -or $positive.passedSuites -ne 1 -or $positive.completedDefaultSuite) { throw 'A subset was incorrectly promoted to the complete default suite.' }
$checks.Add(@{id='positive-subset-and-space-path';passed=$true;completedDefaultSuite=$false})
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'unknown'; $p.Suite = @('unknown-contract')
Reject-BeforeStart 'unknown-suite' $p 'UNKNOWN_SUITE:' $runner
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'missing'; $p.GraphicsBinary = Join-Path $output 'absent/Duxel.Graphics.Tests.dll'
Reject-BeforeStart 'missing-binary' $p 'INPUT_BINARY_MISSING:' $runner
$fixture = Join-Path $output 'classification-fixture'
New-Item -ItemType Directory -Path (Join-Path $fixture 'scripts'),(Join-Path $fixture 'tests/Duxel.Graphics.Tests') | Out-Null
Copy-Item -LiteralPath $runner -Destination (Join-Path $fixture 'scripts/Test-WidgetContracts.ps1')
Copy-Item -LiteralPath (Join-Path $repo 'tests/Duxel.Graphics.Tests/Program.cs') -Destination (Join-Path $fixture 'tests/Duxel.Graphics.Tests/Program.cs')
$manifest = Get-Content -LiteralPath (Join-Path $repo 'tests/Duxel.Graphics.Tests/contract-suites.json') -Raw | ConvertFrom-Json
$manifest.graphicsContracts = @($manifest.graphicsContracts | Select-Object -Skip 1)
$manifest | ConvertTo-Json -Depth 7 | Set-Content -LiteralPath (Join-Path $fixture 'tests/Duxel.Graphics.Tests/contract-suites.json') -Encoding utf8
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'unclassified'
Reject-BeforeStart 'unclassified-source-mode' $p 'CLASSIFICATION_MISMATCH:' (Join-Path $fixture 'scripts/Test-WidgetContracts.ps1')
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'runtime'; $p.ExpectedFramework = '.NET deliberately-wrong'
$message = $null
try { & $runner @p | Out-Null } catch { $message = $_.Exception.Message }
$record = Get-Content -LiteralPath (Join-Path $p.OutputDirectory 'control-contracts/process.json') -Raw | ConvertFrom-Json
$summary = Get-Content -LiteralPath (Join-Path $p.OutputDirectory 'summary.json') -Raw | ConvertFrom-Json
if (!$message -or $record.failureId -ne 'RUNTIME_MISMATCH' -or $record.exitCode -ne 0 -or $summary.status -ne 'failed' -or $summary.completedDefaultSuite) { throw 'Runtime mismatch was not rejected accurately.' }
$checks.Add(@{id='wrong-runtime';passed=$true;failureId=$record.failureId;childExitCode=$record.exitCode;pid=$record.pid})
$p = @{} + $base; $p.OutputDirectory = Join-Path $output 'positive with spaces'
$path = Join-Path $p.OutputDirectory 'summary.json'
$hash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
$message = $null
try { & $runner @p | Out-Null } catch { $message = $_.Exception.Message }
if (!$message -or !$message.StartsWith('OUTPUT_EXISTS:') -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $hash) { throw 'Existing evidence was not protected.' }
$checks.Add(@{id='existing-output';passed=$true;processesStarted=0;previousSummaryPreserved=$true})
$checks.ToArray() | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'result.json') -Encoding utf8
Write-Output ("Passed runner behavior checks : {0}" -f $checks.Count)
