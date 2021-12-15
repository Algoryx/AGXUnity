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

[**Getting started :**](https://youtu.be/IUj0QcniSik)  
[![](https://img.youtube.com/vi/IUj0QcniSik/1.jpg)](https://youtu.be/IUj0QcniSik)

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

## Brick workflow

Everything needed to run a Brick project is already present in this repository, and it should work out of the box. If you want to build your own Brick binaries, check the section below.

### Building Brick

To build new Brick dlls, you need to follow these steps:

1. Find your Brick root directory and save the path to the `BRICK_DIR` environment variable (`set BRICK_DIR=<absolute path>`).
2. Find your AgxBrick directory (located in `brick/AgxBrick` relative to your AGX directory), and save the path to the `AGXBRICK_DIR` environment variable (`set AGXBRICK_DIR=<absolute path>`).
3. Create the file `brick\LocalBuildConfig.props` (relative to your AGX directory) and fill it with the following content:

```xml
<Project>
  <PropertyGroup>
    <BRICK_SOURCE_DIR>$(BRICK_DIR)</BRICK_SOURCE_DIR>
    <BRICK_VERSION></BRICK_VERSION>
  </PropertyGroup>
</Project>
```
4. Run the following commands from the Unity project root directory:

```
set AGXDOTNET_PATH=%cd%\Assets\AGXUnity\Plugins\x86_64\agxDotNet.dll
set BRICK_PUBLISH_DIR=%cd%\Assets\AGXUnity\Plugins\x86_64\Brick
set BRICK_DISABLE_GRPC=1
set TMP_DIR=brick_tmp_build_dir
mkdir %TMP_DIR%
dotnet publish %AGXBRICK_DIR%\cs\brick\AgxBrick -f net471 --output %TMP_DIR% --self-contained false -p:BuildNetFxOnly=true -c Release
robocopy %TMP_DIR% %BRICK_PUBLISH_DIR% /purge /xf *.meta /xf agxDotNet.* /xf agxMathDotNet.* /xf Newtonsoft.Json.* /xf System.Net.Http.* /xd modules
rmdir %TMP_DIR%
robocopy %BRICK_DIR%\modules %BRICK_PUBLISH_DIR%\modules /purge /s /xd .git /xd AgxBrick /xd AgxBrickDemos /xd DevOps /xd Experiments /xf *.meta
robocopy %AGXBRICK_DIR% %BRICK_PUBLISH_DIR%\modules\AgxBrick *.yml /purge /xf *.meta
```

As an alternative to step 3 you can also create and run a batch file in the Unity project root directory with the following contents:

```
@echo off
setlocal

set AGXDOTNET_PATH=%~dp0Assets\AGXUnity\Plugins\x86_64\agxDotNet.dll
if not exist %AGXDOTNET_PATH% (
    echo WARNING! agxDotNet.dll could not be found. Make sure it exists in %AGXDOTNET_PATH%
    exit /b 1
)

if "%BRICK_DIR%"=="" (
    echo Could not find Brick modules, make sure the BRICK_DIR environment variable has been set to point to a Brick root directory.
    exit /b 1
)

if "%AGXBRICK_DIR%"=="" (
    echo AGXBRICK_DIR environment variable not set. Set it to point to the directory of the AgxBrick.csproj file.
    exit /b 1
)

echo Using BRICK_DIR=%BRICK_DIR%
echo Using AGXBRICK_DIR=%AGXBRICK_DIR%
set BRICK_DISABLE_GRPC=1
set BRICK_PUBLISH_DIR=%~dp0Assets\AGXUnity\Plugins\x86_64\Brick
set TMP_DIR=brick_tmp_build_dir
if exist %TMP_DIR% rmdir /s /q %TMP_DIR%
mkdir %TMP_DIR%
dotnet publish %AGXBRICK_DIR%\cs\brick\AgxBrick^
    -f net471^
    --output %TMP_DIR%^
    --self-contained false^
    -p:BuildNetFxOnly=true^
    -c Release^
    || exit /b %ERRORLEVEL%

robocopy %TMP_DIR% %BRICK_PUBLISH_DIR%^
    *.dll *.pdb^
    /purge^
    /xf *.meta^
    /xf agxDotNet.*^
    /xf agxMathDotNet.*^
    /xf Newtonsoft.Json.*^
    /xf System.Net.Http.*^
    /xd modules
rmdir /s /q %TMP_DIR% || exit /b %ERRORLEVEL%

robocopy %BRICK_DIR%\modules %BRICK_PUBLISH_DIR%\modules^
    /purge^
    /s^
    /xd .git^
    /xd AgxBrick^
    /xd AgxBrickDemos^
    /xd DevOps^
    /xd Experiments^
    /xf *.meta

robocopy %AGXBRICK_DIR% %BRICK_PUBLISH_DIR%\modules\AgxBrick^
    *.yml^
    /purge^
    /xf *.meta

exit /b 0
```

### Building Unity-executable with Brick
When building a standalone player from Unity, we want to be able to start the executable without having to install Brick first. To be able to do this a few things are needed.

1. Copy the Brick `modules`-folder from `Assets/AGXUnity/Plugins/x86_64/Brick` to `<build-name>_Data/brick/modules`, in the build folder
2. Copy any Brick-yml files to the build folder and make sure that the file path in the `BrickRuntimeComponent` (set in the editor of the imported Brick object) points at it. It should be a relative path if you want to be able to find it even if the built application is moved to another computer.

An example of what a build-script might look like if the Brick-yml files are located in a folder called BrickModels can be seen below. This should be run in the root directory of the project.

```
echo "Building for %BUILD_TARGET%"

:build_setup
IF defined BUILD_PATH (
  echo Use build path %BUILD_PATH%
) ELSE (
  set BUILD_PATH=Build
)

IF defined BUILD_TARGET (
  echo Use build target %BUILD_TARGET%
) ELSE (
  set BUILD_TARGET=StandaloneWindows64
)

IF defined BUILD_NAME (
  echo Use build name %BUILD_NAME%
) ELSE (
  set BUILD_NAME=brick-in-unity
)

echo on
mkdir %BUILD_PATH% || goto :error

:build
"C:\Program Files\Unity\Hub\Editor\2020.3.24f1\Editor\Unity.exe" ^
  -projectPath "%cd%" ^
  -quit ^
  -batchmode ^
  -buildWindows64Player "%BUILD_PATH%/%BUILD_NAME%.exe" ^
  -buildTarget %BUILD_TARGET% ^
  -logFile buildlog.log ^
  || goto :error

REM Copy BrickDir folder to build path
robocopy Assets\AGXUnity\Plugins\x86_64\Brick\modules %BUILD_PATH%/%BUILD_NAME%_Data\Brick\modules /MIR
robocopy BrickModels %BUILD_PATH%BrickModels /MIR

if NOT "%BUILD_TARGET%" == "StandaloneWindows64" (
  goto :exit
)

:exit
exit /b 0

:error
echo Failed with error #%errorlevel%.
set STATUS=%errorlevel%
if "%errorlevel%" == "0" (set STATUS=1)
exit /b %STATUS%
```

## Issues and contributions

Do not hesitate to send us a Pull Request.

If something is missing or not working as expected - fix it and send a pull request.

If you have no idea how to implement a feature or fix a bug - create an issue.

## License

[Apache License 2.0](LICENSE)
