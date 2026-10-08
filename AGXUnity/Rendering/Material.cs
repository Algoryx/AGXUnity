
using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Rendering
{
  public enum BlendMode
  {
    Opaque,
    Cutout,
    Fade,
    Transparent
  }

  public static partial class Extensions
  {
    public static void RestoreLocalDataFrom( this Material mat, agxCollide.RenderMaterial native )
    {
      if ( native == null )
        return;

      var renderPipeline = RenderingUtils.DetectPipeline();
      var metallic = 0.3f;
      if ( renderPipeline == RenderingUtils.PipelineType.HDRP ) {
        mat.shader = Shader.Find( "HDRP/Lit" );
        if ( native.hasEmissiveColor() )
          mat.SetVector( "_EmissiveColor", native.getEmissiveColor().ToColor() );

        metallic = Mathf.Pow( metallic, 2.2f );
      }
      else if ( renderPipeline == RenderingUtils.PipelineType.Universal ) {
        mat.shader = Shader.Find( "Universal Render Pipeline/Lit" );
        if ( native.hasEmissiveColor() )
          mat.SetVector( "_EmissionColor", native.getEmissiveColor().ToColor() );
      }
      else {
        if ( renderPipeline != RenderingUtils.PipelineType.BuiltIn )
          Debug.LogWarning( "Unsupported render pipeline! Imported render materials might not work." );

        mat.shader = Shader.Find( "Standard" );
        if ( native.hasEmissiveColor() )
          mat.SetVector( "_EmissionColor", native.getEmissiveColor().ToColor() );
      }

      if ( native.hasDiffuseColor() ) {
        var color = native.getDiffuseColor().ToColor();
        color.a = 1.0f - native.getTransparency();
        RenderingUtils.SetColor( mat, color );
      }
      mat.SetFloat( "_Metallic", metallic );
      RenderingUtils.SetSmoothness( mat, 0.8f );
      if ( native.getTransparency() > 0.0f )
        RenderingUtils.SetTransparencyEnabled( mat, true );
    }
  }
}
