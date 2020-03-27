using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Model;

using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainMaterial ))]
  public class DeformableTerrainMaterialTool : CustomTargetTool
  {
    public DeformableTerrainMaterial Material { get { return Targets[ 0 ] as DeformableTerrainMaterial; } }

    public DeformableTerrainMaterialTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var refMaterial = Material;
      var mixedPreset = Targets.Any( target => (target as DeformableTerrainMaterial).Preset != refMaterial.Preset );

      using ( new InspectorGUI.IndentScope( ( InspectorGUI.IndentScope.Level > 0 ? -1 : 0 ) ) ) {
        var resetButtonWidth = EditorGUIUtility.singleLineHeight;
        var rect             = EditorGUILayout.GetControlRect();
        rect.width          -= resetButtonWidth;

        EditorGUI.showMixedValue = mixedPreset;
        var newPreset            = (DeformableTerrainMaterial.PresetLibrary)EditorGUI.EnumPopup( rect,
                                                                                                 GUI.MakeLabel( "Preset" ),
                                                                                                 Material.Preset,
                                                                                                 InspectorEditor.Skin.Popup );
        EditorGUI.showMixedValue = false;

        rect.x                += rect.width;
        rect.width             = resetButtonWidth;
        var resetButtonPressed = InspectorGUI.Button( rect,
                                                      MiscIcon.ResetDefault,
                                                      true,
                                                      $"Reset values to default for preset: {Material.Preset}",
                                                      0.9f );

        if ( newPreset != Material.Preset &&
             EditorUtility.DisplayDialog( "Library preset -> " + newPreset.ToString(),
                                          $"Change preset from {Material.Preset} to {newPreset}?\n" +
                                          "All current values will be overwritten.",
                                          "Yes", "No" ) ) {
          foreach ( var material in GetTargets<DeformableTerrainMaterial>() )
            material.Preset = newPreset;
        }

        if ( resetButtonPressed &&
             EditorUtility.DisplayDialog( "Reset values to default",
                                         $"Reset preset {Material.Preset} to default?",
                                          "Yes", "No" ) ) {
          foreach ( var material in GetTargets<DeformableTerrainMaterial>() )
            material.ResetToPresetDefault();
        }

        InspectorGUI.Separator();
      }
    }
  }
}
