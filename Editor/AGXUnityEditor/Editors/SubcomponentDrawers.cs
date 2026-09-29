using AGXUnity.Util;
using AGXUnity.Sensor;
using AGXUnityEditor.UIElements;
using AGXUnityEditor.Utils;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  [CustomPropertyDrawer( typeof( Subcomponent<> ), true )]
  public class SubcomponentDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var container = new VisualElement();
      foreach ( var subprop in property.FindChildren() )
        container.Add( subprop.CreateDefaultInspector() );
      return container;
    }
  }

  [CustomPropertyDrawer( typeof( SubcomponentList<,> ), true )]
  public class SubcomponentListDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var backingProperty = property.FindPropertyRelative( "m_backing" );
      var listView = new ListView
      {
        headerTitle = property.displayName,
        showFoldoutHeader = true,
        showAddRemoveFooter = true,
        showBoundCollectionSize = false,
        showBorder = true,
        showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
        reorderable = true,
        reorderMode = ListViewReorderMode.Animated,
        selectionType = SelectionType.Single,
        virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
      };

      // Unity's default array add operation copies an existing element or creates
      // a zero-initialized serialized value. Adding through SubcomponentList
      // instead runs the element constructor and preserves its field defaults.
      listView.overridingAddButtonBehavior = ( _, _ ) => {
        var serializedObject = property.serializedObject;
        serializedObject.UpdateIfRequiredOrScript();
        Undo.RecordObjects( serializedObject.targetObjects, $"Add {property.displayName}" );

        if ( property.boxedValue is ISubcomponentList list ) {
          list.AddDefault();
          property.boxedValue = list;
          serializedObject.ApplyModifiedProperties();
          listView.allowRemove = true;
          listView.RefreshItems();
        }
      };

      listView.allowRemove = backingProperty.arraySize > 0;
      listView.itemsRemoved += _ =>
        listView.allowRemove = backingProperty.arraySize > 0;
      listView.BindProperty( backingProperty );
      return listView;
    }
  }

  [CustomPropertyDrawer( typeof( ImuSensorSubcomponentList ) )]
  public class ImuSensorSubcomponentListDrawer : PropertyDrawer
  {
    public override VisualElement CreatePropertyGUI( SerializedProperty property )
    {
      var backingProperty = property.FindPropertyRelative( "m_backing" );
      var listView = new ListView
      {
        headerTitle = property.displayName,
        showFoldoutHeader = true,
        showAddRemoveFooter = true,
        showBoundCollectionSize = false,
        showBorder = true,
        showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly,
        reorderable = true,
        reorderMode = ListViewReorderMode.Animated,
        selectionType = SelectionType.Single,
        virtualizationMethod = CollectionVirtualizationMethod.DynamicHeight
      };

      listView.overridingAddButtonBehavior = ( _, _ ) => {
        var menu = new GenericMenu();
        AddMenuItem<Accelerometer>( menu, "Accelerometer", backingProperty, listView );
        AddMenuItem<AGXUnity.Sensor.Gyroscope>( menu, "Gyroscope", backingProperty, listView );
        AddMenuItem<Magnetometer>( menu, "Magnetometer", backingProperty, listView );
        menu.ShowAsContext();
      };

      listView.BindProperty( backingProperty );
      return listView;
    }

    private static void AddMenuItem<T>( GenericMenu menu,
                                        string label,
                                        SerializedProperty backingProperty,
                                        ListView listView )
      where T : ImuSensorSubcomponent, new()
    {
      menu.AddItem( new GUIContent( label ), false, () => {
        var serializedObject = backingProperty.serializedObject;
        serializedObject.UpdateIfRequiredOrScript();
        Undo.RecordObjects( serializedObject.targetObjects, $"Add {label}" );
        backingProperty.arraySize++;
        backingProperty.GetArrayElementAtIndex( backingProperty.arraySize - 1 ).managedReferenceValue = new T();
        serializedObject.ApplyModifiedProperties();
        listView.RefreshItems();
      } );
    }
  }
}
