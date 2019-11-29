using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using AGXUnity;
using AGXUnity.Models;

using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( TwoBodyTire ) )]
  public class TwoBodyTireTool : CustomTargetTool
  {
    public TwoBodyTire Tire { get { return Targets[ 0 ] as TwoBodyTire; } }

    public TwoBodyTireTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      GUILayout.Label( GUI.MakeLabel( FindHeaderName(),
                                      14,
                                      true ),
                       GUI.Align( InspectorEditor.Skin.label, TextAnchor.MiddleCenter ) );

      GUI.Separator();

      // '\u25CB' circle
      // '\u274D' 3D circle
      bool toggleSelectTireAndRim = false;
      bool toggleSelectTire = false;
      bool toggleSelectRim = false;
      if ( !EditorApplication.isPlaying && NumTargets == 1 ) {
        using ( new GUILayout.HorizontalScope() ) {
          GUI.ToolsLabel( InspectorEditor.Skin );
          using ( GUI.ToolButtonData.ColorBlock ) {
            toggleSelectTireAndRim = GUI.ToolButton( '\u274D',
                                                     SelectTireAndRimToolEnable,
                                                     "Find Tire, Rim and Tire <-> Rim constraint by selecting Tire in scene view.",
                                                     InspectorEditor.Skin );
            toggleSelectTire       = GUI.ToolButton( '\uFFEE',
                                                     SelectTireToolEnable,
                                                     "Find Tire by selecting Tire in scene view.",
                                                     InspectorEditor.Skin,
                                                     38 );
            toggleSelectRim        = GUI.ToolButton( '\u25CB',
                                                     SelectRimToolEnable,
                                                     "Find Rim by selecting Rim in scene view.",
                                                     InspectorEditor.Skin,
                                                     16 );
          }
        }
        GUI.Separator();
      }

      if ( toggleSelectTireAndRim )
        SelectTireAndRimToolEnable = !SelectTireAndRimToolEnable;
      if ( toggleSelectTire )
        SelectTireToolEnable = !SelectTireToolEnable;
      if ( toggleSelectRim )
        SelectRimToolEnable = !SelectRimToolEnable;
    }

    private bool SelectTireAndRimToolEnable
    {
      get { return GetChild<SelectGameObjectTool>() != null &&
                   GetChild<SelectGameObjectTool>().OnSelect == OnTireAndRimSelected; }
      set
      {
        if ( value && !SelectTireAndRimToolEnable )
          AddChild( new SelectGameObjectTool()
          {
            OnSelect = OnTireAndRimSelected
          } );
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
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

    private bool SelectRimToolEnable
    {
      get
      {
        return GetChild<SelectGameObjectTool>() != null &&
               GetChild<SelectGameObjectTool>().OnSelect == OnRimSelected;
      }
      set
      {
        if ( value && !SelectRimToolEnable )
          AddChild( new SelectGameObjectTool()
          {
            OnSelect = OnRimSelected
          } );
        else if ( !value )
          RemoveChild( GetChild<SelectGameObjectTool>() );
      }
    }

    private void OnTireAndRimSelected( GameObject selected )
    {
      if ( selected == null ) {
        Debug.LogError( "Unable to configure TwoBodyTire - selected object is null." );
        return;
      }

      var rb = selected.GetComponentInParent<RigidBody>();
      if ( rb == null ) {
        Debug.LogError( "Unable to configure TwoBodyTire - selected object isn't part of a RigidBody." );
        return;
      }

      // StageUtility is used when a prefab is open in "Open Prefab" tab.
      var allConstraints = StageUtility.GetCurrentStageHandle().Contains( rb.gameObject ) ?
                             StageUtility.GetCurrentStageHandle().FindComponentsOfType<Constraint>() :
                             Object.FindObjectsOfType<Constraint>();
      var tireConstraints = ( from constraint
                              in allConstraints
                              where constraint.AttachmentPair.Contains( rb )
                              select constraint ).ToArray();
      if ( tireConstraints.Length != 1 ) {
        Debug.LogError( "Unable to configure TwoBodyTire - " +
                        rb.name +
                        " has " +
                        tireConstraints.Length +
                        " constraints != 1. Exact one expected." );
        return;
      }

      Undo.RecordObject( Tire, "Configuring TwoBodyTire" );
      Tire.Configure( tireConstraints[ 0 ], rb );
    }

    private void OnTireSelected( GameObject selected )
    {
      AssignTireOrRimGivenSelected( selected, Tire, "Tire", Tire.SetTire );
    }

    private void OnRimSelected( GameObject selected )
    {
      AssignTireOrRimGivenSelected( selected, Tire, "Rim", Tire.SetRim );
    }

    public static void AssignTireOrRimGivenSelected( GameObject selected, Tire tire, string name, System.Func<RigidBody, bool> assignFunc )
    {
      if ( selected == null ) {
        Debug.LogError( $"Select {name}: Null/World is invalid selection for {name}." );
        return;
      }

      var rb = selected.GetComponentInParent<RigidBody>();
      if ( rb == null ) {
        Debug.LogError( $"Select {name}: Unable to find rigid body component in: {selected.name} or its parents." );
        return;
      }

      Undo.RecordObject( tire, $"Tire.Set{name}" );

      if ( !assignFunc( rb ) )
        Debug.LogError( $"Select {name}: Assigning {name} {rb.name} failed." );

      EditorUtility.SetDirty( tire );
    }

    private string FindHeaderName()
    {
      if ( NumTargets > 1 )
        return "Two Body Tires";

      string result = "";
      if ( Tire.TireRigidBody != null )
        result += Tire.TireRigidBody.name;
      else
        result += "null";
      result += " \u274D ";
      if ( Tire.RimRigidBody != null )
        result += Tire.RimRigidBody.name;
      else
        result += "null";
      return result;
    }
  }
}
