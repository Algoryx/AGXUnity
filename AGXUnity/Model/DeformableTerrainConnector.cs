using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity.Model
{
  [AddComponentMenu( "" )]
  [HideInInspector]
  public class DeformableTerrainConnector : MonoBehaviour
  {
    public float[,] InitialHeights { get; private set; } = null;
    public float MaximumDepth { get; set; } = float.NaN;

    public Terrain Terrain { get => GetComponent<Terrain>(); }

    public Vector3 GetOffsetPosition()
    {
      if ( InitialHeights == null )
        return transform.position + MaximumDepth * Vector3.down;
      else
        return transform.position;
    }

    public float[,] WriteTerrainDataOffset( bool needsReturnData = true )
    {
      var resolution = TerrainUtils.TerrainDataResolution(Terrain.terrainData);
      if ( InitialHeights != null )
        return needsReturnData ? Terrain.terrainData.GetHeights( 0, 0, resolution, resolution ) : null;

      if ( float.IsNaN( MaximumDepth ) ) {
        Debug.LogError( "Writing terrain offset without first setting depth!" );
        MaximumDepth = 0;
      }
      InitialHeights = Terrain.terrainData.GetHeights( 0, 0, resolution, resolution );
      transform.position += MaximumDepth * Vector3.down;
      return TerrainUtils.WriteTerrainDataOffsetRaw( Terrain, MaximumDepth );
    }

    internal void OnReset()
    {
      if ( InitialHeights != null ) {
        transform.position += MaximumDepth * Vector3.up;
        Terrain.terrainData.SetHeights( 0, 0, InitialHeights );

#if UNITY_EDITOR
        // If the editor is closed during play the modified height
        // data isn't saved, this resolves corrupt heights in such case.
        UnityEditor.EditorUtility.SetDirty( Terrain.terrainData );
        UnityEditor.AssetDatabase.SaveAssets();
#endif
        InitialHeights = null;
      }
    }

    private void OnDestroy()
    {
      OnReset();
    }
  }
}
