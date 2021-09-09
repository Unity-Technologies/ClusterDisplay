
echo "Beginning Cluster Display project creation."

$projectType = Read-Host "What kind of project do you want to create? (URP or HDRP)"
$destinationPath = Read-Host "Enter in the path where you want the project created"

$templatePath = ""

if ($projectType -eq "URP") {
    $templatePath = "/templates/URPTemplate"
}

elseif ($projectType -eq "HDRP") {
    $templatePath = "/templates/HDRPTemplate"
}

else {
    echo "Unknown project type: $projectType"
    exit
}

$executionPath = Split-Path -parent $MyInvocation.MyCommand.Path
New-Item -ItemType Directory -Force -Path $destinationPath
Copy-Item -Path $executionPath$templatePath -Destination $destinationPath -Recurse -Force