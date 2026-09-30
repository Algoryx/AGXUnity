using AGXUnityEditor.Utils;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Editors
{
  public class DynamicallyShowField : VisualElement
  {
    MemberInfo m_checkedMember;
    SerializedProperty m_wrappedProperty;
    SerializedProperty m_instanceProperty;
    bool m_invert;

    VisualElement m_wrappedElement;

    public DynamicallyShowField( SerializedProperty prop,
                                 MemberInfo checkedMember,
                                 bool invert = false )
    {
      m_checkedMember = checkedMember;
      m_wrappedProperty = prop.Copy();
      m_instanceProperty = m_wrappedProperty.GetParent();
      m_invert = invert;

      if ( m_instanceProperty != null ) {
        contentContainer.TrackPropertyValue( m_instanceProperty );
        contentContainer.RegisterCallback<SerializedPropertyChangeEvent>( _ => schedule.Execute( Update ) );
      }
      else {
        contentContainer.TrackSerializedObjectValue( prop.serializedObject );
        contentContainer.RegisterCallback<SerializedObjectChangeEvent>( _ => schedule.Execute( Update ) );
      }

      m_wrappedElement = new PropertyField( prop );
      contentContainer.Add( m_wrappedElement );

      Update();
    }

    private void Update()
    {
      bool dynamicShow = false;
      object instance = m_instanceProperty?.boxedValue;
      if ( m_checkedMember.MemberType == MemberTypes.Method ) {
        var method = (MethodInfo)m_checkedMember;
        if ( !method.IsStatic ) {
          if ( instance != null )
            dynamicShow = (bool)method.Invoke( instance, new object[] { } );
          else
            dynamicShow = m_wrappedProperty.serializedObject.targetObjects.All( t => (bool)method.Invoke( t, new object[] { } ) );
        }
        else
          dynamicShow = (bool)method.Invoke( null, new object[] { } );
      }
      else if ( m_checkedMember.MemberType == MemberTypes.Property ) {
        var property = (PropertyInfo)m_checkedMember;

        if ( instance != null )
          dynamicShow = (bool)property.GetValue( instance );
        else
          dynamicShow = m_wrappedProperty.serializedObject.targetObjects.All( t => (bool)property.GetValue( t ) );
      }

      if ( m_invert )
        dynamicShow = !dynamicShow;

      if ( dynamicShow )
        m_wrappedElement.style.display = DisplayStyle.Flex;
      else
        m_wrappedElement.style.display = DisplayStyle.None;
    }
  }
}
