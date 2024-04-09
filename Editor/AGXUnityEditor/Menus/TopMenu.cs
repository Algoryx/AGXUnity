using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Utils;
using UnityEditor;
using UnityEngine;
using Mesh = AGXUnity.Collide.Mesh;
using Plane = AGXUnity.Collide.Plane;

namespace AGXUnityEditor
{
  public static class TopMenu
  {
    public static readonly string AGXDynamicsForUnityManualURL = "https://us.download.algoryx.se/AGXUnity/documentation/current/";
    public static readonly string AGXDynamicsForUnityExamplesURL = "https://us.download.algoryx.se/AGXUnity/documentation/current/examples.html";
    public static readonly string AGXUserManualURL = "https://www.algoryx.se/documentation/complete/agx/tags/latest/UserManual/source/";
    public static readonly string AGXAPIReferenceURL = "https://www.algoryx.se/documentation/complete/agx/tags/latest/";
    public static readonly string AGXUnityChangelogURL = "https://us.download.algoryx.se/AGXUnity/documentation/current/changelog.html";

    #region Shapes
    private static GameObject CreateShape<T>( MenuCommand command )
      where T : Shape
    {
      var go = Factory.Create<T>();
      if ( go == null )
        return null;

      var parent = command.context as GameObject;
      if ( parent != null )
        go.transform.SetParent( parent.transform, false );

      AGXUnity.Rendering.ShapeVisual.Create( go.GetComponent<T>() );

      Undo.RegisterCreatedObjectUndo( go, "shape" );

      return go;
    }

