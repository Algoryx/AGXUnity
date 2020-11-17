using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "inertial" defining mass properties of a "link".
  /// This element reads:
  ///   - Optional element "origin" defining the pose of the inertial reference frame relative
  ///     to the reference frame of the "link" (<see cref="Pose"/>).
  ///   - Required element "mass" with required attribute "value".
  ///   - Required element "inertia" with required attributes "ixx", "ixy", "ixz",
  ///     "iyy", "iyz" and "izz" (<see cref="Inertia"/>). 
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Inertial : Pose
  {
    /// <summary>
    /// Reads optional "inertial".
    /// </summary>
    /// <param name="element">Optional element "inertial" under a "link".</param>
    /// <returns>Inertial instance if <paramref name="element"/> != null, otherwise null.</returns>
    public static Inertial ReadOptional( XElement element )
    {
      if ( element == null )
        return null;

      return Instantiate<Inertial>( element );
    }

    /// <summary>
    /// Mass if the link if "inertial" is defined. Default: 0.0.
    /// </summary>
    public float Mass { get { return m_mass; } private set { m_mass = value; } }

    /// <summary>
    /// Inertia of the link if "inertial" is defined. Default: zeros.
    /// </summary>
    public Inertia Inertia { get { return m_inerta; } private set { m_inerta = value; } }

    /// <summary>
    /// Reads optional element "inertial".
    /// </summary>
    /// <param name="element">Optional element "inertial" under "link".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      // <origin> is optional.
      base.Read( element, true );

      if ( element == null )
        return;

      // <mass> and <inertia> is required if <inertial> exists.
      Mass = Utils.ReadFloat( element.Element( "mass" ), "value", false );
      var inertiaElement = element.Element( "inertia" );
      if ( inertiaElement == null )
        throw new UrdfIOException( $"{Utils.GetLineInfo( element )}: Required element 'inertia' is missing from <inertial>." );
      Inertia = Inertia.Read( inertiaElement );
    }

    [SerializeField]
    private float m_mass = 0.0f;

    [SerializeField]
    private Inertia m_inerta = Inertia.zero;
  }
}
