param(
    [Parameter()]
    [byte]$NodeID,

    [Parameter()]
    [int]$RepeaterCount,
    
    [Parameter()]
    [string]$MulticastAddress = "224.0.1.0",
    
    [Parameter()]
    [int]$MulticastPort = 25690,
    
    [Parameter()]
    [string]$AdapterName
)

$registryPath = "HKLM:\Software\Unity Technologies\ClusterDisplay"
$NodeIDKey = "NodeID"
$RepeaterCountKey = "RepeaterCount"
$MulticastAddressKey = "MulticastAddress"
$MulticastPortKey = "MulticastPort"
$AdapterNameKey = "AdapterName"

if (!(Test-Path $registryPath))
{
    New-Item -Path $registryPath -Force | Out-Null
    New-ItemProperty -Path $registryPath -Name $NodeIDKey -Value $NodeID -PropertyType DWORD | Out-Null
    New-ItemProperty -Path $registryPath -Name $RepeaterCountKey -Value $RepeaterCount -PropertyType DWORD | Out-Null
    New-ItemProperty -Path $registryPath -Name $MulticastAddressKey -Value $MulticastAddress | Out-Null
    New-ItemProperty -Path $registryPath -Name $MulticastPortKey -Value $MulticastPort -PropertyType DWORD | Out-Null
    New-ItemProperty -Path $registryPath -Name $AdapterNameKey -Value $AdapterName | Out-Null
}
else {
    if ($PSBoundParameters.ContainsKey($NodeIDKey))
    {
        Set-ItemProperty -Path $registryPath -Name $NodeIDKey -Value $NodeID | Out-Null
    }
    if ($PSBoundParameters.ContainsKey($RepeaterCountKey))
    {
        Set-ItemProperty -Path $registryPath -Name $RepeaterCountKey -Value $RepeaterCount | Out-Null
    }
    if ($PSBoundParameters.ContainsKey($MulticastAddressKey))
    {
        Set-ItemProperty -Path $registryPath -Name $MulticastAddressKey -Value $MulticastAddress | Out-Null
    }
    if ($PSBoundParameters.ContainsKey($MulticastPortKey))
    {
        Set-ItemProperty -Path $registryPath -Name $MulticastPortKey -Value $MulticastPort | Out-Null
    }
    if ($PSBoundParameters.ContainsKey($AdapterNameKey))
    {
        Set-ItemProperty -Path $registryPath -Name $AdapterNameKey -Value $AdapterName | Out-Null
    }
}
Get-Item $registryPath