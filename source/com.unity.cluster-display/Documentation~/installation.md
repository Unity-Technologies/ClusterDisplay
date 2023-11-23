[Contents](TableOfContents.md) | [Home](index.md) > Installation

# Installation

This package is only available in its repository and can't be discovered via the Unity registry.

To install it, you have two main options:

* Get a local copy of the package (i.e. the folder that includes the ­­`package.json` file) and follow the instructions for [local package installation](https://docs.unity3d.com/Manual/upm-ui-local.html), OR

* Install the package directly from its GitHub repository [using a Git URL](https://docs.unity3d.com/Manual/upm-ui-giturl.html).

## Software requirements

|Item |Description |
|---|---|
| **Unity Editor**   | **Unity Editor 2023.1** or later |
| **Platform**       | Windows 10 |

## Hardware recommendations

* Managed switch/router with access to [IGMP](https://en.wikipedia.org/wiki/Internet_Group_Management_Protocol) settings (See [Cluster Times Out After Period](troubleshooting.md#cluster-times-out-after-period)).

* Choose a motherboard that supports [IPMI](https://en.wikipedia.org/wiki/Intelligent_Platform_Management_Interface) so you can remotely shutdown, restart and boot your nodes without needing physical access to the machines.

* If you are using Quadro Sync the following hardware is required:
    * Requires one or more [NVIDIA Quadro GPU](https://www.nvidia.com/en-us/design-visualization/quadro/)s.
    * Requires one or more [NVIDIA Quadro Sync II](https://www.nvidia.com/en-us/design-visualization/solutions/quadro-sync/) boards.
