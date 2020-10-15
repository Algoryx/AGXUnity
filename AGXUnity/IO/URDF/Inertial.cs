using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "inertial" defining mass properties of a "link".
  /// This element reads:
  ///   - Optional element "origin" defining the pose of the inertial reference frame relative
  ///     to the reference frame of the "link" (<see cref="Pose"/>).
  ///   - Required element "mass" with required attribute "value". Default: 0.0.
  ///   - Required element "inertia" with required attributes "ixx", "ixy", "ixz",
  ///     "iyy", "iyz" and "izz" (<see cref="Inertia"/>). 
  /// </summary>
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

      return new Inertial( element );
    }

    /// <summary>
    /// Mass if the link if "inertial" is defined. Default: 0.0.
    /// </summary>
    public float Mass { get; private set; } = 0.0f;

    /// <summary>
    /// Inertia of the link if "inertial" is defined. Default: zeros.
    /// </summary>
    public Inertia Inertia { get; private set; } = Inertia.zero;

    /// <summary>
    /// Reads optional element "inertial".
    /// </summary>
    /// <param name="element">Optional element "inertial" under "link".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      base.Read( element, true );

      Mass    = Utils.ReadFloat( element?.Element( "mass" ), "value" );
      Inertia = Inertia.Read( element?.Element( "inertia" ) );
    }

    /// <summary>
    /// Construct given optional element "inertial".
    /// </summary>
    /// <param name="element">Optional element "inertial" under "link".</param>
    public Inertial( XElement element )
    {
      Read( element );
    }
  }
}
