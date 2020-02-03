using UnityEngine;
using AGXUnity;
using AGXUnity.Utils;

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
        m_indent = new Utils.GUI.IndentScope( indentLevel );
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
      Begin( !Utils.GUI.Foldout( EditorData.Instance.GetData( targetInstance as Object,
                                                              groupIdentifier ),
                                 Utils.GUI.MakeLabel( foldoutBeginAttribute.Name, true ) ) );
    }

    public void Dispose()
    {
      End();
    }

    private Utils.GUI.IndentScope m_indent;
  }
}
