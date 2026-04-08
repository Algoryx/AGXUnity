using System;
using UnityEngine;

namespace AGXUnity.Sensor
{
  /// <summary>
  /// Helper class for triaxial range type configurations
  /// </summary>
  [Serializable]
  public class TriaxialRangeData
  {
    [NonSerialized]
    private Action m_onChanged = null;

    public enum ConfigurationMode
    {
      MaxRange,
      EqualAxisRanges,
      IndividualAxisRanges
    }

    [SerializeField]
    private ConfigurationMode m_mode = ConfigurationMode.MaxRange;
    public ConfigurationMode Mode
    {
      get => m_mode;
      set
      {
        m_mode = value;
        m_onChanged?.Invoke();
      }
    }

    [SerializeField]
    private Vector2 m_equalAxesRange = new( float.MinValue, float.MaxValue );
    public Vector2 EqualAxesRange
    {
      get => m_equalAxesRange;
      set
      {
        m_equalAxesRange = value;
        m_onChanged?.Invoke();
      }
    }

    [SerializeField]
    private Vector2 m_rangeX = new( float.MinValue, float.MaxValue );
    public Vector2 RangeX
    {
      get => m_rangeX;
      set
      {
        m_rangeX = value;
        m_onChanged?.Invoke();
      }
    }

    [SerializeField]
    private Vector2 m_rangeY = new( float.MinValue, float.MaxValue );
    public Vector2 RangeY
    {
      get => m_rangeY;
      set
      {
        m_rangeY = value;
        m_onChanged?.Invoke();
      }
    }

    [SerializeField]
    private Vector2 m_rangeZ = new( float.MinValue, float.MaxValue );
    public Vector2 RangeZ
    {
      get => m_rangeZ;
      set
      {
        m_rangeZ = value;
        m_onChanged?.Invoke();
      }
    }

    internal void SetOnChanged( Action onChanged )
    {
      m_onChanged = onChanged;
    }

    public agxSensor.TriaxialRange GenerateTriaxialRange()
    {
      switch ( Mode ) {
        case ConfigurationMode.MaxRange:
          return new agxSensor.TriaxialRange( new agx.RangeReal( float.MinValue, float.MaxValue ) );
        case ConfigurationMode.EqualAxisRanges:
          return new agxSensor.TriaxialRange( new agx.RangeReal( EqualAxesRange.x, EqualAxesRange.y ) );
        case ConfigurationMode.IndividualAxisRanges:
          return new agxSensor.TriaxialRange(
            new agx.RangeReal( RangeX.x, RangeX.y ),
            new agx.RangeReal( RangeY.x, RangeY.y ),
            new agx.RangeReal( RangeZ.x, RangeZ.y ) );
        default:
          return null;
      }
    }
  }
}
