namespace AGXUnity.IO.OpenPLX
{
  public enum AgxUnityOpenPLXErrors
  {
    Unimplemented = 1,
    NullChild = 2,
    LocalOffsetNotSupported = 3,
    MissingMaterial = 4,
    DuplicateMaterialPairForSurfaceContactModelDefinition = 5,
    InvalidDefomationType = 6,
    UnsupportedFrictionModel = 7,
    RigidBodyOwnerNotSystem = 8,
  }
  public class UnityOpenPLXErrorFormatter : openplx.ErrorFormatter
  {
    private string formatMessage( string message, openplx.Error error )
    {
      return $"{error.getSourceId()}:{error.getLine()}:{error.getColumn()} {message}";
    }
    public override string format( openplx.Error error )
    {
      return error.getErrorCode() switch
      {
        (ulong)AgxUnityOpenPLXErrors.Unimplemented => formatMessage( "The specified model is not implemented by the mapper", error ),
        (ulong)AgxUnityOpenPLXErrors.NullChild => formatMessage( "The child object could not be mapped", error ),
        (ulong)AgxUnityOpenPLXErrors.LocalOffsetNotSupported => formatMessage( "Specifying a local offset is not supported by AGXUnity", error ),
        (ulong)AgxUnityOpenPLXErrors.MissingMaterial => formatMessage( "The specified material could not be found", error ),
        (ulong)AgxUnityOpenPLXErrors.DuplicateMaterialPairForSurfaceContactModelDefinition => formatMessage( "The specified material pair appears in more than one SurfaceContact.Model definition", error ),
        (ulong)AgxUnityOpenPLXErrors.InvalidDefomationType => formatMessage( "AGXUnity does not support the specified deformation type", error ),
        (ulong)AgxUnityOpenPLXErrors.UnsupportedFrictionModel => formatMessage( "AGXUnity only supports dry friction", error ),
        (ulong)AgxUnityOpenPLXErrors.RigidBodyOwnerNotSystem => formatMessage( "RigidBody must be owned by a Physics3D.System", error ),
        _ => base.format( error )
      };
    }
  }
}
