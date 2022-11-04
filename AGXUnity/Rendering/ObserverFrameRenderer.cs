using System.ComponentModel;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Visualization component for the ObserverFrame Component
  /// </summary>
  [AddComponentMenu( "AGXUnity/Rendering/Observer Frame Renderer" )]
  [RequireComponent( typeof( AGXUnity.ObserverFrame ) )]
  [ExecuteInEditMode]
  public class ObserverFrameRenderer : ScriptComponent
  {
    /// <summary>
    /// The ObserverFrame can be visualized using either Unitys gizmos or using plain lines
    /// </summary>
    public enum DrawMode
    {
      Gizmos,
      Lines
    }

    [Description("Whether to use Unity's gizmos which are pickable and stripped out of builds or " +
                 "lines which are not pickable and are included in builds")]
    public DrawMode FrameDrawMode;

    [ClampAboveZeroInInspector]
    public float Size = 1.0f;

    [FloatSliderInInspector(0, 1)]
    public float Alpha = 1.0f;

    public bool RightHanded = false;

    public int LineDivisions = 1;

    private void DrawLine( Vector3 direction, Color color )
    {
      // Setup line
      if ( FrameDrawMode == DrawMode.Lines ) {
        GL.Begin( GL.LINES );
        GL.Color( new Color( color.r, color.g, color.b, Alpha ) );
      }
      else
        Gizmos.color = new Color( color.r, color.g, color.b, Alpha );

      // Draw the line with optional segments
      Vector3 pos = transform.position;
      int segments = LineDivisions * 2 - 1;
      for ( int i = 0; i < segments; i += 2 ) {
        Vector3 p1 = pos + ((float)i / segments) * Size * direction;
        Vector3 p2 = pos + ((i + 1.0f) / segments) * Size * direction;
        if ( FrameDrawMode == DrawMode.Lines ) {
          GL.Vertex3( p1.x, p1.y, p1.z );
          GL.Vertex3( p2.x, p2.y, p2.z );
        }
        else
          Gizmos.DrawLine( p1, p2 );
      }
      if ( FrameDrawMode == DrawMode.Lines )
        GL.End();
    }

    private void EnsureMaterial()
    {
      // Create material used by DrawMode.Lines if it does not exist
      if ( m_lineMaterial == null ) {
        m_lineMaterial = new( Shader.Find( "Hidden/Internal-Colored" ) )
        {
          hideFlags = HideFlags.HideAndDontSave
        };
        m_lineMaterial.SetInt( "_Cull", (int)UnityEngine.Rendering.CullMode.Off );
        m_lineMaterial.SetInt( "_ZWrite", 0 );
      }

      // Use the material
      m_lineMaterial.SetPass( 0 );
    }

    protected void OnRenderObject()
    {
      if ( FrameDrawMode == DrawMode.Lines ) {
        EnsureMaterial();
        DrawLine( transform.up, Color.green );
        DrawLine( transform.right * ( RightHanded ? -1 : 1 ), Color.red );
        DrawLine( transform.forward, Color.blue );
      }
    }

    protected void OnDrawGizmos()
    {
      if ( FrameDrawMode == DrawMode.Gizmos ) {
        DrawLine( transform.up, Color.green );
        DrawLine( transform.right * ( RightHanded ? -1 : 1 ), Color.red );
        DrawLine( transform.forward, Color.blue );
      }
    }

    private Material m_lineMaterial;
  }
}