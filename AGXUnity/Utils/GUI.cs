using System;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class GUI
  {
    /// <summary>
    /// Indent block.
    /// </summary>
    /// <example>
    /// using ( new GUI.Indent( 16.0f ) ) {
    ///   GUILayout.Label( "This label is indented 16 pixels." );
    /// }
    /// GUILayout.Label( "This label isn't indented." );
    /// </example>
    public class Indent : IDisposable
    {
      public Indent( float numPixels )
      {
        GUILayout.BeginHorizontal();
        GUILayout.Space( numPixels );
        GUILayout.BeginVertical();
      }

      public void Dispose()
      {
        GUILayout.EndVertical();
        GUILayout.EndHorizontal();
      }
    }

    public class AlignBlock : IDisposable
    {
      public enum Alignment { Left, Center, Right };

      public static AlignBlock Left { get { return new AlignBlock( Alignment.Left ); } }
      public static AlignBlock Center { get { return new AlignBlock( Alignment.Center ); } }
      public static AlignBlock Right { get { return new AlignBlock( Alignment.Right ); } }

      private Alignment m_alignment = Alignment.Center;

      private AlignBlock( Alignment alignment )
      {
        m_alignment = alignment;

        GUILayout.BeginHorizontal();
        if ( m_alignment != Alignment.Left )
          GUILayout.FlexibleSpace();
        GUILayout.BeginVertical();
      }

      public void Dispose()
      {
        GUILayout.EndVertical();
        if ( m_alignment == Alignment.Center )
          GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
      }
    }

    public class ColorBlock : IDisposable
    {
      private Color m_prevColor = default( Color );

      public ColorBlock( Color color )
      {
        m_prevColor = UnityEngine.GUI.color;
        UnityEngine.GUI.color = color;
      }

      public void Dispose()
      {
        UnityEngine.GUI.color = m_prevColor;
      }
    }

    public class EnabledBlock : IDisposable
    {
      private bool m_prevEnabled = true;

      public EnabledBlock( bool enable )
      {
        m_prevEnabled = UnityEngine.GUI.enabled;
        UnityEngine.GUI.enabled = enable;
      }

      public void Dispose()
      {
        UnityEngine.GUI.enabled = m_prevEnabled;
      }
    }

    public static string AddColorTag( string str, Color color )
    {
      return @"<color=" + color.ToHexStringRGBA() + @">" + str + @"</color>";
    }

    public static GUIContent MakeLabel( string text, bool bold = false, string toolTip = "" )
    {
      GUIContent label = new GUIContent();
      string boldBegin = bold ? "<b>" : "";
      string boldEnd   = bold ? "</b>" : "";
      label.text       = boldBegin + text + boldEnd;

      if ( toolTip != string.Empty )
        label.tooltip = toolTip;

      return label;
    }

    public static GUIContent MakeLabel( string text, int size, bool bold = false, string toolTip = "" )
    {
      GUIContent label = MakeLabel( text, bold, toolTip );
      label.text       = @"<size=" + size + @">" + label.text + @"</size>";
      return label;
    }

    public static GUIContent MakeLabel( string text, Color color, bool bold = false, string toolTip = "" )
    {
      GUIContent label = MakeLabel( text, bold, toolTip );
      label.text       = AddColorTag( text, color );
      return label;
    }

    public static GUIContent MakeLabel( string text, Color color, int size, bool bold = false, string toolTip = "" )
    {
      GUIContent label = MakeLabel( text, size, bold, toolTip );
      label.text       = AddColorTag( label.text, color );
      return label;
    }

    public static GUIStyle Align( GUIStyle style, TextAnchor anchor )
    {
      GUIStyle copy = new GUIStyle( style );
      copy.alignment = anchor;
      return copy;
    }

    private static GUISkin m_editorGUISkin = null;
    public static GUISkin Skin
    {
      get
      {
        if ( m_editorGUISkin == null )
          m_editorGUISkin = Resources.Load<GUISkin>( "AGXEditorGUISkin" );
        return m_editorGUISkin ?? UnityEngine.GUI.skin;
      }
    }

    public static Texture2D CreateColoredTexture( int width, int height, Color color )
    {
      Texture2D texture = new Texture2D( width, height );
      for ( int i = 0; i < width; ++i )
        for ( int j = 0; j < height; ++j )
          texture.SetPixel( i, j, color );

      texture.Apply();

      return texture;
    }

    public static GUIStyle CreateSelectedStyle( GUIStyle orgStyle )
    {
      GUIStyle selectedStyle = new GUIStyle( orgStyle );
      selectedStyle.normal = orgStyle.onActive;

      return selectedStyle;
    }

    public static void WarningLabel( string warning, GUISkin skin )
    {
      var prevBgc = UnityEngine.GUI.backgroundColor;
      UnityEngine.GUI.backgroundColor = Color.Lerp( Color.white, Color.black, 0.55f );
      GUILayout.Label( MakeLabel( warning,
                                  Color.Lerp( Color.red, Color.white, 0.25f ),
                                  true ),
                       new GUIStyle( skin.textArea ) { alignment = TextAnchor.MiddleCenter } );
      UnityEngine.GUI.backgroundColor = prevBgc;
    }
  }
}
