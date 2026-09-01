[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$sdkVersion = '10.0.400'
$runtimeVersion = '10.0.11'

$commonDirectory = (& git rev-parse --git-common-dir 2>$null)
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($commonDirectory)) {
    throw 'Unable to resolve the Git common directory. Run this command inside the repository.'
}

$resolvedCommonDirectory = [System.IO.Path]::GetFullPath(
    [System.IO.Path]::Combine((Get-Location).Path, $commonDirectory.Trim()))
$repositoryRoot = [System.IO.Directory]::GetParent($resolvedCommonDirectory).FullName
$installDirectory = Join-Path $repositoryRoot '.tools\dotnet'
$installerDirectory = Join-Path $repositoryRoot '.tools\installers'
$installerPath = Join-Path $installerDirectory 'dotnet-install.ps1'

New-Item -ItemType Directory -Force -Path $installerDirectory | Out-Null
Invoke-WebRequest -Uri 'https://dot.net/v1/dotnet-install.ps1' -OutFile $installerPath -UseBasicParsing

$signature = Get-AuthenticodeSignature -FilePath $installerPath
if ($signature.Status -ne [System.Management.Automation.SignatureStatus]::Valid -or
    $null -eq $signature.SignerCertificate -or
    $signature.SignerCertificate.Subject -notmatch 'Microsoft Corporation') {
    throw "dotnet-install.ps1 has no valid Microsoft signature. Status: $($signature.Status)"
}

& $installerPath -Version $sdkVersion -InstallDir $installDirectory -NoPath
if ($LASTEXITCODE -ne 0) {
    throw "dotnet-install.ps1 failed with exit code $LASTEXITCODE."
}

$dotNetPath = Join-Path $installDirectory 'dotnet.exe'
$reportedSdk = (& $dotNetPath --version).Trim()
$runtimeOutput = (& $dotNetPath --list-runtimes) -join "`n"
if ($reportedSdk -ne $sdkVersion) {
    throw "Expected SDK $sdkVersion, but found $reportedSdk."
}
if ($runtimeOutput -notmatch "Microsoft\.NETCore\.App $([regex]::Escape($runtimeVersion))" -or
    $runtimeOutput -notmatch "Microsoft\.AspNetCore\.App $([regex]::Escape($runtimeVersion))") {
    throw "Expected .NET and ASP.NET Core runtimes $runtimeVersion."
}

& $dotNetPath --info
