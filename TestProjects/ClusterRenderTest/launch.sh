#!/bin/sh
./ergt.app/Contents/MacOS/ClusterRenderTest -useClusterRendering -masterNode 0 1 224.0.1.1:25689,25690 30&
./ergt.app/Contents/MacOS/ClusterRenderTest -useClusterRendering -node 1 224.0.1.1:25690,25689 30&
