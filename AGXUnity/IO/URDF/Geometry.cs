using System.Linq;
using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public class Geometry : Element
  {
    public static Geometry ReadRequired( XElement parent )
    {
      var geometryElement = parent.Element( "geometry" );
      if ( geometryElement == null )
        throw new UrdfIOException( $"{Utils.GetLineInfo( parent )}: {parent.Name} doesn't contain required 'geometry'." );
      return new Geometry( geometryElement );
    }

    public enum GeometryType { Box, Cylinder, Sphere, Mesh, Unknown }

    public Vector3 Scale
    {
      get
      {
        if ( Type != GeometryType.Mesh )
          throw new UrdfIOException( $"Asking scale of GeometryType {Type} is undefined." );
        return m_size;
      }
      set
      {
        if ( Type != GeometryType.Mesh )
          throw new UrdfIOException( $"Asking scale of GeometryType {Type} is undefined." );
        m_size = value;
      }
    }

    public string Filename
    {
      get
      {
        if ( Type != GeometryType.Mesh )
          throw new UrdfIOException( $"Asking filename of GeometryType {Type} is undefined." );
        return m_filename;
      }
      set
      {
        if ( Type != GeometryType.Mesh )
          throw new UrdfIOException( $"Asking filename of GeometryType {Type} is undefined." );
        m_filename = value;
      }
    }

    public Vector3 FullExtents
    {
      get
      {
        if ( Type != GeometryType.Box )
          throw new UrdfIOException( $"Asking extents of GeometryType {Type} is undefined." );
        return m_size;
      }
      set
      {
        if ( Type != GeometryType.Box )
          throw new UrdfIOException( $"Asking extents of GeometryType {Type} is undefined." );
        m_size = value;
      }
    }

    public float Radius
    {
      get
      {
        if ( Type != GeometryType.Cylinder && Type != GeometryType.Sphere )
          throw new UrdfIOException( $"Asking radius of GeometryType {Type} is undefined." );
        return m_size[ 0 ];
      }
      set
      {
        if ( Type != GeometryType.Cylinder && Type != GeometryType.Sphere )
          throw new UrdfIOException( $"Asking radius of GeometryType {Type} is undefined." );
        m_size[ 0 ] = value;
      }
    }

    public float Length
    {
      get
      {
        if ( Type != GeometryType.Cylinder )
          throw new UrdfIOException( $"Asking length of GeometryType {Type} is undefined." );
        return m_size[ 1 ];
      }
      set
      {
        if ( Type != GeometryType.Cylinder )
          throw new UrdfIOException( $"Asking length of GeometryType {Type} is undefined." );
        m_size[ 1 ] = value;
      }
    }

    public GeometryType Type { get; private set; } = GeometryType.Unknown;

    public override void Read( XElement element, bool optional = true )
    {
      base.Read( element, true );
      var children = element.Elements().ToArray();
      if ( children.Length != 1 )
        throw new UrdfIOException( $"{Utils.GetLineInfo( element )}: Invalid 'geometry' - expecting 1 geometry type, got {children.Length}." );
      Type = children[ 0 ].Name == "box" ?
               GeometryType.Box :
             children[ 0 ].Name == "cylinder" ?
               GeometryType.Cylinder :
             children[ 0 ].Name == "sphere" ?
               GeometryType.Sphere :
             children[ 0 ].Name == "mesh" ?
               GeometryType.Mesh :
               GeometryType.Unknown;
      if ( Type == GeometryType.Unknown )
        throw new UrdfIOException( $"{Utils.GetLineInfo( children[ 0 ] )}: Unknown geometry type '{children[ 0 ].Name}'." );
      if ( Type == GeometryType.Box )
        FullExtents = Utils.ReadVector3( children[ 0 ], "size", false );
      else if ( Type == GeometryType.Cylinder ) {
        Radius = Utils.ReadFloat( children[ 0 ], "radius", false );
        Length = Utils.ReadFloat( children[ 0 ], "length", false );
      }
      else if ( Type == GeometryType.Sphere )
        Radius = Utils.ReadFloat( children[ 0 ], "radius", false );
      else {
        Filename = Utils.ReadString( children[ 0 ], "filename", false );
        Scale    = children[ 0 ].Attribute( "scale" ) != null ?
                     Utils.ReadVector3( children[ 0 ], "scale" ) :
                     Vector3.one;
      }
    }

    public Geometry( XElement element )
    {
      Read( element );
    }

    private Vector3 m_size = Vector3.zero;
    private string m_filename = string.Empty;
  }
}
