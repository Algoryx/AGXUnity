using UnityEditor;
using UnityEngine;

using AGXUnity.IO.BrickIO;

namespace AGXUnityEditor.IO.BrickIO
{
  public class BrickAssetModificationProcessor : AssetModificationProcessor
  {
    private static AssetMoveResult OnWillMoveAsset( string sourcePath, string destinationPath )
    {
      if ( !sourcePath.EndsWith( ".brick" ) )
        return AssetMoveResult.DidNotMove;

      var brickRoot = AssetDatabase.LoadAssetAtPath<GameObject>( sourcePath ).GetComponent<BrickRoot>();

      brickRoot.BrickAssetPath = destinationPath;

      return AssetMoveResult.DidNotMove;
    }
  }
}