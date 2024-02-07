using AGXUnity.Rendering;
using AGXUnity.Utils;
using UnityEngine;
using static AGXUnityEditor.InspectorGUI;
using static UnityEditor.EditorGUILayout;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( UpsamplingParticleRenderer ) )]
  public class UpsamplingParticleRendererTool : CustomTargetTool
  {
    public UpsamplingParticleRenderer Renderer { get { return Targets[ 0 ] as UpsamplingParticleRenderer; } }

    public UpsamplingParticleRendererTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      using (new IndentScope() ) {
        if ( Renderer.RenderMode == UpsamplingParticleRenderer.ParticleRenderMode.Impostor ) {
          using ( new HorizontalScope() ) {
            PrefixLabel( "Color Range" );
            Renderer.color1 = ColorField( Renderer.color1 );
            Renderer.color2 = ColorField( Renderer.color2 );
          }
        }
        else {
          Renderer.GranuleMesh = (Mesh)ObjectField( "Granule Mesh", Renderer.GranuleMesh, typeof( Mesh ), false );
          Renderer.GranuleMaterial = (Material)ObjectField( "Granule Material", Renderer.GranuleMaterial, typeof( Material ), false );
          if ( Renderer.GranuleMaterial != null && !Renderer.GranuleMaterial.SupportsPipeline( RenderingUtils.DetectPipeline() ) )
            HelpBox( "The selected material does not support the currently active Rendering Pipeline. Rendering might be incorrect.", UnityEditor.MessageType.Warning );
        }
      }
    }
  }
}
