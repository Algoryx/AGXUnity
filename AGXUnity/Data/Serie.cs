using System.Collections;
using System.Collections.Generic;

namespace AGXUnity.Data
{
  public class Serie : IEnumerable<float>
  {
    public float[] Values { get { return m_values.ToArray(); } }

    public int Length { get { return m_values.Count; } }

    public float Min { get; private set; }

    public float Max { get; private set; }

    public float Delta { get { return Max - Min; } }

    public bool IsDirty { get; private set; } = false;

    public float this[int index]
    {
      get { return m_values[ index ]; }
      set
      {
        IsDirty = IsDirty || m_values[ index ] != value;
        m_values[ index ] = value;
      }
    }

    public void Add( float value )
    {
      IsDirty = true;

      m_values.Add( value );

      if ( m_values.Count == 1 )
        Min = Max = value;
      else if ( value.CompareTo( Min ) < 0 )
        Min = value;
      else if ( value.CompareTo( Max ) > 0 )
        Max = value;
    }

    public void ResetDirty()
    {
      IsDirty = false;
    }

    public IEnumerator<float> GetEnumerator() => m_values.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => m_values.GetEnumerator();

    private List<float> m_values = new List<float>();
  }
}
