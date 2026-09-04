[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$migrationDirectory = Join-Path $root 'src\DeviceRental.Infrastructure\Persistence\Migrations'
$sqlPath = Join-Path $root 'deploy\database\device-rental-idempotent.sql'

$migrationFiles = @(Get-ChildItem -LiteralPath $migrationDirectory -File -Filter '*.cs' |
    Where-Object { $_.Name -notlike '*.Designer.cs' -and $_.Name -ne 'DeviceRentalDbContextModelSnapshot.cs' } |
    Sort-Object Name)
if ($migrationFiles.Count -ne 5) {
    throw "Expected exactly five executable migrations, found $($migrationFiles.Count)."
}

$expectedNames = @('IdentityAndAccessPolicy', 'AuditAndOutbox', 'DeviceImages', 'LoanPolicyVersions', 'NotificationDeliveryAndOperationalIndexes')
for ($index = 0; $index -lt $expectedNames.Count; $index++) {
    if ($migrationFiles[$index].BaseName -notmatch "^\d{14}_$($expectedNames[$index])$") {
        throw "Migration $index must be $($expectedNames[$index]); found $($migrationFiles[$index].Name)."
    }
}

if (-not (Test-Path -LiteralPath $sqlPath -PathType Leaf)) {
    throw "Committed idempotent migration SQL is missing: $sqlPath"
}

$sql = Get-Content -LiteralPath $sqlPath -Raw
if ([string]::IsNullOrWhiteSpace($sql)) {
    throw 'Committed idempotent migration SQL is empty.'
}

foreach ($migration in $migrationFiles) {
    if (-not $sql.Contains($migration.BaseName, [StringComparison]::Ordinal)) {
        throw "Committed SQL does not include migration $($migration.BaseName)."
    }
}

if ($sql -notmatch '__EFMigrationsHistory' -or $sql -notmatch 'device_rental') {
    throw 'Committed SQL must target the device_rental schema and its EF migration history.'
}

Write-Host 'PASS: exactly five ordered migrations and the committed idempotent SQL artifact agree.'
