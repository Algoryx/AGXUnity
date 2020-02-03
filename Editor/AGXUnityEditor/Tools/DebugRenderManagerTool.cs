using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Rendering;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DebugRenderManager ) )]
  public class DebugRenderManagerTool : CustomTargetTool
  {
    public DebugRenderManager Manager
    {
      get
      {
        return Targets[ 0 ] as DebugRenderManager;
      }
    }

    public DebugRenderManagerTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;
      GUILayout.Label( GUI.MakeLabel( "Debug render manager", 16, true ),
                       skin.LabelMiddleCenter );

      GUI.Separator();

      var newRenderState = GUI.Toggle( GUI.MakeLabel( "Debug render shapes" ), Manager.RenderShapes );
      if ( newRenderState != Manager.RenderShapes ) {
        Manager.RenderShapes = newRenderState;
        EditorUtility.SetDirty( Manager );
      }
      InspectorGUI.UnityMaterial( GUI.MakeLabel( "Shape material" ),
                                  Manager.ShapeRenderMaterial,
                                  newMaterial => Manager.ShapeRenderMaterial = newMaterial );

      GUI.Separator();

      using ( new GUILayout.HorizontalScope() ) {
        Manager.RenderContacts = GUI.Toggle( GUI.MakeLabel( "Render contacts" ), Manager.RenderContacts );
        Manager.ContactColor = EditorGUILayout.ColorField( Manager.ContactColor );
      }

      Manager.ContactScale = EditorGUILayout.Slider( GUI.MakeLabel( "Scale" ), Manager.ContactScale, 0.0f, 1.0f );

      GUI.Separator();

      Manager.ColorizeBodies = GUI.Toggle( GUI.MakeLabel( "Colorize bodies",
                                                          false,
                                                          "Every rigid body instance will be rendered with a unique color (wire framed)." ),
                                           Manager.ColorizeBodies );
      Manager.HighlightMouseOverObject = GUI.Toggle( GUI.MakeLabel( "Highlight mouse over object",
                                                                    false,
                                                                    "Highlight mouse over object in scene view." ),
                                                     Manager.HighlightMouseOverObject );
      Manager.IncludeInBuild = GUI.Toggle( GUI.MakeLabel( "Include in build",
                                                          false,
                                                          "Include debug rendering when building the project." ),
                                           Manager.IncludeInBuild );
    }
  }
}
