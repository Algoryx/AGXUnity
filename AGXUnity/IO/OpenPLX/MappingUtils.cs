using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public static partial class Extensions
  {
    public static agx.Vec3 ToVec3( this openplx.Math.Vec3 vec3, double scale = 1.0 ) =>
      new agx.Vec3( vec3.x() * scale, vec3.y() * scale, vec3.z() * scale );

    public static agx.Vec3f ToVec3f( this openplx.Math.Vec3 vec3, float scale = 1.0f ) =>
      new agx.Vec3f( (float)vec3.x() * scale, (float)vec3.y() * scale, (float)vec3.z() * scale );

    public static agx.Vec2 ToVec2( this openplx.Math.Vec2 vec2, double scale = 1.0 ) =>
      new agx.Vec2( vec2.x() * scale, vec2.y() * scale );

    public static agx.Vec2f ToVec2f( this openplx.Math.Vec2 vec2, float scale = 1.0f ) =>
      new agx.Vec2f( (float)vec2.x() * scale, (float)vec2.y() * scale );

    public static agx.Quat ToQuat( this openplx.Math.Quat quat ) =>
      new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() );

    public static Vector3 ToVector3( this openplx.Math.Vec3 vec3, double scale = 1.0 ) =>
      new Vector3( (float)( vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );

    public static Vector2 ToVector2( this openplx.Math.Vec2 vec2, double scale = 1.0 ) =>
      new Vector2( (float)( vec2.x() * scale ), (float)( vec2.y() * scale ) );

    public static Vector3 ToHandedVector3( this openplx.Math.Vec3 vec3, double scale = 1.0 ) =>
      new Vector3( (float)( -vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );

    public static Quaternion ToHandedQuaternion( this openplx.Math.Quat quat ) =>
      new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() ).ToHandedQuaternion();

    public static openplx.Math.Vec3 ToOpenPLXVec3( this agx.Vec3 vec3 ) =>
      openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );

    public static openplx.Math.Vec3 ToOpenPLXVec3( this agx.Vec3f vec3 ) =>
      openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );

    public static openplx.Math.Vec3 ToOpenPLXVec3( this Vector3 vec3 ) =>
      openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );

    public static openplx.Math.Vec2 ToOpenPLXVec2( this agx.Vec2 vec2 ) =>
      openplx.Math.Vec2.from_xy( vec2.x, vec2.y );

    public static openplx.Math.Vec2 ToOpenPLXVec2( this agx.Vec2f vec2 ) =>
      openplx.Math.Vec2.from_xy( vec2.x, vec2.y );

    public static openplx.Math.Vec2 ToOpenPLXVec2( this Vector2 vec2 ) =>
      openplx.Math.Vec2.from_xy( vec2.x, vec2.y );

    public static bool IsDefault( this openplx.Math.Vec3 vec3 )
    {
      return
           ( vec3.isDefault( "x" ) && vec3.isDefault( "y" ) && vec3.isDefault( "z" ) )
        || vec3.ToVec3().equalsZero();
    }

    public static bool IsDefault( this openplx.Math.Quat quat )
    {
      return
           ( quat.isDefault( "x" ) && quat.isDefault( "y" ) && quat.isDefault( "z" ) && quat.isDefault( "w" ) )
        || quat.ToQuat().zeroRotation();
    }
  }
  public static class Utils
  {

    public static void MapLocalTransform( Transform transform, openplx.Math.AffineTransform local_transform )
    {
      transform.localPosition = local_transform.position().ToHandedVector3();
      transform.localRotation = local_transform.rotation().ToHandedQuaternion();
    }

    public static T? ReportUnimplementedS<T>( openplx.Core.Object obj, openplx.ErrorReporter err )
      where T : struct
    {
      err.reportError( new UnimplementedError( obj ) );
      return null;
    }

    public static T ReportUnimplemented<T>( openplx.Core.Object obj, openplx.ErrorReporter err )
      where T : class
    {
      err.reportError( new UnimplementedError( obj ) );
      return null;
    }

    public static void AddChild( GameObject parent, GameObject child, openplx.ErrorReporter err, openplx.Core.Object obj )
    {
      if ( child != null )
        parent.AddChild( child );
    }

    private static HashSet<System.Type> s_rtMapped = new HashSet<System.Type>()
    {
      typeof(openplx.Physics1D.Interactions.RotationalVelocityMotor),
      typeof(openplx.DriveTrain.Gear),
      typeof(openplx.DriveTrain.FlexibleGear),
      typeof(openplx.DriveTrain.MeanValueEngine),
      typeof(openplx.DriveTrain.HingeActuator),
      typeof(openplx.DriveTrain.PrismaticActuator),
      typeof(openplx.DriveTrain.GearBox),
      typeof(openplx.DriveTrain.Shaft),
      typeof(openplx.DriveTrain.TorqueMotor),
      typeof(openplx.DriveTrain.Differential),
      typeof(openplx.DriveTrain.EmpiricalTorqueConverter),
      typeof(openplx.DriveTrain.ManualBrake),
      typeof(openplx.DriveTrain.AutomaticBrake),
      typeof(openplx.DriveTrain.ManualClutch),
      typeof(openplx.DriveTrain.AutomaticClutch),
    };

    public static bool IsRuntimeMapped( openplx.Core.Object obj )
    {
      return s_rtMapped.Contains( obj.GetType() );
    }
  }
}
