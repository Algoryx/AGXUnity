using AGXUnity;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( ContactMaterial ) )]
  class ContactMaterialTool : CustomTargetTool
  {
    public ContactMaterial ContactMaterial
    {
      get
      {
        return Targets[ 0 ] as ContactMaterial;
      }
    }

    public ContactMaterialTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( !ContactMaterialManager.HasInstanceInScene || Targets.Cast<ContactMaterial>().Any(cm => !ContactMaterialManager.Instance.ContactMaterials.Contains( cm ) ) ) {
        InspectorGUI.Separator( 1, 16 );
        var enabled = UnityEngine.GUI.enabled;
        if ( !ContactMaterialManager.HasInstanceInScene ) {
          EditorGUILayout.HelpBox( "The current scene does not contain a ContactMaterialManager object.", MessageType.Warning, true );
          if ( GUILayout.Button( AGXUnity.Utils.GUI.MakeLabel( "Add a ContactMaterialManager to scene" ) ) ) {
            _ = ContactMaterialManager.Instance;
          }
          UnityEngine.GUI.enabled = false;
        }
        EditorGUILayout.HelpBox( "Contact material has not been added to ContactMaterialManager and will not be used unless added through scripts at runtime.", MessageType.Warning, true );
        if ( GUILayout.Button( AGXUnity.Utils.GUI.MakeLabel( "Add material to ContactMaterialManager" ) ) ) {
          foreach ( var t in Targets )
            if ( t is ContactMaterial cm && !ContactMaterialManager.Instance.ContactMaterials.Contains( cm ) )
              ContactMaterialManager.Instance.Add( cm );
        }
        UnityEngine.GUI.enabled = enabled;
      }
    }
  }
}
