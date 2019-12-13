using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof ( ContactMaterialManager ) )]
  public class ContactMaterialManagerTool : CustomTargetTool
  {
    public static readonly string Identifier = "Contact Materials";

    public ContactMaterialManager Manager
    {
      get
      {
        return Targets[ 0 ] as ContactMaterialManager;
      }
    }

    public ContactMaterialManagerTool( Object[] targets )
      : base( targets )
    {
      Manager.RemoveNullEntries();
    }

    public override void OnAdd()
    {
      InspectorGUI.GetTargetToolArrayGUIData( Manager, Identifier, data => data.Bool = true );
    }

    public override void OnPreTargetMembersGUI()
    {
      Manager.RemoveNullEntries();

      GUILayout.Label( GUI.MakeLabel( "Contact Material Manager", 18, true ),
                       new GUIStyle( InspectorEditor.Skin.label ) { alignment = TextAnchor.MiddleCenter } );

      GUI.Separator();

      InspectorGUI.ToolArrayGUI( this,
                                 Manager.ContactMaterials,
                                 Identifier,
                                 Color.yellow,
                                 cm => Manager.Add( cm ),
                                 cm => Manager.Remove( cm ) );
    }
  }
}
