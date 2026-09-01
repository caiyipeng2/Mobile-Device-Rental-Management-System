[CmdletBinding()]
param(
    [ValidateSet('LocalUnit', 'Database')]
    [string] $Mode = 'LocalUnit'
)

$ErrorActionPreference = 'Stop'
$requiredSdk = (Get-Content -LiteralPath (Join-Path $PSScriptRoot '..\global.json') -Raw | ConvertFrom-Json).sdk.version
$actualSdk = (& (Join-Path $PSScriptRoot 'dotnet.ps1') --version).Trim()
if ($LASTEXITCODE -ne 0 -or $actualSdk -ne $requiredSdk) {
    throw "Expected .NET SDK $requiredSdk, but found '$actualSdk'."
}

function Test-Postgres18 {
    if ([string]::IsNullOrWhiteSpace($env:DEVICERENTAL_TEST_POSTGRES_ADMIN)) {
        return $false
    }

    $psql = Get-Command psql -ErrorAction SilentlyContinue
    if ($null -eq $psql) {
        return $false
    }

    $builder = [System.Data.Common.DbConnectionStringBuilder]::new()
    $builder.ConnectionString = $env:DEVICERENTAL_TEST_POSTGRES_ADMIN

    function Get-ConnectionValue([string[]] $Keys, [string] $DefaultValue = '') {
        foreach ($key in $Keys) {
            if ($builder.ContainsKey($key)) {
                return [string]$builder[$key]
            }
        }

        return $DefaultValue
    }

    $hostName = Get-ConnectionValue @('Host', 'Server')
    $port = Get-ConnectionValue @('Port') '5432'
    $database = Get-ConnectionValue @('Database') 'postgres'
    $userName = Get-ConnectionValue @('Username', 'User ID', 'UserId')
    $password = Get-ConnectionValue @('Password')
    if ([string]::IsNullOrWhiteSpace($hostName) -or [string]::IsNullOrWhiteSpace($userName)) {
        return $false
    }

    $previousPassword = $env:PGPASSWORD
    try {
        $env:PGPASSWORD = $password
        $version = (& $psql.Source `
            --host $hostName `
            --port $port `
            --username $userName `
            --dbname $database `
            --no-password `
            --tuples-only `
            --no-align `
            --command 'SHOW server_version_num;' 2>$null).Trim()
        return $LASTEXITCODE -eq 0 -and $version -match '^18\d{4}$'
    }
    finally {
        if ($null -eq $previousPassword) {
            Remove-Item Env:PGPASSWORD -ErrorAction SilentlyContinue
        }
        else {
            $env:PGPASSWORD = $previousPassword
        }
    }
}

function Test-DockerAvailable {
    $docker = Get-Command docker -ErrorAction SilentlyContinue
    if ($null -eq $docker) {
        return $false
    }

    & $docker.Source info --format '{{.ServerVersion}}' *> $null
    return $LASTEXITCODE -eq 0
}

$databaseAvailable = if ([string]::IsNullOrWhiteSpace($env:DEVICERENTAL_TEST_POSTGRES_ADMIN)) {
    Test-DockerAvailable
}
else {
    Test-Postgres18
}
if ($Mode -eq 'Database' -and -not $databaseAvailable) {
    throw 'PostgreSQL 18 is unavailable. Set DEVICERENTAL_TEST_POSTGRES_ADMIN to a PG18 admin connection string or start Docker.'
}

if ($databaseAvailable) {
    Write-Host "PASS: .NET SDK $actualSdk; PostgreSQL 18 tests are available."
}
else {
    Write-Host "PASS: .NET SDK $actualSdk; database tests are unavailable and will not be run in LocalUnit mode."
}
