using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

namespace AGXUnityEditor
{
  public static class IconManager
  {
    public static string Directory
    {
      get
      {
        if ( string.IsNullOrEmpty( m_directory ) )
          m_directory = IO.Utils.AGXUnityEditorDirectory +
                        Path.DirectorySeparatorChar +
                        "Icons";
        return m_directory;
      }
      set
      {
        m_directory = value;
      }
    }

    public static Texture2D GetIcon( string name )
    {
      var iconIdentifier = Directory + Path.DirectorySeparatorChar + name;
      if ( m_icons.TryGetValue( iconIdentifier, out Texture2D icon ) )
        return icon;

      icon = EditorGUIUtility.Load( iconIdentifier + ".png" ) as Texture2D;
      if ( icon != null )
        m_icons.Add( iconIdentifier, icon );

      return icon;
    }

    public static Rect GetIconRect( Rect buttonRect, float scale = 0.75f )
    {
      var iconSize = new Vector2( scale * buttonRect.width, scale * buttonRect.height );
      return new Rect( buttonRect.position + ( 1.0f - scale ) * iconSize, iconSize );
    }

    private static Dictionary<string, Texture2D> m_icons = new Dictionary<string, Texture2D>();
    private static string m_directory = string.Empty;
  }
}
