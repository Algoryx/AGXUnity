using System;
using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Fundamental element "joint" defining the relation between two links.
  /// This class is named UJoint because Unity is reserving the class name
  /// Joint in some global way.
  /// This element reads:
  ///   - Required attributes "name" and "type" where "type" is either:
  ///     - "revolute":   A hinge joint that rotates along the axis and has a limited
  ///                     range specified by the upper and lower limits.
  ///     - "continuous": A hinge without any limits.
  ///     - "prismatic":  A sliding joint that slides along the axis, and has a limited
  ///                     range specified by the upper and lower limits.
  ///     - "fixed":      This is not really a joint because it cannot move. All degrees of
  ///                     freedom are locked.
  ///     - "floating":   This joint allows motion for all 6 degrees of freedom.
  ///     - "planar":     This joint allows motion in a plane perpendicular to the axis.
  ///   - Required elements "parent" and "child".
  ///   - Optional element "axis". Default: [1, 0, 0].
  ///   - Optional element "dynamics" with optional attributes "damping" (default: 0.0) and
  ///     "friction" (default: 0.0).
  ///   - Optional element "limit" with required attributes "effort" and "velocity", and optional
  ///     attributes "lower" (default: 0.0) and "upper" (default: 0.0)
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class UJoint : Pose
  {
    /// <summary>
    /// Per specification joint types.
    /// </summary>
    public enum JointType
    {
      /// <summary>
      /// Hinge with limits.
      /// </summary>
      Revolute,
      /// <summary>
      /// Hinge without limits.
      /// </summary>
      Continuous,
      /// <summary>
      /// Sliding joint with limits.
      /// </summary>
      Prismatic,
      /// <summary>
      /// 6 DOF constrained.
      /// </summary>
      Fixed,
      /// <summary>
      /// 0 DOF constrained.
      /// </summary>
      Floating,
      /// <summary>
      /// 4 DOF constrained, allowing movement in a plane
      /// defined by Axis.
      /// </summary>
      Planar,
      /// <summary>
      /// Parse error.
      /// </summary>
      Unknown
    }

    /// <summary>
    /// Element "limit" data under "joint". This element is required for
    /// "revolute" and "prismatic".
    /// </summary>
    [Serializable]
    public struct LimitData
    {
      /// <summary>
      /// Reads data, throws exception if "limit" isn't present in <paramref name="parent"/>
      /// and <paramref name="optional"/> is false.
      /// </summary>
      /// <param name="parent">Parent element "link".</param>
      /// <param name="optional">False to throw if "limit" isn't defined in parent.</param>
      /// <returns>Limit data.</returns>
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
          Effort       = Utils.ReadFloat( element, "effort", false ),
          Lower        = Utils.ReadFloat( element, "lower" ),
          Upper        = Utils.ReadFloat( element, "upper" ),
          Velocity     = Utils.ReadFloat( element, "velocity", false ),
          Enabled      = true,
          RangeEnabled = element.Attribute( "lower" ) != null &&
                         element.Attribute( "upper" ) != null
        };
      }

      /// <summary>
      /// Maximum joint effort.
      /// </summary>
      public float Effort;

      /// <summary>
      /// Lower range.
      /// </summary>
      public float Lower;

      /// <summary>
      /// Upper range.
      /// </summary>
      public float Upper;

      /// <summary>
      /// Maximum velocity.
      /// </summary>
      public float Velocity;

      /// <summary>
      /// True when data has been read.
      /// </summary>
      public bool Enabled;

      /// <summary>
      /// True when "lower" and "upper" is specified.
      /// </summary>
      public bool RangeEnabled;
    }

    /// <summary>
    /// Constraint type.
    /// TODO URDF: Change from ConstraintType to JointType using the terminology of the specification.
    /// </summary>
    public JointType Type { get { return m_type; } private set { m_type = value; } }

    /// <summary>
    /// Parent link (name) of this joint.
    /// </summary>
    public string Parent { get { return m_parent; } private set { m_parent = value; } }

    /// <summary>
    /// Child link (name) of this joint.
    /// </summary>
    public string Child { get { return m_child; } private set { m_child = value; } }

    /// <summary>
    /// Axis of this joint.
    /// </summary>
    public Vector3 Axis { get { return m_axis; } private set { m_axis = value; } }

    /// <summary>
    /// Damping of this joint.
    /// </summary>
    public float Damping { get { return m_damping; } private set { m_damping = value; } }

    /// <summary>
    /// Friction coefficient of this joint.
    /// </summary>
    public float Friction { get { return m_friction; } private set { m_friction = value; } }

    /// <summary>
    /// Limit data of this joint.
    /// </summary>
    public LimitData Limit { get { return m_limitData; } private set { m_limitData = value; } }

    /// <summary>
    /// Reads element "joint" with required attributes "name" and "type".
    /// </summary>
    /// <param name="element">Optional element "joint".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      if ( element == null )
        return;

      base.Read( element, false );
      var type = Utils.ReadString( element, "type", false );
      Type = type == "revolute" ?
               JointType.Revolute :
             type == "continuous" ?
               JointType.Continuous :
             type == "prismatic" ?
               JointType.Prismatic :
             type == "fixed" ?
               JointType.Fixed:
             type == "planar" ?
               JointType.Planar :
               JointType.Unknown;
      if ( Type == JointType.Unknown )
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

    [SerializeField]
    private JointType m_type = JointType.Unknown;

    [SerializeField]
    private string m_parent = string.Empty;

    [SerializeField]
    private string m_child = string.Empty;

    [SerializeField]
    private Vector3 m_axis = Vector3.right;

    [SerializeField]
    private float m_damping = 0.0f;

    [SerializeField]
    private float m_friction = 0.0f;

    [SerializeField]
    private LimitData m_limitData;
  }
}
