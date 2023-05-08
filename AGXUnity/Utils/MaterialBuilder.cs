using AGXUnity.Rendering;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class MaterialBuilder
  {
    Material m_Material;
    RenderPipelineDetector.PipelineType m_Pipeline;

    public MaterialBuilder( string shader = "" )
    {
      m_Pipeline = RenderPipelineDetector.DetectPipeline();
      if ( shader == "" ) {
        if ( m_Pipeline == RenderPipelineDetector.PipelineType.BuiltIn )
          shader = "Standard";
        else if ( m_Pipeline == RenderPipelineDetector.PipelineType.HDRP )
          shader = "HDRP/Lit";
        else if ( m_Pipeline == RenderPipelineDetector.PipelineType.Universal )
          shader = "Universal Render Pipeline/Lit";
        else
          Debug.LogError( "Unsupported render Pipeline" );
      }

      m_Material = new Material( Shader.Find( shader ) );
    }

    public MaterialBuilder Color( Color c )
    {
      if ( m_Pipeline == RenderPipelineDetector.PipelineType.BuiltIn )
        m_Material.SetVector( "_Color", c );
      else
        m_Material.SetVector( "_BaseColor", c );

      m_Material.SetBlendMode( c.a < 1.0f ? BlendMode.Transparent : BlendMode.Opaque );

      return this;
    }

    public MaterialBuilder Metallic( float metallic )
    {
      m_Material.SetFloat( "_Metallic", metallic );
      return this;
    }

    public MaterialBuilder Smoothness( float smooth )
    {
      if ( m_Pipeline == RenderPipelineDetector.PipelineType.BuiltIn )
        m_Material.SetFloat( "_Smoothness", smooth );
      else
        m_Material.SetFloat( "_Glossiness", smooth );
      return this;
    }

    public Material Build()
    {
      return m_Material;
    }
  }
}