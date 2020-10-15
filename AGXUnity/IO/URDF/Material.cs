using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "material" defining color or texture of one or many "visual" elements.
  /// "material" can be a reference, i.e, only containing the required attribute "name".
  /// The element reads:
  ///   - Required attribute "name".
  ///   - Optional element "color" with required attribute "rgba".
  ///   - Optional element "texture" with required attribute "filename".
  /// </summary>
  public class Material : Element
  {
    /// <summary>
    /// Color of this material.
    /// </summary>
    public Color Color { get; private set; } = Color.white;

    /// <summary>
    /// Texture filename of this material.
    /// </summary>
    public string Texture { get; private set; } = string.Empty;

    /// <summary>
    /// True when "material" only contains the required attribute "name"
    /// and no further elements.
    /// </summary>
    public bool IsReference { get; private set; } = false;

    /// <summary>
    /// Reads required attribute "name" and optional elements "color" and/or "texture".
    /// </summary>
    /// <param name="element">Optional element "material".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      if ( element == null )
        return;

      // Name of material is mandatory.
      base.Read( element, false );

      // This material is only a named reference.
      IsReference = !element.HasElements;
      if ( IsReference )
        return;

      Color   = Utils.ReadColor( element.Element( "color" ), "rgba", Color );
      Texture = Utils.ReadString( element.Element( "texture" ), "filename" );
    }

    /// <summary>
    /// Construct given optional element "material".
    /// </summary>
    /// <param name="element">Optional element "material".</param>
    public Material( XElement element )
    {
      Read( element );
    }
  }
}
