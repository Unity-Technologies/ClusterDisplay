

set APP=Build/TestProject.exe
START %APP% -useClusterRendering -masterNode 0 1 224.0.1.0:25689,25690 30
START %APP% -useClusterRendering -node 1 224.0.1.0:25690,25689 30
