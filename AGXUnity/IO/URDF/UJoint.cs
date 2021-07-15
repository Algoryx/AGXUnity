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
    /// Optional element "calibration" data under "joint". The reference positions
    /// of the joint, used to calibrate the absolute position of the joint.
    /// </summary>
    [Serializable]
    public struct CalibrationData
    {
      /// <summary>
      /// Read "calibration" given parent. The default values of "rising" and
      /// "falling" are both 0.0. Enabled is true when the element "calibration"
      /// exists.
      /// </summary>
      /// <param name="parent">Parent element.</param>
      /// <returns>Calibration data.</returns>
      public static CalibrationData ReadOptional( XElement parent )
      {
        var element = parent?.Element( "calibration" );
        if ( element == null )
          return new CalibrationData();

        return new CalibrationData()
        {
          Rising  = Utils.ReadFloat( element, "rising" ),
          Falling = Utils.ReadFloat( element, "falling" ),
          Enabled = true
        };
      }

      /// <summary>
      /// When the joint moves in a positive direction, this reference position will trigger a rising edge.
      /// </summary>
      public float Rising;

      /// <summary>
      /// When the joint moves in a positive direction, this reference position will trigger a falling edge.
      /// </summary>
      public float Falling;

      /// <summary>
      /// True when "calibration" exists.
      /// </summary>
      public bool Enabled;
    }

    /// <summary>
    /// Optional element "dynamics" data under "joint".
    /// </summary>
    [Serializable]
    public struct DynamicsData
    {
      /// <summary>
      /// Read "dynamics" given parent. Defaults to Damping = 0.0 and Friction = 0.0
      /// if "dynamics" is null.
      /// </summary>
      /// <param name="parent">Parent element.</param>
      /// <returns>Dynamics data.</returns>
      public static DynamicsData ReadOptional( XElement parent )
      {
        var element = parent?.Element( "dynamics" );
        if ( element == null )
          return new DynamicsData();

        return new DynamicsData()
        {
          Damping  = Utils.ReadFloat( element, "damping" ),
          Friction = Utils.ReadFloat( element, "friction" ),
          Enabled  = true
        };
      }

      /// <summary>
      /// Damping of the joint.
      /// </summary>
      public float Damping;

      /// <summary>
      /// Minimum static friction force in the joint.
      /// </summary>
      public float Friction;

      /// <summary>
      /// True when "dynamics" exists.
      /// </summary>
      public bool Enabled;
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
    /// Element "mimic" data under "joint".
    /// </summary>
    [Serializable]
    public struct MimicData
    {
      /// <summary>
      /// Reads optional "mimic" under given parent. If "mimic" is given,
      /// "joint" is required and "multiplier" has default value 1.0 and
      /// "offset" 0.0.
      /// </summary>
      /// <param name="parent"></param>
      /// <returns></returns>
      public static MimicData ReadOptional( XElement parent )
      {
        var element = parent?.Element( "mimic" );
        if ( element == null )
          return new MimicData();

        return new MimicData()
        {
          Joint      = Utils.ReadString( element, "joint", false ),
          Multiplier = element.Attribute( "multiplier" ) == null ?
                         1.0f :
                         Utils.ReadFloat( element, "multiplier" ),
          Offset     = Utils.ReadFloat( element, "offset" ),
          Enabled    = true
        };
      }

      /// <summary>
      /// Joint name to mimic.
      /// </summary>
      public string Joint;

      /// <summary>
      /// Multiplicative factor.
      /// </summary>
      public float Multiplier;

      /// <summary>
      /// Joint angle offset in radians.
      /// </summary>
      public float Offset;

      /// <summary>
      /// True when "mimic" exists.
      /// </summary>
      public bool Enabled;
    }

    /// <summary>
    /// Element "safety_controller" under "joint".
    /// </summary>
    [Serializable]
    public struct SafetyControllerData
    {
      /// <summary>
      /// Reads optional "safety_controller" under given parent. If "safety_controller"
      /// is given, "k_velocity" is required and the rest of the parameters are optional
      /// and default 0.0.
      /// </summary>
      /// <param name="parent">Parent element.</param>
      /// <returns></returns>
      public static SafetyControllerData ReadOptional( XElement parent )
      {
        var element = parent?.Element( "safety_controller" );
        if ( element == null )
          return new SafetyControllerData();

        return new SafetyControllerData()
        {
          SoftLowerLimit = Utils.ReadFloat( element, "soft_lower_limit" ),
          SoftUpperLimit = Utils.ReadFloat( element, "soft_upper_limit" ),
          KPosition      = Utils.ReadFloat( element, "k_position" ),
          KVelocity      = Utils.ReadFloat( element, "k_velocity", false ),
          Enabled        = true
        };
      }

      /// <summary>
      /// Lower boundary of the joint where the safety controller starts
      /// limiting the position of the joint. Default: 0.0
      /// </summary>
      public float SoftLowerLimit;

      /// <summary>
      /// Upper boundary of the joint where the safety controller start
      /// to limiting the position of the joint. Default: 0.0
      /// </summary>
      public float SoftUpperLimit;

      /// <summary>
      /// Value specifying the relation between position and velocity limits. Default: 0.0
      /// </summary>
      public float KPosition;

      /// <summary>
      /// Value specifying the relation between effort and velocity limits. Required.
      /// </summary>
      public float KVelocity;

      /// <summary>
      /// True when "safety_controller" is given under "joint".
      /// </summary>
      public bool Enabled;
    }

    /// <summary>
    /// Joint type.
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
    /// Calibration data of this joint.
    /// </summary>
    public CalibrationData Calibration { get { return m_calibrationData; } private set { m_calibrationData = value; } }

    /// <summary>
    /// Dynamics data of this joint.
    /// </summary>
    public DynamicsData Dynamics { get { return m_dynamicsData; } private set { m_dynamicsData = value; } }

    /// <summary>
    /// Limit data of this joint.
    /// </summary>
    public LimitData Limit { get { return m_limitData; } private set { m_limitData = value; } }

    /// <summary>
    /// Mimic data of this joint.
    /// </summary>
    public MimicData Mimic { get { return m_mimicData; } private set { m_mimicData = value; } }

    /// <summary>
    /// Safety controller data of this joint.
    /// </summary>
    public SafetyControllerData SafetyController { get { return m_safetyControllerData; } private set { m_safetyControllerData = value; } }

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

      Calibration      = CalibrationData.ReadOptional( element );
      Dynamics         = DynamicsData.ReadOptional( element );
      Limit            = LimitData.Read( element, type != "revolute" && type != "prismatic" );
      Mimic            = MimicData.ReadOptional( element );
      SafetyController = SafetyControllerData.ReadOptional( element );
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
    private CalibrationData m_calibrationData;

    [SerializeField]
    private DynamicsData m_dynamicsData;

    [SerializeField]
    private LimitData m_limitData;

    [SerializeField]
    private MimicData m_mimicData;

    [SerializeField]
    private SafetyControllerData m_safetyControllerData;
  }
}
