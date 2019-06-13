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

    [MenuItem( "AGXUnity/Collide/Box" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Box" )]
    public static GameObject CreateBox( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Box>( command );
    }

    [MenuItem( "AGXUnity/Collide/Sphere" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Sphere" )]
    public static GameObject CreateSphere( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Sphere>( command );
    }

    [MenuItem( "AGXUnity/Collide/Capsule" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Capsule" )]
    public static GameObject CreateCapsule( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Capsule>( command );
    }

    [MenuItem( "AGXUnity/Collide/Cylinder" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Cylinder" )]
    public static GameObject CreateCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Cylinder>( command );
    }

    [MenuItem( "AGXUnity/Collide/Plane" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Plane" )]
    public static GameObject CreatePlane( MenuCommand command )
    {
      return Selection.activeGameObject = CreateShape<Plane>( command );
    }

    [MenuItem( "AGXUnity/Collide/Mesh" )]
    [MenuItem( "GameObject/AGX Unity/Collide/Mesh" )]
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

    [MenuItem( "AGXUnity/Rigid body/Empty" ) ]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Empty" )]
    public static GameObject CreateRigidBodyEmpty( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Box" )]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Box" )]
    public static GameObject CreateRigidBodyBox( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Box>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Sphere" )]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Sphere" )]
    public static GameObject CreateRigidBodySphere( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Sphere>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Capsule" )]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Capsule" )]
    public static GameObject CreateRigidBodyCapsule( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Capsule>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Cylinder" )]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Cylinder" )]
    public static GameObject CreateRigidBodyCylinder( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Cylinder>( command );
    }

    [MenuItem( "AGXUnity/Rigid body/Mesh" )]
    [MenuItem( "GameObject/AGX Unity/Rigid body/Mesh" )]
    public static GameObject CreateRigidBodyMesh( MenuCommand command )
    {
      return Selection.activeGameObject = CreateRigidBody<Mesh>( command );
    }
    #endregion

    #region Constraint
    [MenuItem( "AGXUnity/Constraints/Hinge" )]
    public static GameObject ConstraintHinge()
    {
      GameObject go = Factory.Create( ConstraintType.Hinge );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Prismatic" )]
    public static GameObject ConstraintPrismatic()
    {
      GameObject go = Factory.Create( ConstraintType.Prismatic );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Lock Joint" )]
    public static GameObject ConstraintLockJoint()
    {
      GameObject go = Factory.Create( ConstraintType.LockJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Cylindrical Joint" )]
    public static GameObject ConstraintCylindricalJoint()
    {
      GameObject go = Factory.Create( ConstraintType.CylindricalJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Ball Joint" )]
    public static GameObject ConstraintBallJoint()
    {
      GameObject go = Factory.Create( ConstraintType.BallJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Distance Joint" )]
    public static GameObject ConstraintDistanceJoint()
    {
      GameObject go = Factory.Create( ConstraintType.DistanceJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Angular Lock Joint" )]
    public static GameObject ConstraintAngularLockJoint()
    {
      GameObject go = Factory.Create( ConstraintType.AngularLockJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Constraints/Plane Joint" )]
    public static GameObject ConstraintPlaneJoint()
    {
      GameObject go = Factory.Create( ConstraintType.PlaneJoint );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "constraint" );
      return Selection.activeGameObject = go;
    }
    #endregion

    #region Wire
    [MenuItem( "AGXUnity/Wire/New" )]
    public static GameObject WireEmpty()
    {
      GameObject go = Factory.Create<Wire>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "wire" );
      return Selection.activeGameObject = go;
    }
    #endregion

    #region Cable
    [MenuItem( "AGXUnity/Cable/New" )]
    public static GameObject CableEmpty()
    {
      GameObject go = Factory.Create<Cable>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "cable" );

      return Selection.activeGameObject = go;
    }
    #endregion

    #region Managers
    [ MenuItem( "AGXUnity/Debug Render Manager" ) ]
    public static GameObject DebugRenderer()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<AGXUnity.Rendering.DebugRenderManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Simulation" )]
    public static GameObject Simulation()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<Simulation>().gameObject;
    }

    [MenuItem( "AGXUnity/Collision Groups Manager" )]
    public static GameObject CollisionGroupsManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<CollisionGroupsManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Contact Material Manager" )]
    public static GameObject ContactMaterialManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<ContactMaterialManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Wind and Water Manager" )]
    public static GameObject WindAndWaterManager()
    {
      return Selection.activeGameObject = GetOrCreateUniqueGameObject<WindAndWaterManager>().gameObject;
    }

    [MenuItem( "AGXUnity/Pick Handler (Game View)" )]
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

    #region Utils
    [MenuItem( "AGXUnity/Utils/Generate Custom Editors" )]
    public static void GenerateEditors()
    {
      Utils.CustomEditorGenerator.Generate();
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
  }
}
