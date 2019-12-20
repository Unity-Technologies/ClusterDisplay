#!/bin/sh
./ergt.app/Contents/MacOS/ClusterRenderTest -adapterName en6 -masterNode 0 1 224.0.1.1:25689,25690 30 -logFile /Users/vladal/Downloads/0.txt&
./ergt.app/Contents/MacOS/ClusterRenderTest -adapterName en6 -node 1 224.0.1.1:25690,25689 30 -logFile /Users/vladal/Downloads/1.txt&
