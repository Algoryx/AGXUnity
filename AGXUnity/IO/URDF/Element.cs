using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Base URDF element with either required or optional "name" attribute.
  /// </summary>
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#urdf-import" )]
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
    [HideInInspector]
    public string Name { get { return m_name; } private set { name = m_name = value; } }

    /// <summary>
    /// Line number this element is located in the read URDF document.
    /// -1 if unknown.
    /// </summary>
    [HideInInspector]
    public int LineNumber { get { return m_lineNumber; } private set { m_lineNumber = value; } }

    /// <summary>
    /// Reads attribute "name". Throws if <paramref name="optional"/> == false
    /// and "name" isn't present.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="optional">True for optional "name" attribute, false to throw if "name" isn't present.</param>
    public virtual void Read( XElement element, bool optional = true )
    {
      name = Name = Utils.ReadString( element, "name", optional );

      var lineInfo = element as System.Xml.IXmlLineInfo;
      if ( lineInfo != null && lineInfo.HasLineInfo() )
        LineNumber = lineInfo.LineNumber;
    }

    [SerializeField]
    private string m_name = string.Empty;

    [SerializeField]
    private int m_lineNumber = -1;
  }
}
