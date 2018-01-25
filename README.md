[Algoryx Simulation AB](https://www.algoryx.se)

# AGXUnity - AGX Dynamics Unity 3D plugin

*Unity 3D bindings for [AGX Dynamics](https://www.algoryx.se/products/agx-dynamics/)* from [Algoryx Simulation AB](https://www.algoryx.se)

AGX Dynamics is a professional multi-purpose physics engine for simulators, Virtual Reality (VR), engineering, large scale granular simulations and more. AGX is being utilized in hundreds of training simulators and helps engineers design and evaluate new mechanical systems in *[Algoryx Momentum](https://www.algoryx.se/products/algoryx-momentum/)*.

## Installation

Unity has to be started with AGX environment variables. For example (command prompt):

---
```
C:\>"Program Files\Algoryx\agx-2.21.0.0\setup_env.bat"

Visual Studio version range:
    [15.0, 16.0)
Visual Studio installation path:
    C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional
vcvarsall.bat used:
    "C:\Program Files (x86)\Microsoft Visual Studio\2017\Professional\VC\Auxiliary\Build\vcvarsall.bat"

**********************************************************************
** Visual Studio 2017 Developer Command Prompt v15.5.1
** Copyright (c) 2017 Microsoft Corporation
**********************************************************************
[vcvarsall.bat] Environment initialized for: 'x64'

Using Python interpreter C:\Program Files\Python35\python.exe

C:\>"Program Files\Unity\Editor\Unity.exe"
```
---

Without the AGX environment, Unity could crash or output error: `DllNotFoundException: agxDotNetRuntime.dll`

### From package
You can find packages in [Releases](releases).

1. Drag and drop `AGXUnity-x.y.unitypackage` into the `Assets` folder in Unity.
2. Copy `agxDotNet.dll` (`<agx_dynamics_install_dir>/bin/x64/agxDotNet.dll`) into AGXUnity plugins folder (`Assets/AGXUnity/Plugins/x86_64`).

### From source

1. `git clone https://github.com/Algoryx/AGXUnity.git` in the `Assets` folder of your Unity project.
2. Copy `agxDotNet.dll` (`<agx_dynamics_install_dir>/bin/x64/agxDotNet.dll`) into AGXUnity plugins folder (`Assets/AGXUnity/Plugins/x86_64`).

### Requirements

+ AGX Dynamics 2.20.0.0 (64-bit) or later.
+ Unity 3D 2017.1.1 (64-bit) or later. Could work in earlier version but hasn't been tested.
+ Valid AGX Dynamics license. [Contact us for more information.](https://www.algoryx.se/contact/)

## Developer

For more information about how to develop new functionality in AGXUnity - [read the developer guide](DeveloperGuide.md).

## Tutorials

[**Modelling with primitives:**](https://www.youtube.com/watch?v=1ddfgIwAd0U)  
[![](https://img.youtube.com/vi/1ddfgIwAd0U/1.jpg)](https://www.youtube.com/watch?v=1ddfgIwAd0U)

[**Modelling with materials:**](https://www.youtube.com/watch?v=bB6d8ZI8bt4)  
[![](https://img.youtube.com/vi/bB6d8ZI8bt4/1.jpg)](https://www.youtube.com/watch?v=bB6d8ZI8bt4)

[**Modelling with constraints:**](https://www.youtube.com/watch?v=dmlyozKuVlM)  
[![](https://img.youtube.com/vi/dmlyozKuVlM/1.jpg)](https://www.youtube.com/watch?v=dmlyozKuVlM)

[**Modelling with triangle meshes:**](https://www.youtube.com/watch?v=L2kRByHcT7g)  
[![](https://img.youtube.com/vi/L2kRByHcT7g/1.jpg)](https://www.youtube.com/watch?v=L2kRByHcT7g)

[**Modelling with wires:**](https://www.youtube.com/watch?v=Accpit3LmIA)  
[![](https://img.youtube.com/vi/Accpit3LmIA/1.jpg)](https://www.youtube.com/watch?v=Accpit3LmIA)

## Issues and contributions

Do not hesitate to send us a Pull Request.

If something is missing or not working as expected - fix it and send a pull request.

If you have no idea how to implement a feature or fix a bug - create an issue.

## License

[Apache License 2.0](LICENSE)
