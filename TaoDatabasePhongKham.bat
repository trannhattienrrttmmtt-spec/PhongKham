@echo off
setlocal
chcp 65001 >nul

title Tao Database Phong Kham
cd /d "%~dp0"

echo ==========================================
echo        TAO DATABASE PHONG KHAM
echo ==========================================
echo.

set "SQL_SERVER=.\SQLEXPRESS"
if not "%~1"=="" set "SQL_SERVER=%~1"

if not exist "scripts\PhongKhamFullDb.sql" (
    echo [LOI] Khong tim thay scripts\PhongKhamFullDb.sql
    pause
    exit /b 1
)

where sqlcmd >nul 2>nul
if errorlevel 1 (
    echo [LOI] May chua co sqlcmd.
    echo Cach lam thu cong:
    echo   1. Mo SQL Server Management Studio.
    echo   2. Ket noi SQL Server cua may.
    echo   3. Mo file scripts\PhongKhamFullDb.sql.
    echo   4. Bam Execute.
    echo.
    pause
    exit /b 1
)

echo Dang tao database tren server: %SQL_SERVER%
echo Neu SQL Server cua may khac, chay:
echo   TaoDatabasePhongKham.bat TEN_SERVER
echo Vi du:
echo   TaoDatabasePhongKham.bat .\SQLEXPRESS03
echo.

sqlcmd -S "%SQL_SERVER%" -E -i "scripts\PhongKhamFullDb.sql" -b
if errorlevel 1 (
    echo.
    echo [LOI] Tao database that bai.
    echo Hay kiem tra ten SQL Server hoac quyen Windows Authentication.
    pause
    exit /b 1
)

echo.
echo [OK] Da tao database PhongKhamFullDb.
echo Sau do chay ChayPhongKham.bat de mo web.
pause

endlocal
