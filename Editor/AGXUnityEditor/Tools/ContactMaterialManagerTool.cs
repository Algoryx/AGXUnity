using UnityEngine;
using UnityEditor;
using AGXUnity;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof ( ContactMaterialManager ) )]
  public class ContactMaterialManagerTool : Tool
  {
    public ContactMaterialManager Manager { get; private set; }

    public ContactMaterialManagerTool( ContactMaterialManager manager )
    {
      Manager = manager;
      Manager.RemoveNullEntries();
    }

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      Manager.RemoveNullEntries();

      OnContactMaterialsList( skin );
    }

    private EditorDataEntry FoldoutDataEntry { get { return EditorData.Instance.GetData( Manager, "ContactMaterials" ); } }

    private void OnContactMaterialsList( GUISkin skin )
    {
      ContactMaterial contactMaterialToAdd = null;
      ContactMaterial contactMaterialToRemove = null;

      GUILayout.Label( GUI.MakeLabel( "Contact Material Manager", 18, true ), new GUIStyle( skin.label ) { alignment = TextAnchor.MiddleCenter } );
      GUILayout.Space( 4 );
      GUILayout.Label( GUI.MakeLabel( "Drag and drop contact materials into the list below to add/enable the contact material in the simulation." ),
                       new GUIStyle( skin.textArea ) { alignment = TextAnchor.MiddleCenter } );
      GUILayout.Space( 4 );

      GUI.Separator3D();

      GUILayout.BeginVertical();
      {
        if ( GUI.Foldout( FoldoutDataEntry, GUI.MakeLabel( "Contact Materials [" + Manager.ContactMaterialEntries.Length + "]" ), skin ) ) {
          var contactMaterials = Manager.ContactMaterials;
          using ( new GUI.Indent( 12 ) ) {
            foreach ( var contactMaterial in contactMaterials ) {
              GUI.Separator();

              bool foldoutActive = false;

              GUILayout.BeginHorizontal();
              {
                foldoutActive = GUI.Foldout( EditorData.Instance.GetData( Manager, contactMaterial.name ), GUI.MakeLabel( contactMaterial.name ), skin );
                using ( GUI.NodeListButtonColor )
                  if ( GUILayout.Button( GUI.MakeLabel( GUI.Symbols.ListEraseElement.ToString(), false, "Erase this element" ),
                                         skin.button,
                                         new GUILayoutOption[] { GUILayout.Width( 20 ), GUILayout.Height( 14 ) } ) )
                    contactMaterialToRemove = contactMaterial;
              }
              GUILayout.EndHorizontal();

              if ( foldoutActive ) {
                using ( new GUI.Indent( 12 ) )
                  BaseEditor<ContactMaterial>.Update( contactMaterial, contactMaterial, skin );
              }
            }
          }
        }
      }
      GUILayout.EndVertical();

      // Note that GetLastRect is used here and it's expecting the begin/end vertical rect.
      GUI.HandleDragDrop<ContactMaterial>( GUILayoutUtility.GetLastRect(),
                                           Event.current,
                                           ( contactMaterial ) =>
                                           {
                                             contactMaterialToAdd = contactMaterial;
                                           } );

      GUI.Separator();

      GUILayout.BeginHorizontal();
      {
        GUILayout.Label( GUI.MakeLabel( "Add:" ), skin.label );
        contactMaterialToAdd = EditorGUILayout.ObjectField( null, typeof( ContactMaterial ), false ) as ContactMaterial ?? contactMaterialToAdd;
      }
      GUILayout.EndHorizontal();

      GUI.Separator3D();

      if ( contactMaterialToAdd != null ) {
        Manager.Add( contactMaterialToAdd );
        FoldoutDataEntry.Bool = true;
      }

      if ( contactMaterialToRemove != null )
        Manager.Remove( contactMaterialToRemove );
    }
  }
}
