start "Patate" ClusterRenderTest.exe -useClusterRendering -adapterName "Ethernet 3" -masterNode 0 3 224.0.1.1:25689,25690 30&
start "Patate" ClusterRenderTest.exe -useClusterRendering -adapterName "Ethernet 3" -node 1 224.0.1.1:25690,25689 30&
start "Patate" ClusterRenderTest.exe -useClusterRendering -adapterName "Ethernet 3" -node 2 224.0.1.1:25690,25689 30&
start "Patate" ClusterRenderTest.exe -useClusterRendering -adapterName "Ethernet 3" -node 3 224.0.1.1:25690,25689 30&