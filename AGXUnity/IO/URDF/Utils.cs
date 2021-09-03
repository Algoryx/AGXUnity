using System;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using System.Collections.Generic;
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
    /// Returns "line(<paramref name="lineNumber"/>)" when <paramref name="lineNumber"/> is
    /// larger than zero, otherwise "line(unknown)".
    /// </summary>
    /// <param name="lineNumber">Line number.</param>
    /// <returns>Line number info string.</returns>
    public static string GetLineInfo( int lineNumber )
    {
      return $"line({(lineNumber <= 0 ? "unknown" : lineNumber.ToString())})";
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

    public struct ColladaInfo
    {
      public enum Axis
      {
        X,
        Y,
        Z,
        Unknown
      }

      public Axis UpAxis;

      public bool IsDefault => UpAxis == Axis.Z;
    }

    /// <summary>
    /// Parsing a small part of the Collada file, searching for the
    /// "asset" element, containing information about the up axis.
    /// Default up axis is Z.
    /// </summary>
    /// <remarks>
    /// Scale is handled by the Collada importer in Unity.
    /// </remarks>
    /// <param name="colladaFilename">Collada filename including path.</param>
    /// <returns>Collada info parsed.</returns>
    public static ColladaInfo ParseColladaInfo( string colladaFilename )
    {
      var colladaInfo = new ColladaInfo()
      {
        UpAxis = ColladaInfo.Axis.Z
      };

      try {
        var lines = ParseColladaAssetElement( colladaFilename );
        if ( lines != null ) {
          var doc = XDocument.Parse( string.Join( "\n", lines ) );
          var assetElement = doc?.Element( "asset" );
          var upAxisStr = assetElement?.Element( "up_axis" )?.Value;
          if ( !string.IsNullOrEmpty( upAxisStr ) )
            colladaInfo.UpAxis = upAxisStr == "Z_UP" ?
                                    ColladaInfo.Axis.Z :
                                  upAxisStr == "Y_UP" ?
                                    ColladaInfo.Axis.Y :
                                  upAxisStr == "X_UP" ?
                                    ColladaInfo.Axis.X :
                                    ColladaInfo.Axis.Z; // Defaults to z if something is wrong.
        }
      }
      catch {
      }

      return colladaInfo;
    }

    public static string[] ParseColladaAssetElement( string colladaFilename, int maxNumLines = 50 )
    {
      List<string> assetElement = null;

      try {
        using ( var stream = System.IO.File.OpenText( colladaFilename ) ) {
          var line = string.Empty;
          var lineNumber = 0;
          while ( ++lineNumber <= maxNumLines && (line = stream.ReadLine()) != null ) {
            if ( assetElement == null && line.TrimStart().StartsWith( "<asset>" ) ) {
              assetElement = new List<string>();
              assetElement.Add( line );
            }
            else if ( assetElement != null && line.TrimStart().StartsWith( "</asset>" ) ) {
              assetElement.Add( line );
              break;
            }
            else if ( assetElement != null )
              assetElement.Add( line );
          }
        }
      }
      catch {
        assetElement?.Clear();
      }

      return assetElement?.ToArray();
    }

    public static T GetElement<T>( GameObject gameObject )
      where T : ScriptableObject
    {
      if ( gameObject == null )
        return null;
      var components = gameObject.GetComponents<ElementComponent>();
      foreach ( var component in components )
        if ( component.Element is T element )
          return element;
      return null;
    }

    public static T GetElementInChildren<T>( GameObject gameObject )
      where T : ScriptableObject
    {
      if ( gameObject == null )
        return null;
      var components = gameObject.GetComponentsInChildren<ElementComponent>();
      foreach ( var component in components )
        if ( component.Element is T element )
          return element;
      return null;
    }

    public static T[] GetElementsInChildren<T>( GameObject gameObject )
      where T : ScriptableObject
    {
      if ( gameObject == null )
        return new T[] { };

      var components = gameObject.GetComponentsInChildren<ElementComponent>();
      return ( from component in components
               where component.Element is T
               select component.Element as T ).ToArray();
    }

    public static GameObject FindGameObjectWithElement( GameObject rootGameObject, Element element )
    {
      if ( rootGameObject == null )
        return null;
      var components = rootGameObject.GetComponentsInChildren<ElementComponent>();
      foreach ( var component in components )
        if ( component.Element == element )
          return component.gameObject;
      return null;
    }
  }
}
