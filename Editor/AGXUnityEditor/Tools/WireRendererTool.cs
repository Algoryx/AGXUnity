using UnityEngine;
using UnityEditor;
using GUI = AGXUnity.Utils.GUI;
using System.Linq;

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
      var WRTargets = GetTargets<WireRenderer>();

      bool differentModes = false;
      if (IsMultiSelect)
        foreach (var WR in WRTargets)
          if (WR.RenderMode != WireRenderer.RenderMode)
          {
            differentModes = true;
          }

      EditorGUI.showMixedValue = WRTargets.Any( WR => !Equals( WireRenderer.Material, WR.Material ) );
      var material = (Material)EditorGUILayout.ObjectField(GUI.MakeLabel("Render Material"), WireRenderer.Material, typeof(Material), true);
      if ( UnityEngine.GUI.changed )
      {
        foreach (var WR in WRTargets)
          WR.Material = material;
        UnityEngine.GUI.changed = false;
      }
      EditorGUI.showMixedValue = false;

      EditorGUI.showMixedValue = WRTargets.Any( WR => !Equals( WireRenderer.RenderMode, WR.RenderMode ) );
      var renderMode = (WireRenderer.SegmentRenderMode)EditorGUILayout.EnumPopup( 
                                      GUI.MakeLabel( "Render Mode" ),
                                      WireRenderer.RenderMode,
                                      InspectorEditor.Skin.Popup );
      if ( UnityEngine.GUI.changed )
      {
        foreach (var WR in WRTargets)
          WR.RenderMode = renderMode;
        UnityEngine.GUI.changed = false;
      }
      EditorGUI.showMixedValue = false;

      using ( InspectorGUI.IndentScope.Single ) 
      {
        if (differentModes)
          return;

        if (WireRenderer.RenderMode == WireRenderer.SegmentRenderMode.GameObject)
        {
          EditorGUI.showMixedValue = WRTargets.Any( WR => !Equals( WireRenderer.NumberOfSegmentsPerMeter, WR.NumberOfSegmentsPerMeter ) );
          var nrs = EditorGUILayout.FloatField( GUI.MakeLabel("Segments per Meter"), WireRenderer.NumberOfSegmentsPerMeter );
          if ( UnityEngine.GUI.changed )
          {
            foreach (var WR in WRTargets)
              WR.NumberOfSegmentsPerMeter = nrs;
            UnityEngine.GUI.changed = false;
          }
          EditorGUI.showMixedValue = false;
        }
        else 
        {
          EditorGUI.showMixedValue = WRTargets.Any( WR => !Equals( WireRenderer.ShadowCastingMode, WR.ShadowCastingMode ) );
          var castShadows = (UnityEngine.Rendering.ShadowCastingMode)EditorGUILayout.EnumPopup(
                                              GUI.MakeLabel("Shadow Casting Mode"), 
                                              WireRenderer.ShadowCastingMode,
                                              InspectorEditor.Skin.Popup);
          if ( UnityEngine.GUI.changed )
          {
            foreach (var WR in WRTargets)
              WR.ShadowCastingMode = castShadows;
            UnityEngine.GUI.changed = false;
          }
          EditorGUI.showMixedValue = false;

          EditorGUI.showMixedValue = WRTargets.Any( WR => !Equals( WireRenderer.ReceiveShadows, WR.ReceiveShadows ) );
          var receiveShadows = EditorGUILayout.Toggle(GUI.MakeLabel("Receive Shadows"), WireRenderer.ReceiveShadows );
          if ( UnityEngine.GUI.changed )
          {
            foreach (var WR in WRTargets)
              WR.ReceiveShadows = receiveShadows;
            UnityEngine.GUI.changed = false;
          }
          EditorGUI.showMixedValue = false;
        }
      }
    }
  }
}
