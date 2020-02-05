using System.Collections.Generic;
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

      var newSingleSource = ShapeMeshSourceGUI( singleSource );
      if ( newSingleSource != null ) {
        if ( IsMultiSelect ) {
          foreach ( var target in GetTargets<AGXUnity.Collide.Mesh>() )
            if ( target != null )
              target.SetSourceObject( newSingleSource );
        }
        else
          Mesh.SetSourceObject( newSingleSource );
      }
    }

    public static Mesh ShapeMeshSourceGUI( Mesh currentSource )
    {
      var newSource = EditorGUILayout.ObjectField( GUI.MakeLabel( "Source:" ),
                                                   currentSource,
                                                   typeof( Mesh ),
                                                   false ) as Mesh;
      return newSource != currentSource ? newSource : null;
    }
  }
}
