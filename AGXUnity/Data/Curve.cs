using UnityEngine;

namespace AGXUnity.Data
{
  public class Curve
  {
    public Color Color { get; set; } = Color.black;

    public int NumValues { get { return m_x.Length; } }

    public Rect ValueRect
    {
      get
      {
        SynchronizeValueRect();
        return m_valueRect;
      }
    }

    public void Add( float x, float y )
    {
      m_x.Add( x );
      m_y.Add( y );
    }

    public void Add( Vector2 v )
    {
      Add( v.x, v.y );
    }

    public Vector2 ToGUI( ref Rect guiRect, ref Rect valuesRect, int index )
    {
      // Mirror y-axis when (0, 0) is upper left in GUI.
      return new Vector2( Utils.FromNormalizedX( ref guiRect,
                                                 Utils.ToNormalizedX( ref valuesRect,
                                                                      m_x[ index ] ) ),
                          Utils.FromNormalizedY( ref guiRect,
                                                 1.0f - Utils.ToNormalizedY( ref valuesRect,
                                                                             m_y[ index ] ) ) );
    }

    public void Draw( Rect rect, Rect valuesRect )
    {
      if ( m_x.Length < 2 )
        return;

      GL.Begin( GL.LINE_STRIP );
      GL.Color( Color );

      for ( int i = 0; i < m_x.Length; ++i ) {
        var guiCoord = ToGUI( ref rect, ref valuesRect, i );
        GL.Vertex3( guiCoord.x, guiCoord.y, 0 );
      }

      GL.End();
    }

    private void SynchronizeValueRect()
    {
      if ( m_x.IsDirty || m_y.IsDirty ) {
        m_valueRect = Rect.MinMaxRect( m_x.Min,
                                       m_y.Min,
                                       m_x.Max,
                                       m_y.Max );
        m_x.ResetDirty();
        m_y.ResetDirty();
      }
    }

    private Rect m_valueRect = Rect.zero;
    private Serie m_x = new Serie();
    private Serie m_y = new Serie();
  }
}
