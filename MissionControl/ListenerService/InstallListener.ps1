$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    Write-Host "This script must be executed with administrative privileges." -ForegroundColor Red
    Read-Host -Prompt "Press Enter to continue"
    Exit 1
}

# Load previous restore values of previous InstallListener.ps1 executions
$uninstallInfoPath = Join-Path -Path $PSScriptRoot -ChildPath "Uninstall.json"
$uninstallInfo = [PSCustomObject]@{}
if (Test-Path $uninstallInfoPath)
{
    $uninstallInfo = Get-Content -Raw -Path $uninstallInfoPath | ConvertFrom-Json
}

function Set-RegistryValue
{
    param( [string]$Path, [string]$KeyName, [uint32]$Value )

    if ($uninstallInfo.$KeyName -eq $null)
    {
        $previousValue = Get-ItemPropertyValue -Path $Path -Name $KeyName
        $uninstallInfo | Add-Member -MemberType NoteProperty -Name $KeyName -Value $previousValue
    }
    Set-ItemProperty -Path $Path -Name $KeyName -Value $Value -Type DWORD -Force | Out-Null
}

# Do work
Write-Host "Adding to startup $value to startup"
$registryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$name = "ClusterListenerService"
$value = Join-Path -Path $PSScriptRoot -ChildPath "ListenerService.exe"
Set-ItemProperty -Path $registryPath -Name $name -Value $value -Type String -Force | Out-Null
    
Write-Host "Removing taskbar lockout"
$registryPath = "HKCU:\Control Panel\Desktop"
Set-RegistryValue -Path $registryPath -KeyName "ForegroundFlashCount" -Value 0
Set-RegistryValue -Path $registryPath -KeyName "ForegroundLockTimeout" -Value 0

Write-Host "Disable Multimedia Class Scheduler Service (MMCSS) network throttling"
$registryPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
Set-RegistryValue -Path $registryPath -KeyName "NetworkThrottlingIndex" -Value ([UInt32]::MaxValue)

# Update Uninstall.json
$uninstallInfo | ConvertTo-Json -depth 100 | Out-File $uninstallInfoPath

# Write conclude message
Write-Host "Done installing the Listener Service. Please restart the computer."
Read-Host -Prompt "Press Enter to continue"