using openplx;
using System.Collections.Generic;
using UnityEngine;
using Object = openplx.Core.Object;

namespace AGXUnity.IO.OpenPLX
{
  [AddComponentMenu( "" )]
  [DisallowMultipleComponent]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#openplx-root" )]
  public class OpenPLXRoot : ScriptComponent
  {

    /// <summary>
    /// By default, objects have no reference to where their correesponding assets are located on disk since assets do not exist outside of the editor.
    /// Since .openplx objects to require a reference to their source files (currently) to load we need to store the path to the corresponding OpenPLX file manually
    /// This is set by the <seealso cref="OpenPLXImporter"/>.
    /// </summary>
    [field: SerializeField]
    public string OpenPLXAssetPath { get; set; }

    public static string TransformOpenPLXPath( string path ) => path.Replace( "Assets/", OpenPLXImporter.OpenPLXRoot + "/" );

    /// <summary>
    /// In the editor the files will be located in the assets folder (Unless the path is absolute). In this case it is fine loading the path as is.
    /// However, when the application is built, the OpenPLX file is copied to a corresponding directory in the build directory and the path of the
    /// OpenPLX file needs to be updated accordingly.
    /// </summary>
    public string OpenPLXFile => TransformOpenPLXPath( OpenPLXAssetPath );

    public Object Native { get; internal set; }

    public Dictionary<string, agx.Referenced> RuntimeMapped { get; private set; } = new Dictionary<string, agx.Referenced>();
    private Dictionary<string, GameObject> m_objectMap;

    public GameObject FindMappedObject( string declaration )
    {
      var relativeDeclaration = declaration.Substring( declaration.IndexOf( '.' ) + 1 );
      declaration = Native.getObject( relativeDeclaration ).getName();
      if ( Native != null ) {
        if ( m_objectMap.ContainsKey( declaration ) )
          return m_objectMap[ declaration ];
        else
          return null;
      }
      else {
        foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() )
          if ( openPLXObj.SourceDeclarations.Contains( declaration ) )
            return openPLXObj.gameObject;
        return null;
      }
    }

    public object FindRuntimeMappedObject( string declaration )
    {
      return RuntimeMapped.GetValueOrDefault( declaration, null );
    }

    public T FindRuntimeMappedObject<T>( string declaration )
      where T : class
    {
      return RuntimeMapped.GetValueOrDefault( declaration, null ) as T;
    }

    protected override bool Initialize()
    {
      if ( Native == null ) {
        var importer = new OpenPLXImporter();
        importer.ErrorReporter = ReportError;
        Native = importer.ParseOpenPLXSource( OpenPLXFile );

        if ( Native == null ) {
          Debug.LogError( $"Failed to initialize OpenPLX object '{name}'", this );
          return false;
        }
      }

      m_objectMap = new Dictionary<string, GameObject>();
      foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() )
        foreach ( var decl in openPLXObj.SourceDeclarations )
          m_objectMap.Add( decl, openPLXObj.gameObject );

      var RTMapper = new RuntimeMapper();
      RTMapper.PerformRuntimeMapping( this );
      RuntimeMapped = RTMapper.MapperData.RuntimeMap;

      return base.Initialize();
    }

    private static void ReportError( Error error )
    {
      UnityOpenPLXErrorFormatter error_formatter = new UnityOpenPLXErrorFormatter();
      Debug.LogError( error_formatter.format( error ) );
    }
  }
}
