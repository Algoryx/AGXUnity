using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( AGXUnity.Collide.Mesh ) )]
  public class ShapeMeshTool : ShapeTool
  {
    public AGXUnity.Collide.Mesh Mesh { get { return Shape as AGXUnity.Collide.Mesh; } }

    public ShapeMeshTool( AGXUnity.Collide.Shape shape )
      : base( shape )
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

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      base.OnPreTargetMembersGUI( skin );

      var sourceObjects = Mesh.SourceObjects;
      var singleSource  = sourceObjects.FirstOrDefault();

      Undo.RecordObjects( Mesh.GetUndoCollection(), "Mesh source" );

      var newSingleSource = GUI.ShapeMeshSourceGUI( singleSource, skin );
      if ( newSingleSource != null )
        Mesh.SetSourceObject( newSingleSource );

      GUI.Separator();
    }
  }
}
