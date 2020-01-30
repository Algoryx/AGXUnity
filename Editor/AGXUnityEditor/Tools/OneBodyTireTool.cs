using UnityEngine;
using UnityEditor;

using AGXUnity;
using AGXUnity.Model;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( OneBodyTire ) )]
  public class OneBodyTireTool : CustomTargetTool
  {
    public OneBodyTire Tire { get { return Targets[ 0 ] as OneBodyTire; } }

    public OneBodyTireTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      bool toggleSelectTire = false;
      if ( !EditorApplication.isPlaying && NumTargets == 1 ) {
        using ( new GUILayout.HorizontalScope() ) {
          GUI.ToolsLabel();
          using ( GUI.ToolButtonData.ColorBlock ) {
            toggleSelectTire = GUI.ToolButton( '\uFFEE',
                                               SelectTireToolEnable,
                                               "Find Tire by selecting Tire in scene view.",
                                               InspectorGUISkin.ButtonType.Normal );
          }
        }
        GUI.Separator();
      }

      if ( toggleSelectTire )
        SelectTireToolEnable = !SelectTireToolEnable;
    }

    private bool SelectTireToolEnable
    {
      get
      {
        return GetChild<SelectGameObjectTool>() != null &&
               GetChild<SelectGameObjectTool>().OnSelect == OnTireSelected;
      }
      set
      {
        if ( value && !SelectTireToolEnable )
          AddChild( new SelectGameObjectTool()
          {
            OnSelect = OnTireSelected
          } );
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
    }

    private void OnTireSelected( GameObject selected )
    {
      TwoBodyTireTool.AssignTireOrRimGivenSelected( selected, Tire, "Tire", Tire.SetRigidBody );
    }
  }
}
