using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  public class Collision : Pose
  {
    public Geometry Geometry { get; private set; } = null;

    public override void Read( XElement element, bool optional = true )
    {
      // 'collision' is an optional element.
      if ( element == null )
        return;

      base.Read( element, true );
      Geometry = Geometry.ReadRequired( element );
    }

    public Collision( XElement element )
    {
      Read( element );
    }
  }
}
