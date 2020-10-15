using System.Xml.Linq;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Base URDF element with either required or optional "name" attribute.
  /// </summary>
  public class Element
  {
    /// <summary>
    /// Name attribute of the element.
    /// </summary>
    public string Name { get; private set; } = null;

    /// <summary>
    /// Reads attribute "name". Throws if <paramref name="optional"/> == false
    /// and "name" isn't present.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="optional">True for optional "name" attribute, false to throw if "name" isn't present.</param>
    public virtual void Read( XElement element, bool optional = true )
    {
      Name = Utils.ReadString( element, "name", optional );
    }
  }
}
