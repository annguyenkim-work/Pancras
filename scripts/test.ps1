<#
.SYNOPSIS
    Build (không cảnh báo) rồi chạy test trên máy dev, kể cả Infrastructure.Tests trên PostgreSQL local khi máy không có Docker.

.DESCRIPTION
    Nếu biến PANCRAS_TEST_POSTGRES chưa đặt, script đọc chuỗi kết nối từ user-secrets của Pancras.Web (id pancras-web-7c1a9e2b,
    khóa ConnectionStrings:DefaultConnection) và đặt biến đó cho tiến trình này. Không in chuỗi kết nối ra màn hình.
    Máy có Docker thì không cần user-secrets: Testcontainers tự khởi động PostgreSQL.
    Dừng app (Pancras.Web, cổng 5010) trước khi chạy để build không bị khóa file.

.EXAMPLE
    ./scripts/test.ps1
    ./scripts/test.ps1 -Project Web -Filter Category=Browser
#>
param(
    # Domain, Application, Infrastructure, Web; bỏ trống là cả solution.
    [ValidateSet('', 'Domain', 'Application', 'Infrastructure', 'Web')]
    [string]$Project = '',
    [string]$Filter = '',
    [switch]$NoBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

if (-not $env:PANCRAS_TEST_POSTGRES) {
    $secrets = Join-Path $env:APPDATA 'Microsoft\UserSecrets\pancras-web-7c1a9e2b\secrets.json'
    if (Test-Path $secrets) {
        $json = Get-Content $secrets -Raw | ConvertFrom-Json
        $connection = $json.'ConnectionStrings:DefaultConnection'
        if (-not $connection -and $json.ConnectionStrings) {
            $connection = $json.ConnectionStrings.DefaultConnection
        }

        if ($connection) {
            $env:PANCRAS_TEST_POSTGRES = $connection
            Write-Host 'Infrastructure.Tests dùng PostgreSQL local (chuỗi kết nối lấy từ user-secrets).'
        }
    }
}

if (-not $env:PANCRAS_TEST_POSTGRES) {
    Write-Host 'Không thấy chuỗi kết nối trong user-secrets: Infrastructure.Tests sẽ dùng Docker (Testcontainers).'
}

$target = if ($Project) { "tests/Pancras.$Project.Tests" } else { 'Pancras.slnx' }

if (-not $NoBuild) {
    dotnet build $target --warnaserror --nologo
    if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
}

$arguments = @('test', $target, '--no-build', '--warnaserror', '--nologo')
if ($Filter) { $arguments += @('--filter', $Filter) }
dotnet @arguments
exit $LASTEXITCODE
