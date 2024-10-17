using System;
using System.Collections.Generic;

namespace AGXUnity.Utils
{
  /// <summary>
  /// Used to average a numeric value over a number of frames. Don't use a non-numeric type please
  /// </summary>
  public class MovingAverage<T>
  {
    private readonly Queue<T> m_samples;
    private readonly int m_frameSize;
    private double m_accumulatedValue;

    public int Size => m_frameSize;

    public MovingAverage( int size )
    {
      m_frameSize = size;
      m_samples = new Queue<T>( size );
    }

    public double Value => (double)m_accumulatedValue / m_samples.Count;

    public void Add( T entry )
    {
      m_accumulatedValue += Convert.ToDouble( entry );
      m_samples.Enqueue( entry );

      if ( m_samples.Count > m_frameSize ) {
        double last = Convert.ToDouble(m_samples.Dequeue());
        m_accumulatedValue -= last;
      }
    }
  }
}
