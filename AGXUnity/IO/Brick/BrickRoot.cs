using Brick;
using System.Collections.Generic;
using UnityEngine;
using Object = Brick.Core.Object;

namespace AGXUnity.IO.BrickIO
{
  [Icon( "Assets/Brick/brick-icon.png" )]
  public class BrickRoot : ScriptComponent
  {

    /// <summary>
    /// By default, objects have no reference to where their correesponding assets are located on disk since assets do not exist outside of the editor.
    /// Since .brick objects to require a reference to their source files (currently) to load we need to store the path to the corresponding brick file manually
    /// This is set by the <seealso cref="BrickImporter"/>.
    /// </summary>
    [field: SerializeField]
    public string BrickAssetPath { get; set; }

    /// <summary>
    /// In the editor the files will be located in the assets folder (Unless the path is absolute). In this case it is fine loading the path as is.
    /// However, when the application is built, the brick file is copied to a corresponding directory in the build directory and the path of the
    /// brick file needs to be updated accordingly.
    /// </summary>
    public string BrickFile => BrickAssetPath.Replace( "Assets/", BrickImporter.BrickDir + "/" );

    public Object Native { get; private set; }

    private Dictionary<string, object> m_runtimeMap;
    private Dictionary<string, GameObject> m_objectMap;

    public GameObject FindMappedObject( string declaration )
    {
      if(Native != null ) {
        if(m_objectMap.ContainsKey( declaration ))
          return m_objectMap[ declaration ];
        else
          return null;
      }
      else {
        foreach ( var brickObj in GetComponentsInChildren<BrickObject>() )
          if ( brickObj.SourceDeclarations.Contains( declaration ) )
            return brickObj.gameObject;
        return null;
      }
    }

    public object FindRuntimeMappedObject( string declaration )
    {
      return m_runtimeMap.GetValueOrDefault( declaration, null );
    }

    protected override bool Initialize()
    {
      var importer = new BrickImporter();
      importer.ErrorReporter = ReportError;
      Native = importer.ParseBrickSource( BrickFile );

      if ( Native == null ) {
        Debug.LogError( $"Failed to initialize Brick object '{name}'", this );
        return false;
      }

      m_objectMap = new Dictionary<string, GameObject>();
      foreach ( var brickObj in GetComponentsInChildren<BrickObject>() )
        foreach (var decl in brickObj.SourceDeclarations )
          m_objectMap.Add( decl, brickObj.gameObject );

      var RTMapper = new RuntimeMapper();
      RTMapper.PerformRuntimeMapping( this );
      m_runtimeMap = RTMapper.MapperData.RuntimeMap;

      return base.Initialize();
    }

    private static void ReportError( Error error )
    {
      UnityBrickErrorFormatter error_formatter = new UnityBrickErrorFormatter();
      Debug.LogError( error_formatter.format( error ) );
    }
  }
}
