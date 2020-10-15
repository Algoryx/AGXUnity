using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  public struct Inertia
  {
    public static readonly Inertia zero;

    public static Inertia Read( XElement element )
    {
      if ( element == null )
        return zero;

      float xx = (float)element.Attribute( "ixx" );
      float xy = (float)element.Attribute( "ixy" );
      float xz = (float)element.Attribute( "ixz" );
      float yy = (float)element.Attribute( "iyy" );
      float yz = (float)element.Attribute( "iyz" );
      float zz = (float)element.Attribute( "izz" );

      var inertia = new Inertia();
      inertia[ 0, 0 ] = xx;
      inertia[ 1, 1 ] = yy;
      inertia[ 2, 2 ] = zz;
      inertia[ 0, 1 ] = xy;
      inertia[ 0, 2 ] = xz;
      inertia[ 1, 2 ] = yz;

      return inertia;
    }

    public static implicit operator agx.SPDMatrix3x3( Inertia inertia )
    {
      return new agx.SPDMatrix3x3( inertia[ 0, 0 ], inertia[ 0, 1 ], inertia[ 0, 2 ],
                                   inertia[ 1, 0 ], inertia[ 1, 1 ], inertia[ 1, 2 ],
                                   inertia[ 2, 0 ], inertia[ 2, 1 ], inertia[ 2, 2 ] );
    }

    public static explicit operator agx.Matrix3x3( Inertia inertia )
    {
      return new agx.Matrix3x3( inertia[ 0, 0 ], inertia[ 0, 1 ], inertia[ 0, 2 ],
                                inertia[ 1, 0 ], inertia[ 1, 1 ], inertia[ 1, 2 ],
                                inertia[ 2, 0 ], inertia[ 2, 1 ], inertia[ 2, 2 ] );
    }

    public float this[ int i, int j ]
    {
      get
      {
        var index = GetIndex( i, j );
        return index.y == 0 ? m_data1[ index.x ] : m_data2[ index.x ];
      }
      set
      {
        var index = GetIndex( i, j );
        if ( index.y == 0 )
          m_data1[ index.x ] = value;
        else
          m_data2[ index.x ] = value;
      }
    }

    public Vector3 Diagonal
    {
      get { return new Vector3( m_data1[ 0 ], m_data2[ 1 ], m_data2[ 2 ] ); }
    }

    private Vector2Int GetIndex( int i, int j )
    {
      var globalIndex = i + j;
      if ( globalIndex == 2 && i == 1 )
        globalIndex = 5;
      return new Vector2Int( globalIndex % 3, globalIndex / 3 );
    }

    private Vector3 m_data1;
    private Vector3 m_data2;
  }

  public class Inertial : Pose
  {
    public static Inertial ReadOptional( XElement element )
    {
      if ( element == null )
        return null;

      return new Inertial( element );
    }

    public float Mass { get; private set; } = 0.0f;
    public Inertia Inertia { get; private set; } = Inertia.zero;

    public override void Read( XElement element, bool optional = true )
    {
      base.Read( element, true );

      Mass    = Utils.ReadFloat( element?.Element( "mass" ), "value" );
      Inertia = Inertia.Read( element?.Element( "inertia" ) );
    }

    public Inertial( XElement element )
    {
      Read( element );
    }
  }
}
