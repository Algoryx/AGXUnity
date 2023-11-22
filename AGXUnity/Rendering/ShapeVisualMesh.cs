using System;
using System.Linq;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// Shape visual for shape type Mesh.
  /// </summary>
  [AddComponentMenu( "" )]
  [DoNotGenerateCustomEditor]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#create-visual-tool-icon-small-create-visual-tool" )]
  public class ShapeVisualMesh : ShapeVisual
  {
    /// <summary>
    /// Callback from Collide.Mesh when a new source has been assigned to
    /// the mesh.
    /// </summary>
    /// <param name="mesh">Mesh shape with updated source object.</param>
    /// <param name="source">Source mesh that has been changed.</param>
    /// <param name="added">True if source has been added, false if removed.</param>
    public static void HandleMeshSource( Collide.Mesh mesh, Mesh source, bool added )
    {
      var instance = Find( mesh ) as ShapeVisualMesh;
      if ( instance != null )
        instance.HandleMeshSource( source, added );
    }

    /// <summary>
    /// Callback when shape size has been changed.
    /// </summary>
    public override void OnSizeUpdated()
    {
      // We don't do anything here since we support any type of scale of the meshes.
    }

    /// <summary>
    /// Callback when constructed.
    /// </summary>
    protected override void OnConstruct()
    {
      gameObject.AddComponent<MeshFilter>();
      gameObject.AddComponent<MeshRenderer>();
    }

    /// <summary>
    /// Callback when our shape has update mesh source. The material used in the last
    /// source will be assigned to the new since we don't know if the number of
    /// sub-meshes are the same.
    /// </summary>
    protected virtual void HandleMeshSource( Mesh source, bool added )
    {
      var shapeMesh = Shape as Collide.Mesh;
      if ( added ) {
        var material = GetMaterials().LastOrDefault() ?? DefaultMaterial;
        var sourceIndex = Array.IndexOf( shapeMesh.SourceObjects, source );
        AddChildMesh( Shape,
                      gameObject,
                      source,
                      gameObject.name + "_" + ( sourceIndex + 1 ).ToString(),
                      material,
                      sourceIndex > 0 );
      }
      else {
        var filters = GetComponentsInChildren<MeshFilter>();
        GameObject goToDestroy = null;
        foreach ( var filter in filters ) {
          if ( filter.sharedMesh == source ) {
            goToDestroy = filter.gameObject;
            break;
          }
        }

        if ( goToDestroy == gameObject ) {
          GetComponent<MeshFilter>().sharedMesh = null;
          GetComponent<MeshRenderer>().sharedMaterials = new Material[] { };
        }
        else if ( goToDestroy != null )
          DestroyImmediate( goToDestroy );
      }
    }
  }
}
