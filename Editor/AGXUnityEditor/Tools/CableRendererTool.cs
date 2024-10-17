using AGXUnity.Rendering;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( CableRenderer ) )]
  public class CableRendererTool : CustomTargetTool
  {
    public CableRenderer CableRenderer
    {
      get
      {
        return Targets[ 0 ] as CableRenderer;
      }
    }

    public CableRendererTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;
      var CRTargets = GetTargets<CableRenderer>();

      bool differentModes = false;
      if ( IsMultiSelect )
        foreach ( var CR in CRTargets )
          if ( CR.RenderMode != CableRenderer.RenderMode )
            differentModes = true;

      EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.Material, CR.Material ) );
      EditorGUI.BeginChangeCheck();
      var material = (Material)EditorGUILayout.ObjectField(GUI.MakeLabel("Render Material"), CableRenderer.Material, typeof(Material), true);
      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var CR in CRTargets ) {
          EditorUtility.SetDirty( CR );
          CR.Material = material;
        }
      }

      EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.RenderMode, CR.RenderMode ) );
      EditorGUI.BeginChangeCheck();
      var renderMode = (CableRenderer.SegmentRenderMode)EditorGUILayout.EnumPopup(
                                      GUI.MakeLabel( "Render Mode" ),
                                      CableRenderer.RenderMode,
                                      InspectorEditor.Skin.Popup );
      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var CR in CRTargets ) {
          EditorUtility.SetDirty( CR );
          CR.RenderMode = renderMode;
        }
      }
      EditorGUI.showMixedValue = false;

      using ( InspectorGUI.IndentScope.Single ) {
        if ( differentModes )
          return;

        if ( CableRenderer.RenderMode == CableRenderer.SegmentRenderMode.DrawMeshInstanced ) {
          EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.ShadowCastingMode, CR.ShadowCastingMode ) );
          EditorGUI.BeginChangeCheck();
          var castShadows = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                                              GUI.MakeLabel("Shadow Casting Mode"),
                                              CableRenderer.ShadowCastingMode,
                                              InspectorEditor.Skin.Popup);
          if ( EditorGUI.EndChangeCheck() ) {
            foreach ( var CR in CRTargets ) {
              EditorUtility.SetDirty( CR );
              CR.ShadowCastingMode = castShadows;
            }
          }
          EditorGUI.showMixedValue = false;

          EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.ReceiveShadows, CR.ReceiveShadows ) );
          EditorGUI.BeginChangeCheck();
          var receiveShadows = EditorGUILayout.Toggle(GUI.MakeLabel("Receive Shadows"), CableRenderer.ReceiveShadows );
          if ( EditorGUI.EndChangeCheck() ) {
            foreach ( var CR in CRTargets ) {
              EditorUtility.SetDirty( CR );
              CR.ReceiveShadows = receiveShadows;
            }
          }
          EditorGUI.showMixedValue = false;
        }
      }
    }
  }
}
