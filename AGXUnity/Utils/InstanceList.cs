using System;
using System.Collections.Generic;
using UnityEngine;

public class InstanceList<T>
{
  private List<T[]> m_instanceGroups;

  public int Instances { get; set; } = 0;
  public int Groups => m_instanceGroups.Count;

  public T[] Group( int groupIdx ) => m_instanceGroups[ groupIdx ];
  public int NumGroupInstances( int groupIdx ) => Mathf.Min( Instances - groupIdx * 1023, 1023 );

  public InstanceList()
  {
    m_instanceGroups = new List<T[]>();
  }

  public void Clear() => Instances = 0;

  public void Add( T instance )
  {
    if ( Instances / 1023 >= m_instanceGroups.Count )
      m_instanceGroups.Add( new T[ 1023 ] );

    m_instanceGroups[ Instances / 1023 ][ Instances % 1023 ] = instance;
    Instances++;
  }

  public T this[ int index ]
  {
    get
    {
      if ( index >= Instances )
        throw new IndexOutOfRangeException( $"Trying to access element at index {index} in an InstanceList with {Instances} elements!" );
      return m_instanceGroups[ index / 1023 ][ index % 1023 ];
    }
  }

  public IEnumerable<Tuple<T[], int>> InstanceGroups
  {
    get
    {
      for ( int i = 0; i < Groups; i++ ) {
        yield return Tuple.Create( m_instanceGroups[ i ], NumGroupInstances( i ) );
      }
    }
  }
}
