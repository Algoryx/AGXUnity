using UnityEngine;
using UnityEditor;

using AGXUnity;
using AGXUnity.Models;

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
          GUI.ToolsLabel( InspectorEditor.Skin );
          using ( GUI.ToolButtonData.ColorBlock ) {
            toggleSelectTire = GUI.ToolButton( '\uFFEE',
                                               SelectTireToolEnable,
                                               "Find Tire by selecting Tire in scene view.",
                                               InspectorEditor.Skin,
                                               38 );
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
#if AGX_DYNAMICS_2_28_OR_LATER
      TwoBodyTireTool.AssignTireOrRimGivenSelected( selected, Tire, "Tire", Tire.SetRigidBody );
#endif
    }
  }
}
