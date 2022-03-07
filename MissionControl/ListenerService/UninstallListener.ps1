$registryPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Run"
$name = "ClusterListenerService"

$key = Get-Item -Path $registryPath 
if ($key -ne $null -and $key.GetValue($name, $null) -ne $null)
{
    Remove-ItemProperty -Path $registryPath -Name $name
}
else
{
    Write-Host "Nothing to do."
}
   
Write-Host "Listener Service uninstalled."
Read-Host -Prompt "Press Enter to continue"