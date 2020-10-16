using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Base URDF element with either required or optional "name" attribute.
  /// </summary>
  [DoNotGenerateCustomEditor]
  public class Element : ScriptableObject
  {
    public static T Instantiate<T>()
      where T : Element
    {
      var instance = CreateInstance<T>();
      return instance;
    }

    public static T Instantiate<T>( XElement element, bool optional = true )
      where T : Element
    {
      var instance = Instantiate<T>();
      instance.Read( element, optional );
      return instance;
    }

    /// <summary>
    /// Name attribute of the element.
    /// </summary>
    public string Name { get { return m_name; } private set { m_name = value; } }

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

    [SerializeField]
    private string m_name = string.Empty;
  }
}
