namespace AGXUnity
{
  /// <summary>
  /// Array segment for structs with ref enumerator. E.g.,
  /// foreach ( var data in arraySegment ) {
  ///   data.Foo = 2.0f; // Error: Cannot modify member ...
  ///   Debug.Log( data.Foo ) // Ok.
  /// }
  /// 
  /// foreach ( ref var data in arraySegment ) {
  ///   data.Foo = 2.0f; // Ok.
  ///   Debug.Log( data.Foo ) // Ok.
  /// }
  /// </summary>
  /// <typeparam name="T">Any primitive type.</typeparam>
  public struct RefArraySegment<T>
    where T : struct
  {
    /// <summary>
    /// The array this segment belongs to.
    /// </summary>
    public T[] Array { get; private set; }

    /// <summary>
    /// Start index offset in the Array.
    /// </summary>
    public int Offset { get; private set; }

    /// <summary>
    /// Number of elements this segment covers.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Access Offset + <paramref name="index"/> of Array.
    /// </summary>
    /// <param name="index">Local index to this segment.</param>
    /// <returns>Element at Offset + <paramref name="index"/> in the Array.</returns>
    public ref T this[ int index ]
    {
      get
      {
        return ref Array[ Offset + index ];
      }
    }

    /// <summary>
    /// Construct given array, index offset in the array and number of
    /// elements (count) of this segment.
    /// </summary>
    /// <param name="array">Array this segment indexes into.</param>
    /// <param name="offset">Start index offset in <paramref name="array"/>.</param>
    /// <param name="count">Number of elements this segment covers.</param>
    public RefArraySegment( T[] array, int offset, int count )
    {
      Array = array;
      Offset = offset;
      Count = count;
    }

    public Enumerator GetEnumerator()
    {
      return new Enumerator()
      {
        Segment = this
      };
    }

    public struct Enumerator
    {
      public RefArraySegment<T> Segment
      {
        get { return m_segment; }
        set
        {
          m_segment = value;
          m_index = 0;
        }
      }

      public bool MoveNext()
      {
        return m_index < Segment.Count;
      }

      public ref T Current
      {
        get
        {
          return ref Segment[ m_index++ ];
        }
      }

      private int m_index;
      private RefArraySegment<T> m_segment;
    }
  }
}
