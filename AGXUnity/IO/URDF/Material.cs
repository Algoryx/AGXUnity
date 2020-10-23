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
  [DoNotGenerateCustomEditor]
  public class Material : Element
  {
    /// <summary>
    /// Reads optional "material".
    /// </summary>
    /// <param name="element">Optional element "material" under "robot" or "visual".</param>
    /// <returns>Material instance if <paramref name="element"/> != null, otherwise null.</returns>
    public static Material ReadOptional( XElement element )
    {
      if ( element == null )
        return null;

      return Instantiate<Material>( element );
    }

    /// <summary>
    /// Color of this material.
    /// </summary>
    public Color Color { get { return m_color; } private set { m_color = value; } }

    /// <summary>
    /// Texture filename of this material.
    /// </summary>
    public string Texture { get { return m_texture; } private set { m_texture = value; } }

    /// <summary>
    /// True when "material" only contains the required attribute "name"
    /// and no further elements.
    /// </summary>
    public bool IsReference { get { return m_isReference; } private set { m_isReference = value; } }

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

    [SerializeField]
    private Color m_color = Color.white;

    [SerializeField]
    private string m_texture = string.Empty;

    [SerializeField]
    private bool m_isReference = false;
  }
}
