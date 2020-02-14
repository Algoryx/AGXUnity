using System;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor.Tools
{
  public class SelectGameObjectTool : Tool
  {
    public SelectGameObjectDropdownMenuTool MenuTool { get { return GetChild<SelectGameObjectDropdownMenuTool>(); } }

    public bool SelectionWindowActive { get { return MenuTool != null && MenuTool.WindowIsActive; } }

    public Action<GameObject> OnSelect = delegate { };

    public SelectGameObjectTool()
      : base( isSingleInstanceTool: true )
    {
    }

    public override void OnAdd()
    {
      var menuTool = new SelectGameObjectDropdownMenuTool();
      menuTool.RemoveOnClickMiss = false;
      menuTool.OnSelect = go =>
      {
        OnSelect( go );
        PerformRemoveFromParent();
      };
      AddChild( menuTool );
    }

    public override void OnSceneViewGUI( SceneView sceneView )
    {
      if ( MenuTool == null ) {
        PerformRemoveFromParent();
        return;
      }

      if ( ( !MenuTool.WindowIsActive || MenuTool.Target != Manager.MouseOverObject ) &&
           Manager.HijackLeftMouseClick() &&
           Manager.SceneViewGUIWindowHandler.GetMouseOverWindow( Event.current.mousePosition ) == null ) {
        // We know that we're hovering 'mouse over object' but we should check if we have a
        // good hit on some if it's children as well. Since:
        //   - We're using unity's internal 'pick object' functionality and it's not always
        //     (understandably) finding the leafs. We're interested in the leafs since we
        //     present a list of all parents to MenuTool.Target.
        GameObject target = Manager.MouseOverObject;
        if ( target != null && target.transform.childCount > 0 ) {
          var childHits = Utils.Raycast.IntersectChildren( HandleUtility.GUIPointToWorldRay( Event.current.mousePosition ), target );
          if ( childHits.Length > 0 )
            target = childHits[ 0 ].Target;
        }

        MenuTool.Target = target;
        MenuTool.Show();
      }
    }
  }
}
