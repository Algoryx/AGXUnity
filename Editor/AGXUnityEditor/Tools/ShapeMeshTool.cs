using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( AGXUnity.Collide.Mesh ) )]
  public class ShapeMeshTool : ShapeTool
  {
    public AGXUnity.Collide.Mesh Mesh { get { return Shape as AGXUnity.Collide.Mesh; } }

    public ShapeMeshTool( Object[] targets )
      : base( targets )
    {
      SynchronizeLocalMeshOptions();
    }

    public override void OnAdd()
    {
      base.OnAdd();
    }

    public override void OnRemove()
    {
      base.OnRemove();

      var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
      for ( int i = 0; i < NumTargets; ++i ) {
        var mesh = meshes[ i ];
        if ( mesh.PrecomputedMeshData == null )
          CacheMeshOptions( mesh, m_targetMeshOptions[ i ] );
      }

      m_targetMeshOptions = new AGXUnity.Collide.CollisionMeshOptions[] { };
    }

    public override void OnPreTargetMembersGUI()
    {
      base.OnPreTargetMembersGUI();

      var sourceObjects = Mesh.SourceObjects;
      var singleSource  = sourceObjects.FirstOrDefault();

      if ( IsMultiSelect ) {
        var undoCollection = new List<Object>();
        foreach ( var target in GetTargets<AGXUnity.Collide.Mesh>() )
          if ( target != null )
            undoCollection.AddRange( target.GetUndoCollection() );
        Undo.RecordObjects( undoCollection.ToArray(), "Mesh source" );
      }
      else
        Undo.RecordObjects( Mesh.GetUndoCollection(), "Mesh source" );

      // TODO: Display Dialog if CollisionMeshOptions should be applied
      //       to the new source.

      ShapeMeshSourceGUI( singleSource, newSource =>
      {
        if ( IsMultiSelect ) {
          foreach ( var target in GetTargets<AGXUnity.Collide.Mesh>() )
            if ( target != null )
              target.SetSourceObject( newSource );
        }
        else
          Mesh.SetSourceObject( newSource );
      } );
    }

    public override void OnPostTargetMembersGUI()
    {
      base.OnPostTargetMembersGUI();

      MeshOptionsGUI();

      MeshStatisticsGUI();
    }

    public override void OnUndoRedo()
    {
      SynchronizeLocalMeshOptions();

      foreach ( var go in Selection.gameObjects ) {
        if ( go.GetComponent<AGXUnity.Collide.Mesh>() is var mesh && mesh != null )
          mesh.OnPrecomputedCollisionMeshDataDirty();
      }
    }

    private void MeshOptionsGUI()
    {
      InspectorGUI.Separator();

      if ( InspectorGUI.Foldout( GetEditorData( Mesh ), GUI.MakeLabel( "Properties" ) ) ) {
        InspectorEditor.DrawMembersGUI( m_targetMeshOptions );
        var result = InspectorGUI.PositiveNegativeButtons( true,
                                                           "Apply",
                                                           "Apply the changes",
                                                           "Reset",
                                                           "Reset values to default." );
        if ( result == InspectorGUI.PositiveNegativeResult.Positive ) {
          var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
          using ( new Utils.UndoCollapseBlock( "Apply collision mesh data" ) ) {
            for ( int i = 0; i < meshes.Length; ++i ) {
              var mesh = meshes[ i ];
              if ( mesh.PrecomputedMeshData == null ) {
                Undo.RecordObject( mesh, "Generating mesh data" );
                mesh.PrecomputedMeshData = ScriptableObject.CreateInstance<AGXUnity.Collide.PrecomputedCollisionMeshData>();
                mesh.PrecomputedMeshData.Options = m_targetMeshOptions[ i ];
                CacheMeshOptions( mesh, null );
              }

              Undo.RecordObjects( new Object[] { mesh, mesh.PrecomputedMeshData }, "Apply" );
              mesh.PrecomputedMeshData.Apply( mesh );
            }
          }

          SynchronizeLocalMeshOptions();
        }
        else if ( result == InspectorGUI.PositiveNegativeResult.Negative ) {
          // Dialog: Are you sure?

          // Doesn't work.
          //var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
          //using ( new Utils.UndoCollapseBlock( "Reset collision mesh data" ) ) {
          //  for ( int i = 0; i < meshes.Length; ++i ) {
          //    var mesh = meshes[ i ];
          //    if ( mesh.PrecomputedMeshData == null )
          //      continue;
          //    //Undo.RecordObject( mesh, "Resetting collision mesh data" );
          //    Undo.RecordObjects( new Object[] { mesh, mesh.PrecomputedMeshData, mesh.PrecomputedMeshData.Options }, "Reset" );

          //    Undo.DestroyObjectImmediate( mesh.PrecomputedMeshData.Options );
          //    mesh.PrecomputedMeshData.Options = null;
          //    Undo.DestroyObjectImmediate( mesh.PrecomputedMeshData );

          //    mesh.PrecomputedMeshData = null;
          //    CacheMeshOptions( mesh, null );

          //    mesh.OnPrecomputedCollisionMeshDataDirty();
          //  }

          //  SynchronizeLocalMeshOptions();
          //}

          // OLD
          //Undo.RecordObjects( meshes, "Generated mesh data" );
          //for ( int i = 0; i < meshes.Length; ++i ) {
          //  var mesh = meshes[ i ];
          //  if ( mesh.PrecomputedMeshData != null ) {
          //    mesh.PrecomputedMeshData.DestroyCollisionMeshes();

          //    Undo.DestroyObjectImmediate( mesh.PrecomputedMeshData.Options );

          //    var data = mesh.PrecomputedMeshData;
          //    mesh.PrecomputedMeshData = null;
          //    Undo.DestroyObjectImmediate( data );

          //    m_targetMeshOptions[ i ] = ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>();
          //    CacheMeshOptions( mesh, m_targetMeshOptions[ i ] );

          //    mesh.OnPrecomputedCollisionMeshDataDirty();
          //  }
          //}

          //m_targetMeshOptions = ( from mesh in meshes
          //                        select mesh.PrecomputedMeshData?.Options ??
          //                               GetCachedMeshOptions( mesh ) ??
          //                               ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>() ).ToArray();
        }
      }
    }

    private void MeshStatisticsGUI()
    {
      if ( IsMultiSelect || Mesh.PrecomputedMeshData == null )
        return;

      EditorGUILayout.Space();

      InspectorGUI.BrandSeparator();

      var numCollisionMeshes = Mesh.PrecomputedMeshData.CollisionMeshes.Length;
      var totNumVertices = Mesh.PrecomputedMeshData.CollisionMeshes.Select( collisionMesh => collisionMesh.Vertices.Length ).Sum();
      var totNumTriangles = Mesh.PrecomputedMeshData.CollisionMeshes.Select( collisionMesh => collisionMesh.Indices.Length ).Sum() / 3;
      var meshPlural = numCollisionMeshes > 1 ? "es" : string.Empty;
      var summaryString = $"Summary ({numCollisionMeshes} mesh{meshPlural}, {totNumTriangles} triangles, {totNumVertices} vertices)";
      if ( InspectorGUI.Foldout( GetMeshStatisticsEditorData( Mesh ),
                                 GUI.MakeLabel( summaryString ) ) ) {
        InspectorGUI.Separator();

        EditorGUILayout.LabelField( GUI.MakeLabel( "Number of meshes" ),
                                    GUI.MakeLabel( Mesh.PrecomputedMeshData.CollisionMeshes.Length.ToString(), Color.green ),
                                    InspectorEditor.Skin.TextField );
        using ( InspectorGUI.IndentScope.Single ) {
          InspectorGUI.Separator();
          for ( int i = 0; i < Mesh.PrecomputedMeshData.CollisionMeshes.Length; ++i ) {
            var numVertices = Mesh.PrecomputedMeshData.CollisionMeshes[ i ].Vertices.Length;
            var numTriangles = Mesh.PrecomputedMeshData.CollisionMeshes[ i ].Indices.Length / 3;
            EditorGUILayout.LabelField( GUI.MakeLabel( $"[{i}] Number of triangles (vertices)" ),
                                        GUI.MakeLabel( $"{numTriangles.ToString().Color( InspectorGUISkin.BrandColorBlue )} ({numVertices.ToString()})" ),
                                        InspectorEditor.Skin.TextField );
          }
          InspectorGUI.Separator();
        }
        var totNumTrianglesString = totNumTriangles.ToString().Color( InspectorGUISkin.BrandColorBlue );
        if ( Mesh.PrecomputedMeshData.Options.Mode != AGXUnity.Collide.CollisionMeshOptions.MeshMode.Trimesh ||
             Mesh.PrecomputedMeshData.Options.ReductionEnabled ) {
          totNumTrianglesString += $" (originally: {Mesh.SourceObjects.Select( source => source.triangles.Length / 3 ).Sum().ToString().Color( Color.red )})";
        }
        EditorGUILayout.LabelField( GUI.MakeLabel( "Total number of triangles" ),
                                    GUI.MakeLabel( totNumTrianglesString ),
                                    InspectorEditor.Skin.TextField );
      }
    }

    private void SynchronizeLocalMeshOptions()
    {
      var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
      m_targetMeshOptions = ( from mesh in meshes
                              select mesh.PrecomputedMeshData?.Options ??
                                     GetCachedMeshOptions( mesh ) ??
                                     ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>() ).ToArray();
    }

    private EditorDataEntry GetMeshStatisticsEditorData( AGXUnity.Collide.Mesh mesh )
    {
      return EditorData.Instance.GetData( mesh, "ShapeMeshTool_StatisticsData", entry => entry.Bool = false );
    }

    private EditorDataEntry GetEditorData( AGXUnity.Collide.Mesh mesh )
    {
      return EditorData.Instance.GetData( mesh, "ShapeMeshTool" );
    }

    private AGXUnity.Collide.CollisionMeshOptions GetCachedMeshOptions( AGXUnity.Collide.Mesh mesh )
    {
      var cachedOptionsAsset = GetEditorData( mesh ).Asset;
      if ( cachedOptionsAsset == null )
        return null;
      return cachedOptionsAsset as AGXUnity.Collide.CollisionMeshOptions;
    }

    private void CacheMeshOptions( AGXUnity.Collide.Mesh mesh, AGXUnity.Collide.CollisionMeshOptions options )
    {
      GetEditorData( mesh ).Asset = options;
    }

    public static void ShapeMeshSourceGUI( Mesh currentSource,
                                           System.Action<Mesh> onNewMesh )
    {
      using ( new GUI.EnabledBlock( UnityEngine.GUI.enabled && !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
        var newSource = EditorGUILayout.ObjectField( GUI.MakeLabel( "Source" ),
                                                     currentSource,
                                                     typeof( Mesh ),
                                                     false ) as Mesh;
        if ( newSource != currentSource )
          onNewMesh?.Invoke( newSource );
      }
    }

    private AGXUnity.Collide.CollisionMeshOptions[] m_targetMeshOptions = null;
  }
}
