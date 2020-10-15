using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public class UJoint : Pose
  {
    public struct LimitData
    {
      public static LimitData Read( XElement parent, bool optional )
      {
        var element = parent.Element( "limit" );
        if ( element == null ) {
          if ( optional )
            return new LimitData();
          else
            throw new UrdfIOException( $"{Utils.GetLineInfo( parent )}: {parent.Name} doesn't contain required 'limit'." );
        }

        return new LimitData()
        {
          Effort   = Utils.ReadFloat( element, "effort", false ),
          Lower    = Utils.ReadFloat( element, "lower", false ),
          Upper    = Utils.ReadFloat( element, "upper", false ),
          Velocity = Utils.ReadFloat( element, "velocity", false ),
          Enabled  = true
        };
      }

      public float Effort;
      public float Lower;
      public float Upper;
      public float Velocity;
      public bool Enabled;
    }

    public ConstraintType Type { get; private set; } = ConstraintType.Unknown;
    public string Parent { get; private set; } = string.Empty;
    public string Child { get; private set; } = string.Empty;
    public Vector3 Axis { get; private set; } = Vector3.right;
    public float Damping { get; private set; } = 0.0f;
    public float Friction { get; private set; } = 0.0f;
    public LimitData Limit { get; private set; }

    public override void Read( XElement element, bool optional = true )
    {
      if ( element == null )
        return;

      base.Read( element, false );
      var type = Utils.ReadString( element, "type", false );
      Type = type == "revolute" || type == "continuous" ?
               ConstraintType.Hinge :
             type == "prismatic" ?
               ConstraintType.Prismatic :
             type == "fixed" ?
               ConstraintType.LockJoint :
             // Unsure if PlaneJoint is the same as "planar" - from the specification:
             //    "This joint allows motion in a plane perpendicular to the axis."
             // PlaneJoint is only 1D and I suspect "planar" is 4D.
             type == "planar" ?
               ConstraintType.PlaneJoint :
               ConstraintType.Unknown;
      if ( Type == ConstraintType.Unknown )
        throw new UrdfIOException( $"{Utils.GetLineInfo( element )}: Unknown joint type '{type}'." );

      Parent = Utils.ReadString( element.Element( "parent" ), "link", false );
      Child  = Utils.ReadString( element.Element( "child" ), "link", false );

      if ( element.Element( "axis" ) != null )
        Axis = Utils.ReadVector3( element.Element( "axis" ), "xyz", false );

      if ( element.Element( "dynamics" ) != null ) {
        Damping  = Utils.ReadFloat( element.Element( "dynamics" ), "damping" );
        Friction = Utils.ReadFloat( element.Element( "dynamics" ), "friction" );
      }

      Limit = LimitData.Read( element, type != "revolute" && type != "prismatic" );
    }

    public UJoint( XElement element )
    {
      Read( element );
    }
  }
}
