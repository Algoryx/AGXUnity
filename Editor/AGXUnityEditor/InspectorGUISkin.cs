using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor
{
  public class InspectorGUISkin
  {
    public enum ButtonType
    {
      Normal,
      Left,
      Middle,
      Right
    }

    public static InspectorGUISkin Instance { get { return s_instance ?? ( s_instance = new InspectorGUISkin() ); } }

    public GUIStyle NumberField { get; private set; } = null;

    public GUIStyle Button { get; private set; } = null;

    public GUIStyle ButtonActive { get; private set; } = null;

    public GUIStyle ButtonLeft { get; private set; } = null;

    public GUIStyle ButtonLeftActive { get; private set; } = null;

    public GUIStyle ButtonMiddle { get; private set; } = null;

    public GUIStyle ButtonMiddleActive { get; private set; } = null;

    public GUIStyle ButtonRight { get; private set; } = null;

    public GUIStyle ButtonRightActive { get; private set; } = null;

    public GUIStyle Label { get; private set; } = null;

    public GUIStyle LabelMiddleCenter { get; private set; } = null;

    public GUIStyle LabelMiddleLeft { get; private set; } = null;

    public GUIStyle TextArea { get; private set; } = null;

    public GUIStyle TextAreaMiddleCenter { get; private set; } = null;

    public GUIStyle TextField { get; private set; } = null;

    public GUIStyle TextFieldMiddleLeft { get; private set; } = null;

    public GUIStyle Popup { get; private set; } = null;

    public GUIStyle GetButton( bool active, ButtonType type = ButtonType.Normal )
    {
      return active ?
               m_buttonActiveStyles[ (int)type ] :
               m_buttonStyles[ (int)type ];
    }

    private InspectorGUISkin()
    {
      Initialize();
    }

    private void Initialize()
    {
      // Foldout and Toggle are used explicitly when calling
      // EditorGUILayout.Foldout and EditorGUILayout.Toggle.
      EditorStyles.foldout.richText     = true;
      EditorStyles.toggle.richText      = true;
      EditorStyles.label.richText       = true; // Used in object field.

      NumberField = new GUIStyle( EditorStyles.numberField );

      Button = new GUIStyle( EditorStyles.miniButton )
      {
        richText = true
      };
      ButtonActive = InvertStyle( Button );

      ButtonLeft = new GUIStyle( EditorStyles.miniButtonLeft )
      {
        richText = true
      };
      ButtonLeftActive = InvertStyle( ButtonLeft );

      ButtonMiddle = new GUIStyle( EditorStyles.miniButtonMid )
      {
        richText = true
      };
      ButtonMiddleActive = InvertStyle( ButtonMiddle );

      ButtonRight = new GUIStyle( EditorStyles.miniButtonRight )
      {
        richText = true
      };
      ButtonRightActive = InvertStyle( ButtonRight );

      m_buttonStyles = new GUIStyle[]
      {
        Button,
        ButtonLeft,
        ButtonMiddle,
        ButtonRight
      };

      m_buttonActiveStyles = new GUIStyle[]
      {
        ButtonActive,
        ButtonLeftActive,
        ButtonMiddleActive,
        ButtonRightActive
      };

      Label = new GUIStyle( EditorStyles.label )
      {
        richText = true
      };
      LabelMiddleCenter = AnchorStyle( Label, TextAnchor.MiddleCenter );
      LabelMiddleLeft   = AnchorStyle( Label, TextAnchor.MiddleLeft );

      TextArea = new GUIStyle( EditorStyles.textArea )
      {
        richText = true
      };
      TextAreaMiddleCenter = AnchorStyle( TextArea, TextAnchor.MiddleCenter );

      TextField = new GUIStyle( EditorStyles.textField )
      {
        richText = true
      };
      TextFieldMiddleLeft = AnchorStyle( TextField, TextAnchor.MiddleLeft );

      Popup = new GUIStyle( EditorStyles.popup )
      {
        richText = true
      };
    }

    private GUIStyle InvertStyle( GUIStyle org )
    {
      return new GUIStyle( org )
      {
        normal = org.active,
        active = org.normal
      };
    }

    private GUIStyle AnchorStyle( GUIStyle org, TextAnchor anchor )
    {
      return new GUIStyle( org )
      {
        alignment = anchor
      };
    }

    private GUIStyle[] m_buttonStyles = null;
    private GUIStyle[] m_buttonActiveStyles = null;
    private static InspectorGUISkin s_instance = null;
  }
}
