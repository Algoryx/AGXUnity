using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  public class Visual : Pose
  {
    public Geometry Geometry { get; private set; } = null;
    public string Material { get; private set; } = string.Empty;

    public override void Read( XElement element, bool optional = true )
    {
      // 'visual' is an optional element.
      if ( element == null )
        return;

      base.Read( element, true );
      Geometry = Geometry.ReadRequired( element );
      Material = Utils.ReadString( element.Element( "material" ), "name" );
    }

    public Visual( XElement element )
    {
      Read( element );
    }
  }
}
