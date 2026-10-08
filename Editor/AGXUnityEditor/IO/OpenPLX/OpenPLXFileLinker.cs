using AGXUnity.IO.OpenPLX;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.IO.OpenPLX
{
  public class OpenPLXAssetModificationProcessor : AssetModificationProcessor
  {
    /// <summary>
    /// When OpenPLX files are moved, the imported object's reference to it's source file is no longer valid (see <see cref="AGXUnity.IO.OpenPLX.OpenPLXRoot.OpenPLXAssetPath"/>).
    /// This method intercepts move-requests and updates the imported object to match the destination path.s
    /// </summary>
    private static AssetMoveResult OnWillMoveAsset( string sourcePath, string destinationPath )
    {
      if ( !sourcePath.EndsWith( ".openplx" ) )
        return AssetMoveResult.DidNotMove;

      var openPLXRoot = AssetDatabase.LoadAssetAtPath<GameObject>( sourcePath ).GetComponent<OpenPLXRoot>();

      openPLXRoot.OpenPLXAssetPath = destinationPath;

      return AssetMoveResult.DidNotMove;
    }
  }
}
