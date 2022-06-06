$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    Write-Host "This script must be executed with administrative privileges." -ForegroundColor Red
    Read-Host -Prompt "Press Enter to continue"
    Exit 1
}

# Load restore values of previous InstallListener.ps1 executions
$uninstallInfoPath = Join-Path -Path $PSScriptRoot -ChildPath "Uninstall.json"
$uninstallInfo = [PSCustomObject]@{}
if (Test-Path $uninstallInfoPath)
{
    $uninstallInfo = Get-Content -Raw -Path $uninstallInfoPath | ConvertFrom-Json
}

function Restore-RegistryValue 
{
    param( [string]$Path, [string]$KeyName, [uint32]$ExpectedValue )
    if ($uninstallInfo.$KeyName -ne $null)
    {
        $previousValue = Get-ItemPropertyValue -Path $Path -Name $KeyName
        if ($previousValue -eq $ExpectedValue)
        {
            Set-ItemProperty -Path $Path -Name $KeyName -Value $uninstallInfo.$KeyName -Type DWORD -Force | Out-Null
        }
        else
        {
            Write-Error -Message "Failed to restore registry value of $Path\$KeyName, expected it had a value of $ExpectedValue but it was $previousValue.  Has something else modified it after InstallListener.ps1?  You will have to manually restore it."
        }
    }
}

# Remove automatic startup of ListenerService.exe
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

# Restore tuning some desktop integration settings to better perform a node work
$registryPath = "HKCU:\Control Panel\Desktop"
Restore-RegistryValue -Path $registryPath -KeyName "ForegroundFlashCount" -ExpectedValue 0
Restore-RegistryValue -Path $registryPath -KeyName "ForegroundLockTimeout" -ExpectedValue 0

# Restore Multimedia Class Scheduler Service (MMCSS) network throttling
$registryPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
Restore-RegistryValue -Path $registryPath -KeyName "NetworkThrottlingIndex" -ExpectedValue ([UInt32]::MaxValue)

# Conclude
Write-Host "Listener Service uninstalled. Please restart the computer."
Read-Host -Prompt "Press Enter to continue"