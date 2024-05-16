using UnityEditor;
using UnityEngine;

using AGXUnity.IO.BrickIO;

namespace AGXUnityEditor.IO.BrickIO
{
  public class BrickAssetModificationProcessor : AssetModificationProcessor
  {
    /// <summary>
    /// When brick files are moved, the imported object's reference to it's source file is no longer valid (see <see cref="AGXUnity.IO.BrickIO.BrickRoot.BrickAssetPath"/>).
    /// This method intercepts move-requests and updates the imported object to match the destination path.s
    /// </summary>
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