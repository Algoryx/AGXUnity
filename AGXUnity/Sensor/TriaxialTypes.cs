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
    public enum ConfigurationMode
    {
      MaxRange,
      EqualAxisRanges,
      IndividualAxisRanges
    }

    [SerializeField]
    public ConfigurationMode Mode = ConfigurationMode.MaxRange;

    [SerializeField]
    public Vector2 EqualAxesRange = new(float.MinValue, float.MaxValue);

    [SerializeField]
    public Vector2 RangeX = new(float.MinValue, float.MaxValue);
    public Vector2 RangeY = new(float.MinValue, float.MaxValue);
    public Vector2 RangeZ = new(float.MinValue, float.MaxValue);

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
