using AGXUnity.Utils;
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

  }
  public static class Utils
  {
    public static void mapLocalTransform( Transform transform, Brick.Physics3D.Transform local_transform )
    {
      transform.localPosition = local_transform.position().ToHandedVector3();
      transform.localRotation = local_transform.rotation().ToHandedQuaternion();
    }

    public static T ReportUnimplemented<T>(Brick.Core.Object obj, Brick.ErrorReporter err )
      where T : class
    {
      var tok = obj.getOwner().getType().getNameToken();
      err.reportError( Brick.Error.create( (int)AgxUnityBrickErrors.Unimplemented, tok.line, tok.column,obj.getType().getOwningDocument().getSourceId() ) );
      return null;
    }

    public static void AddChild( GameObject parent, GameObject child, Brick.ErrorReporter err, Brick.Core.Object obj )
    {
      if ( child != null )
        parent.AddChild( child );  
      else {
        var tok = obj.getOwner().getType().getNameToken();
        err.reportError( Brick.Error.create( (int)AgxUnityBrickErrors.NullChild, tok.line, tok.column, obj.getType().getOwningDocument().getSourceId() ) );
      }
    }

    public static bool IsRuntimeMapped(Brick.Core.Object obj )
    {
      return obj.GetType().Assembly == typeof( Brick.DriveTrain.Gear ).Assembly;
    }
  }
}