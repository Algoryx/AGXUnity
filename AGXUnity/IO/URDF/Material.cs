using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public class Material : Element
  {
    public Color Color { get; private set; } = Color.white;
    public string Texture { get; private set; } = string.Empty;
    public bool IsReference { get; private set; } = false;

    public override void Read( XElement element, bool optional = true )
    {
      // Name of material is mandatory.
      base.Read( element, false );

      // This material is only a named reference.
      IsReference = !element.HasElements;
      if ( IsReference )
        return;

      Color   = Utils.ReadColor( element.Element( "color" ), "rgba", Color );
      Texture = Utils.ReadString( element.Element( "texture" ), "filename" );
    }

    public Material( XElement element )
    {
      Read( element );
    }
  }
}
