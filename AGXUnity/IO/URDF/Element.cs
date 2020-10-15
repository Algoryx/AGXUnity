using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  public class Element
  {
    public string Name { get; private set; } = null;

    public virtual void Read( XElement element, bool optional = true )
    {
      Name = Utils.ReadString( element, "name", optional );
    }
  }
}
