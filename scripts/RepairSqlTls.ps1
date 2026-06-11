param(
    [string[]]$SqlServices = @(
        'MSSQLSERVER',
        'MSSQL$SQLEXPRESS',
        'MSSQL$SQLEXPRESS01',
        'MSSQL$SQLEXPRESS02',
        'MSSQL$SQLEXPRESS03',
        'SQLBrowser'
    )
)

$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole(
    [Security.Principal.WindowsBuiltInRole]::Administrator
)

if (-not $isAdmin) {
    Write-Host 'Hay chay script nay bang PowerShell Run as Administrator.'
    exit 1
}

$tlsPaths = @(
    'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Client',
    'HKLM:\SYSTEM\CurrentControlSet\Control\SecurityProviders\SCHANNEL\Protocols\TLS 1.2\Server'
)

foreach ($path in $tlsPaths) {
    New-Item -Path $path -Force | Out-Null
    New-ItemProperty -Path $path -Name Enabled -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $path -Name DisabledByDefault -Value 0 -PropertyType DWord -Force | Out-Null
}

$dotnetPaths = @(
    'HKLM:\SOFTWARE\Microsoft\.NETFramework\v4.0.30319',
    'HKLM:\SOFTWARE\WOW6432Node\Microsoft\.NETFramework\v4.0.30319'
)

foreach ($path in $dotnetPaths) {
    New-Item -Path $path -Force | Out-Null
    New-ItemProperty -Path $path -Name SchUseStrongCrypto -Value 1 -PropertyType DWord -Force | Out-Null
    New-ItemProperty -Path $path -Name SystemDefaultTlsVersions -Value 1 -PropertyType DWord -Force | Out-Null
}

Get-Service -Name $SqlServices -ErrorAction SilentlyContinue | Restart-Service -Force

Write-Host 'Da bat TLS 1.2 va restart SQL Server. Neu van loi, hay restart Windows roi thu lai.'
