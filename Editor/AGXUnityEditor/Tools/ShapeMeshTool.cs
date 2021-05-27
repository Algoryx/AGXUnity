using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
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
      m_targetMeshOptions = ( from mesh in GetTargets<AGXUnity.Collide.Mesh>()
                              select mesh.PrecomputedMeshData?.Options ??
                                     GetCachedMeshOptions( mesh ) ??
                                     ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>() ).ToArray();
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
    }

    public void DoIt()
    {
      var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
      m_targetMeshOptions = ( from mesh in meshes
                              select mesh.PrecomputedMeshData?.Options ??
                                     GetCachedMeshOptions( mesh ) ??
                                     ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>() ).ToArray();
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
          for ( int i = 0; i < NumTargets; ++i ) {
            var mesh = meshes[ i ];
            if ( mesh.PrecomputedMeshData == null ) {
              mesh.PrecomputedMeshData = ScriptableObject.CreateInstance<AGXUnity.Collide.PrecomputedCollisionMeshData>();
              mesh.PrecomputedMeshData.Options = m_targetMeshOptions[ i ];
              CacheMeshOptions( mesh, null );
              Undo.RegisterCreatedObjectUndo( mesh.PrecomputedMeshData, "Pre-computed mesh data" );
            }

            Debug.Assert( mesh.PrecomputedMeshData.Options != null, "Options expected to be assigned." );
            Debug.Assert( mesh.PrecomputedMeshData.Options == m_targetMeshOptions[ i ], "Options mismatch." );

            Undo.RecordObject( mesh.PrecomputedMeshData, "Applying precomputed mesh data" );
            mesh.PrecomputedMeshData.Apply( mesh );
          }
          m_targetMeshOptions = ( from mesh in meshes
                                  select mesh.PrecomputedMeshData?.Options ??
                                         GetCachedMeshOptions( mesh ) ??
                                         ScriptableObject.CreateInstance<AGXUnity.Collide.CollisionMeshOptions>() ).ToArray();
        }
        else if ( result == InspectorGUI.PositiveNegativeResult.Negative ) {
          //var meshes = GetTargets<AGXUnity.Collide.Mesh>().ToArray();
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
