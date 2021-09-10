# Graphics
Cluster display supports both URP (Universal Render Pipeline) and HDRP (High Definition Render Pipeline). Most features throughout both render pipelines are supported. However, there are some noteable edge cases.

## Setup
If you execute the powershell script: **CreateNewProject.ps1** located in the root of the cluster display package, you can setup a project with either render pipelines. Furthermore, it will:
1. Prompt you whether you want a HDRP or URP project.
2. Then ask you for a path to create the project.
3. Then it will create a new Unity project at that path with your desired render pipeline.