$instanceName = 'MSSQLSERVER'
$instanceMap = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\Instance Names\SQL'
$instanceId = $instanceMap.$instanceName

if (-not $instanceId) {
    Write-Host "Khong tim thay SQL Server instance $instanceName."
    exit 1
}

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

if (-not $isAdmin) {
    Write-Host 'Hay chay script nay bang PowerShell Run as Administrator.'
    exit 1
}

$sqlServerKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer"
$tcpKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\SuperSocketNetLib\Tcp"
$npKey = "HKLM:\SOFTWARE\Microsoft\Microsoft SQL Server\$instanceId\MSSQLServer\SuperSocketNetLib\Np"

Set-ItemProperty -Path $sqlServerKey -Name LoginMode -Value 2
Set-ItemProperty -Path $tcpKey -Name Enabled -Value 1
Set-ItemProperty -Path $npKey -Name Enabled -Value 1

Restart-Service -Name 'MSSQLSERVER' -Force
Get-Service -Name 'SQLBrowser' -ErrorAction SilentlyContinue | Restart-Service -Force

Write-Host "Da bat Mixed Mode cho $instanceName, mo TCP/Named Pipes va restart service."
