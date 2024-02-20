using AGXUnity.Rendering;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  /// <summary>
  /// Since AGX materials are stored in the scene (in memory) these material instances cannot be added
  /// to prefabs. This causes the prefab preview to not have the correct materials when rendering.
  /// This AssetPostprocessor adds the relevant materials to prefabs on save to avoid broken previews.
  /// </summary>
  public class PrefabMaterialPatcher : AssetPostprocessor
  {
    private void OnPostprocessPrefab( GameObject gameObject )
    {
      // Shape visual materials can break
      ReplaceDefaultShapeVisualMats( gameObject );

      // Wire and cable materials can break as well but these don't render properly in previews currently anyway
      // so ignore for now.
      // TODO: Replace wire and cable materials when their corresponding previews are fixed.
    }

    private void ReplaceDefaultShapeVisualMats(GameObject gameObject)
    {
      var svs = gameObject.GetComponentsInChildren<ShapeVisual>();

      var defaultMat = ShapeVisual.DefaultMaterial;
      defaultMat.hideFlags |= HideFlags.HideInHierarchy;

      bool addToContext = false;
      foreach ( var sv in svs ) {
        foreach ( var mat in sv.GetMaterials() ) {
          if ( mat == null || mat.name == ShapeVisual.DefaultMaterialName ) {
            addToContext = true;
            sv.ReplaceMaterial( null, defaultMat );
          }
        }
      }
      if ( addToContext )
        context.AddObjectToAsset( "Shape Visual Default Material", defaultMat );
    }
  }
}
