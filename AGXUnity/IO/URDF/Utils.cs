using System;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

using AGXUnity.Utils;

namespace AGXUnity.IO.URDF
{
  public static class Utils
  {
    /// <summary>
    /// Finds line number of the current XElement or XAttribute if
    /// this feature has been enabled when loading the XDocument.
    /// </summary>
    /// <typeparam name="T">XObject type.</typeparam>
    /// <param name="xmlObject">Current object.</param>
    /// <returns>A string with "line(X)" where X is the line number.</returns>
    public static string GetLineInfo<T>( T xmlObject )
      where T : XObject
    {
      if ( xmlObject == null || !((IXmlLineInfo)xmlObject).HasLineInfo() )
        return "line(unknown)";
      return $"line({((IXmlLineInfo)xmlObject).LineNumber})";
    }

    /// <summary>
    /// Finds attribute of given <paramref name="attributeName"/> in <paramref name="element"/>.
    /// If <paramref name="optional"/> == false and the attribute isn't present in
    /// <paramref name="element"/> an UrdfIOException is thrown.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="attributeName">Name of the attribute.</param>
    /// <param name="optional">False to throw an exception if <paramref name="attributeName"/> isn't
    ///                        present in <paramref name="element"/>.</param>
    /// <returns>Attribute in <paramref name="element"/> with given <paramref name="attributeName"/>.</returns>
    public static XAttribute GetAttribute( XElement element,
                                           string attributeName,
                                           bool optional )
    {
      var attribute = element?.Attribute( attributeName );
      if ( attribute == null && !optional )
        throw new UrdfIOException( $"{GetLineInfo( element )}: Required attribute '{attributeName}' is missing from " +
                                 $"'{(element == null ? "null" : element.Name)}'." );
      return attribute;
    }

    /// <summary>
    /// Reads <paramref name="attributeName"/> from <paramref name="element"/> as
    /// an UnityEngine.Color, expecting four space separated values.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="attributeName">Color attribute name.</param>
    /// <param name="defaultColor">Default value if the attribute is optional.</param>
    /// <param name="optional">False to throw an exception if <paramref name="attributeName"/> isn't
    ///                        present in <paramref name="element"/>.</param>
    /// <returns>Read color or <paramref name="defaultColor"/> if the attribute isn't present.</returns>
    public static Color ReadColor( XElement element,
                                   string attributeName,
                                   Color defaultColor,
                                   bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return defaultColor;
      var values = attribute.Value.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
      if ( values.Length != 4 )
        throw new UrdfIOException( $"{GetLineInfo( attribute )}: Attribute {attributeName} in {element.Name} of {element.Parent.Name} " +
                                 $"expected 4 values != read {values.Length}." );
      return new Color( Convert.ToSingle( values[ 0 ] ),
                        Convert.ToSingle( values[ 1 ] ),
                        Convert.ToSingle( values[ 2 ] ),
                        Convert.ToSingle( values[ 3 ] ) );
    }

    /// <summary>
    /// Reads <paramref name="attributeName"/> from <paramref name="element"/> as
    /// an UnityEngine.Vector3, expecting three space separated values.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="attributeName">Attribute name to read from <paramref name="element"/>.</param>
    /// <param name="optional">False to throw an exception if <paramref name="attributeName"/> isn't
    ///                        present in <paramref name="element"/>.</param>
    /// <returns>Read Vector3 or Vector3.zero if the attribute isn't present.</returns>
    public static Vector3 ReadVector3( XElement element,
                                       string attributeName,
                                       bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return Vector3.zero;
      var values = attribute.Value.SplitSpace();
      if ( values.Length != 3 )
        throw new UrdfIOException( $"{GetLineInfo( attribute )}: Attribute {attributeName} in {element.Name} of {element.Parent.Name} " +
                                 $"expected 3 values != read {values.Length}." );
      return new Vector3( Convert.ToSingle( values[ 0 ] ),
                          Convert.ToSingle( values[ 1 ] ),
                          Convert.ToSingle( values[ 2 ] ) );
    }

    /// <summary>
    /// Reads <paramref name="attributeName"/> from <paramref name="element"/> as a float.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="attributeName">Attribute name to read from <paramref name="element"/>.</param>
    /// <param name="optional">False to throw an exception if <paramref name="attributeName"/> isn't
    ///                        present in <paramref name="element"/>.</param>
    /// <returns>Read float or 0.0f if the attribute isn't present.</returns>
    public static float ReadFloat( XElement element,
                                   string attributeName,
                                   bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return 0.0f;
      return (float)attribute;
    }

    /// <summary>
    /// Reads <paramref name="attributeName"/> from <paramref name="element"/> as a string.
    /// </summary>
    /// <param name="element">Current element.</param>
    /// <param name="attributeName">Attribute name to read from <paramref name="element"/>.</param>
    /// <param name="optional">False to throw an exception if <paramref name="attributeName"/> isn't
    ///                        present in <paramref name="element"/>.</param>
    /// <returns>Read string or string.Empty if the attribute isn't present.</returns>
    public static string ReadString( XElement element,
                                     string attributeName,
                                     bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return string.Empty;
      return (string)attribute;
    }
  }
}
