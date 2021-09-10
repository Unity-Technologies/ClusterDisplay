# Troubleshooting

## Compile Time Errors in GeneratedInspectors.cs 
This can be a common issue while changing Unity versions, updating packages or removing packages, and you can read about the solution here:
* [How to Generate Them](network-events#how-to-generate-them)

## Help! Everything is Gray
In the context of cluster display, this means that your manifest.json is NOT referencing the custom branch of the **com.unity.render-pipeline.core** package. Walk through [Project Setup](project-setup.md) again, verify your package configuration and this problem should resolve yourself.

Here is an example **snippet** of **manifest.json**
```
  "dependencies": {
    "com.unity.cluster-display": "file:../../Packages/ClusterDisplay/source/com.unity.cluster-display",
    "com.unity.cluster-display.graphics": "file:../../Packages/ClusterDisplay/source/com.unity.cluster-display.graphics",
    "com.unity.render-pipelines.core": "file:../../Packages/Graphics/com.unity.render-pipelines.core",
    "com.unity.render-pipelines.universal": "file:../../Packages/Graphics/com.unity.render-pipelines.universal",
```