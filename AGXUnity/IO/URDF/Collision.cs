using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "collision" defining the collision shape given "geometry".
  /// This element reads:
  ///   - Optional attribute "name".
  ///   - Optional element "origin" (<see cref="Pose"/>).
  ///   - Required element "geometry" (<see cref="Geometry"/>).
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Collision : Pose
  {
    /// <summary>
    /// Geometry of this collision.
    /// </summary>
    public Geometry Geometry { get { return m_geometry; } private set { m_geometry = value; } }

    /// <summary>
    /// Reads optional "name" and "origin" and required "geometry" if
    /// <paramref name="element"/> != null.
    /// </summary>
    /// <param name="element">Optional element "collision".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      // 'collision' is an optional element.
      if ( element == null )
        return;

      base.Read( element, true );
      Geometry = Geometry.ReadRequired( element );
    }

    /// <summary>
    /// Construct given optional element <paramref name="element"/>.
    /// </summary>
    /// <param name="element">Optional element "collision".</param>
    public Collision( XElement element )
    {
      Read( element );
    }

    [SerializeField]
    private Geometry m_geometry = null;
  }
}
