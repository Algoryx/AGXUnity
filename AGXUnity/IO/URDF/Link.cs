using System.Linq;
using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  public class Link : Element
  {
    public Inertial Inertial { get; private set; } = null;
    public Visual[] Visuals { get; private set; } = new Visual[] { };
    public Collision[] Collisions { get; private set; } = new Collision[] { };
    public bool IsWorld { get; private set; } = true;
    public bool IsStatic { get; private set; } = true;

    public override void Read( XElement element, bool optional = true )
    {
      base.Read( element, false );

      IsWorld = !element.HasElements;
      if ( IsWorld )
        return;

      Inertial   = Inertial.ReadOptional( element.Element( "inertial" ) );
      Visuals    = ( from visualElement in element.Elements( "visual" ) select new Visual( visualElement ) ).ToArray();
      Collisions = ( from collisionElement in element.Elements( "collision" ) select new Collision( collisionElement ) ).ToArray();
      IsStatic   = Inertial == null || Inertial.Mass == 0.0f;
    }

    public Link( XElement element )
    {
      Read( element );
    }
  }
}
