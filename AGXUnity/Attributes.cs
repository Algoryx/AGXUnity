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
  [AttributeUsage( AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = false)]
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
  }

  [AttributeUsage( AttributeTargets.Class, AllowMultiple = false)]
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

  /// <summary>
  /// Attribute for marking methods to be called during the editor update. 
  /// The containing script still needs to be added to the scene for the method to be picked up
  /// by the manager, even if the method is static. If the method is static the method will be 
  /// called once for each instance. To call the static method once for each scene instance,
  /// set StatticCallMultiple to true in the attribute constructor
  /// 
  /// Note that this update is only called in Edit Mode since the regular unity Update signal serves the
  /// same purpose in play mode. To update in both places, simply add this attribute to the Update method.
  /// </summary>
  [AttributeUsage( AttributeTargets.Method, AllowMultiple = false )]
  public class EditorUpdateAttribute : Attribute
  {
    /// <summary>
    /// Whether or not to call a static method once for each reference to the script in the scene.
    /// Has no effect if the method is non-static.
    /// </summary>
    public bool StaticCallMultiple { get;set; }

    /// <summary>
    /// Attribute for marking methods to be called during the editor update. 
    /// The containing script still needs to be added to the scene for the method to be picked up
    /// by the manager, even if the method is static.
    /// </summary>
    /// <param name="StaticCallMultiple">Whether or not to call a static method once for each reference to the script in the scene. Has no effect if the method is non-static.</param>
    public EditorUpdateAttribute(bool StaticCallMultiple = false) 
    {
      this.StaticCallMultiple = StaticCallMultiple;
    }
  }
}
