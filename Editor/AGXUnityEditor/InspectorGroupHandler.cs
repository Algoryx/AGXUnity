using UnityEngine;
using AGXUnity;
using AGXUnity.Utils;

using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor
{
  /// <summary>
  /// Handle groups in the Inspector using FoldOut.
  /// </summary>
  public struct InspectorGroupHandler
  {
    public static InspectorGroupHandler Create()
    {
      var instance = new InspectorGroupHandler();
      instance.IsHidden = false;
      return instance;
    }

    public bool IsHidden { get; private set; }

    public void Begin( bool isHidden, int indentLevel = 1 )
    {
      End();
      IsHidden = isHidden;
      if ( !isHidden )
        m_indent = new InspectorGUI.IndentScope( indentLevel );
    }

    public void End()
    {
      m_indent?.Dispose();
      m_indent = null;
      IsHidden = false;
    }

    public void Update( InvokeWrapper wrapper, object targetInstance )
    {
      var foldoutBeginAttribute = wrapper.GetAttribute<InspectorGroupBeginAttribute>();
      if ( foldoutBeginAttribute != null || wrapper.HasAttribute<InspectorGroupEndAttribute>() )
        End();

      if ( foldoutBeginAttribute == null )
        return;

      var groupIdentifier = ( targetInstance != null ? targetInstance.GetType().FullName : "null" ) +
                            "_" + foldoutBeginAttribute.Name;
      Begin( !InspectorGUI.Foldout( EditorData.Instance.GetData( targetInstance as Object,
                                                                 groupIdentifier ),
                                    GUI.MakeLabel( foldoutBeginAttribute.Name ) ) );
    }

    public void Dispose()
    {
      End();
    }

    private InspectorGUI.IndentScope m_indent;
  }
}
