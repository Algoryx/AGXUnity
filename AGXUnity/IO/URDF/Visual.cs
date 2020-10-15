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
  public class Visual : Pose
  {
    /// <summary>
    /// Geometry of this visual.
    /// </summary>
    public Geometry Geometry { get; private set; } = null;

    /// <summary>
    /// Material reference (name) of this visual.
    /// </summary>
    public string Material { get; private set; } = string.Empty;

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
      Material = Utils.ReadString( element.Element( "material" ), "name" );
    }

    /// <summary>
    /// Construct given optional element <paramref name="element"/>.
    /// </summary>
    /// <param name="element">Optional element "visual".</param>
    public Visual( XElement element )
    {
      Read( element );
    }
  }
}
