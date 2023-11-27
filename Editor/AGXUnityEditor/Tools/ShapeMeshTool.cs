using System.Linq;
using System.Collections.Generic;

using UnityEngine;
using UnityEditor;

using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  public static class CancelledAsyncCollisionMeshGenerators
  {
    public static void RegisterCancelled( AGXUnity.Collide.CollisionMeshGenerator generator )
    {
      if ( generator == null || s_cancelledGenerators.Contains( generator ) )
        return;

      if ( s_cancelledGenerators.Count == 0 )
        EditorApplication.update += OnUpdate;

      Debug.Log( $"Registering cancelled: {generator.GetHashCode()}" );
      s_cancelledGenerators.Add( generator );
    }

    private static void OnUpdate()
    {
      var generators = s_cancelledGenerators.ToArray();
      if ( generators.Length == 0 ) {
        Debug.Log( "Unregister update callback, all cancelled tasks has been removed." );
        EditorApplication.update -= OnUpdate;
        return;
      }

      foreach ( var generator in generators ) {
        if ( generator.IsRunning )
          continue;
        Debug.Log( $"Cancelled generator is done - removing {generator.GetHashCode()} from queue." );
        generator.Dispose();
        s_cancelledGenerators.Remove( generator );
      }
    }

    private static List<AGXUnity.Collide.CollisionMeshGenerator> s_cancelledGenerators = new List<AGXUnity.Collide.CollisionMeshGenerator>();
  }

  [CustomTool( typeof( AGXUnity.Collide.Mesh ) )]
  public class ShapeMeshTool : ShapeTool
  {
    public AGXUnity.Collide.Mesh Mesh { get { return Shape as AGXUnity.Collide.Mesh; } }

    public ShapeMeshTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      base.OnAdd();
    }

    public override void OnRemove()
    {
      base.OnRemove();
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
      foreach ( var go in Selection.gameObjects ) {
        var meshes = go.GetComponentsInChildren<AGXUnity.Collide.Mesh>();
        foreach ( var mesh in meshes )
          mesh.OnPrecomputedCollisionMeshDataDirty();
      }
    }

    private void MeshOptionsGUI()
    {
      InspectorGUI.Separator();

      using ( new GUI.EnabledBlock( !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
        if ( InspectorGUI.Foldout( GetEditorData( Mesh ), GUI.MakeLabel( "Options" ) ) ) {
          using ( InspectorGUI.IndentScope.Single ) {
            InspectorEditor.DrawMembersGUI( Targets, t => ( t as AGXUnity.Collide.Mesh ).Options );
            var applyResetResult = InspectorGUI.PositiveNegativeButtons( UnityEngine.GUI.enabled,
                                                                         "Apply",
                                                                         "Apply the changes",
                                                                         "Reset",
                                                                         "Delete collision meshes and reset mesh options values to default." );
            if ( applyResetResult == InspectorGUI.PositiveNegativeResult.Positive ) {
              var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
              var collisionMeshGenerator = new AGXUnity.Collide.CollisionMeshGenerator();
              var generatorStartTime = EditorApplication.timeSinceStartup;
              collisionMeshGenerator.GenerateAsync( meshes );
              var isCancelled = false;

              while ( !isCancelled && collisionMeshGenerator.IsRunning ) {
                var progressBarTitle = $"Generating collision meshes: {(int)( EditorApplication.timeSinceStartup - generatorStartTime )} s";
                var progressBarInfo = string.Empty;
                var progress = collisionMeshGenerator.Progress;
                isCancelled = EditorUtility.DisplayCancelableProgressBar( progressBarTitle, progressBarInfo, progress );
                if ( !isCancelled )
                  System.Threading.Thread.Sleep( 50 );
              }

              EditorUtility.ClearProgressBar();

              if ( isCancelled )
                CancelledAsyncCollisionMeshGenerators.RegisterCancelled( collisionMeshGenerator );
              else {
                var results = collisionMeshGenerator.CollectResults();
                using ( new Utils.UndoCollapseBlock( "Apply collision mesh data" ) ) {
                  foreach ( var result in results ) {
                    Undo.RecordObject( result.Mesh, "Collision Meshes" );
                    result.Mesh.Options = result.Options;
                    result.Mesh.PrecomputedCollisionMeshes = result.CollisionMeshes;
                  }
                }
              
                var hasPrefabAssetBeenChanged = results.Any( result =>
                                                               PrefabUtility.GetCorrespondingObjectFromOriginalSource( result.Mesh.gameObject ) == null &&
                                                               PrefabUtility.GetPrefabInstanceHandle( result.Mesh.gameObject ) == null );
                // Trying to dirty gizmos rendering of all affected prefab instances.
                // We don't have to dirty them all but it's hard to determine where
                // the instance is located in the hierarchy.
                if ( hasPrefabAssetBeenChanged ) {
                  var allMeshes = Object.FindObjectsOfType<AGXUnity.Collide.Mesh>();
                  foreach ( var m in allMeshes )
                    m.OnPrecomputedCollisionMeshDataDirty();
                }
              }

              collisionMeshGenerator = null;

              GUIUtility.ExitGUI();
            }
            else if ( applyResetResult == InspectorGUI.PositiveNegativeResult.Negative &&
                      EditorUtility.DisplayDialog( "Reset collision meshes to default",
                                                   "Destroy collision meshes and reset mesh options to default?",
                                                   "Yes", "Cancel" ) ) {
              var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
              using ( new Utils.UndoCollapseBlock( "Reset collision mesh data" ) ) {
                for ( int i = 0; i < meshes.Length; ++i ) {
                  var mesh = meshes[ i ];
                  Undo.RecordObject( mesh, "Resetting collision mesh data" );
                  mesh.DestroyCollisionMeshes();
                  if ( mesh.Options != null ) {
                    Undo.RecordObject( mesh, "Resetting mesh options to default" );
                    mesh.Options.ResetToDefault();
                  }
                }
              }
            }
          }
        }
      }
    }

    private void MeshStatisticsGUI()
    {
      if ( IsMultiSelect || Mesh.PrecomputedCollisionMeshes.Length == 0 )
        return;

      EditorGUILayout.Space();

      InspectorGUI.BrandSeparator();

      var numCollisionMeshes = Mesh.PrecomputedCollisionMeshes.Length;
      var totNumVertices = Mesh.PrecomputedCollisionMeshes.Select( collisionMesh => collisionMesh.Vertices.Length ).Sum();
      var totNumTriangles = Mesh.PrecomputedCollisionMeshes.Select( collisionMesh => collisionMesh.Indices.Length ).Sum() / 3;
      var meshPlural = numCollisionMeshes > 1 ? "es" : string.Empty;
      var summaryString = $"Summary ({numCollisionMeshes} mesh{meshPlural}, {totNumTriangles} triangles, {totNumVertices} vertices)";
      if ( InspectorGUI.Foldout( GetMeshStatisticsEditorData( Mesh ),
                                 GUI.MakeLabel( summaryString ) ) ) {
        InspectorGUI.Separator();

        EditorGUILayout.LabelField( GUI.MakeLabel( "Number of meshes" ),
                                    GUI.MakeLabel( Mesh.PrecomputedCollisionMeshes.Length.ToString(), Color.green ),
                                    InspectorEditor.Skin.TextField );
        using ( InspectorGUI.IndentScope.Single ) {
          InspectorGUI.Separator();
          for ( int i = 0; i < Mesh.PrecomputedCollisionMeshes.Length; ++i ) {
            var numVertices = Mesh.PrecomputedCollisionMeshes[ i ].Vertices.Length;
            var numTriangles = Mesh.PrecomputedCollisionMeshes[ i ].Indices.Length / 3;
            EditorGUILayout.LabelField( GUI.MakeLabel( $"[{i}] Number of triangles (vertices)" ),
                                        GUI.MakeLabel( $"{numTriangles.ToString().Color( InspectorGUISkin.BrandColorBlue )} ({numVertices.ToString()})" ),
                                        InspectorEditor.Skin.TextField );
          }
          InspectorGUI.Separator();
        }
        var totNumTrianglesString = totNumTriangles.ToString().Color( InspectorGUISkin.BrandColorBlue );
        var hasReducedNumTriangles = Mesh.Options != null &&
                                     ( Mesh.Options.Mode != AGXUnity.Collide.CollisionMeshOptions.MeshMode.Trimesh ||
                                       Mesh.Options.ReductionEnabled );
        if ( hasReducedNumTriangles ) {
          totNumTrianglesString += $" (originally: {Mesh.SourceObjects.Select( source => source.triangles.Length / 3 ).Sum().ToString().Color( Color.red )})";
        }
        EditorGUILayout.LabelField( GUI.MakeLabel( "Total number of triangles" ),
                                    GUI.MakeLabel( totNumTrianglesString ),
                                    InspectorEditor.Skin.TextField );
      }
    }

    private EditorDataEntry GetMeshStatisticsEditorData( AGXUnity.Collide.Mesh mesh )
    {
      return EditorData.Instance.GetData( mesh, "ShapeMeshTool_StatisticsData", entry => entry.Bool = false );
    }

    private EditorDataEntry GetEditorData( AGXUnity.Collide.Mesh mesh )
    {
      return EditorData.Instance.GetData( mesh, "ShapeMeshTool" );
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
  }
}
