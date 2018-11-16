using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

namespace AGXUnityEditor
{
  public static class TopMenu
  {
    #region Shapes
    [MenuItem( "AGXUnity/Collide/Box" )]
    public static GameObject Box()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Box>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Collide/Sphere" )]
    public static GameObject Sphere()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Sphere>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Collide/Capsule" )]
    public static GameObject Capsule()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Capsule>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Collide/Cylinder" )]
    public static GameObject Cylinder()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Cylinder>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Collide/Plane" )]
    public static GameObject Plane()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Plane>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Collide/Mesh" )]
    public static GameObject Mesh()
    {
      GameObject go = Factory.Create<AGXUnity.Collide.Mesh>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "shape" );
      return Selection.activeGameObject = go;
    }
    #endregion

    #region Rigid bodies
    [MenuItem( "AGXUnity/Rigid body/Empty" )]
    public static GameObject RigidBodyEmpty()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>();
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Rigid body/Box" )]
    public static GameObject RigidBodyBox()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>( Factory.Create<AGXUnity.Collide.Box>() );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Rigid body/Sphere" )]
    public static GameObject RigidBodySphere()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>( Factory.Create<AGXUnity.Collide.Sphere>() );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Rigid body/Capsule" )]
    public static GameObject RigidBodyCapsule()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>( Factory.Create<AGXUnity.Collide.Capsule>() );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Rigid body/Cylinder" )]
    public static GameObject RigidBodyCylinder()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>( Factory.Create<AGXUnity.Collide.Cylinder>() );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
    }

    [MenuItem( "AGXUnity/Rigid body/Mesh" )]
    public static GameObject RigidBodyMesh()
    {
      GameObject go = Factory.Create<AGXUnity.RigidBody>( Factory.Create<AGXUnity.Collide.Mesh>() );
      if ( go != null )
        Undo.RegisterCreatedObjectUndo( go, "body" );
      return Selection.activeGameObject = go;
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
