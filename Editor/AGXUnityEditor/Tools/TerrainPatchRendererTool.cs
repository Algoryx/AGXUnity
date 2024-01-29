using AGXUnity.Model;
using AGXUnity.Rendering;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static UnityEditor.EditorGUILayout;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{

  [CustomTool( typeof( AGXUnity.Rendering.TerrainPatchRenderer ) )]
  public class TerrainPatchRendererTool : CustomTargetTool
  {
    public TerrainPatchRenderer TerrainPatchRenderer { get { return Targets[ 0 ] as TerrainPatchRenderer; } }

    public TerrainPatchRendererTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( TerrainPatchRenderer, "TerrainPatchExplicitMaterialMap" ), GUI.MakeLabel( "Explicit Material Mapping" ) ) ) {
        List<System.Tuple<DeformableTerrainMaterial,TerrainLayer>> toSet = new List<System.Tuple<DeformableTerrainMaterial, TerrainLayer>>();
        List<System.Tuple<DeformableTerrainMaterial,TerrainLayer>> toRemove = new List<System.Tuple<DeformableTerrainMaterial, TerrainLayer>>();

        foreach ( var (k, v) in TerrainPatchRenderer.ExplicitMaterialRenderMap ) {
          using ( new HorizontalScope() ) {
            using ( new VerticalScope( GUILayout.Width( 20 ) ) ) {
              GUILayout.FlexibleSpace();
              if ( InspectorGUI.Button( MiscIcon.EntryRemove, true, "Remove material mapping" ) )
                toRemove.Add( System.Tuple.Create( k, v ) );
              GUILayout.FlexibleSpace();
            }
            var news = InspectorGUI.PairObjectsField( k, v );
            if ( v != news.Item2 )
              toSet.Add( news );
            if ( news.Item1 == null )
              toRemove.Add( System.Tuple.Create( k, v ) );
            else if ( news.Item1 != k ) {
              if ( TerrainPatchRenderer.ExplicitMaterialRenderMap.ContainsKey( news.Item1 ) )
                Debug.LogWarning( $"{news.Item1}" );
              else {
                toSet.Add( news );
                toRemove.Add( System.Tuple.Create( k, v ) );
              }
            }
          }
          GUILayout.Space( 8 );
        }

        DeformableTerrainMaterial itemToAdd = null;
        GUILayout.Space( 2.0f * EditorGUIUtility.standardVerticalSpacing );
        using ( new GUILayout.HorizontalScope() ) {
          GUILayout.Space( 15.0f * EditorGUI.indentLevel );
          using ( new GUILayout.VerticalScope( InspectorGUI.FadeNormalBackground( InspectorEditor.Skin.Label, 0.1f ) ) ) {
            using ( GUI.AlignBlock.Center )
              GUILayout.Label( GUI.MakeLabel( "Add item", true ), InspectorEditor.Skin.Label );
            itemToAdd = ObjectField( null, typeof( DeformableTerrainMaterial ), true ) as DeformableTerrainMaterial;
          }
        }

        if ( itemToAdd )
          toSet.Add( System.Tuple.Create<DeformableTerrainMaterial, TerrainLayer>( itemToAdd, null ) );

        foreach ( var t in toRemove )
          TerrainPatchRenderer.ExplicitMaterialRenderMap.Remove( t.Item1 );
        foreach ( var t in toSet )
          TerrainPatchRenderer.ExplicitMaterialRenderMap[ t.Item1 ] = t.Item2;
      }
      if ( InspectorGUI.Foldout( EditorData.Instance.GetData( TerrainPatchRenderer, "TerrainPatchFinalMaterialMap" ), GUI.MakeLabel( "Final Material Mapping" ) ) ) {
        var map = TerrainPatchRenderer.MaterialRenderMap;
        using ( new HorizontalScope() ) {
          GUILayout.FlexibleSpace();
          using ( new VerticalScope() ) {
            foreach ( var mat in map.Keys )
              GUILayout.Label( mat.name );
          }
          GUILayout.FlexibleSpace();
          using ( new VerticalScope() ) {
            for ( int i = 0; i < map.Count; i++ )
              GUILayout.Label( GUI.Symbols.ArrowRight.ToString() );
          }
          GUILayout.FlexibleSpace();
          using ( new VerticalScope() ) {
            foreach ( var tex in map.Values )
              GUILayout.Label( tex != null ? tex.name : "None" );
          }
          GUILayout.FlexibleSpace();
        }
      }
    }
  }
}