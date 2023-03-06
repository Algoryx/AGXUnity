[![agx-dynamics-for-unity](https://github.com/Algoryx/AGXUnity/blob/master/Editor/Data/agx_for_unity_logo_black.png)](https://www.algoryx.se/agx-unity/)

# AGX Dynamics for Unity - AGXUnity

*Unity bindings for [AGX Dynamics](https://www.algoryx.se/agx-dynamics/)* from [Algoryx Simulation AB](https://www.algoryx.se).

AGX Dynamics is a professional multi-purpose physics engine for simulators, Virtual Reality (VR), engineering, large scale granular simulations and more. AGX is being utilized in hundreds of training simulators and helps engineers design and evaluate new mechanical systems in *[Algoryx Momentum](https://www.algoryx.se/momentum/)* and *[Algoryx Momentum Granular](https://www.algoryx.se/momentum-granular/)*.

## Official package

  - Contains a clone of the source from this repository but also AGX Dynamics (64-bit Windows) binaries as [native plugins](https://docs.unity3d.com/Manual/NativePlugins.html).
  - Doesn't require AGX Dynamics environment variables inside Unity nor in builds, so any project with **AGX Dynamics for Unity** supports Unity Hub.
  - Supports `Check for updates...` and update of the installed package when a new official release is available.

Please check [AGX Dynamics for Unity product page](https://www.algoryx.se/agx-unity/) for more information.

## Installation - with AGX Dynamics installed separately

If you don't want to use **AGX Dynamics for Unity (with AGX Dynamics included)**, the AGXUnity plugin can be installed separately following these instructions and alternatives. Unity has to be started with AGX Dynamics environment variables - for example (command prompt):

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

C:\>"Program Files\Unity\Editor\Unity.exe" -projectPath MyUnityProject
```
---

Note that `-projectPath` has to be given when you have Unity Hub installed,
[since Unity Hub doesn't forward the environment variables to the Unity process](https://issuetracker.unity3d.com/issues/environment-variables-dropped-when-using-unity-hub-2-dot-0-0).

Without the AGX environment, Unity could crash or output error: `DllNotFoundException: agxDotNetRuntime.dll`

### From package
You can find packages in [Releases](https://github.com/Algoryx/AGXUnity/releases).

1. Drag and drop `AGXUnity-x.y.unitypackage` into the `Assets` folder in Unity.
2. Copy `agxDotNet.dll` (`<agx_dynamics_install_dir>/bin/x64/agxDotNet.dll`) into AGXUnity plugins folder (`Assets/AGXUnity/Plugins/x86_64`).
3. Change Script Runtime Version to .NET 4.x: `Edit -> Project Settings -> Player`. Under `Other Settings` set `Scripting Runtime Version` to `.NET 4.x Equivalent` (Unity 2017.x: `Experimental (.NET 4.6 Equivalent)`). [More information.](https://docs.unity3d.com/Manual/ScriptingRuntimeUpgrade.html)

### From source

1. `git clone https://github.com/Algoryx/AGXUnity.git` in the `Assets` folder of your Unity project.
2. Copy `agxDotNet.dll` (`<agx_dynamics_install_dir>/bin/x64/agxDotNet.dll`) into AGXUnity plugins folder (`Assets/AGXUnity/Plugins/x86_64`).
3. Change Script Runtime Version to .NET 4.x: `Edit -> Project Settings -> Player`. Under `Other Settings` set `Scripting Runtime Version` to `.NET 4.x Equivalent` (Unity 2017.x: `Experimental (.NET 4.6 Equivalent)`). [More information.](https://docs.unity3d.com/Manual/ScriptingRuntimeUpgrade.html)

### Requirements

+ AGX Dynamics 2.31.0.0 (64-bit) or later (2.29.1.0 in rc/2.3, 2.29.0.0 in rc/2.1, 2.28.1.0 in rc/2.0).
+ Unity 3D 2018.4 LTS (64-bit) or later. Could work in earlier version but hasn't been tested.
+ Unity Script Runtime Version .NET 4.x Equivalent.
+ Valid AGX Dynamics license. [Contact us for more information.](https://www.algoryx.se/contact/)

## Migrating from AGXUnity-deprecated to AGXUnity

[See "Migration" in the AGXUnity-deprecated repository.](https://github.com/Algoryx/AGXUnity-deprecated/#migration)

## Developer

For more information about how to develop new functionality in AGXUnity - [read the developer guide](DeveloperGuide.md).

## Tutorials

[**Modelling a crane :**](https://www.youtube.com/watch?v=YNEDk1417iM)  
[![](https://img.youtube.com/vi/YNEDk1417iM/1.jpg)](https://www.youtube.com/watch?v=YNEDk1417iM)

### Older tutorials

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

[**Modelling a terrain vehicle:**](https://www.youtube.com/watch?v=ku6GyMba9Cw)  
[![](https://img.youtube.com/vi/ku6GyMba9Cw/1.jpg)](https://www.youtube.com/watch?v=ku6GyMba9Cw)

## Sample scenes
The project [AGXUnityScenes](https://github.com/Algoryx/AGXUnityScenes) contains a growing list of various demonstration scenes.

## Binary distribution
To distribute an Unity3D application together with AGX Dynamics, you need to collect the required runtime files from your AGX installation. These files must match the version of AGX used when building the Unity3D application.

Below is a list of required files/directories which come from the <agx-install-dir>/bin/x64/ directory. These files should be placed in the top directory together with your Unity exe-file.
In version 2.22.1.0 and later, AGX Dynamics comes with a python script that will copy the required runtime files to a named (non-existing) directory: <agx-dir>\data\python\utilities\copy_runtimes.py

```
agx.lic
agxCable.dll
agxCore.dll
agxDotNet.dll
agxDotNetRuntime.dll
agxHydraulics.dll
agxLua.dll
agxMex.dll
agxModel.dll
agxOSG.dll
agxPhysics.dll
agxPython.dll
agxSabre.dll
agxSensor.dll
agxVehicle.dll
colamd.dll
glew.dll
libpng.dll
lua.dll
mscorlib.dll
msvcp140.dll
ois.dll
osg141-osg.dll
osg141-osgDB.dll
osg141-osgGA.dll
osg141-osgShadow.dll
osg141-osgSim.dll
osg141-osgText.dll
osg141-osgUtil.dll
osg141-osgViewer.dll
ot20-OpenThreads.dll
python35.dll
vcruntime140.dll
websockets.dll
zlib.dll

And also, the directory: Components (from bin\x64\plugins)
```

Directory: **plugins**

Depending on Visual Studio version, the following files might differ:

```
mscorlib.dll
msvcp140.dll
vcruntime140.dll
```

## Issues and contributions

Do not hesitate to send us a Pull Request.

If something is missing or not working as expected - fix it and send a pull request.

If you have no idea how to implement a feature or fix a bug - create an issue.

## License

[Apache License 2.0](LICENSE)
