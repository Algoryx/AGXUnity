using AGXUnity;
using AGXUnityEditor.UIElements;
using AGXUnityEditor.Utils;
using UnityEditor;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( ScriptComponent ), true, isFallback = true )]
  [CanEditMultipleObjects]
  public class ScriptableComponentEditor : Editor
  {
    public override VisualElement CreateInspectorGUI()
    {
      var container = new VisualElement();
      foreach ( var prop in serializedObject.FindChildren() )
        container.Add( prop.CreateDefaultInspector() );
      return container;
    }
  }
}
