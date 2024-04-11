using Brick;
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

      if ( Native == null ) {
        Debug.LogError( $"Failed to initialize Brick object '{name}'", this );
        return false;
      }

      return base.Initialize();
    }

    private static void _report_errors( Error error )
    {
      UnityBrickErrorFormatter error_formatter = new UnityBrickErrorFormatter();
      Debug.LogError( error_formatter.format( error ) );
    }
  }
}
