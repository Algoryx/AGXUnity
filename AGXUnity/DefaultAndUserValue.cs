using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Object handling values (float or Vector3) which has one default and
  /// one user defined value. This object enables switching between
  /// the two, e.g., mass and inertia in MassProperties.
  /// </summary>
  [Serializable]
  public class DefaultAndUserValue<T>
    where T : struct
  {
    [SerializeField]
    private bool m_defaultToggle = true;
    [SerializeField]
    private T m_defaultValue;
    [SerializeField]
    private T m_userValue;

    /// <summary>
    /// Construct given default and user value.
    /// </summary>
    /// <param name="defaultValue">Default value.</param>
    /// <param name="userValue">User specified value.</param>
    public DefaultAndUserValue( T defaultValue, T userValue )
    {
      m_defaultValue = defaultValue;
      m_userValue = userValue;
    }

    /// <summary>
    /// Use default value toggle. True to use default value when
    /// accessing this.Value.
    /// </summary>
    public bool UseDefault
    {
      get { return m_defaultToggle; }
      set
      {
        OnUseDefaultToggle( value );
        m_defaultToggle = value;
      }
    }

    /// <summary>
    /// The default value to use if UseDefault == true.
    /// </summary>
    public T DefaultValue
    {
      get { return m_defaultValue; }
      set
      {
        m_defaultValue = value;
      }
    }

    /// <summary>
    /// The user value to use if UseDefault == false.
    /// </summary>
    public T UserValue
    {
      get { return m_userValue; }
      set
      {
        OnNewUserValue( value );
        m_userValue = value;
      }
    }

    /// <summary>
    /// Callback when button "Update" has been pressed.
    /// </summary>
    public Action OnForcedUpdate = delegate { };

    public Action<T> OnNewUserValue = delegate { };
    public Action<bool> OnUseDefaultToggle = delegate { };

    /// <summary>
    /// Copies values from source to this.
    /// </summary>
    /// <param name="source">Source object.</param>
    public void CopyFrom( DefaultAndUserValue<T> source )
    {
      m_defaultToggle = source.m_defaultToggle;
      m_defaultValue = source.m_defaultValue;
      m_userValue = source.m_userValue;
    }

    /// <summary>
    /// Assigning this property when UseDefault == true will NOT change any value.
    /// Use explicit DefaultValue and UserValue for that. If UseDefault == false
    /// the user value will be changed.
    /// </summary>
    public T Value
    {
      get { return UseDefault ? DefaultValue : UserValue; }
      set
      {
        if ( !UseDefault )
          UserValue = value;
      }
    }
  }

  /// <summary>
  /// Object handling values float which has one default and
  /// one user defined value. This object enables switching between
  /// the two, e.g., mass in MassProperties.
  /// </summary>
  [Serializable]
  public class DefaultAndUserValueFloat : DefaultAndUserValue<float>
  {
    public DefaultAndUserValueFloat()
      : base( 1.0f, 1.0f ) { }

    public override bool Equals( object obj )
    {
      var other = obj as DefaultAndUserValueFloat;
      return other != null &&
             other.UseDefault == UseDefault &&
             Utils.Math.Approximately( other.Value, Value );
    }

    public override int GetHashCode() => base.GetHashCode();
  }

  /// <summary>
  /// Object handling values Vector3 which has one default and
  /// one user defined value. This object enables switching between
  /// the two, e.g., inertia in MassProperties.
  /// </summary>
  [Serializable]
  public class DefaultAndUserValueVector3 : DefaultAndUserValue<Vector3>
  {
    public DefaultAndUserValueVector3()
      : base( new Vector3( 1, 1, 1 ), new Vector3( 1, 1, 1 ) )
    {
    }

    public DefaultAndUserValueVector3( Vector3 defaultValue, Vector3 userValue )
      : base( defaultValue, userValue )
    {
    }

    public override bool Equals( object obj )
    {
      var other = obj as DefaultAndUserValueVector3;
      return other != null &&
             other.UseDefault == UseDefault &&
             Utils.Math.Approximately( other.Value.x, Value.x ) &&
             Utils.Math.Approximately( other.Value.y, Value.y ) &&
             Utils.Math.Approximately( other.Value.z, Value.z );
    }

    public override int GetHashCode() => base.GetHashCode();
  }
}
