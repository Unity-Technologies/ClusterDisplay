# New Project setup
We've tried to simplify the process of setting up a new cluster display project by providing the following powershell script: **CreateNewProject.ps1**. This script is located in the root of the cluster display package, you can setup a project with either render pipelines. Furthermore, it will:
1. Prompt you whether you want a empty, HDRP, URP or sample project.
2. Then it will ask you to name your new project.
3. Then ask you for a path to create the project.
4. Then it will create a new Unity project at that path with your desired render pipeline.

If you want to create a new project:
1. execute the **CreateNewProject.ps1** script
2. Select the type of project you want to create and where you want to place it. 

    ![](images/new-project-script.gif)

3. Open the created projects from the Unity Hub:
    ![Scene Composition Manager](images/samples-open-hub.png)

4. If you created a sample project, follow the instructions here: