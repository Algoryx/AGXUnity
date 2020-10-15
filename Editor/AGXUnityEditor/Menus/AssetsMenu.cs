using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using AGXUnity;
using AGXUnity.Model;

namespace AGXUnityEditor
{
  public static class AssetsMenu
  {
    [MenuItem( "Assets/AGXUnity/Shape Material" )]
    public static UnityEngine.Object CreateMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ShapeMaterial>( "material" );
    }

    [MenuItem( "Assets/AGXUnity/Contact Material" )]
    public static UnityEngine.Object CreateContactMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ContactMaterial>( "contact material" );
    }

    [MenuItem( "Assets/AGXUnity/Friction Model" )]
    public static UnityEngine.Object CreateFrictionModel()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<FrictionModel>( "friction model" );
    }

    [MenuItem( "Assets/AGXUnity/Cable Properties" )]
    public static UnityEngine.Object CreateCableProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<CableProperties>( "cable properties" );
    }

    [MenuItem( "Assets/AGXUnity/Geometry Contact Merge Split Thresholds" )]
    public static UnityEngine.Object CreateGeometryContactMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<GeometryContactMergeSplitThresholds>( "contact merge split thresholds" );
    }

    [MenuItem( "Assets/AGXUnity/Constraint Merge Split Thresholds" )]
    public static UnityEngine.Object CreateConstraintMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ConstraintMergeSplitThresholds>( "constraint merge split thresholds" );
    }

    [MenuItem("Assets/AGXUnity/Two Body Tire Properties")]
    public static UnityEngine.Object CreateTwoBodyTireProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<AGXUnity.Model.TwoBodyTireProperties>("two body tire properties");
    }

    [MenuItem( "Assets/AGXUnity/Solver Settings" )]
    public static UnityEngine.Object CrateSolverSettings()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<SolverSettings>( "solver settings" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Properties" )]
    public static UnityEngine.Object CrateDeformableTerrainProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainProperties>( "deformable terrain properties" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Material" )]
    public static UnityEngine.Object CrateDeformableTerrainMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainMaterial>( "deformable terrain material" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Shovel Settings" )]
    public static UnityEngine.Object CrateDeformableTerrainShovelSettings()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainShovelSettings>( "deformable terrain shovel settings" );
    }

    [MenuItem( "Assets/AGXUnity/Track Properties" )]
    public static UnityEngine.Object CreateTrackProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<AGXUnity.Model.TrackProperties>( "track properties" );
    }

    [MenuItem( "Assets/AGXUnity/Track Internal Merge Properties" )]
    public static UnityEngine.Object CreateTrackInternalMergeProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<AGXUnity.Model.TrackInternalMergeProperties>( "track internal merge properties" );
    }

    [MenuItem( "Assets/AGXUnity/URDF/Instantiate selected", validate = true )]
    public static bool IsUrdfSelected()
    {
      return IO.URDF.Reader.GetSelectedUrdfFiles().Length > 0;
    }

    [MenuItem( "Assets/AGXUnity/URDF/Instantiate selected" )]
    public static UnityEngine.GameObject[] InstantiateSelectedUrdfFiles()
    {
      var urdfFilePaths = IO.URDF.Reader.GetSelectedUrdfFiles( true );
      var instances = IO.URDF.Reader.Instantiate( urdfFilePaths, null, false );
      if ( instances.Length > 0 )
        Selection.objects = instances;
      return instances;
    }

    [MenuItem( "Assets/Import AGX file as prefab", validate = true )]
    public static bool IsAGXFileSelected()
    {
      bool agxFileFound = false;
      foreach ( var obj in Selection.objects ) {
        var assetPath = AssetDatabase.GetAssetPath( obj );
        if ( assetPath == "" )
          continue;

        agxFileFound = agxFileFound ||
                       IO.AGXFileInfo.IsExistingAGXFile( new System.IO.FileInfo( assetPath ) );
      }
      return agxFileFound;
    }

    [MenuItem( "Assets/Import AGX file as prefab" )]
    public static void GenerateAGXFileAsPrefab()
    {
      IO.AGXFileImporter.Import( Selection.objects );
    }

    [MenuItem( "Assets/AGXUnity/Utils/Patch AGXUnity asset(s)" )]
    public static void ConvertDeprecatedToAGXUnity()
    {
      var hasFolder = false;
      var hasFile = false;

      foreach ( var guid in Selection.assetGUIDs ) {
        var isFolder = AssetDatabase.IsValidFolder( AssetDatabase.GUIDToAssetPath( guid ) );
        hasFolder = hasFolder || isFolder;
        hasFile = hasFile || !isFolder;
      }

      var searchSubFolders = hasFolder && EditorUtility.DisplayDialog( "Patch AGXUnity assets(s)", "Search sub-folders for files to patch?", "Yes", "No" );
      if ( hasFolder )
        System.Threading.Thread.Sleep( 250 );
      var saveBackup = ( hasFolder || hasFile ) && EditorUtility.DisplayDialog( "Patch AGXUnity assets(s)", "Save backup of affected files?", "Yes", "No" );

      var dllToScriptResolver = new IO.DllToScriptResolver();
      if ( !dllToScriptResolver.IsValid )
        return;

      var numChanged = 0;
      foreach ( var guid in Selection.assetGUIDs ) {
        var localAssetPath = AssetDatabase.GUIDToAssetPath( guid ).Remove( 0, "Assets".Length );
        var path = UnityEngine.Application.dataPath + localAssetPath;
        if ( AssetDatabase.IsValidFolder( AssetDatabase.GUIDToAssetPath( guid ) ) ) {
          try {
            numChanged += dllToScriptResolver.PatchFilesInDirectory( path,
                                                                     searchSubFolders ?
                                                                       System.IO.SearchOption.AllDirectories :
                                                                       System.IO.SearchOption.TopDirectoryOnly,
                                                                     saveBackup );
          }
          catch ( Exception e ) {
            UnityEngine.Debug.LogException( e );
          }
        }
        else {
          try {
            numChanged += System.Convert.ToInt32( dllToScriptResolver.PatchFile( path, saveBackup ) );
          }
          catch ( Exception e ) {
            UnityEngine.Debug.LogException( e );
          }
        }

        UnityEngine.Debug.Log( "Number of files changed: " + numChanged );
      }
    }
  }
}
