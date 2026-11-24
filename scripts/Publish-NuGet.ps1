param(
    [string]$Version,
    [string]$Source = "nuget.org",
    [string]$PackageDirectory = "nupkgs",
    [switch]$Pack,
    [switch]$NoSkipDuplicate
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

function Import-DotEnv {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    Get-Content -LiteralPath $Path | ForEach-Object {
        $line = $_.Trim()
        if ($line.Length -eq 0 -or $line.StartsWith("#")) {
            return
        }

        $equals = $line.IndexOf("=")
        if ($equals -le 0) {
            throw "Invalid .env line: expected KEY=VALUE."
        }

        $name = $line.Substring(0, $equals).Trim()
        $value = $line.Substring($equals + 1).Trim()
        if (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'"))) {
            $value = $value.Substring(1, $value.Length - 2)
        }

        [Environment]::SetEnvironmentVariable($name, $value, "Process")
    }
}

function Get-PackageVersion {
    $projectPath = Join-Path $repoRoot "src\Duxel.App\Duxel.App.csproj"
    $version = & dotnet msbuild $projectPath -nologo -getProperty:PackageVersion
    if ($LASTEXITCODE -ne 0 -or ($version -join '').Trim() -notmatch '^\d+\.\d+\.\d+(?:[-+][\w.-]+)?$') {
        throw "Could not evaluate the package version for $projectPath."
    }

    return $version.Trim()
}

Import-DotEnv (Join-Path $repoRoot ".env")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-PackageVersion
}

if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw "NUGET_API_KEY is missing. Add it to .env or set it in the current environment."
}

$projects = [ordered]@{
    'Duxel.App' = 'src/Duxel.App/Duxel.App.csproj'
    'Duxel.Windows.App' = 'src/Duxel.Windows.App/Duxel.Windows.App.csproj'
    'Duxel.Linux.App' = 'src/Duxel.Linux.App/Duxel.Linux.App.csproj'
    'Duxel.Gallery' = 'samples/Duxel.Gallery/Duxel.Gallery.csproj'
    'Duxel.Gallery.Linux' = 'samples/Duxel.Gallery.Linux/Duxel.Gallery.Linux.csproj'
}
$output = Join-Path $repoRoot $PackageDirectory

if ($Pack) {
    foreach ($project in $projects.GetEnumerator()) {
        $packArgs = @('pack', (Join-Path $repoRoot $project.Value), '-c', 'Release', '-o', $output,
            '--disable-build-servers', '-warnaserror', "-p:DuxelVersion=$Version")
        & dotnet @packArgs
        if ($LASTEXITCODE -ne 0) { throw "Could not pack $($project.Key)." }
    }
}

$packages = @($projects.Keys | ForEach-Object { Join-Path $output "$($_).$Version.nupkg" })
foreach ($package in $packages) {
    if (-not (Test-Path -LiteralPath $package)) {
        throw "Package not found: $package"
    }
}

foreach ($package in $packages) {
    $pushArgs = @('nuget', 'push', $package, '--source', $Source)
    if (-not $NoSkipDuplicate) {
        $pushArgs += '--skip-duplicate'
    }

    & dotnet @pushArgs
    if ($LASTEXITCODE -ne 0) { throw "NuGet push failed: $([IO.Path]::GetFileName($package))." }
}
