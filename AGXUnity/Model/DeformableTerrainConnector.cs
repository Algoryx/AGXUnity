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

    public float[,] WriteTerrainDataOffset()
    {
      Terrain.terrainData = Instantiate( Terrain.terrainData );
      if ( float.IsNaN( MaximumDepth ) ) {
        Debug.LogError( "Writing terrain offset without first setting depth!" );
        MaximumDepth = 0;
      }
      var resolution = TerrainUtils.TerrainDataResolution(Terrain.terrainData);
      InitialHeights = Terrain.terrainData.GetHeights( 0, 0, resolution, resolution );
      transform.position += MaximumDepth * Vector3.down;
      return TerrainUtils.WriteTerrainDataOffsetRaw( Terrain, MaximumDepth );
    }

    private void OnDestroy()
    {
      if ( InitialHeights != null ) {
        transform.position += MaximumDepth * Vector3.up;
        Terrain.terrainData.SetHeights( 0, 0, InitialHeights );
      }
    }
  }
}
