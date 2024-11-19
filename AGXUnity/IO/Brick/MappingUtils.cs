using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static partial class Extensions
  {
    public static agx.Vec3 ToVec3( this openplx.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new agx.Vec3( vec3.x() * scale, vec3.y() * scale, vec3.z() * scale );
    }

    public static agx.Vec3f ToVec3f( this openplx.Math.Vec3 vec3, float scale = 1.0f )
    {
      return new agx.Vec3f( (float)vec3.x() * scale, (float)vec3.y() * scale, (float)vec3.z() * scale );
    }

    public static agx.Quat ToQuat( this openplx.Math.Quat quat )
    {
      return new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() );
    }

    public static Vector3 ToVector3( this openplx.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Vector3 ToHandedVector3( this openplx.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( -vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Quaternion ToHandedQuaternion( this openplx.Math.Quat quat )
    {
      return new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() ).ToHandedQuaternion();
    }

    public static openplx.Math.Vec3 ToBrickVec3( this agx.Vec3 vec3 )
    {
      return openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );
    }

    public static openplx.Math.Vec3 ToBrickVec3( this agx.Vec3f vec3 )
    {
      return openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );
    }

    public static openplx.Math.Vec3 ToBrickVec3( this Vector3 vec3 )
    {
      return openplx.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );
    }

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

    public static void Report( this openplx.ErrorReporter err, openplx.Core.Object obj, AgxUnityBrickErrors errorType )
    {
      var member = obj.getOwner().getType().findFirstMember(obj.getName().Substring(obj.getName().LastIndexOf('.') + 1));
      var tok = member.isVarDeclaration() ? member.asVarDeclaration().getNameToken() : member.asVarAssignment().getTargetSegments().Last();
      var document = member.isVarDeclaration() ? member.asVarDeclaration().getOwningDocument() : member.asVarAssignment().getOwningDocument();
      string sourceID = document?.getSourceId();
      err.reportError( openplx.Error.create( (ulong)errorType, tok.line, tok.column, sourceID ?? "" ) );
    }

    public static T? ReportUnimplementedS<T>( openplx.Core.Object obj, openplx.ErrorReporter err )
      where T : struct
    {
      err.Report( obj, AgxUnityBrickErrors.Unimplemented );
      return null;
    }

    public static T ReportUnimplemented<T>( openplx.Core.Object obj, openplx.ErrorReporter err )
      where T : class
    {
      err.Report( obj, AgxUnityBrickErrors.Unimplemented );
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
      typeof(openplx.DriveTrain.CombustionEngine),
      typeof(openplx.DriveTrain.HingeActuator),
      typeof(openplx.DriveTrain.PrismaticActuator),
      typeof(openplx.DriveTrain.GearBox),
      typeof(openplx.DriveTrain.Shaft),
      typeof(openplx.DriveTrain.TorqueMotor),
      typeof(openplx.DriveTrain.Differential),
      typeof(openplx.DriveTrain.EmpiricalTorqueConverter),
    };

    public static bool IsRuntimeMapped( openplx.Core.Object obj )
    {
      return s_rtMapped.Contains( obj.GetType() );
    }
  }
}
