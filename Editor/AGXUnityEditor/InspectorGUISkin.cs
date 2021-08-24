using System;
using System.IO;
using UnityEngine;
using UnityEditor;

using GUI = AGXUnity.Utils.GUI;

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

    public GUIStyle ButtonLeft { get; private set; } = null;

    public GUIStyle ButtonMiddle { get; private set; } = null;

    public GUIStyle ButtonRight { get; private set; } = null;

    public GUIStyle Label { get; private set; } = null;

    public GUIStyle LabelWordWrap { get; private set; } = null;

    public GUIStyle LabelMiddleCenter { get; private set; } = null;

    public GUIStyle LabelMiddleLeft { get; private set; } = null;

    public GUIStyle TextArea { get; private set; } = null;

    public GUIStyle TextAreaMiddleCenter { get; private set; } = null;

    public GUIStyle TextField { get; private set; } = null;

    public GUIStyle TextFieldMiddleLeft { get; private set; } = null;

    public GUIStyle Popup { get; private set; } = null;

    public static Color BrandColor { get; private set; } = new Color( 243.0f / 255.0f,
                                                                      139.0f / 255.0f,
                                                                      0.0f );

    public static Color BrandColorBlue { get; private set; } = new Color( 45.0f / 255.0f,
                                                                          204.0f / 255.0f,
                                                                          211.0f / 255.0f );

    /// <summary>
    /// Size of toolbar buttons.
    /// </summary>
    public static Vector2 ToolButtonSize { get; set; } = new Vector2( 24.0f,
#if UNITY_2019_3_OR_NEWER
                                                                      18.0f );
#else
                                                                      16.0f );
#endif

    /// <summary>
    /// Button style given button type.
    /// </summary>
    /// <param name="type">Button type; left, middle, right or normal.</param>
    /// <returns>Button style to use.</returns>
    public GUIStyle GetButton( ButtonType type = ButtonType.Normal )
    {
      return m_buttonStyles[ (int)type ];
    }

    public string TagTypename( string typename )
    {
      return '[' +
             GUI.AddColorTag( typename,
                              Color.Lerp( InspectorGUI.ProBackgroundColor,
                                          BrandColor,
                                          0.6f ) ) +
             ']';
    }

    private InspectorGUISkin()
    {
      Initialize();
    }

    private void Initialize()
    {
      // Foldout and Toggle are used explicitly when calling
      // EditorGUILayout.Foldout and EditorGUILayout.Toggle.
      EditorStyles.foldout.richText = true;
      EditorStyles.toggle.richText  = true;
      EditorStyles.label.richText   = true; // Used in object field.

      NumberField = new GUIStyle( EditorStyles.numberField );

      Button = new GUIStyle( EditorStyles.miniButton )
      {
        richText = true
      };

      ButtonLeft = new GUIStyle( EditorStyles.miniButtonLeft )
      {
        richText = true
      };

      ButtonMiddle = new GUIStyle( EditorStyles.miniButtonMid )
      {
        richText = true
      };

      ButtonRight = new GUIStyle( EditorStyles.miniButtonRight )
      {
        richText = true
      };

      m_buttonStyles = new GUIStyle[]
      {
        Button,
        ButtonLeft,
        ButtonMiddle,
        ButtonRight
      };

      Label = new GUIStyle( EditorStyles.label )
      {
        richText = true
      };
      LabelWordWrap     = new GUIStyle( Label ) { wordWrap = true };
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

    private GUIStyle AnchorStyle( GUIStyle org, TextAnchor anchor )
    {
      return new GUIStyle( org )
      {
        alignment = anchor
      };
    }

    private GUIStyle[] m_buttonStyles = null;
    private static InspectorGUISkin s_instance = null;
  }
}