    [MenuItem( "AGXUnity/Collide/Box", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Box", validate = false, priority = 10 )]
    public static GameObject CreateBox( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Box>( command );
    }

    [MenuItem( "AGXUnity/Collide/Sphere", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Sphere", validate = false, priority = 10 )]
    public static GameObject CreateSphere( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Sphere>( command );
    }

    [MenuItem( "AGXUnity/Collide/Capsule", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Capsule", validate = false, priority = 10 )]
    public static GameObject CreateCapsule( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Capsule>( command );
    }

    [MenuItem( "AGXUnity/Collide/Cylinder", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Cylinder", validate = false, priority = 10 )]
    public static GameObject CreateCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Cylinder>( command );
    }

    [MenuItem( "AGXUnity/Collide/Plane", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Plane", validate = false, priority = 10 )]
    public static GameObject CreatePlane( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Plane>( command );
    }

    [MenuItem( "AGXUnity/Collide/Mesh", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Mesh", validate = false, priority = 10 )]
    public static GameObject CreateMesh( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Mesh>( command );
    }

    [MenuItem( "AGXUnity/Collide/HollowCylinder", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Hollow Cylinder", validate = false, priority = 10 )]
    public static GameObject CreateHollowCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<HollowCylinder>( command );
    }

    [MenuItem( "AGXUnity/Collide/Cone", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Cone", validate = false, priority = 10 )]
    public static GameObject CreateCone( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Cone>( command );
    }

    [MenuItem( "AGXUnity/Collide/HollowCone", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Collide/Hollow Cone", validate = false, priority = 10 )]
    public static GameObject CreateHollowCone( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<HollowCone>( command );
    }
    #endregion

    #region Rigid bodies
    private static GameObject CreateRigidBody( MenuCommand command, GameObject child = null )
    {
      var parent = command.context as GameObject;
      var go = child != null ?
                 Factory.Create<RigidBody>( child ) :
                 Factory.Create<RigidBody>();
      if ( go == null )
        return null;

      if ( parent != null )
        go.transform.SetParent( parent.transform, false );

      Undo.RegisterCreatedObjectUndo( go, "Rigid body" );

      return go;
    }

    private static GameObject CreateRigidBody<T>( MenuCommand command )
      where T : Shape
    {
      return CreateRigidBody( command, CreateShape<T>( new MenuCommand( null ) ) );
    }

    [MenuItem( "AGXUnity/Rigid body/Box", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Box", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyBox( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Box>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Sphere", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Sphere", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodySphere( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Sphere>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Capsule", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Capsule", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyCapsule( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Capsule>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Cylinder", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Cylinder", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Cylinder>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Mesh", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Mesh", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyMesh( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Mesh>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Hollow Cylinder", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Hollow Cylinder", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyHollowCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<HollowCylinder>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Cone", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Cone", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyCone( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Cone>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Hollow Cone", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Hollow Cone", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyHollowCone( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<HollowCone>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Empty", priority = 31 )]
    [MenuItem( "GameObject/AGXUnity/Rigid body/Empty", validate = false, priority = 10 )]
    public static GameObject CreateRigidBodyEmpty( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody( command );
    }
    #endregion

    #region Constraint
    private static GameObject CreateConstraint( MenuCommand command, ConstraintType type )
    {
      var parent = command.context as GameObject;
      var go = Factory.Create( type );
      if ( go == null )
        return null;

      if ( parent != null )
        go.transform.SetParent( parent.transform, false );

      Undo.RegisterCreatedObjectUndo( go, "Constraint" );

      return go;
    }

    [MenuItem( "AGXUnity/Constraints/Hinge", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Hinge", validate = false, priority = 10 )]
    public static GameObject ConstraintHinge( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.Hinge );
    }

    [MenuItem( "AGXUnity/Constraints/Prismatic", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Prismatic", validate = false, priority = 10 )]
    public static GameObject ConstraintPrismatic( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.Prismatic );
    }

    [MenuItem( "AGXUnity/Constraints/Lock Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Lock Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintLockJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.LockJoint );
    }

    [MenuItem( "AGXUnity/Constraints/Cylindrical Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Cylindrical Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintCylindricalJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.CylindricalJoint );
    }

    [MenuItem( "AGXUnity/Constraints/Ball Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Ball Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintBallJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.BallJoint );
    }

    [MenuItem( "AGXUnity/Constraints/Distance Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Distance Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintDistanceJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.DistanceJoint );
    }

    [MenuItem( "AGXUnity/Constraints/Angular Lock Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Angular Lock Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintAngularLockJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.AngularLockJoint );
    }

    [MenuItem( "AGXUnity/Constraints/Plane Joint", priority = 20 )]
    [MenuItem( "GameObject/AGXUnity/Constraints/Plane Joint", validate = false, priority = 10 )]
    public static GameObject ConstraintPlaneJoint( MenuCommand command )
    {
      return Selection.activeGameObject = CreateConstraint( command, ConstraintType.PlaneJoint );
    }
    #endregion

    #region Model
    private static GameObject CreateModel<T>( MenuCommand command ) where T : ScriptComponent
    {
      var go = Factory.Create<T>();
      if ( go == null )
        return null;

      var parent = command.context as GameObject;
      if ( parent != null )
        go.transform.SetParent( parent.transform, false );

      Undo.RegisterCreatedObjectUndo( go, $"New {typeof( T ).Name}" );

      return go;
    }

    [MenuItem( "AGXUnity/Model/Wire", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Wire", validate = false, priority = 10 )]
    public static GameObject WireEmpty( MenuCommand command )
    {
      return Selection.activeGameObject = CreateModel<Wire>( command );
    }

    [MenuItem( "AGXUnity/Model/Cable", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Cable", validate = false, priority = 10 )]
    public static GameObject CableEmpty( MenuCommand command )
    {
      return Selection.activeGameObject = CreateModel<Cable>( command );
    }

    [MenuItem( "AGXUnity/Model/Track", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Track", validate = false, priority = 10 )]
    public static GameObject CreateTrack( MenuCommand command )
    {
      return Selection.activeGameObject = CreateModel<Track>( command );
    }

    [MenuItem( "AGXUnity/Model/Deformable Terrain", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Deformable Terrain", validate = false, priority = 10 )]
    public static GameObject CreateDeformableTerrain( MenuCommand command )
    {
      var terrainData = new TerrainData()
      {
        size = new Vector3( 60 / 8.0f, 45, 60 / 8.0f ),
        heightmapResolution = 257
      };
      terrainData.SetDetailResolution( 1024, terrainData.detailResolutionPerPatch );

      var terrainDataName = AssetDatabase.GenerateUniqueAssetPath( "Assets/New Terrain.asset" );
      AssetDatabase.CreateAsset( terrainData, terrainDataName );

      var go = Terrain.CreateTerrainGameObject( terrainData );
      go.name = Factory.CreateName<DeformableTerrain>();
      if ( go == null ) {
        AssetDatabase.DeleteAsset( terrainDataName );
        return null;
      }

      AGXUnity.Utils.PrefabUtils.PlaceInCurrentStange( go );

      go.transform.position = new Vector3( -30, 0, -30 );
      go.AddComponent<DeformableTerrain>();

      if ( command.context is GameObject ctx )
        go.transform.SetParent( ctx.transform, false );

      Undo.RegisterCreatedObjectUndo( go, "New Deformable Terrain" );

      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Deformable Terrain Pager", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Deformable Terrain Pager", validate = false, priority = 10 )]
    public static GameObject CreateTerrainPager( MenuCommand command )
    {
      var terrainData = new TerrainData()
      {
        size = new Vector3( 60 / 8.0f, 45, 60 / 8.0f ),
        heightmapResolution = 517
      };
      terrainData.SetDetailResolution( 1024, terrainData.detailResolutionPerPatch );

      var terrainDataName = AssetDatabase.GenerateUniqueAssetPath( "Assets/New Terrain.asset" );
      AssetDatabase.CreateAsset( terrainData, terrainDataName );

      var go = Terrain.CreateTerrainGameObject( terrainData );
      go.name = Factory.CreateName<DeformableTerrainPager>();
      if ( go == null ) {
        AssetDatabase.DeleteAsset( terrainDataName );
        return null;
      }

      AGXUnity.Utils.PrefabUtils.PlaceInCurrentStange( go );

      go.transform.position = new Vector3( -60, 0, -60 );
      go.AddComponent<DeformableTerrainPager>();

      if ( command.context is GameObject ctx )
        go.transform.SetParent( ctx.transform, false );

      Undo.RegisterCreatedObjectUndo( go, "New Terrain Pager" );

      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Movable Terrain", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Movable Terrain", validate = false, priority = 10 )]
    public static GameObject CreateMovableTerrain( MenuCommand command )
    {
      var go = new GameObject();
      go.name = Factory.CreateName<MovableTerrain>();

      AGXUnity.Utils.PrefabUtils.PlaceInCurrentStange( go );

      go.AddComponent<MeshFilter>();
      var renderer = go.AddComponent<MeshRenderer>();
      renderer.sharedMaterial = RenderingUtils.CreateDefaultMaterial();
      RenderingUtils.SetMainTexture( renderer.sharedMaterial, AssetDatabase.GetBuiltinExtraResource<Texture2D>( "Default-Checker-Gray.png" ) );
      go.AddComponent<MovableTerrain>();

      if ( command.context is GameObject ctx )
        go.transform.SetParent( ctx.transform, false );

      Undo.RegisterCreatedObjectUndo( go, "New Movable Terrain" );

      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Terrain Material Patch", priority = 50 )]
    [MenuItem( "GameObject/AGXUnity/Model/Terrain Material Patch", validate = false, priority = 10 )]
    public static GameObject CreateTerrainMaterialPatch( MenuCommand command )
    {
      var go = CreateModel<TerrainMaterialPatch>(command);

      var box = Factory.Create<Box>();
      box.transform.SetParent( go.transform, false );
      box.GetComponent<Box>().HalfExtents = new Vector3( 2.5f, 1.0f, 2.5f );
      AGXUnity.Rendering.ShapeVisual.Create( box.GetComponent<Box>() );

      Undo.RegisterCreatedObjectUndo( go, "New Terrain Material Patch" );

      return Selection.activeGameObject = go;
    }

    #endregion

    #region Managers
    [MenuItem( "AGXUnity/Managers/Debug Render Manager", validate = true )]
    private static bool DebugRendererValidate()
    {
      return ValidateManager<AGXUnity.Rendering.DebugRenderManager>();
    }

    [MenuItem( "AGXUnity/Managers/Debug Render Manager" )]
    public static GameObject DebugRenderer()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<AGXUnity.Rendering.DebugRenderManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Simulation", validate = true )]
    private static bool SimulationValidate()
    {
      return ValidateManager<Simulation>();
    }

    [MenuItem( "AGXUnity/Simulation", priority = 66 )]
    public static GameObject Simulation()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<Simulation>()?.gameObject;
    }

    [MenuItem( "AGXUnity/Plot", priority = 66 )]
    public static GameObject Plot()
    {
      var PlotObject = new GameObject("PlotObject");
      PlotObject.AddComponent<AGXUnity.Utils.Plot>();
      PlotObject.AddComponent<AGXUnity.Utils.DataSeries>();
      PlotObject.AddComponent<AGXUnity.Utils.DataSeries>();

#if USE_VISUAL_SCRIPTING
      var plotAssetPath = AGXUnityEditor.IO.Utils.AGXUnityResourceDirectory + "/Plot/TemplatePlot.Asset";
      var targetAssetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Plot.Asset");
      AssetDatabase.CopyAsset( plotAssetPath, targetAssetPath );

      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();

      var sm = PlotObject.AddComponent<Unity.VisualScripting.ScriptMachine>();
      sm.nest.SwitchToMacro( AssetDatabase.LoadAssetAtPath<Unity.VisualScripting.ScriptGraphAsset>( targetAssetPath ) );
#endif

      return Selection.activeGameObject = PlotObject.gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Collision Groups Manager", validate = true )]
    private static bool CollisionGroupsManagerValidate()
    {
      return ValidateManager<CollisionGroupsManager>();
    }


    [MenuItem( "AGXUnity/Managers/Collision Groups Manager", priority = 65 )]
    public static GameObject CollisionGroupsManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<CollisionGroupsManager>()?.gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Contact Material Manager", validate = true )]
    private static bool ContactMaterialManagerValidate()
    {
      return ValidateManager<ContactMaterialManager>();
    }

    [MenuItem( "AGXUnity/Managers/Contact Material Manager", priority = 65 )]
    public static GameObject ContactMaterialManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<ContactMaterialManager>()?.gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Wind and Water Manager", validate = true )]
    private static bool WindAndWaterManagerValidate()
    {
      return ValidateManager<WindAndWaterManager>();
    }

    [MenuItem( "AGXUnity/Managers/Wind and Water Manager", priority = 65 )]
    public static GameObject WindAndWaterManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<WindAndWaterManager>()?.gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Script Asset Manager", validate = true )]
    private static bool ScriptAssetManagerValidate()
    {
      return ValidateManager<ScriptAssetManager>();
    }

    [MenuItem( "AGXUnity/Managers/Script Asset Manager", priority = 65 )]
    public static GameObject ScriptAssetManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<ScriptAssetManager>()?.gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Pick Handler (Game View)", validate = true )]
    private static bool PickHandlerValidate()
    {
      return ValidateManager<PickHandler>();
    }

    [MenuItem( "AGXUnity/Managers/Pick Handler (Game View)", priority = 65 )]
    public static GameObject PickHandler()
    {
      var ph = GetOrCreateUniqueGameObject<PickHandler>();
      if ( ph == null )
        return null;

      if ( ph.MainCamera == null ) {
        // Check for tagged main camera.
        if ( Camera.main != null ) {
          ph.MainCamera = Camera.main.gameObject;
        }
        // Search for any camera containing "Main".
        else {
          foreach ( var camera in Camera.allCameras ) {
            if ( camera.name.Contains( "Main" ) ) {
              ph.MainCamera = camera.gameObject;
              break;
            }
          }
        }

        if ( ph.MainCamera == null )
          Debug.LogWarning( "Unable to find Main Camera. You have to manually assign view camera for pick handler to work.", ph );
      }
      return Selection.activeGameObject = ph.gameObject;
    }
    #endregion

    #region Utils Settings
    [MenuItem( "AGXUnity/Utils/Generate Custom Editors", priority = 80 )]
    public static void GenerateEditors()
    {
      Utils.CustomEditorGenerator.Generate();
    }

    [MenuItem( "AGXUnity/Utils/Convert Rendering Materials", priority = 80 )]
    public static void ConvertRenderingMaterials()
    {
      Windows.ConvertMaterialsWindow.Open();
    }

    [MenuItem( "AGXUnity/Utils/Convert PhysX components to AGX", priority = 80 )]
    public static void ConvertPhysXToAGX()
    {
      Windows.ConvertPhysXToAGXWindow.Open();
    }

    [MenuItem( "AGXUnity/Settings...", priority = 81 )]
    public static void OpenSettings()
    {
      SettingsService.OpenProjectSettings( "Project/AGXSettings" );
    }

    public static T GetOrCreateUniqueGameObject<T>()
      where T : ScriptComponent
    {
      bool hadInstance = UniqueGameObject<T>.HasInstanceInScene;
      if ( !hadInstance && AGXUnity.Utils.PrefabUtils.IsEditingPrefab ) {
        Debug.LogWarning( $"Invalid to create {typeof( T ).FullName} while editing prefabs." );
        return null;
      }

      T obj = UniqueGameObject<T>.Instance;
      if ( !hadInstance && obj != null )
        Undo.RegisterCreatedObjectUndo( obj.gameObject, "Created " + obj.name );

      return obj;
    }

    private static bool ValidateManager<T>()
      where T : UniqueGameObject<T>
    {
      return !AGXUnity.Utils.PrefabUtils.IsEditingPrefab;
    }
    #endregion

    #region Documentation, About and Update
    [MenuItem( "AGXUnity/AGX Dynamics for Unity Manual", priority = 2001 )]
    public static void AGXDynamicsForUnityManual()
    {
      Application.OpenURL( AGXDynamicsForUnityManualURL );
    }

    [MenuItem( "AGXUnity/AGX Dynamics for Unity Examples", priority = 2002 )]
    public static void AGXDynamicsForUnityExamples()
    {
      Application.OpenURL( AGXDynamicsForUnityExamplesURL );
    }

    [MenuItem( "AGXUnity/AGX Dynamics Manual", priority = 2020 )]
    public static void AGXManual()
    {
      Application.OpenURL( AGXUserManualURL );
    }

    [MenuItem( "AGXUnity/AGX Dynamics API Reference", priority = 2021 )]
    public static void AGXAPI()
    {
      Application.OpenURL( AGXAPIReferenceURL );
    }

    [MenuItem( "AGXUnity/About AGX Dynamics for Unity", priority = 2040 )]
    public static void AboutWindow()
    {
      Windows.AboutWindow.Open();
    }

    [MenuItem( "AGXUnity/License/License Manager", priority = 2041 )]
    public static void LicenseManagerWindow()
    {
      Windows.LicenseManagerWindow.Open();
    }

    [MenuItem( "AGXUnity/License/Runtime Activation Generator", priority = 2042 )]
    public static void RuntimeGeneratorWindow()
    {
      Windows.GenerateRuntimeLicenseActivationWindow.Open();
    }

    [MenuItem( "AGXUnity/Check for Updates...", priority = 2060, validate = true )]
    public static bool CheckForUpdatesWindowValidater()
    {
      return PackageManifest.Instance.GetAGXUnityVersionInfo().IsValid;
    }

    [MenuItem( "AGXUnity/Check for Updates...", priority = 2060 )]
    public static void CheckForUpdatesWindow()
    {
      Windows.CheckForUpdatesWindow.Open();
    }
    #endregion
  }
}

