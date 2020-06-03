using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Rendering
{
  public class GizmosData
  {
    public Vector3 Offset { get; set; }

    public void DrawSphere( Vector3 position, float radius, Color color, bool isWired = false )
    {
      m_shereData.Add( new SphereData()
      {
        Position = position + Offset,
        Radius = radius,
        Color = color,
        IsWired = isWired
      } );
    }

    public void DrawLine( Vector3 start, Vector3 end, Color color )
    {
      m_lineData.Add( new LineData()
      {
        Start = start + Offset,
        End = end + Offset,
        Color = color
      } );
    }

    public void Draw()
    {
      if ( m_lineData.Count == 0 && m_shereData.Count == 0 )
        return;

      var orgColor = Gizmos.color;
      foreach ( var data in m_shereData ) {
        Gizmos.color = data.Color;
        if ( data.IsWired )
          Gizmos.DrawWireSphere( data.Position, data.Radius );
        else
          Gizmos.DrawSphere( data.Position, data.Radius );
      }
      foreach ( var data in m_lineData ) {
        Gizmos.color = data.Color;
        Gizmos.DrawLine( data.Start, data.End );
      }
      Gizmos.color = orgColor;
    }

    public void Clear()
    {
      m_shereData.Clear();
      m_lineData.Clear();
    }

    private struct SphereData
    {
      public Vector3 Position;
      public float Radius;
      public bool IsWired;
      public Color Color;
    }

    private struct LineData
    {
      public Vector3 Start;
      public Vector3 End;
      public Color Color;
    }

    private List<SphereData> m_shereData = new List<SphereData>();
    private List<LineData> m_lineData = new List<LineData>();
  }
}
