using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// In general UnityEngine objects are ignored in our custom inspector.
  /// This attribute enables UnityEngine objects to be shown in the editor.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class ShowInInspector : Attribute
  {
    public ShowInInspector() { }
  }

  /// <summary>
  /// Disable changes of field or property in the Inspector during runtime.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class DisableInRuntimeInspectorAttribute : Attribute
  {
  }

  /// <summary>
  /// Hide field or property in the Inspector during runtime.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class HideInRuntimeInspectorAttribute : Attribute
  {
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class InspectorPriorityAttribute : Attribute
  {
    public int Priority { get; private set; }

    public InspectorPriorityAttribute( int priority )
    {
      Priority = priority;
    }
  }

  /// <summary>
  /// Ignore synchronization of properties during initialize
  /// of a ScriptComponent/Asset.
  /// </summary>
  [AttributeUsage( AttributeTargets.Property )]
  public class IgnoreSynchronizationAttribute : Attribute
  {
  }

  /// <summary>
  /// Slider in inspector for float with given min and max value.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class FloatSliderInInspector : Attribute
  {
    private float m_min = 0.0f;
    private float m_max = 0.0f;

    public float Min { get { return m_min; } }
    public float Max { get { return m_max; } }

    public FloatSliderInInspector( float min, float max )
    {
      m_min = min;
      m_max = max;
    }
  }

  /// <summary>
  /// Attribute for method to be executed from a button in the editor.
  /// </summary>
  [AttributeUsage( AttributeTargets.Method, AllowMultiple = false )]
  public class InvokableInInspectorAttribute : Attribute
  {
    public InvokableInInspectorAttribute( string label = "", bool onlyInStatePlay = false )
    {
      Label = label;
      OnlyInStatePlay = onlyInStatePlay;
    }

    public string Label = string.Empty;
    public bool OnlyInStatePlay = false;
  }

  /// <summary>
  /// Adds separator line in the inspector before the field/property
  /// that carries this attribute.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class InspectorSeparatorAttribute : Attribute
  {
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class InspectorGroupBeginAttribute : Attribute
  {
    public string Name;
    public bool DefaultExpanded = false;
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class InspectorGroupEndAttribute : Attribute
  {
  }

  /// <summary>
  /// Attribute for public fields or properties to not receive values
  /// less than (or equal to) zero. It's possible to receive exact
  /// zeros though this is not the default behavior.
  /// </summary>
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class ClampAboveZeroInInspector : Attribute
  {
    private bool m_acceptZero = false;
    public ClampAboveZeroInInspector( bool acceptZero = false ) { m_acceptZero = acceptZero; }

    public bool IsValid( object value )
    {
      Type type = value.GetType();
      if ( type == typeof( Vector4 ) )
        return IsValid( (Vector4)value );
      else if ( type == typeof( Vector3 ) )
        return IsValid( (Vector3)value );
      else if ( type == typeof( Vector2 ) )
        return IsValid( (Vector2)value );
      else if ( type == typeof( Vector3Int ) )
        return IsValid( (Vector3Int)value );
      else if ( type == typeof( Vector2Int ) )
        return IsValid( (Vector2Int)value );
      else if ( type == typeof( DefaultAndUserValueFloat ) ) {
        DefaultAndUserValueFloat val = (DefaultAndUserValueFloat)value;
        return val.Value > 0 || ( m_acceptZero && val.Value == 0 );
      }
      else if ( type == typeof( DefaultAndUserValueVector3 ) ) {
        DefaultAndUserValueVector3 val = (DefaultAndUserValueVector3)value;
        return IsValid( (Vector3)val.Value );
      }
      else if ( type == typeof( int ) )
        return (int)value > 0 || ( m_acceptZero && (int)value == 0 );
      else if ( type == typeof( Vector2Int ) )
        return IsValid( (Vector2Int)value );
      else if ( type == typeof( Vector3Int ) )
        return IsValid( (Vector3Int)value );
      else if ( value is IComparable ) {
        int returnCheck = m_acceptZero ? -1 : 0;
        // CompareTo returns 0 if the values are equal.
        return ( value as IComparable ).CompareTo( 0.0f ) > returnCheck;
      }
      else if ( type == typeof( float ) )
        return (float)value > 0 || ( m_acceptZero && (float)value == 0 );
      else if ( type == typeof( double ) )
        return (double)value > 0 || ( m_acceptZero && (double)value == 0 );
      else if ( type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof( OptionalOverrideValue<> ) )
        return OptionalOverrideIsValid( value );
      return true;
    }

    public bool IsValid( Vector4 value )
    {
      return ( value[ 0 ] > 0 || ( m_acceptZero && value[ 0 ] == 0 ) ) &&
             ( value[ 1 ] > 0 || ( m_acceptZero && value[ 1 ] == 0 ) ) &&
             ( value[ 2 ] > 0 || ( m_acceptZero && value[ 2 ] == 0 ) ) &&
             ( value[ 3 ] > 0 || ( m_acceptZero && value[ 3 ] == 0 ) );
    }

    public bool IsValid( Vector3 value )
    {
      return ( value[ 0 ] > 0 || ( m_acceptZero && value[ 0 ] == 0 ) ) &&
             ( value[ 1 ] > 0 || ( m_acceptZero && value[ 1 ] == 0 ) ) &&
             ( value[ 2 ] > 0 || ( m_acceptZero && value[ 2 ] == 0 ) );
    }

    public bool IsValid( Vector2 value )
    {
      return ( value[ 0 ] > 0 || ( m_acceptZero && value[ 0 ] == 0 ) ) &&
             ( value[ 1 ] > 0 || ( m_acceptZero && value[ 1 ] == 0 ) );
    }

    public bool IsValid( Vector3Int value )
    {
      return ( value[ 0 ] > 0 || ( m_acceptZero && value[ 0 ] == 0 ) ) &&
             ( value[ 1 ] > 0 || ( m_acceptZero && value[ 1 ] == 0 ) ) &&
             ( value[ 2 ] > 0 || ( m_acceptZero && value[ 2 ] == 0 ) );
    }

    public bool IsValid( Vector2Int value )
    {
      return ( value[ 0 ] > 0 || ( m_acceptZero && value[ 0 ] == 0 ) ) &&
             ( value[ 1 ] > 0 || ( m_acceptZero && value[ 1 ] == 0 ) );
    }

    public bool OptionalOverrideIsValid( object value )
    {
      var wrappedType = value.GetType().GenericTypeArguments[ 0 ];
      var ooType = typeof( OptionalOverrideValue<> ).MakeGenericType( wrappedType );
      var wrappedVal = ooType.GetProperty( "OverrideValue" ).GetValue( value );
      var validator = this.GetType().GetMethod( "IsValid", new Type[] { wrappedType } );
      if ( validator == null )
        return true;
      return (bool)validator.Invoke( this, new object[] { wrappedVal } );
    }
  }

  [AttributeUsage( AttributeTargets.Class, AllowMultiple = false )]
  public class DoNotGenerateCustomEditor : Attribute
  {
    public DoNotGenerateCustomEditor()
    {
    }
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class AllowRecursiveEditing : Attribute
  {
    public AllowRecursiveEditing()
    {
    }
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class StringAsFilePicker : Attribute
  {
    public bool IsFolder { get; private set; }
    public StringAsFilePicker( bool IsFolder = false ) { this.IsFolder = IsFolder; }
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class DynamicallyShowInInspector : Attribute
  {
    public DynamicallyShowInInspector( string name, bool isMethod = false, bool invert = false )
    {
      Name = name;
      IsMethod = isMethod;
      Invert = invert;
    }

    public bool IsMethod { get; private set; }
    public string Name { get; private set; }
    public bool Invert { get; private set; }
  }

  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false )]
  public class RuntimeValue : Attribute
  {
    public string Unit { get; private set; }
    public RuntimeValue( string unit = "" ) { this.Unit = unit; }
  }

}
