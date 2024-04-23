namespace AGXUnity.IO.BrickIO
{
  public enum AgxUnityBrickErrors
  {
    Unimplemented = 1,
    NullChild = 2,
    LocalOffsetNotSupported = 3,
    MissingMaterial = 4,
    DuplicateMaterialPairForSurfaceContactModelDefinition = 5,
    InvalidDefomationType = 6,
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
        (ulong)AgxUnityBrickErrors.LocalOffsetNotSupported => formatMessage( "Specifying a local offset is not supported by AGXUnity", error ),
        (ulong)AgxUnityBrickErrors.MissingMaterial => formatMessage( "The specified material could not be found", error ),
        (ulong)AgxUnityBrickErrors.DuplicateMaterialPairForSurfaceContactModelDefinition => formatMessage( "The specified material pair appears in more than one SurfaceContact.Model definition", error ),
        (ulong)AgxUnityBrickErrors.InvalidDefomationType => formatMessage( "AGXUnity does not support the specified deformation type", error ),
        _ => base.format( error )
      };
    }
  }
}