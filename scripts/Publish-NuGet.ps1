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
    [xml]$project = Get-Content -LiteralPath $projectPath -Raw
    $versionNode = $project.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($versionNode)) {
        throw "Could not read package version from $projectPath."
    }

    return $versionNode
}

Import-DotEnv (Join-Path $repoRoot ".env")

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-PackageVersion
}

if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) {
    throw "NUGET_API_KEY is missing. Add it to .env or set it in the current environment."
}

if ($Pack) {
    dotnet pack (Join-Path $repoRoot "src\Duxel.App\Duxel.App.csproj") -c Release -o (Join-Path $repoRoot $PackageDirectory) -p:NodeReuse=false
    dotnet pack (Join-Path $repoRoot "src\Duxel.Windows.App\Duxel.Windows.App.csproj") -c Release -o (Join-Path $repoRoot $PackageDirectory) -p:NodeReuse=false
}

$packages = @(
    (Join-Path $repoRoot "$PackageDirectory\Duxel.App.$Version.nupkg"),
    (Join-Path $repoRoot "$PackageDirectory\Duxel.Windows.App.$Version.nupkg")
)

foreach ($package in $packages) {
    if (-not (Test-Path -LiteralPath $package)) {
        throw "Package not found: $package"
    }

    $args = @("nuget", "push", $package, "--source", $Source, "--api-key", $env:NUGET_API_KEY)
    if (-not $NoSkipDuplicate) {
        $args += "--skip-duplicate"
    }

    dotnet @args
}
