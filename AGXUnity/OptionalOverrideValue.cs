using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Object handling values (float or Vector3) which can optionally be overridden.
  /// This object handles switching between overridden and non-overridden states.
  /// Example usage of this class can be seen in BottomContactThreshold in DeformableTerrainShovel.
  /// </summary>
  [Serializable]
  public class OptionalOverrideValue<T>
    where T : struct
  {
    [SerializeField]
    private bool m_overrideToggle;
    [SerializeField]
    private T m_overrideValue;

    /// <summary>
    /// Construct given the override value.
    /// </summary>
    /// <param name="overrideValue">The value to use when overriding is enabled.</param>
    public OptionalOverrideValue( T overrideValue, bool overriden = false )
    {
      m_overrideValue = overrideValue;
      m_overrideToggle = overriden;
    }

    /// <summary>
    /// Whether or not to use the override value or not. True uses the override value.
    /// </summary>
    public bool UseOverride
    {
      get { return m_overrideToggle; }
      set
      {
        m_overrideToggle = value;
        OnUseOverrideToggle( value );
      }
    }

    /// <summary>
    /// The override value to use when UseOverride == true.
    /// </summary>
    public T OverrideValue
    {
      get { return m_overrideValue; }
      set
      {
        m_overrideValue = value;
        OnOverrideValue( value );
      }
    }

    /// <summary>
    /// Chooses from the override value or a provided default based on the UseOverride toggle
    /// </summary>
    /// <param name="def">The default value to use when not using the override</param>
    /// <returns>The override value if used or default value otherwise</returns>
    public T ValueOrDefault( T def )
    {
      if ( !UseOverride )
        return def;
      else return m_overrideValue;
    }

    /// <summary>
    /// Callback when the override value is changed.
    /// </summary>
    public Action<T> OnOverrideValue = delegate { };

    /// <summary>
    /// Callback when UseOverride flag is toggled.
    /// </summary>
    public Action<bool> OnUseOverrideToggle = delegate { };

    /// <summary>
    /// Copies values from source to this.
    /// </summary>
    /// <param name="source">Source object.</param>
    public void CopyFrom( OptionalOverrideValue<T> source )
    {
      m_overrideToggle = source.m_overrideToggle;
      m_overrideValue = source.m_overrideValue;
    }
  }
}
