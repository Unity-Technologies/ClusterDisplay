$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (!$currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
{
    Write-Host "This script must be executed with administrative privileges." -ForegroundColor Red
    Read-Host -Prompt "Press Enter to continue"
    Exit 1
}

# Check for presence of configureDriver.exe
$configureDriverPath = Join-Path -Path $PSScriptRoot -ChildPath "configureDriver.exe"
$tryAgain = $true
while ((-not (Test-Path $configureDriverPath)) -and $tryAgain)
{
    $goToDownload = New-Object System.Management.Automation.Host.ChoiceDescription "&Download","Go to download page."
    $skip = New-Object System.Management.Automation.Host.ChoiceDescription "&Skip","Skip configuration of present behavior."
    $tryAgain = New-Object System.Management.Automation.Host.ChoiceDescription "&Try again","I just manually copied configureDriver.exe, check again."
    $options = [System.Management.Automation.Host.ChoiceDescription[]]($goToDownload, $skip, $tryAgain)

    $title = "configureDriver.exe is missing"
    $downloadUrl = "https://www.nvidia.com/en-us/drivers/driver-utility/"
    $message = "configureDriver.exe from Nvidia used to configure present behavior (to maximize performes) is not found in the folder of the ListenerServer." + 
               "`r`n`r`nIt can be downloaded from $downloadUrl."
    $result = $host.ui.PromptForChoice($title, $message, $options, 0)

    switch ($result)
    {
        0 { 
            # Show download page
            Start-Process $downloadUrl
            # ... and try again
        }
        1 {
            # Skip it
            $tryAgain = $false
        }
        2 {
            # Check again...  Nothing to do
        }
    }
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

if (Test-Path $configureDriverPath)
{
    Write-Host "Setting Pre-Present Wait for Quadro Sync"
    cd $PSScriptRoot
    & ./configureDriver.exe DxSwapGroupControl=3
}
else
{
    Write-Host "Skipped setting Pre-Present Wait for Quadro Sync" -ForegroundColor Yellow
}

# Update Uninstall.json
$uninstallInfo | ConvertTo-Json -depth 100 | Out-File $uninstallInfoPath

# Write conclude message and prompt for restart
Write-Host "Done installing the Listener Service. Please restart the computer."
$yes = New-Object System.Management.Automation.Host.ChoiceDescription "&Yes","Restart now."
$no = New-Object System.Management.Automation.Host.ChoiceDescription "&No","No, I'll restart later (but before using ClusterListener)."
$options = [System.Management.Automation.Host.ChoiceDescription[]]($yes, $no)

$title = "Restart now?"
$message = "Do you want to restart the computer now?"
$result = $host.ui.PromptForChoice($title, $message, $options, 0)
if ($result -eq 0)
{
    Restart-Computer -Force
}