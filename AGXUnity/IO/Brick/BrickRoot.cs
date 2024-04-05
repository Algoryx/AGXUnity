using Brick.Core.Api;
using UnityEngine;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  [Icon( "Assets/Brick/brick-icon.png" )]
  public class BrickRoot : ScriptComponent
  {

    [field: SerializeField]
    public string BrickAssetPath { get; set; }

    public string BrickFile => BrickAssetPath.Replace( "Assets/", BrickImporter.BrickDir + "/" );

    public Object Native { get; private set; }

    public GameObject FindMappedObject( string declaration )
    {
      var relativeDecl = declaration.Replace( '.', '/' );
      var toFind = gameObject.name + '/' + relativeDecl;
      return GameObject.Find( toFind );
    }

    protected override bool Initialize()
    {
      Native = BrickImporter.ParseBrickSource( BrickFile, _report_errors );

      return base.Initialize();
    }

    private static Object _report_errors( Object evalTree, BrickContext brick_pub_ctx )
    {
      UnityBrickErrorFormatter error_formatter = new UnityBrickErrorFormatter();
      foreach ( var error in brick_pub_ctx.getErrors() )
        Debug.LogError( error_formatter.format( error ) );
      if ( evalTree == null && brick_pub_ctx.getErrors().Count == 0 )
        Debug.LogError( "Evaluation failed, but without any reported errors." );
      if ( brick_pub_ctx.getErrors().Count != 0 ) {
        Debug.LogError( "Errors found - ignoring input." );
        return null;
      }

      return evalTree;
    }

    class UnityBrickErrorFormatter : Brick.ErrorFormatter
    {
      public override string format( Brick.Error error )
      {
        //switch ( error.getErrorCode() ) {
        //  case AGXBrickError::ModelWasNotSimulatable:
        //    return formatMessage( "Expected Model to inherit one of Body, Geometry or System", error );
        //  case AGXBrickError::MissingConnectedBody:
        //    return formatMessage( "Hinge must have at least one mateconnector belonging to a RigidBody", error );
        //  case AGXBrickError::AgxIsNotInitialized:
        //    return formatMessage( "AGX is not initialized, did you forget agxInit?", error );
        //  case AGXBrickError::InvalidMateConnectorAxis:
        //    return formatMessage( "main_axis and normal in MateConnector must not be parallel", error );
        //  case AGXBrickError::PathNotAbsolute:
        //    return formatMessage( "Path is not absolute", error );
        //  case AGXBrickError::InvalidObjFile:
        //    return formatMessage( "Invalid .obj file", error );
        //  default:
        return base.format( error );
        //}
      }
    }
  }
}
