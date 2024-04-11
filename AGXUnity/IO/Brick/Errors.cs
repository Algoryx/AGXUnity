namespace AGXUnity.IO.BrickIO
{
  enum AgxUnityBrickErrors
  {
    Unimplemented = 1,
    NullChild = 2,
  }
  public class UnityBrickErrorFormatter : Brick.ErrorFormatter
  {
    private string formatMessage( string message, Brick.Error error )
    {
      return $"{error.getSourceId()}:{error.getLine()}:{error.getColumn()} {message}";
    }
    public override string format( Brick.Error error )
    {
      return error.getErrorCode() switch
      {
        (ulong)AgxUnityBrickErrors.Unimplemented => formatMessage( "The specified model is not implemented by the mapper", error ),
        (ulong)AgxUnityBrickErrors.NullChild => formatMessage( "The child object could not be mapped", error ),
        _ => base.format( error )
      };
    }
  }
}