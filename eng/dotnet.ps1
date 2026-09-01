$ErrorActionPreference = 'Stop'

function Get-RepositoryRoot {
    $commonDirectory = (& git rev-parse --git-common-dir 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commonDirectory)) {
        throw 'Unable to resolve the Git common directory. Run this command inside the repository.'
    }

    $resolvedCommonDirectory = [System.IO.Path]::GetFullPath(
        [System.IO.Path]::Combine((Get-Location).Path, $commonDirectory.Trim()))
    if ([System.IO.Path]::GetFileName($resolvedCommonDirectory) -ne '.git') {
        throw "Unexpected Git common directory: $resolvedCommonDirectory"
    }

    return [System.IO.Directory]::GetParent($resolvedCommonDirectory).FullName
}

function Get-RequiredSdkVersion {
    $globalJsonPath = Join-Path $PSScriptRoot '..\global.json'
    return (Get-Content -LiteralPath $globalJsonPath -Raw | ConvertFrom-Json).sdk.version
}

$requiredVersion = Get-RequiredSdkVersion
$repositoryRoot = Get-RepositoryRoot
$localDotNet = Join-Path $repositoryRoot '.tools\dotnet\dotnet.exe'
$dotNetExecutable = $null

if (Test-Path -LiteralPath $localDotNet -PathType Leaf) {
    $dotNetExecutable = $localDotNet
}
else {
    $pathDotNet = Get-Command dotnet -ErrorAction SilentlyContinue
    if ($null -ne $pathDotNet) {
        $pathVersion = (& $pathDotNet.Source --version 2>$null).Trim()
        if ($LASTEXITCODE -eq 0 -and $pathVersion -eq $requiredVersion) {
            $dotNetExecutable = $pathDotNet.Source
        }
    }
}

if ([string]::IsNullOrWhiteSpace($dotNetExecutable)) {
    throw "Required .NET SDK $requiredVersion was not found. Run eng/Bootstrap-DotNet.ps1."
}

$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_ADD_GLOBAL_TOOLS_TO_PATH = '0'
$env:DOTNET_NOLOGO = '1'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_HOME = Join-Path $repositoryRoot '.tools\dotnet-home'
$env:DOTNET_ROOT = Split-Path -Parent $dotNetExecutable
$env:NUGET_PACKAGES = Join-Path $repositoryRoot '.tools\nuget\packages'
$env:MSBUILDDISABLENODEREUSE = '1'

& $dotNetExecutable @args
exit $LASTEXITCODE
