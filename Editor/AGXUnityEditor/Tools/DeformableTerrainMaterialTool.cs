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
      var mixedPreset = Targets.Any( target => (target as DeformableTerrainMaterial).PresetName != refMaterial.PresetName );

      using ( new InspectorGUI.IndentScope( ( InspectorGUI.IndentScope.Level > 0 ? -1 : 0 ) ) ) {
        var resetButtonWidth = EditorGUIUtility.singleLineHeight;
        var rect             = EditorGUILayout.GetControlRect();
        var totalRectWidth   = rect.width;

        var availablePresets = DeformableTerrainMaterial.GetAvailablePresets().ToArray();
        var presetIndex      = FindPresetIndex( availablePresets,
                                                refMaterial.PresetName );
        var invalidPreset = presetIndex < 0;
        EditorGUI.showMixedValue = mixedPreset || invalidPreset;
        if ( invalidPreset )
          InspectorGUI.WarningLabel( $"Material preset name {refMaterial.PresetName} doesn't exist in the material presets library." );

        rect.width = EditorGUIUtility.labelWidth;
        EditorGUI.PrefixLabel( rect, GUI.MakeLabel( "Preset" ), InspectorEditor.Skin.Label );

        rect.x    += rect.width;
        rect.width = totalRectWidth - EditorGUIUtility.labelWidth - resetButtonWidth;
        EditorGUI.BeginChangeCheck();
        var newPresetIndex = EditorGUI.Popup( rect, Mathf.Max( presetIndex, 0 ), availablePresets, InspectorEditor.Skin.Popup );
        if ( EditorGUI.EndChangeCheck() && invalidPreset )
          invalidPreset = false;
        EditorGUI.showMixedValue = false;

        rect.x    += rect.width;
        rect.width = resetButtonWidth;
        var resetButtonPressed = InspectorGUI.Button( rect,
                                                      MiscIcon.ResetDefault,
                                                      !invalidPreset,
                                                      $"Reset values to default for preset: {refMaterial.PresetName}",
                                                      0.9f );

        if ( !invalidPreset &&
             newPresetIndex != presetIndex &&
             EditorUtility.DisplayDialog( "Library preset -> " + availablePresets[ newPresetIndex ],
                                          $"Change preset from {refMaterial.PresetName} to {availablePresets[ newPresetIndex ]}?\n" +
                                          "All current values will be overwritten.",
                                          "Yes", "No" ) ) {
          foreach ( var material in GetTargets<DeformableTerrainMaterial>() )
            material.SetPresetNameAndUpdateValues( availablePresets[ newPresetIndex ] );
        }

        if ( resetButtonPressed &&
             EditorUtility.DisplayDialog( "Reset values to default",
                                         $"Reset preset {refMaterial.PresetName} to default?",
                                          "Yes", "No" ) ) {
          foreach ( var material in GetTargets<DeformableTerrainMaterial>() )
            material.ResetToPresetDefault();
        }

        InspectorGUI.Separator();
      }
    }

    private static int FindPresetIndex( string[] presets, string presetName )
    {
      for ( int i = 0; i < presets.Length; ++i )
        if ( presets[ i ].ToLower() == presetName.ToLower() )
          return i;
      return -1;
    }
  }
}
