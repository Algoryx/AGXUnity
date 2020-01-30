using System;
using UnityEngine;
using UnityEditor;
using AGXUnity;

using GUI    = AGXUnityEditor.Utils.GUI;
using Object = UnityEngine.Object;

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
                       new GUIStyle( InspectorEditor.Skin.Label ) { alignment = TextAnchor.MiddleCenter } );

      GUI.Separator();

      InspectorGUI.ToolArrayGUI( this,
                                 Manager.ContactMaterials,
                                 Identifier,
                                 Color.yellow,
                                 cm => Manager.Add( cm ),
                                 cm => Manager.Remove( cm ),
                                 PreContactMaterialEditor( Manager.ContactMaterialEntries ) );
    }

    private Action<ContactMaterial, int> PreContactMaterialEditor( ContactMaterialEntry[] entries )
    {
      return ( cm, index ) =>
      {
        entries[ index ].IsOriented = GUI.Toggle( GUI.MakeLabel( "Is Oriented",
                                                                  false,
                                                                  "Enable/disable oriented friction models." ),
                                                  entries[ index ].IsOriented );
        if ( entries[ index ].IsOriented ) {
          using ( new GUI.Indent( 28 ) ) {
            entries[ index ].ReferenceObject = (GameObject)EditorGUILayout.ObjectField( GUI.MakeLabel( "Reference Object" ),
                                                                                        entries[ index ].ReferenceObject,
                                                                                        typeof( GameObject ),
                                                                                        true );
            entries[ index ].PrimaryDirection = (FrictionModel.PrimaryDirection)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Primary Direction",
                                                                                                                          false,
                                                                                                                          "Primary direction in object local frame." ),
                                                                                                           entries[ index ].PrimaryDirection,
                                                                                                           InspectorEditor.Skin.Popup );
          }
        }
        GUI.Separator();
      };
    }
  }
}
