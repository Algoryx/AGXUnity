using AGXUnity.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( TerrainPager ) )]
  public class TerrainPagerTool : CustomTargetTool
  {
    public TerrainPager TerrainPager { get { return Targets[ 0 ] as TerrainPager; } }

    public TerrainPagerTool( UnityEngine.Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      TerrainPager.RemoveInvalidBodies();
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      Undo.RecordObject( TerrainPager, "Recalculate parameters" );
      bool validParams = true;

      if ( !TerrainPager.ValidateParameters() ) {
        validParams = false;
        InspectorGUI.WarningLabel( "Current TileSize and TileOverlap parameters does not tile the underlying Unity Terrain" );
      }

      // This check is required due to how heightmaps are offset in AGX
      if ( !IsValidSize( TerrainPager.TileSize ) ) {
        InspectorGUI.WarningLabel( "Current only odd TileSize values are allowed" );
        validParams = false;
      }

      if ( !validParams ) {
        if ( GUILayout.Button( "Recalculate pager parameters" ) ) {
          var (overlap, size) = FindSuitableParameters( TerrainPager.TerrainDataResolution, TerrainPager.TileOverlap, TerrainPager.TileSize );
          TerrainPager.TileOverlap = overlap;
          TerrainPager.TileSize = size;
          EditorUtility.SetDirty( TerrainPager );
        }
      }

      Undo.RecordObject( TerrainPager, "Shovel add/remove." );

      InspectorGUI.ToolListGUI( this,
                                TerrainPager.Shovels,
                                "Shovels",
                                shovel => TerrainPager.Add( shovel.body, shovel.requiredRadius, shovel.preloadRadius ),
                                shovel => TerrainPager.Remove( shovel.body ),
                                null,
                                (shovel,_) => RadiiEditor(shovel)
                                );
    }

    private void RadiiEditor(PagingBody<DeformableTerrainShovel> shovel)
    {
      float requiredRadius = EditorGUILayout.FloatField( "Required radius", shovel.requiredRadius);
      float preloadRadius = EditorGUILayout.FloatField( "Preload radius", shovel.preloadRadius);

      TerrainPager.SetTileLoadRadius( shovel.body, requiredRadius, preloadRadius );
    }

    private Tuple<int, int> FindSuitableParameters( int heightmapSize, int overlap, int size )
    {
      // Performing same validity check as in ValidateParameters
      float r = (float)( heightmapSize - overlap - 1 ) / (float)( size - overlap - 1 );
      if ( IsInteger( r ) && IsValidSize( size ) )
        return Tuple.Create( overlap, size );

      int overlap_search_range = 5; // Search for overlaps in the range [overlap, overlap + range)
      r = Mathf.Round( r ); // Start search from closest integer R-value

      List<Tuple<int, int>> candidates = new();

      // Gather up to two candidates for each overlap in [overlap, overlap + range)
      // Candidates for a given overlap is created by searching first the rounded R and then by (R+1,R-1), (R+2,R-2) until candidates are found.
      // If both R+n and R-n are valid then both candidates are added
      // The size S is given by reordering the validity formula
      for ( int newOverlap = overlap; newOverlap < overlap + overlap_search_range; newOverlap++ ) {
        bool added = false;
        float newSize = ( heightmapSize - newOverlap - 1 ) / r + newOverlap + 1;
        if ( IsValidSize( newSize ) )
          candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );

        for ( int rdiff = 1; !added; rdiff++ ) {
          newSize = ( heightmapSize - newOverlap - 1 ) / ( r + rdiff ) + newOverlap + 1;
          if ( IsValidSize( newSize ) ) {
            candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );
            added = true;
          }
          if ( r - rdiff > 1 ) {
            newSize = ( heightmapSize - newOverlap - 1 ) / ( r - rdiff ) + newOverlap + 1;
            if ( IsValidSize( newSize ) ) {
              candidates.Add( Tuple.Create( newOverlap, Mathf.RoundToInt( newSize ) ) );
              added = true;
            }
          }
        }
      }

      // Select the best candidate based on some metric
      return SelectCandidate( candidates, heightmapSize, overlap, size );
    }

    private static Tuple<int, int> SelectCandidate( List<Tuple<int, int>> candidates,
                                                   int heightmapSize,
                                                   int desiredOverlap,
                                                   int desiredSize )
    {
      // Augument the list with the metric values for each candidate
      var cand = candidates
        .Select( ( c ) => Tuple.Create( c, RMetric( heightmapSize, c.Item1, c.Item2, desiredOverlap, desiredSize ) ) )
        .ToList();
      // Return the item with the lowest metric value
      cand.Sort( ( c1, c2 ) => (int)( c1.Item2 - c2.Item2 ) );
      return cand[ 0 ].Item1;
    }

    private static float RMetric( int heightmapSize, int overlap, int size, int desiredOverlap, int desiredSize )
    {
      // The R-Metric is defined as the difference in non-rounded R-value for the desired parameters and the actual R-Value of the calculated parameters
      float desiredR = ( ( heightmapSize - desiredOverlap - 1 ) / ( desiredSize - desiredOverlap - 1 ) );
      float actualR = ( ( heightmapSize - overlap - 1 ) / ( size - overlap - 1 ) );
      return Mathf.Abs( desiredR - actualR );
    }

    private static bool IsInteger( float v )
    {
      return Mathf.Approximately( v, Mathf.Round( v ) );
    }

    /// This function ensures sizes are integers and odd as is currently required by AGX
    /// If the odd requirement is lifted this function can be replaced by IsInteger
    private static bool IsValidSize( float s )
    {
      return IsInteger( s ) && s % 2 == 1;
    }
  }
}
