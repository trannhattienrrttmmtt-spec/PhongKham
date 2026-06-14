$instanceKey = 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\MSSQL17.SQLEXPRESS03\MSSQLServer'

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

if (-not $isAdmin) {
    Write-Host 'Hay chay script nay bang PowerShell Run as Administrator.'
    exit 1
}

Set-ItemProperty -Path $instanceKey -Name LoginMode -Value 2
Restart-Service -Name 'MSSQL$SQLEXPRESS03' -Force
Restart-Service -Name 'SQLBrowser' -Force
Write-Host 'Da bat Mixed Mode cho SQLEXPRESS03 va restart service.'
