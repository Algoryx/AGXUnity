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
    private T m_accumulatedValue;

    public int Size => m_frameSize;

    public MovingAverage(int size)
    {
      m_frameSize = size;
      m_samples = new Queue<T>(size);
    }

    public T Value => (dynamic)m_accumulatedValue / (float)m_samples.Count;

    public void Add(dynamic entry)
    {
      m_accumulatedValue += entry;
      m_samples.Enqueue(entry);

      if (m_samples.Count > m_frameSize)
      {
        dynamic last = m_samples.Dequeue();
        m_accumulatedValue -= last;
      }
    }
  }
}