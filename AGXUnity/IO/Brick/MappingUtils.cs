using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.BrickIO
{
  public static partial class Extensions
  {
    public static agx.Vec3 ToVec3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new agx.Vec3( vec3.x() * scale, vec3.y() * scale, vec3.z() * scale );
    }

    public static agx.Vec3f ToVec3f( this Brick.Math.Vec3 vec3, float scale = 1.0f )
    {
      return new agx.Vec3f( (float)vec3.x() * scale, (float)vec3.y() * scale, (float)vec3.z() * scale );
    }

    public static agx.Quat ToQuat( this Brick.Math.Quat quat )
    {
      return new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() );
    }

    public static Vector3 ToVector3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Vector3 ToHandedVector3( this Brick.Math.Vec3 vec3, double scale = 1.0 )
    {
      return new Vector3( (float)( -vec3.x() * scale ), (float)( vec3.y() * scale ), (float)( vec3.z() * scale ) );
    }

    public static Quaternion ToHandedQuaternion( this Brick.Math.Quat quat )
    {
      return new agx.Quat( quat.x(), quat.y(), quat.z(), quat.w() ).ToHandedQuaternion();
    }

    public static Brick.Math.Vec3 ToBrickVec3( this agx.Vec3 vec3 )
    {
      return Brick.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );
    }

    public static Brick.Math.Vec3 ToBrickVec3( this Vector3 vec3 )
    {
      return Brick.Math.Vec3.from_xyz( vec3.x, vec3.y, vec3.z );
    }

    public static bool IsDefault( this Brick.Math.Vec3 vec3 )
    {
      return
           ( vec3.isDefault( "x" ) && vec3.isDefault( "y" ) && vec3.isDefault( "z" ) )
        || vec3.ToVec3().equalsZero();
    }

    public static bool IsDefault( this Brick.Math.Quat quat )
    {
      return
           ( quat.isDefault( "x" ) && quat.isDefault( "y" ) && quat.isDefault( "z" ) && quat.isDefault( "w" ) )
        || quat.ToQuat().zeroRotation();
    }
  }
  public static class Utils
  {

    public static void MapLocalTransform( Transform transform, Brick.Math.AffineTransform local_transform )
    {
      transform.localPosition = local_transform.position().ToHandedVector3();
      transform.localRotation = local_transform.rotation().ToHandedQuaternion();
    }

    public static void Report( this Brick.ErrorReporter err, Brick.Core.Object obj, AgxUnityBrickErrors errorType )
    {
      var member = obj.getOwner().getType().findFirstMember(obj.getName().Substring(obj.getName().LastIndexOf('.') + 1));
      var tok = member.isVarDeclaration() ? member.asVarDeclaration().getNameToken() : member.asVarAssignment().getTargetSegments().Last();
      var document = member.isVarDeclaration() ? member.asVarDeclaration().getOwningDocument() : member.asVarAssignment().getOwningDocument();
      string sourceID = document?.getSourceId();
      err.reportError( Brick.Error.create( (ulong)errorType, tok.line, tok.column, sourceID ?? "" ) );
    }

    public static T? ReportUnimplementedS<T>( Brick.Core.Object obj, Brick.ErrorReporter err )
      where T : struct
    {
      err.Report( obj, AgxUnityBrickErrors.Unimplemented );
      return null;
    }

    public static T ReportUnimplemented<T>( Brick.Core.Object obj, Brick.ErrorReporter err )
      where T : class
    {
      err.Report( obj, AgxUnityBrickErrors.Unimplemented );
      return null;
    }

    public static void AddChild( GameObject parent, GameObject child, Brick.ErrorReporter err, Brick.Core.Object obj )
    {
      if ( child != null )
        parent.AddChild( child );
    }

    private static HashSet<System.Type> s_rtMapped = new HashSet<System.Type>()
    {
      typeof(Brick.Physics1D.Interactions.RotationalVelocityMotor),
      typeof(Brick.DriveTrain.Gear),
      typeof(Brick.DriveTrain.CombustionEngine),
      typeof(Brick.DriveTrain.HingeActuator),
      typeof(Brick.DriveTrain.PrismaticActuator),
      typeof(Brick.DriveTrain.GearBox),
      typeof(Brick.DriveTrain.Shaft),
      typeof(Brick.DriveTrain.TorqueMotor),
      typeof(Brick.DriveTrain.Differential),
      typeof(Brick.DriveTrain.EmpiricalTorqueConverter),
    };

    public static bool IsRuntimeMapped( Brick.Core.Object obj )
    {
      return s_rtMapped.Contains( obj.GetType() );
    }
  }
}