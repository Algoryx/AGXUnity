using System;
using System.Xml;
using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public static class Utils
  {
    public static string GetLineInfo<T>( T xmlObject )
      where T : XObject
    {
      if ( xmlObject == null || !((IXmlLineInfo)xmlObject).HasLineInfo() )
        return "line(unknown)";
      return $"line({((IXmlLineInfo)xmlObject).LineNumber})";
    }

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

    public static Vector3 ReadVector3( XElement element,
                                       string attributeName,
                                       bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return Vector3.zero;
      var values = attribute.Value.Split( new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries );
      if ( values.Length != 3 )
        throw new UrdfIOException( $"{GetLineInfo( attribute )}: Attribute {attributeName} in {element.Name} of {element.Parent.Name} " +
                                 $"expected 3 values != read {values.Length}." );
      return new Vector3( Convert.ToSingle( values[ 0 ] ),
                          Convert.ToSingle( values[ 1 ] ),
                          Convert.ToSingle( values[ 2 ] ) );
    }

    public static float ReadFloat( XElement element,
                                   string attributeName,
                                   bool optional = true )
    {
      var attribute = GetAttribute( element, attributeName, optional );
      if ( attribute == null )
        return 0.0f;
      return (float)attribute;
    }

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
