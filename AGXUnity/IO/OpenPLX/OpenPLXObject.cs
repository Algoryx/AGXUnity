using AGXUnity.Collide;
using AGXUnity.Model;
using AGXUnity.Sensor;
using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;
using Interactions = openplx.Physics3D.Interactions;

namespace AGXUnity.IO.OpenPLX
{
  [AddComponentMenu( "" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#openplx-import" )]
  public class OpenPLXObject : ScriptComponent
  {
    agx.Constraint GetNativeConstraint() =>
      GetComponent<Constraint>().GetInitialized().Native;

    agx.Constraint1DOF GetGeneric1DOFNative() =>
      GetComponent<Generic1DOFControlledConstraint>().GetInitialized().Native.asConstraint1DOF();

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
      return obj switch
      {
        openplx.Visuals.Geometries.Geometry => false,
        openplx.Physics.System => false,
        openplx.Physics.KinematicLock => false,
        _ => true
      };
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
      openplx.Physics.Geometries.ContactGeometry => gameObject.GetInitializedComponent<Shape>().NativeGeometry,
      Interactions.MateConnector => gameObject.GetInitializedComponent<ObserverFrame>().Native,
      openplx.Physics3D.Bodies.RigidBody => gameObject.GetInitializedComponent<RigidBody>().Native,
      openplx.Terrain.Terrain => gameObject.GetInitializedComponent<MovableTerrain>().Native,
      openplx.Sensors.SensorLogic => gameObject.GetInitializedComponent<LidarSensor>().Native,
      _ => DefaultHandling( obj )
    };

    [field: SerializeField]
    [DisableInRuntimeInspector]
    public List<string> SourceDeclarations { get; private set; } = new List<string>();
  }
}
