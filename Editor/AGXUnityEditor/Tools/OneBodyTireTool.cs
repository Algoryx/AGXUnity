using UnityEngine;
using UnityEditor;

using AGXUnity;
using AGXUnity.Model;

using GUI = AGXUnity.Utils.GUI;

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
        InspectorGUI.ToolButtons( InspectorGUI.ToolButtonData.Create( ToolIcon.FindTire,
                                                                      SelectTireToolEnable,
                                                                      "Find Tire by selecting Tire in scene view.",
                                                                      () => toggleSelectTire = true ) );
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
