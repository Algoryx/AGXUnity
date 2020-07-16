using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnity.Data
{
  public class Plot2D : System.IDisposable
  {
    public int NumCurves { get { return m_curves.Count; } }

    public Rect MainSurfaceRect
    {
      get
      {
        if ( m_windowData == null )
          return Rect.zero;
        const float windowTitleHeight = 16.0f;
        const float windowBorderSize  = 2.0f;

        var rect     = m_windowData.Rect;
        rect.x       = 0;
        rect.width  -= 2.0f * windowBorderSize;
        rect.x      += windowBorderSize;
        rect.y       = windowTitleHeight;
        rect.height -= ( windowTitleHeight + windowBorderSize );
        return rect;
      }
    }

    public Plot2D( string title, Vector2 size, Vector2 position )
    {
      m_windowData = GUIWindowHandler.Instance.Show( OnGUI,
                                                     size,
                                                     position,
                                                     title );
      m_windowData.Movable    = true;
      m_windowData.ExpandSize = false;

      m_material = new Material( Shader.Find( "Lines/Foo" ) );
    }

    public void Dispose()
    {
      if ( GUIWindowHandler.HasInstance )
        GUIWindowHandler.Instance.Close( OnGUI );
      m_windowData = null;
    }

    public Curve Add( string name )
    {
      var curve = new Curve();
      Add( name, curve );
      return curve;
    }

    public bool Add( string name, Curve curve )
    {
      if ( curve == null || m_curves.ContainsKey( name ) )
        return false;

      m_curves.Add( name, curve );

      return true;
    }

    public bool Remove( string name )
    {
      return m_curves.Remove( name );
    }

    private void OnGUI( EventType eventType )
    {
      const float outerBorderSize = 32.0f;
      const float axesSize        = 2.0f;

      var mainSurfaceRect = MainSurfaceRect;
      var xAxisRect = new Rect( mainSurfaceRect.x + outerBorderSize,
                                mainSurfaceRect.y + mainSurfaceRect.height - outerBorderSize,
                                mainSurfaceRect.width - 2.0f * outerBorderSize,
                                axesSize );
      var yAxisRect = new Rect( mainSurfaceRect.x + outerBorderSize,
                                mainSurfaceRect.y + outerBorderSize,
                                axesSize,
                                mainSurfaceRect.height - 2.0f * outerBorderSize );
      var axesRect = new Rect( mainSurfaceRect.x + outerBorderSize,
                               mainSurfaceRect.y + outerBorderSize,
                               mainSurfaceRect.width - 2.0f * outerBorderSize,
                               mainSurfaceRect.height - 2.0f * outerBorderSize );
      var valuesRect = Rect.zero;
      foreach ( var curve in m_curves.Values ) {
        if ( curve.NumValues > 1 ) {
          if ( valuesRect == Rect.zero )
            valuesRect = curve.ValueRect;
          else {
            valuesRect.xMin = Mathf.Min( valuesRect.xMin,
                                         curve.ValueRect.xMin );
            valuesRect.xMax = Mathf.Max( valuesRect.xMax,
                                         curve.ValueRect.xMax );
            valuesRect.yMin = Mathf.Min( valuesRect.yMin,
                                         curve.ValueRect.yMin );
            valuesRect.yMax = Mathf.Max( valuesRect.yMax,
                                         curve.ValueRect.yMax );
          }
        }
      }
      
      if ( eventType == EventType.Repaint ) {
        GL.PushMatrix();

        GL.Clear( true, false, Color.black );
        m_material.SetPass( 0 );

        // Background.
        DrawRect( mainSurfaceRect, new Color( 1, 1, 1, 0.7f ) );
        // x-axis
        DrawRect( xAxisRect, Color.black );
        // y-axis
        DrawRect( yAxisRect, Color.black );
        // Grid.
        DrawGrid( axesRect, new Color( 0.5f, 0.5f, 0.5f, 0.8f ) );
        // Curves.
        DrawCurves( axesRect, valuesRect );

        GL.PopMatrix();
      }

      if ( m_valuesTextStyle == null ) {
        m_valuesTextStyle = new GUIStyle( GUI.Skin.label );
        var fonts = Font.GetOSInstalledFontNames();
        Font valuesTextFont = null;
        foreach ( var font in fonts ) {
          if ( font == "Consolas" ) {
            valuesTextFont = Font.CreateDynamicFontFromOSFont( font, 8 );
            break;
          }
        }
        if ( valuesTextFont != null )
          m_valuesTextStyle.font = valuesTextFont;
        m_valuesTextStyle.wordWrap = false;
      }

      var yValuesRect = new Rect( yAxisRect );
      var yAxisScale = 1.0f;
      while ( yAxisScale > -0.05f ) {
        yValuesRect.x = yAxisRect.x - 0.85f * outerBorderSize;
        yValuesRect.y = yAxisRect.yMin + yAxisScale * yAxisRect.height - 5.0f;
        yValuesRect.width  = 0.85f * outerBorderSize;
        yValuesRect.height = 10.0f;
        var yValue = Utils.FromNormalizedY( ref valuesRect, 1.0f - yAxisScale );
        UnityEngine.GUI.Label( yValuesRect, GUI.MakeLabel( yValue.ToString( "F1" ), Color.black ), m_valuesTextStyle );
        yAxisScale -= 0.1f;
      }
    }

    private void DrawRect( Rect rect, Color color )
    {
      GL.Begin( GL.QUADS );
      GL.Color( color );
      GL.Vertex3( rect.xMin, rect.yMin, 0 );
      GL.Vertex3( rect.xMax, rect.yMin, 0 );
      GL.Vertex3( rect.xMax, rect.yMax, 0 );
      GL.Vertex3( rect.xMin, rect.yMax, 0 );
      GL.End();
    }

    private void DrawGrid( Rect rect, Color color )
    {
      GL.Begin( GL.LINES );
      GL.Color( new Color( 0.5f, 0.5f, 0.5f, 0.8f ) );
      var scale = 0.1f;
      while ( scale < 1.05f ) {
        GL.Vertex3( rect.xMin, rect.yMin + scale * rect.height, 0 );
        GL.Vertex3( rect.xMax, rect.yMin + scale * rect.height, 0 );

        GL.Vertex3( rect.xMin + scale * rect.width, rect.yMin, 0 );
        GL.Vertex3( rect.xMin + scale * rect.width, rect.yMax, 0 );

        scale += 0.1f;
      }
      GL.End();
    }

    private void DrawCurves( Rect rect, Rect valuesRect )
    {
      foreach ( var curve in m_curves.Values )
        curve.Draw( rect, valuesRect );
    }

    private Dictionary<string, Curve> m_curves = new Dictionary<string, Curve>();
    private Material m_material = null;
    private GUIWindowHandler.Data m_windowData = null;
    private GUIStyle m_valuesTextStyle = null;
  }
}
