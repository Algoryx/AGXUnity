using System;
using UnityEngine;

namespace AGXUnity.Utils
{
  public class GUI
  {
    public static class Symbols
    {
      //public const char ArrowRight = '\u21D2';
      public const char ArrowRight = '\u2192';
      public const char ArrowLeftRight = '\u2194';

      public const char CircleArrowAcw = '\u21ba';
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

    public class BackgroundColorBlock : IDisposable
    {
      private Color m_prevBackgroundColor = default( Color );

      public BackgroundColorBlock( Color color )
      {
        m_prevBackgroundColor = UnityEngine.GUI.backgroundColor;
        UnityEngine.GUI.backgroundColor = color;
      }

      public void Dispose()
      {
        UnityEngine.GUI.backgroundColor = m_prevBackgroundColor;
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
      var content   = new GUIContent();
      var boldBegin = bold ? "<b>" : "";
      var boldEnd   = bold ? "</b>" : "";
      content.text  = boldBegin + text + boldEnd;

      if ( toolTip != string.Empty )
        content.tooltip = toolTip;

      return content;
    }

    public static GUIContent MakeLabel( string text, int size, bool bold = false, string toolTip = "" )
    {
      var label  = MakeLabel( text, bold, toolTip );
      label.text = @"<size=" + size + @">" + label.text + @"</size>";
      return label;
    }

    public static GUIContent MakeLabel( string text, Color color, bool bold = false, string toolTip = "" )
    {
      var label  = MakeLabel( text, bold, toolTip );
      label.text = AddColorTag( text, color );
      return label;
    }

    public static GUIContent MakeLabel( string text, Color color, int size, bool bold = false, string toolTip = "" )
    {
      var label  = MakeLabel( text, size, bold, toolTip );
      label.text = AddColorTag( label.text, color );
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
          m_editorGUISkin = Resources.Load<GUISkin>( "AGXUnitySceneViewGUISkin" );
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
  }
}
