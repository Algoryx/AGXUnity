namespace AGXUnity
{
  /// <summary>
  /// Our own type of exception for filtering.
  /// </summary>
  public class Exception : System.Exception
  {
    public Exception() : base() {}
    public Exception( string message ) : base( message ) {}
    public Exception( string message, System.Exception inner ) : base( message, inner ) {}
  }

  public class UrdfIOException : System.Xml.XmlException
  {
    public UrdfIOException( string message )
      : base( message )
    {
    }
  }
}
