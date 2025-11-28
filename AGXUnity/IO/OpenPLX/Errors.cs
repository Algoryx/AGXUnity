using System.Linq;

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
    IncompatibleImportType = 9,
    UnmappableRootModel = 10,
    FileDoesNotExist = 11,
  }



  public class BaseError : openplx.Error
  {
    public struct ErrorData
    {
      public uint fromLine;
      public uint toLine;
      public uint fromColumn;
      public uint toColumn;
      public string sourceID;
    };
    public static ErrorData CreateErrorData( openplx.Core.Object source )
    {
      var subName = source.getName().Substring(source.getName().LastIndexOf('.') + 1);

      openplx.Token tok = null;
      openplx.Document document = null;
      if ( source.getOwner() != null ) {
        var ownerType = source.getOwner().getType();
        openplx.Node member = ownerType?.findFirstMember( subName );
        if ( member != null ) {
          tok = member.isVarDeclaration() ? member?.asVarDeclaration()?.getNameToken() : member?.asVarAssignment()?.getTargetSegments()?.Last();
          document = member.isVarDeclaration() ? member?.asVarDeclaration()?.getOwningDocument() : member?.asVarAssignment()?.getOwningDocument();
        }
      }
      else {
        tok = source.getType().getNameToken();
        document = source.getType().getOwningDocument();
      }
      if ( tok == null || document == null ) {
        // Fallback to declaration of root model
        var owner = source.getOwner();
        while ( owner.getOwner() != null )
          owner = owner.getOwner();
        tok = owner.getType().getNameToken();
        document = owner.getType().getOwningDocument();
      }
      return new ErrorData
      {
        fromLine    = (uint)tok.line,
        toLine      = (uint)tok.line,
        fromColumn  = (uint)tok.column,
        toColumn    = (uint)tok.column,
        sourceID    = document?.getSourceId(),
      };
    }

    protected BaseError( openplx.Core.Object source, AgxUnityOpenPLXErrors code )
      : this( CreateErrorData( source ), code )
    { }

    private BaseError( ErrorData data, AgxUnityOpenPLXErrors code )
      : base( (uint)code, data.fromLine, data.fromColumn, data.toLine, data.toColumn, data.sourceID )
    { }

    protected BaseError( string sourceID, AgxUnityOpenPLXErrors code )
      : base( (uint)code, 1, 1, 1, 1, sourceID )
    { }
  }

  public class UnimplementedError : BaseError
  {
    public UnimplementedError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.Unimplemented )
    { }

    protected override string createErrorMessage() => "The specified model is not implemented by the mapper";
  }

  public class NullChildError : BaseError
  {
    public NullChildError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.NullChild )
    { }

    protected override string createErrorMessage() => "The child object could not be mapped";
  }

  public class LocalOffsetNotSupportedError : BaseError
  {
    public LocalOffsetNotSupportedError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.LocalOffsetNotSupported )
    { }

    protected override string createErrorMessage() => "Specifying a local offset is not supported by AGXUnity";
  }

  public class MissingMaterialError : BaseError
  {
    public MissingMaterialError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.MissingMaterial )
    { }

    protected override string createErrorMessage() => "The specified material could not be found";
  }

  public class DuplicateMaterialPairForSurfaceContactModelDefinitionError : BaseError
  {
    public DuplicateMaterialPairForSurfaceContactModelDefinitionError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.DuplicateMaterialPairForSurfaceContactModelDefinition )
    { }

    protected override string createErrorMessage() => "The specified material pair appears in more than one SurfaceContact.Model definition";
  }

  public class InvalidDefomationTypeError : BaseError
  {
    public InvalidDefomationTypeError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.InvalidDefomationType )
    { }

    protected override string createErrorMessage() => "AGXUnity does not support the specified deformation type";
  }

  public class UnsupportedFrictionModelError : BaseError
  {
    public UnsupportedFrictionModelError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.UnsupportedFrictionModel )
    { }

    protected override string createErrorMessage() => "AGXUnity only supports dry friction";
  }

  public class RigidBodyOwnerNotSystemError : BaseError
  {
    public RigidBodyOwnerNotSystemError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.RigidBodyOwnerNotSystem )
    { }

    protected override string createErrorMessage() => "RigidBody must be owned by a Physics3D.System";
  }

  public class IncompatibleImportTypeError : BaseError
  {
    public IncompatibleImportTypeError( string path )
      : base( path, AgxUnityOpenPLXErrors.IncompatibleImportType )
    { }

    protected override string createErrorMessage() => "Imported model could not be mapped to the provided type";
  }

  public class UnmappableRootModelError : BaseError
  {
    public UnmappableRootModelError( openplx.Core.Object source )
      : base( source, AgxUnityOpenPLXErrors.UnmappableRootModel )
    { }

    protected override string createErrorMessage() => "The root openplx model could not be mapped to an AGXUnity representation";
  }

  public class FileDoesNotExistError : BaseError
  {
    private string m_path;

    public FileDoesNotExistError( openplx.Core.Object source, string path )
      : base( source, AgxUnityOpenPLXErrors.FileDoesNotExist )
    {
      m_path = path;
    }

    public FileDoesNotExistError( string path )
      : base( path, AgxUnityOpenPLXErrors.FileDoesNotExist )
    {
      m_path = path;
    }

    protected override string createErrorMessage() => $"Provided file does not exist: '{m_path}'";
  }
}
