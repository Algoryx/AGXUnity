using UnityEngine;
using UnityEditor;
using System.Linq;
using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Utils;

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
      if(!ContactMaterialManager.HasInstanceInScene || !ContactMaterialManager.Instance.ContactMaterials.Contains(ContactMaterial)) {
        InspectorGUI.Separator( 1, 16 );
        var enabled = UnityEngine.GUI.enabled;
        if ( !ContactMaterialManager.HasInstanceInScene ) {
          EditorGUILayout.HelpBox( "The current scene does not contain a ContactMaterialManager object.", MessageType.Warning,true );
          if ( GUILayout.Button( AGXUnity.Utils.GUI.MakeLabel( "Add a ContactMaterialManager to scene" ) ) ) {
            _ = ContactMaterialManager.Instance;
          }
          UnityEngine.GUI.enabled = false;
        }
        EditorGUILayout.HelpBox( "Contact material has not been added to ContactMaterialManager and will not be used unless added through scripts at runtime.", MessageType.Warning, true );
        if(GUILayout.Button( AGXUnity.Utils.GUI.MakeLabel( "Add material to ContactMaterialManager" ) ) ) {
          ContactMaterialManager.Instance.Add( ContactMaterial );
        }
        UnityEngine.GUI.enabled = enabled;
      }
    }
  }
}
