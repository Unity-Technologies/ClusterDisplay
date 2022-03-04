$registryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$name = "ClusterListenerService"
$value = Join-Path -Path $PSScriptRoot -ChildPath "ListenerService.exe"

Write-Host "Adding to startup $value to startup"

Set-ItemProperty -Path $registryPath -Name $name -Value $value -Type String -Force | Out-Null
    
Write-Host "Removing taskbar lockout"

$registryPath = "HKCU:\Control Panel\Desktop"
Set-ItemProperty -Path $registryPath -Name "ForegroundFlashCount" -Value 0 -Type DWORD -Force | Out-Null

Set-ItemProperty -Path $registryPath -Name "ForegroundLockTimeout" -Value 0 -Type DWORD -Force | Out-Null
    
Write-Host "Done installing the Listener Service. Please restart the computer."
Read-Host -Prompt "Press Enter to continue"