using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;

using AGXUnity.Rendering;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( WireRenderer ) )]
  public class WireRendererTool : CustomTargetTool
  {
    public WireRenderer WireRenderer
    {
      get
      {
        return Targets[ 0 ] as WireRenderer;
      }
    }

    public WireRendererTool( Object[] targets )
      : base( targets )
    {
    }
    
    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      WireRenderer.Material = (Material)EditorGUILayout.ObjectField(GUI.MakeLabel("Render Material"), WireRenderer.Material, typeof(Material), true);
      WireRenderer.RenderMode = (WireRenderer.SegmentRenderMode)EditorGUILayout.EnumPopup( 
                                      GUI.MakeLabel( "Render Mode" ),
                                      WireRenderer.RenderMode,
                                      InspectorEditor.Skin.Popup );
      using ( InspectorGUI.IndentScope.Single ) {
        if (WireRenderer.RenderMode == WireRenderer.SegmentRenderMode.GameObject){
          WireRenderer.NumberOfSegmentsPerMeter = EditorGUILayout.FloatField( GUI.MakeLabel("Segments per Meter"), WireRenderer.NumberOfSegmentsPerMeter );
        }
        else {
          WireRenderer.ShadowCastingMode = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                                              GUI.MakeLabel("Shadow Casting Mode"), 
                                              WireRenderer.ShadowCastingMode,
                                              InspectorEditor.Skin.Popup);
          WireRenderer.ReceiveShadows = EditorGUILayout.Toggle(GUI.MakeLabel("Receive Shadows"), WireRenderer.ReceiveShadows );
        }
      }
    }
  }
}
