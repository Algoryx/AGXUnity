namespace AGXUnity
{
  /// <summary>
  /// Range real object containing min (default -infinity) and
  /// max value (default +infinity).
  /// </summary>
  [System.Serializable]
  public struct RangeReal
  {
    /// <summary>
    /// Get or set min value less than max value.
    /// </summary>
    public float Min;

    /// <summary>
    /// Get or set max value larger than min value.
    /// </summary>
    public float Max;

    /// <summary>
    /// Convert to native type agx.RangeReal given current min and max.
    /// </summary>
    public agx.RangeReal Native { get { return new agx.RangeReal( Min, Max ); } }

    /// <summary>
    /// Construct given minimum and maximum values.
    /// </summary>
    /// <param name="min">Minimum/Lower bound value.</param>
    /// <param name="max">Maximum/Upper bound value.</param>
    public RangeReal( float min, float max )
    {
      Min = min;
      Max = max;
    }

    /// <summary>
    /// Construct given value of negative and positive bound, i.e.,
    /// range = (-value, value).
    /// </summary>
    /// <param name="value">Negative and positive value of the range.</param>
    public RangeReal( float value )
      : this( -UnityEngine.Mathf.Abs( value ), UnityEngine.Mathf.Abs( value ) )
    {
    }

    /// <summary>
    /// Copy constructor.
    /// </summary>
    public RangeReal( RangeReal other )
    {
      Min = other.Min;
      Max = other.Max;
    }

    /// <summary>
    /// Construct given native range real.
    /// </summary>
    /// <param name="native">Native range real to copy values from.</param>
    public RangeReal( agx.RangeReal native )
    {
      Min = System.Convert.ToSingle( native.lower() );
      Max = System.Convert.ToSingle( native.upper() );
    }
  }
}
