using System;
using System.Xml.Linq;
using UnityEngine;

namespace AGXUnity.IO.URDF
{
  /// <summary>
  /// Optional element "inertia" reading required attributes "ixx",
  /// "ixy", "ixz", "iyy", "iyz" and "izz".
  /// </summary>
  [Serializable]
  public struct Inertia
  {
    /// <summary>
    /// Default "inertia" is zero.
    /// </summary>
    public static readonly Inertia zero;

    /// <summary>
    /// Reading required element "inertia" from parent "inertial".
    /// If <paramref name="element"/> != null, required attributes
    /// "ixx", "ixy", "ixz", "iyy", "iyz" and "izz" are read.
    /// </summary>
    /// <param name="element">Required element "inertia".</param>
    /// <returns>Read "inertia" if <paramref name="element"/> != null, otherwise zero.</returns>
    public static Inertia Read( XElement element )
    {
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

    /// <summary>
    /// Implicit conversion to an agx.SPDMatrix3x3.
    /// </summary>
    public static implicit operator agx.SPDMatrix3x3( Inertia inertia )
    {
      return new agx.SPDMatrix3x3( (agx.Matrix3x3)inertia );
    }

    /// <summary>
    /// Explicit conversion to an agx.Matrix3x3.
    /// </summary>
    /// <param name="inertia"></param>
    public static explicit operator agx.Matrix3x3( Inertia inertia )
    {
      return new agx.Matrix3x3( inertia[ 0, 0 ], inertia[ 0, 1 ], inertia[ 0, 2 ],
                                inertia[ 1, 0 ], inertia[ 1, 1 ], inertia[ 1, 2 ],
                                inertia[ 2, 0 ], inertia[ 2, 1 ], inertia[ 2, 2 ] );
    }

    /// <summary>
    /// Get or set inertia element at row <paramref name="i"/> and column <paramref name="j"/>.
    /// </summary>
    /// <param name="i">Row number.</param>
    /// <param name="j">Column number.</param>
    /// <returns>Value at row <paramref name="i"/> and column <paramref name="j"/>.</returns>
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

    /// <summary>
    /// Inertia diagonal.
    /// </summary>
    public Vector3 Diagonal
    {
      get { return new Vector3( m_data1[ 0 ], m_data2[ 1 ], m_data2[ 2 ] ); }
    }

    /// <summary>
    /// Row 0, 1 or 2 of this inertia.
    /// </summary>
    /// <param name="i">Row number.</param>
    /// <returns>Row <paramref name="i"/> of this inertia.</returns>
    public Vector3 GetRow( int i )
    {
      return new Vector3( this[ i, 0 ], this[ i, 1 ], this[ i, 2 ] );
    }

    private Vector2Int GetIndex( int i, int j )
    {
      var globalIndex = i + j;
      if ( globalIndex == 2 && i == 1 )
        globalIndex = 5;
      return new Vector2Int( globalIndex % 3, globalIndex / 3 );
    }

    [SerializeField]
    private Vector3 m_data1;

    [SerializeField]
    private Vector3 m_data2;
  }
}
