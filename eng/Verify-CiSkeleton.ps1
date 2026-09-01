[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$workflowPath = Join-Path $repositoryRoot '.github\workflows\ci.yml'
$imageManifestPath = Join-Path $repositoryRoot 'eng\container-images.json'
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure([string] $Message) {
    $failures.Add($Message)
}

if (-not (Test-Path -LiteralPath $workflowPath -PathType Leaf)) {
    Add-Failure "Missing workflow: $workflowPath"
}

if (-not (Test-Path -LiteralPath $imageManifestPath -PathType Leaf)) {
    Add-Failure "Missing container image manifest: $imageManifestPath"
}

$workflow = if (Test-Path -LiteralPath $workflowPath) {
    Get-Content -LiteralPath $workflowPath -Raw
}
else {
    ''
}

foreach ($jobName in @('build-unit', 'integration')) {
    if ($workflow -notmatch "(?m)^  $([regex]::Escape($jobName)):\s*$") {
        Add-Failure "Workflow job is missing: $jobName"
    }
}

if ($workflow -notmatch '(?ms)^permissions:\s*\r?\n\s+contents:\s*read\s*$') {
    Add-Failure 'Workflow must declare top-level permissions: contents: read.'
}

foreach ($actionName in @('actions/checkout', 'actions/setup-dotnet', 'actions/upload-artifact')) {
    if ($workflow -notmatch "(?m)^\s*-?\s*uses:\s*$([regex]::Escape($actionName))@[0-9a-f]{40}(?:\s*#.*)?$") {
        Add-Failure "Workflow action is absent or not pinned to a 40-character SHA: $actionName"
    }
}

if ($workflow -notmatch '(?m)--health-cmd(?:=|\s+)["'']?pg_isready\b') {
    Add-Failure 'PostgreSQL service must define a pg_isready health check.'
}

if ($workflow -notmatch [regex]::Escape('--Logger:trx')) {
    Add-Failure 'The build-unit job must emit a TRX test report.'
}

if ($workflow -notmatch [regex]::Escape('--Collect:XPlat Code Coverage')) {
    Add-Failure 'The build-unit job must collect XPlat code coverage.'
}

$filesToScan = [System.Collections.Generic.List[string]]::new()
if (Test-Path -LiteralPath (Join-Path $repositoryRoot '.github\workflows')) {
    Get-ChildItem -LiteralPath (Join-Path $repositoryRoot '.github\workflows') -File |
        Where-Object Extension -in @('.yml', '.yaml') |
        ForEach-Object { $filesToScan.Add($_.FullName) }
}

foreach ($folder in @('src', 'deploy')) {
    $folderPath = Join-Path $repositoryRoot $folder
    if (Test-Path -LiteralPath $folderPath) {
        Get-ChildItem -LiteralPath $folderPath -Recurse -File |
            Where-Object { $_.Name -eq 'Dockerfile' -or $_.Name -match '^compose.*\.ya?ml$' } |
            ForEach-Object { $filesToScan.Add($_.FullName) }
    }
}

foreach ($file in $filesToScan) {
    $lineNumber = 0
    foreach ($line in Get-Content -LiteralPath $file) {
        $lineNumber++
        if ($line -match '^\s*uses:\s*(?<reference>\S+)' -and
            $Matches.reference -notmatch '^\./' -and
            $Matches.reference -notmatch '@[0-9a-f]{40}$') {
            Add-Failure "$file`:$lineNumber contains a floating action reference: $($Matches.reference)"
        }

        if ($line -match '^\s*(?:image:|FROM\s+)\s*(?<reference>\S+)' -and
            $Matches.reference -notmatch '\$\{\{') {
            $reference = $Matches.reference.Trim('"', "'")
            if ($reference -match ':latest(?:@|$)' -or
                $reference -notmatch ':[^@\s]+@sha256:[0-9a-f]{64}$') {
                Add-Failure "$file`:$lineNumber contains a tag-only, latest, or malformed container reference: $reference"
            }
        }
    }
}

if (Test-Path -LiteralPath $imageManifestPath) {
    $manifest = Get-Content -LiteralPath $imageManifestPath -Raw | ConvertFrom-Json
    foreach ($property in $manifest.images.PSObject.Properties) {
        if ($property.Value -notmatch '^[^\s@]+:[^\s@]+@sha256:[0-9a-f]{64}$' -or
            $property.Value -match ':latest@') {
            Add-Failure "Container image '$($property.Name)' is not pinned to tag and digest: $($property.Value)"
        }
    }

    if (-not [string]::IsNullOrWhiteSpace($workflow) -and
        $workflow -notmatch [regex]::Escape($manifest.images.postgres)) {
        Add-Failure 'The integration service does not use the PostgreSQL image declared in eng/container-images.json.'
    }
}

if ($failures.Count -gt 0) {
    $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    throw "CI skeleton verification failed with $($failures.Count) error(s)."
}

Write-Host "PASS: CI skeleton uses read-only permissions, required jobs, SHA-pinned actions, and digest-pinned containers."
