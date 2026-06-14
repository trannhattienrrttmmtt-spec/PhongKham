$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

if (-not $isAdmin) {
    Write-Host 'Hay chay script nay bang PowerShell Run as Administrator.'
    exit 1
}

$query = @"
IF SUSER_ID(N'phongkham_app') IS NULL
BEGIN
    CREATE LOGIN [phongkham_app]
        WITH PASSWORD = N'PhongKham@Dev123',
             CHECK_POLICY = OFF,
             CHECK_EXPIRATION = OFF;
END;

IF DB_ID(N'PhongKhamFullDb') IS NULL
BEGIN
    CREATE DATABASE [PhongKhamFullDb];
END;

USE [PhongKhamFullDb];

IF DATABASE_PRINCIPAL_ID(N'phongkham_app') IS NULL
BEGIN
    CREATE USER [phongkham_app] FOR LOGIN [phongkham_app];
END;

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members drm
    INNER JOIN sys.database_principals rp ON rp.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals mp ON mp.principal_id = drm.member_principal_id
    WHERE rp.name = N'db_owner'
      AND mp.name = N'phongkham_app'
)
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [phongkham_app];
END;
"@

sqlcmd -S lpc:(local) -E -b -Q $query

if ($LASTEXITCODE -ne 0) {
    Write-Host 'Khong tao duoc login/database bang Windows auth. Hay chay scripts/RepairSqlTls.ps1 roi thu lai.'
    exit $LASTEXITCODE
}

Write-Host 'Da tao xong login phongkham_app va cap quyen cho database PhongKhamFullDb.'
