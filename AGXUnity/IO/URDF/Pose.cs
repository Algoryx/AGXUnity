using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "origin" defining the pose of "link", "joint", "collision" and "visual".
  /// If "origin" isn't given, "xyz" and "rpy" are both [0, 0, 0].
  /// This element reads:
  ///   - Conditionally optional "name" (required for element "link" which inherits from this class).
  ///   - Optional attributes "xyz" and "rpy", both defaults to [0, 0, 0].
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Pose : Element
  {
    /// <summary>
    /// Returns 'this', this property is a mapping to the specification,
    /// e.g., link.Origin.Xyz which is identical to link.Xyz.
    /// </summary>
    [HideInInspector]
    public Pose Origin { get { return this; } }

    /// <summary>
    /// Offset position.
    /// </summary>
    [HideInInspector] // Rendered explicitly in Element Inspector GUI.
    public Vector3 Xyz { get { return m_xyz; } private set { m_xyz = value; } }

    /// <summary>
    /// Offset rotation; roll, pitch and yaw in radians.
    /// </summary>
    [HideInInspector] // Rendered explicitly in Element Inspector GUI.
    public Vector3 Rpy { get { return m_rpy; } private set { m_rpy = value; } }

    /// <summary>
    /// Reads "name" (conditionally optional) and optional element "origin"
    /// from current <paramref name="element"/>. Note that <paramref name="element"/>
    /// is the parent element, e.g., "link", "joint", "collision" or "visual".
    /// </summary>
    /// <param name="element">Parent element with optional element "origin".</param>
    /// <param name="optional">False for required "name", otherwise false.</param>
    public override void Read( XElement element, bool optional = true )
    {
      // Reading "name" which can be required ("link").
      base.Read( element, optional );

      // "origin" is optional and we shouldn't look at the 'optional' argument.
      var origin = element?.Element( "origin" );
      if ( origin == null )
        return;

      Xyz = Utils.ReadVector3( origin, "xyz" );
      Rpy = Utils.ReadVector3( origin, "rpy" );
    }

    [SerializeField]
    private Vector3 m_xyz = Vector3.zero;

    [SerializeField]
    private Vector3 m_rpy = Vector3.zero;
  }
}
