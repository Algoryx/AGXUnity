using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Collide;

using Plane = AGXUnity.Collide.Plane;
using Mesh = AGXUnity.Collide.Mesh;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  public static class TopMenu
  {
    public static string AGXDynamicsForUnityManualURL = "https://www.algoryx.se/documentation/complete/AGXUnity/current/";
    public static string AGXUserManualURL = "https://www.algoryx.se/documentation/complete/agx/tags/latest/UserManual/source/";
    public static string AGXAPIReferenceURL = "https://www.algoryx.se/documentation/complete/agx/tags/latest/";

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
    #endregion

    #region Rigid bodies
    private static GameObject CreateRigidBody( MenuCommand command, GameObject child = null )
    {
      // It's possible, but very unintuitive, to have validation methods
      // for GameObject context menu since validation is performed when
      // the menu item is clicked (i.e., not when shown), so the invalid items
      // aren't grayed out. Currently it's better to given the user a warning
      // with context.
      var parent      = command.context as GameObject;
      var parentValid = parent == null ||
                        parent.GetComponentInParent<RigidBody>() == null;
      if ( !parentValid ) {
        Debug.LogWarning( "Invalid to create child rigid body to " +
                          parent.name +
                          " because parent rigid body already exists.",
                          parent.GetComponentInParent<RigidBody>() );
        return null;
      }

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
      return CreateRigidBody( command, Factory.Create<T>() );
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
        size = new Vector3( 60 / 8.0f, 25, 60 / 8.0f ),
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

      go.transform.position = new Vector3( -30, 0, -30 );
      go.AddComponent<AGXUnity.Model.DeformableTerrain>();

      Undo.RegisterCreatedObjectUndo( go, "New Deformable Terrain" );

      return Selection.activeGameObject = go;
    }

    #endregion

    #region Managers
    [MenuItem( "AGXUnity/Managers/Debug Render Manager" ) ]
    public static GameObject DebugRenderer()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<AGXUnity.Rendering.DebugRenderManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Simulation", priority = 66 )]
    public static GameObject Simulation()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<Simulation>().gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Collision Groups Manager", priority = 65 )]
    public static GameObject CollisionGroupsManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<CollisionGroupsManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Contact Material Manager", priority = 65 )]
    public static GameObject ContactMaterialManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<ContactMaterialManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Wind and Water Manager", priority = 65 )]
    public static GameObject WindAndWaterManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<WindAndWaterManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Managers/Pick Handler (Game View)", priority = 65 )]
    public static GameObject PickHandler()
    {
      var ph = GetOrCreateUniqueGameObject<PickHandler>();
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
      bool hadInstance = UniqueGameObject<T>.HasInstance;
      if ( UniqueGameObject<T>.Instance == null )
        UniqueGameObject<T>.ResetDestroyedState();

      T obj = UniqueGameObject<T>.Instance;
      if ( !hadInstance && obj != null )
        Undo.RegisterCreatedObjectUndo( obj.gameObject, "Created " + obj.name );

      return obj;
    }
    #endregion

    #region Documentation
    [MenuItem( "AGXUnity/AGX Dynamics for Unity Manual", priority = 2001 )]
    public static void AGXDynamicsForUnityManual()
    {
      Application.OpenURL( AGXDynamicsForUnityManualURL );
    }

    [MenuItem("AGXUnity/AGX Dynamics Manual", priority = 2002)]
    public static void AGXManual()
    {
      Application.OpenURL(AGXUserManualURL);
    }

    [MenuItem("AGXUnity/AGX Dynamics API Reference", priority = 2003)]
    public static void AGXAPI()
    {
      Application.OpenURL(AGXAPIReferenceURL);
    }

    // Separator through priority

    [MenuItem("AGXUnity/About AGXUnity", priority = 2020)]
    public static void Documentation()
    {
      DocumentationWindow.Init();
    }
    #endregion
  }

  public class DocumentationWindow : EditorWindow
  {
    private static Texture2D m_logo;

    // Add menu named "My Window" to the Window menu
    public static void Init()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindowWithRect<DocumentationWindow>( new Rect( 100, 100, 400, 360 ), true, "AGX Dynamics for Unity" );
      window.Show();
    }

    private void OnGUI()
    {
      GUILayout.BeginHorizontal( GUILayout.Width( 570 ) );
      GUILayout.Box( GetOrCreateLogo(), AGXUnity.Utils.GUI.Skin.customStyles[ 3 ], GUILayout.Width( 400 ), GUILayout.Height( 100 ) );
      GUILayout.EndHorizontal();

      EditorGUILayout.SelectableLabel( "© " + System.DateTime.Now.Year + " Algoryx Simulations AB",
                                       InspectorEditor.Skin.LabelMiddleCenter );

      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

      string agxDynamicsVersion = string.Empty;
      try {
        agxDynamicsVersion = agx.agxSWIG.agxGetVersion( false );
        if ( agxDynamicsVersion.ToLower().StartsWith( "agx-" ) )
          agxDynamicsVersion = agxDynamicsVersion.Remove( 0, 4 );
        agxDynamicsVersion = GUI.AddColorTag( agxDynamicsVersion,
                                              EditorGUIUtility.isProSkin ?
                                                Color.white :
                                                Color.black );
      }
      catch ( Exception ) {
      }
      EditorGUILayout.SelectableLabel( "Thank you for using AGX Dynamics for Unity!\n\nAGX Dynamics version: " +
                                       agxDynamicsVersion,
                                       GUILayout.Height( 45 ) );

      GUILayout.Space( 10 );
      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

      GUILayout.Label( GUI.MakeLabel( "Online Documentation", true ), InspectorEditor.Skin.Label );
      if ( Link( GUI.MakeLabel( "AGX Dynamics for Unity" ) ) )
        Application.OpenURL( TopMenu.AGXDynamicsForUnityManualURL );
      GUILayout.BeginHorizontal( GUILayout.Width( 200 ) );
      if ( Link( GUI.MakeLabel( "AGX Dynamics user manual" ) ) )
        Application.OpenURL( TopMenu.AGXUserManualURL );
      GUILayout.Label( " - ", InspectorEditor.Skin.Label );
      if ( Link( GUI.MakeLabel( "AGX Dynamics API Reference" ) ) )
        Application.OpenURL( TopMenu.AGXAPIReferenceURL );
      GUILayout.EndHorizontal();

      GUILayout.Space( 10 );
      InspectorGUI.BrandSeparator();
      GUILayout.Space( 10 );

      GUILayout.Label( "Support", EditorStyles.boldLabel );
      EditorGUILayout.SelectableLabel( "Please refer to the information received when purchasing your license for support contact information.",
                                       InspectorEditor.Skin.LabelWordWrap );
    }

    private bool Link( GUIContent content )
    {
      content.text = GUI.AddColorTag( content.text, Color.Lerp( Color.blue, Color.white, 0.35f ) );
      var clicked = GUILayout.Button( content, InspectorEditor.Skin.Label );
      EditorGUIUtility.AddCursorRect( GUILayoutUtility.GetLastRect(), MouseCursor.Link );
      return clicked;
    }

    private Texture2D GetOrCreateLogo()
    {
      if ( m_logo == null )
        m_logo = EditorGUIUtility.Load( IO.Utils.AGXUnityEditorDirectory +
                                        System.IO.Path.DirectorySeparatorChar +
                                        "Data" +
                                        System.IO.Path.DirectorySeparatorChar +
                                        ( EditorGUIUtility.isProSkin ?
                                            "agx_for_unity_logo_white.png" :
                                            "agx_for_unity_logo_black.png" ) ) as Texture2D;
      return m_logo;
    }
  }
}

