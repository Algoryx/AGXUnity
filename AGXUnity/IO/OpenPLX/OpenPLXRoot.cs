using openplx;
using System.Collections.Generic;
using System.Linq;
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
    [field: SerializeField]
    public string OpenPLXModelName { get; set; }

    // TODO: This is a workaround to the root object being prefixed with the bundle name but not the child objects. When this issue is fixed in OpenPLX, this should be removed
    [HideInInspector]
    public string PrunedNativeName
    {
      get
      {
        if ( Native == null )
          throw new System.NullReferenceException( "Cannot get the pruned name before the OpenPLX root has been initialized" );
        return Native.getName().Split( '.' ).Last();
      }
    }

    /// <summary>
    /// In the editor the files will be located in the assets folder (Unless the path is absolute). In this case it is fine loading the path as is.
    /// However, when the application is built, the OpenPLX file is copied to a corresponding directory in the build directory and the path of the
    /// OpenPLX file needs to be updated accordingly.
    /// </summary>
    public string OpenPLXFile => OpenPLXImporter.TransformOpenPLXPath( OpenPLXAssetPath );
    public Object Native { get; private set; }

    internal void ExternalInitialize( Object native, Dictionary<string, Object> openplxMap )
    {
      Native = native;
      m_openplxMap = openplxMap;
    }

    public Dictionary<string, agx.Referenced> RuntimeMapped { get; private set; } = new Dictionary<string, agx.Referenced>();
    private Dictionary<string, Object> m_openplxMap;
    private Dictionary<string, GameObject> m_objectMap;

    public Object FindNestedNative( string declaration )
      => ResolveNativeDeclaration( Native, m_openplxMap, declaration );

    internal static Object ResolveNativeDeclaration( Object root, IReadOnlyDictionary<string, Object> objectMap, string declaration )
    {
      if ( root == null || string.IsNullOrEmpty( declaration ) )
        return null;
      var rootName = root.getName();
      // First, assume that the declaration given is relative and prepend it with the root name
      if ( objectMap.TryGetValue( rootName + "." + declaration, out Object obj ) )
        return obj;
      // Second, assume that the declaration given is absolute.
      if ( objectMap.TryGetValue( declaration, out obj ) )
        return obj;

      // References (including signal interface aliases) aren't registered as
      // separate objects. Resolve their declaration paths through the root.
      var prefix = rootName + ".";
      var relative = declaration.StartsWith( prefix, System.StringComparison.Ordinal ) ? declaration.Substring( prefix.Length ) : declaration;
      if ( relative == declaration ) {
        // Cached imports can retain the model name while omitting its bundle.
        // Remove only a leading model prefix, never an occurrence inside a path.
        var modelPrefix = rootName.Substring( rootName.LastIndexOf( '.' ) + 1 ) + ".";
        if ( relative.StartsWith( modelPrefix, System.StringComparison.Ordinal ) )
          relative = relative.Substring( modelPrefix.Length );
      }
      return root.getObject( relative );
    }

    public GameObject FindMappedObject( string declaration )
    {
      if ( Native != null ) {
        declaration = FindNestedNative( declaration )?.getName();
        if ( declaration != null && m_objectMap.ContainsKey( declaration ) )
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

    public object FindRuntimeMappedObject( string declaration ) => RuntimeMapped.GetValueOrDefault( declaration, null );

    public T FindRuntimeMappedObject<T>( string declaration )
      where T : class
    => RuntimeMapped.GetValueOrDefault( declaration, null ) as T;

    protected override bool Initialize()
    {
      if ( Native == null ) {
        var importer = new OpenPLXImporter();
        importer.ErrorReporter = ReportError;
        m_openplxMap = new Dictionary<string, Object>();
        Native = importer.ParseOpenPLXSource( OpenPLXFile, OpenPLXModelName == "" ? null : OpenPLXModelName, null, m_openplxMap );

        if ( Native == null ) {
          Debug.LogError( $"Failed to initialize OpenPLX object '{name}'", this );
          return false;
        }
      }

      m_objectMap = new Dictionary<string, GameObject>();
      foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() )
        foreach ( var decl in openPLXObj.SourceDeclarations )
          m_objectMap[ FindNestedNative( decl )?.getName() ?? decl ] = openPLXObj.gameObject;

      var m_nativeMap = new std.StringReferenceLookup();
      foreach ( var openPLXObj in GetComponentsInChildren<OpenPLXObject>() ) {
        foreach ( var decl in openPLXObj.SourceDeclarations ) {
          var obj = FindNestedNative( decl );
          if ( obj == null )
            continue;
          var native = openPLXObj.FindCorrespondingNative( this, obj );
          if ( native != null )
            m_nativeMap.Add( new( obj.getName(), native ) );
        }
      }

      var map = agxopenplx.AgxObjectMap.createPreMapped(m_nativeMap,agxopenplx.AgxObjectMapMode.Name);
      var errorReporter = new ErrorReporter();
      var mapper = new agxopenplx.OpenPlxDriveTrainMapper( errorReporter, map );

      RuntimeMapped = new Dictionary<string, agx.Referenced>();

      agxPowerLine.PowerLineRef powerline = mapper.mapDriveTrainIntoPowerLine( Native as openplx.Physics.System, Simulation.Instance.TimeStep);
      // TODO: Fix null return from this method
      if ( powerline != null && powerline.get() != null ) {
        Simulation.Instance.Native.add( powerline.get() );
        foreach ( var (obj, constraint) in mapper.getMappedConstraints() ) {
          Simulation.Instance.Native.add( constraint.get() );
          RuntimeMapped.Add( obj.getName(), constraint.get() );
        }
        foreach ( var obj in powerline.getUnits() ) {
          var objName = obj.getName();
          if ( objName.StartsWith( PrunedNativeName + "." ) )
            RuntimeMapped.Add( objName, obj.get() );
        }
        foreach ( var obj in powerline.getConnectors() ) {
          var objName = obj.getName();
          if ( objName.StartsWith( PrunedNativeName + "." ) )
            RuntimeMapped.Add( objName, obj.get() );
        }
      }

      return base.Initialize();
    }

    private static void ReportError( Error error )
    {
      Debug.LogError( error.getMessage( true ) );
    }
  }
}
