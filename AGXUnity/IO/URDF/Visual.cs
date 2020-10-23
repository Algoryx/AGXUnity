using UnityEngine;
using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "visual" defining the visual shape given "geometry".
  /// This element reads:
  ///   - Optional attribute "name".
  ///   - Optional element "origin" (<see cref="Pose"/>).
  ///   - Optional element "material" (<see cref="Material"/>).
  ///   - Required element "geometry" (<see cref="Geometry"/>).
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Visual : Pose
  {
    /// <summary>
    /// Geometry of this visual.
    /// </summary>
    public Geometry Geometry { get { return m_geometry; } private set { m_geometry = value; } }

    /// <summary>
    /// Material reference (name) of this visual.
    /// </summary>
    public Material Material { get { return m_material; } private set { m_material = value; } }

    /// <summary>
    /// Reads optional "name" and "origin" and required "geometry" if
    /// <paramref name="element"/> != null.
    /// </summary>
    /// <param name="element">Optional element "visual".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      // 'visual' is an optional element.
      if ( element == null )
        return;

      base.Read( element, true );
      Geometry = Geometry.ReadRequired( element );
      Material = Material.ReadOptional( element.Element( "material" ) );
    }

    [SerializeField]
    private Geometry m_geometry = null;
    [SerializeField]
    private Material m_material = null;
  }
}
