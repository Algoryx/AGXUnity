using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using CT = AGXUnity.Constraint.ControllerType;
using Interactions = openplx.Physics3D.Interactions;

namespace AGXUnity.IO.OpenPLX
{
  public class OpenPLXObject : ScriptComponent
  {
    agx.ElementaryConstraint GetNativeController<T>( CT controllerType ) where T : ElementaryConstraintController
    {
      return GetComponent<Constraint>().GetInitialized().GetController<T>( controllerType ).Native;
    }

    agx.Constraint GetNativeConstraint()
    {
      return GetComponent<Constraint>().GetInitialized().Native;
    }

    agx.Constraint1DOF GetGeneric1DOFNative()
    {
      return GetComponent<Generic1DOFControlledConstraint>().GetInitialized<Generic1DOFControlledConstraint>().Native.asConstraint1DOF();
    }

    agx.Referenced DefaultHandling( openplx.Core.Object obj )
    {
      if ( NeedsNativeMapping( obj ) )
        Debug.Log( $"Failed to find native for '{obj.getName()}' ({obj.GetType().FullName})" );
      return null;
    }

    bool NeedsNativeMapping( openplx.Core.Object obj )
    {
      if ( Utils.IsRuntimeMapped( obj ) )
        return false;
      if ( obj is openplx.Visuals.Geometries.Geometry )
        return false;
      if ( obj is openplx.Physics.System )
        return false;
      return true;
    }

    internal agx.Referenced FindCorrespondingNative( OpenPLX.OpenPLXRoot root, openplx.Core.Object obj ) => obj switch
    {
      Interactions.Lock => GetNativeConstraint().asLockJoint(),
      Interactions.Hinge => GetNativeConstraint().asHinge(),
      Interactions.Prismatic => GetNativeConstraint().asPrismatic(),
      Interactions.Cylindrical => GetNativeConstraint().asCylindricalJoint(),
      Interactions.Ball => GetNativeConstraint().asBallJoint(),
      Interactions.RotationalRange => GetGeneric1DOFNative().getRange1D(),
      Interactions.TorsionSpring => GetGeneric1DOFNative().getLock1D(),
      Interactions.RotationalVelocityMotor => GetGeneric1DOFNative().getMotor1D(),
      Interactions.TorqueMotor => GetGeneric1DOFNative().getMotor1D(),
      Interactions.LinearRange => GetGeneric1DOFNative().getRange1D(),
      Interactions.LinearSpring => GetGeneric1DOFNative().getLock1D(),
      Interactions.LinearVelocityMotor => GetGeneric1DOFNative().getMotor1D(),
      Interactions.ForceMotor => GetGeneric1DOFNative().getMotor1D(),
      openplx.Physics.Charges.ContactGeometry => GetComponent<Shape>().NativeGeometry,
      openplx.Physics3D.Charges.MateConnector => GetComponent<ObserverFrame>().Native,
      openplx.Physics3D.Bodies.RigidBody => gameObject.GetInitializedComponent<RigidBody>().Native,
      openplx.Terrain.Terrain => gameObject.GetInitializedComponent<MovableTerrain>().Native,
      _ => DefaultHandling( obj )
    };

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public List<string> SourceDeclarations { get; private set; } = new List<string>();

    public static GameObject CreateGameObject( string name )
    {
      GameObject go = new GameObject( );
      RegisterGameObject( name, go );

      return go;
    }

    public static void RegisterGameObject( string name, GameObject go, bool overrideName = false )
    {
      var bo = go.GetOrCreateComponent<OpenPLXObject>();
      if ( bo.SourceDeclarations.Count == 0 || overrideName ) {
        var nameShort = name.Split('.').Last();
        go.name = nameShort;
      }
      bo.SourceDeclarations.Add( name );
    }
  }
}
