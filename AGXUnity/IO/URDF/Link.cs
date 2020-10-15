using System.Linq;
using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Fundamental element "link" defining visual en physical properties of a
  /// link in a model/robot.
  /// This element reads:
  ///   - Required attribute "name".
  ///   - Optional element "inertial" (<see cref="Inertial"/>).
  ///   - Optional element(s) "visual" - multiple supported (<see cref="Visual"/>).
  ///   - Optional element(s) "collision" - multiple supported (<see cref="Collision"/>).
  /// </summary>
  public class Link : Element
  {
    /// <summary>
    /// Inertial of this link. Null if not defined for this link.
    /// </summary>
    public Inertial Inertial { get; private set; } = null;

    /// <summary>
    /// Array of visuals, empty array if none defined for this link.
    /// </summary>
    public Visual[] Visuals { get; private set; } = new Visual[] { };

    /// <summary>
    /// Array of collisions, empty array if none defined for this link.
    /// </summary>
    public Collision[] Collisions { get; private set; } = new Collision[] { };

    /// <summary>
    /// True if this link doesn't have any child elements, otherwise false.
    /// </summary>
    public bool IsWorld { get; private set; } = true;

    /// <summary>
    /// True if "inertial" isn't defined, otherwise false.
    /// </summary>
    public bool IsStatic { get; private set; } = true;

    /// <summary>
    /// Reads element "link" with required attribute "name".
    /// </summary>
    /// <param name="element">Optional element "link".</param>
    /// <param name="optional">Unused.</param>
    public override void Read( XElement element, bool optional = true )
    {
      if ( element == null )
        return;

      base.Read( element, false );

      IsWorld = !element.HasElements;
      if ( IsWorld )
        return;

      Inertial   = Inertial.ReadOptional( element.Element( "inertial" ) );
      Visuals    = ( from visualElement in element.Elements( "visual" ) select new Visual( visualElement ) ).ToArray();
      Collisions = ( from collisionElement in element.Elements( "collision" ) select new Collision( collisionElement ) ).ToArray();
      IsStatic   = Inertial == null || Inertial.Mass == 0.0f;
    }

    /// <summary>
    /// Construct given optional element "link".
    /// </summary>
    /// <param name="element">Optional element "link".</param>
    public Link( XElement element )
    {
      Read( element );
    }
  }
}
