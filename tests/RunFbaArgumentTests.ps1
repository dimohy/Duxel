$ErrorActionPreference = 'Stop'
$path = Join-Path (Split-Path $PSScriptRoot -Parent) 'run-fba.ps1'
$tokens = $null; $errors = $null
$ast = [System.Management.Automation.Language.Parser]::ParseFile($path, [ref]$tokens, [ref]$errors)
if ($errors.Count) { throw 'run-fba.ps1 parse failed.' }
$function = $ast.Find({ param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Resolve-PlatformFromExtraArgs'
}, $false)
if ($null -eq $function) { throw 'Argument resolver not found.' }
# Load only the repository's pure resolver; do not execute its build/launch entry point.
. ([scriptblock]::Create($function.Extent.Text))
$cases = @(
    @{input=@();platform=$null;remaining=@()},
    @{input=@('-p:OptimizationPreference=Speed');platform=$null;remaining=@('-p:OptimizationPreference=Speed')},
    @{input=@('--platform','windows','-p:OptimizationPreference=Speed','-v:diagnostic');platform='windows';remaining=@('-p:OptimizationPreference=Speed','-v:diagnostic')},
    @{input=@('-p:OptimizationPreference=Speed','--platform=windows','--literal=value with spaces');platform='windows';remaining=@('-p:OptimizationPreference=Speed','--literal=value with spaces')},
    @{input=@('--PLATFORM=windows');platform='windows';remaining=@()},
    @{input=@('',' ','-p:PublishAot=true');platform=$null;remaining=@('-p:PublishAot=true')}
)
foreach ($case in $cases) {
    $result = Resolve-PlatformFromExtraArgs -InputArguments $case.input
    if ($result.Platform -ne $case.platform -or $result.RemainingArgs.Count -ne $case.remaining.Count) {
        throw 'Argument resolver changed platform or argument count.'
    }
    for ($i = 0; $i -lt $case.remaining.Count; $i++) {
        if ($result.RemainingArgs[$i] -cne $case.remaining[$i]) { throw 'Argument content/order changed.' }
    }
}
$missingPlatformRejected = $false
try { Resolve-PlatformFromExtraArgs -InputArguments @('--platform') | Out-Null }
catch {
    if ($_.Exception.Message -notmatch '--platform') { throw }
    $missingPlatformRejected = $true
}
if (-not $missingPlatformRejected) { throw 'Missing platform value was accepted.' }
Write-Output 'PASS run-fba argument contracts 7/7 (single option,order,spaces,platform extraction,missing value).'
