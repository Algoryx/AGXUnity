using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Rendering;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DebugRenderManager ) )]
  public class DebugRenderManagerTool : Tool
  {
    public DebugRenderManager Manager { get; private set; }

    public DebugRenderManagerTool( DebugRenderManager manager )
    {
      Manager = manager;
    }

    public override void OnPreTargetMembersGUI( GUISkin skin )
    {
      GUILayout.Label( GUI.MakeLabel( "Debug render manager", 16, true ), GUI.Align( skin.label, TextAnchor.MiddleCenter ) );

      GUI.Separator();

      Manager.RenderShapes = GUI.Toggle( GUI.MakeLabel( "Debug render shapes" ), Manager.RenderShapes, skin.button, skin.label );
      GUI.MaterialEditor( GUI.MakeLabel( "Shape material" ), 100, Manager.ShapeRenderMaterial, skin, newMaterial => Manager.ShapeRenderMaterial = newMaterial, true );

      GUI.Separator();

      using ( new GUILayout.HorizontalScope() ) {
        Manager.RenderContacts = GUI.Toggle( GUI.MakeLabel( "Render contacts" ), Manager.RenderContacts, skin.button, skin.label );
        Manager.ContactColor = EditorGUILayout.ColorField( Manager.ContactColor );
      }

      Manager.ContactScale = EditorGUILayout.Slider( GUI.MakeLabel( "Scale" ), Manager.ContactScale, 0.0f, 1.0f );

      GUI.Separator();

      Manager.ColorizeBodies = GUI.Toggle( GUI.MakeLabel( "Colorize bodies",
                                                          false,
                                                          "Every rigid body instance will be rendered with a unique color (wire framed)." ),
                                           Manager.ColorizeBodies,
                                           skin.button,
                                           skin.label );
      Manager.HighlightMouseOverObject = GUI.Toggle( GUI.MakeLabel( "Highlight mouse over object",
                                                                    false,
                                                                    "Highlight mouse over object in scene view." ),
                                                     Manager.HighlightMouseOverObject,
                                                     skin.button,
                                                     skin.label );
      Manager.IncludeInBuild = GUI.Toggle( GUI.MakeLabel( "Include in build",
                                                          false,
                                                          "Include debug rendering when building the project." ),
                                           Manager.IncludeInBuild,
                                           skin.button,
                                           skin.label );
    }
  }
}
