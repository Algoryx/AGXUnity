using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;
using System.Linq;

using AGXUnity.Rendering;

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
      if (IsMultiSelect)
        foreach (var CR in CRTargets)
          if (CR.RenderMode != CableRenderer.RenderMode)
          {
            differentModes = true;
          }

      EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.Material, CR.Material ) );
      var material = (Material)EditorGUILayout.ObjectField(GUI.MakeLabel("Render Material"), CableRenderer.Material, typeof(Material), true);
      if ( UnityEngine.GUI.changed )
      {
        foreach (var CR in CRTargets)
          CR.Material = material;
        UnityEngine.GUI.changed = false;
      }
      EditorGUI.showMixedValue = false;

      EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.RenderMode, CR.RenderMode ) );
      var renderMode = (CableRenderer.SegmentRenderMode)EditorGUILayout.EnumPopup( 
                                      GUI.MakeLabel( "Render Mode" ),
                                      CableRenderer.RenderMode,
                                      InspectorEditor.Skin.Popup );
      if ( UnityEngine.GUI.changed )
      {
        foreach (var CR in CRTargets)
          CR.RenderMode = renderMode;
        UnityEngine.GUI.changed = false;
      }
      EditorGUI.showMixedValue = false;

      using ( InspectorGUI.IndentScope.Single ) 
      {
        if (differentModes)
          return;

        if (CableRenderer.RenderMode == CableRenderer.SegmentRenderMode.GameObject)
        {
        }
        else 
        {
          EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.ShadowCastingMode, CR.ShadowCastingMode ) );
          var castShadows = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                                              GUI.MakeLabel("Shadow Casting Mode"), 
                                              CableRenderer.ShadowCastingMode,
                                              InspectorEditor.Skin.Popup);
          if ( UnityEngine.GUI.changed )
          {
            foreach (var CR in CRTargets)
              CR.ShadowCastingMode = castShadows;
            UnityEngine.GUI.changed = false;
          }
          EditorGUI.showMixedValue = false;

          EditorGUI.showMixedValue = CRTargets.Any( CR => !Equals( CableRenderer.ReceiveShadows, CR.ReceiveShadows ) );
          var receiveShadows = EditorGUILayout.Toggle(GUI.MakeLabel("Receive Shadows"), CableRenderer.ReceiveShadows );
          if ( UnityEngine.GUI.changed )
          {
            foreach (var CR in CRTargets)
              CR.ReceiveShadows = receiveShadows;
            UnityEngine.GUI.changed = false;
          }
          EditorGUI.showMixedValue = false;
        }
      }
    }
  }
}
