using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;

using Plane = AGXUnity.Collide.Plane;
using Mesh = AGXUnity.Collide.Mesh;

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
    [MenuItem( "AGXUnity/Constraints/Hinge", priority = 20 )]
    public static GameObject ConstraintHinge()
    {
      GameObject go = Factory.Create( ConstraintType.Hinge );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Prismatic", priority = 20 )]
    public static GameObject ConstraintPrismatic()
    {
      GameObject go = Factory.Create( ConstraintType.Prismatic );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Lock Joint", priority = 20 )]
    public static GameObject ConstraintLockJoint()
    {
      GameObject go = Factory.Create( ConstraintType.LockJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Cylindrical Joint", priority = 20 )]
    public static GameObject ConstraintCylindricalJoint()
    {
      GameObject go = Factory.Create( ConstraintType.CylindricalJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Ball Joint", priority = 20 )]
    public static GameObject ConstraintBallJoint()
    {
      GameObject go = Factory.Create( ConstraintType.BallJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Distance Joint", priority = 20 )]
    public static GameObject ConstraintDistanceJoint()
    {
      GameObject go = Factory.Create( ConstraintType.DistanceJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Angular Lock Joint", priority = 20 )]
    public static GameObject ConstraintAngularLockJoint()
    {
      GameObject go = Factory.Create( ConstraintType.AngularLockJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Plane Joint", priority = 20 )]
    public static GameObject ConstraintPlaneJoint()
    {
      GameObject go = Factory.Create( ConstraintType.PlaneJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }
    #endregion

    #region Model
    [MenuItem( "AGXUnity/Model/Wire", priority = 50 )]
    public static GameObject WireEmpty()
    {
      GameObject go = Factory.Create<Wire>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "New Wire" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Cable", priority = 50 )]
    public static GameObject CableEmpty()
    {
      GameObject go = Factory.Create<Cable>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "New Cable" );

      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Track", priority = 50 )]
    public static GameObject CreateTrack()
    {
      var go = Factory.Create<AGXUnity.Model.Track>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "New Track" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Model/Deformable Terrain", priority = 50 )]
    public static GameObject CreateDeformableTerrain()
    {
      var terrainData = new TerrainData()
      {
        size = new Vector3( 60 / 8.0f, 45, 60 / 8.0f ),
        heightmapResolution = 257
      };
#if UNITY_2018_1_OR_NEWER
      terrainData.SetDetailResolution( 1024, terrainData.detailResolutionPerPatch );
#else
      terrainData.SetDetailResolution( 1024, terrainData.detailResolution );
#endif

      var terrainDataName = AssetDatabase.GenerateUniqueAssetPath( "Assets/New Terrain.asset" );
      AssetDatabase.CreateAsset( terrainData, terrainDataName );

      var go = Terrain.CreateTerrainGameObject( terrainData );
      go.name = Factory.CreateName<AGXUnity.Model.DeformableTerrain>();
      if ( go == null ) {
        AssetDatabase.DeleteAsset( terrainDataName );
        return null;
      }

      AGXUnity.Utils.PrefabUtils.PlaceInCurrentStange( go );

      go.transform.position = new Vector3( -30, 0, -30 );
      go.AddComponent<AGXUnity.Model.DeformableTerrain>();

      Undo.RegisterCreatedObjectUndo( go, "New Deformable Terrain" );

      return Selection.activeGameObject = go;
    }

    #endregion

    #region Managers
    [MenuItem( "AGXUnity/Managers/Debug Render Manager", validate = true )]
    private static bool DebugRendererValidate()
    {
      return ValidateManager<AGXUnity.Rendering.DebugRenderManager>();
    }

    [MenuItem( "AGXUnity/Managers/Debug Render Manager" ) ]
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

    [MenuItem( "AGXUnity/Settings...", priority = 81 )]
    public static void FocusSettings()
    {
      var instance = EditorSettings.Instance;
      if ( instance == null )
        return;

      EditorUtility.FocusProjectWindow();
      Selection.activeObject = instance;
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

    [MenuItem("AGXUnity/AGX Dynamics Manual", priority = 2020)]
    public static void AGXManual()
    {
      Application.OpenURL(AGXUserManualURL);
    }

    [MenuItem("AGXUnity/AGX Dynamics API Reference", priority = 2021)]
    public static void AGXAPI()
    {
      Application.OpenURL(AGXAPIReferenceURL);
    }

    [MenuItem("AGXUnity/About AGX Dynamics for Unity", priority = 2040)]
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
      return PackageUpdateHandler.FindCurrentVersion().IsValid;
    }

    [MenuItem( "AGXUnity/Check for Updates...", priority = 2060 )]
    public static void CheckForUpdatesWindow()
    {
      Windows.CheckForUpdatesWindow.Open();
    }
    #endregion
  }
}

