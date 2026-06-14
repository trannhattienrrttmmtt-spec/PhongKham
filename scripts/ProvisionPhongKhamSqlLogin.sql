USE [master];
GO

IF SUSER_ID(N'phongkham_app') IS NULL
BEGIN
    CREATE LOGIN [phongkham_app]
        WITH PASSWORD = N'PhongKham@Dev123',
             CHECK_POLICY = OFF,
             CHECK_EXPIRATION = OFF;
    PRINT N'Da tao login phongkham_app.';
END
ELSE
BEGIN
    PRINT N'Login phongkham_app da ton tai.';
END
GO

IF DB_ID(N'PhongKhamFullDb') IS NULL
BEGIN
    CREATE DATABASE [PhongKhamFullDb];
    PRINT N'Da tao database PhongKhamFullDb.';
END
ELSE
BEGIN
    PRINT N'Database PhongKhamFullDb da ton tai.';
END
GO

USE [PhongKhamFullDb];
GO

ALTER LOGIN [phongkham_app] WITH DEFAULT_DATABASE = [PhongKhamFullDb];
GO

IF DATABASE_PRINCIPAL_ID(N'phongkham_app') IS NULL
BEGIN
    CREATE USER [phongkham_app] FOR LOGIN [phongkham_app];
    PRINT N'Da tao user phongkham_app trong database PhongKhamFullDb.';
END
ELSE
BEGIN
    PRINT N'User phongkham_app trong database da ton tai.';
END
GO

IF NOT EXISTS (
    SELECT 1
    FROM sys.database_role_members AS drm
    INNER JOIN sys.database_principals AS role_principal
        ON role_principal.principal_id = drm.role_principal_id
    INNER JOIN sys.database_principals AS member_principal
        ON member_principal.principal_id = drm.member_principal_id
    WHERE role_principal.name = N'db_owner'
      AND member_principal.name = N'phongkham_app'
)
BEGIN
    ALTER ROLE [db_owner] ADD MEMBER [phongkham_app];
    PRINT N'Da cap quyen db_owner cho phongkham_app.';
END
ELSE
BEGIN
    PRINT N'phongkham_app da co quyen db_owner.';
END
GO

BEGIN TRY
    EXECUTE AS LOGIN = N'phongkham_app';
    SELECT
        SUSER_SNAME() AS CurrentLogin,
        ORIGINAL_LOGIN() AS OriginalLogin,
        DB_NAME() AS CurrentDatabase;
    REVERT;
    PRINT N'Kiem tra login thanh cong.';
END TRY
BEGIN CATCH
    PRINT N'Kiem tra login that bai: ' + ERROR_MESSAGE();
END CATCH
GO
