@echo off
setlocal
chcp 65001 >nul

title Chay Web Phong Kham

cd /d "%~dp0"

echo ==========================================
echo        WEB PHONG KHAM - CHAY NHANH
echo ==========================================
echo.

where dotnet >nul 2>nul
if errorlevel 1 (
    echo [LOI] May chua cai .NET SDK.
    echo Tai .NET SDK tai: https://dotnet.microsoft.com/download
    echo.
    pause
    exit /b 1
)

if not exist "PhongKham\PhongKham.csproj" (
    echo [LOI] Khong tim thay file PhongKham\PhongKham.csproj.
    echo Hay dat file .bat nay o thu muc goc cua source code.
    echo.
    pause
    exit /b 1
)

echo [1/4] Kiem tra cau hinh database...
echo File cau hinh: PhongKham\appsettings.json
echo Neu may khac SQL Server, sua ConnectionStrings:ClinicDatabase trong file nay.
echo.

echo [2/4] Restore package...
dotnet restore "PhongKham\PhongKham.csproj"
if errorlevel 1 (
    echo.
    echo [LOI] Restore that bai.
    pause
    exit /b 1
)

echo.
echo [3/4] Build project...
dotnet build "PhongKham\PhongKham.csproj" --no-restore
if errorlevel 1 (
    echo.
    echo [LOI] Build that bai. Neu file dang bi khoa, hay tat web dang chay roi thu lai.
    pause
    exit /b 1
)

echo.
echo [4/4] Mo web...
echo Tai khoan mau:
echo   Admin:      admin@phongkham.local / Dev@123456
echo   Bac si:     bacsi@phongkham.local / Dev@123456
echo   Duoc si:    duocsi@phongkham.local / Dev@123456
echo   Benh nhan:  benhnhan@phongkham.local / Dev@123456
echo.
echo Dang chay tai: http://localhost:5217
echo Nhan Ctrl + C de dung server.
echo.

start "" "http://localhost:5217"
dotnet run --project "PhongKham\PhongKham.csproj" --launch-profile http --no-build

endlocal
