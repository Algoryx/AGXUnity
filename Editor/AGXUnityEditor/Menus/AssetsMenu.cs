using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Model;

namespace AGXUnityEditor
{
  public static class AssetsMenu
  {
    [MenuItem( "Assets/AGXUnity/Shape Material", priority = 600 )]
    public static Object CreateMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ShapeMaterial>( "material" );
    }

    [MenuItem( "Assets/AGXUnity/Contact Material", priority = 600 )]
    public static Object CreateContactMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ContactMaterial>( "contact material" );
    }

    [MenuItem( "Assets/AGXUnity/Friction Model", priority = 600 )]
    public static Object CreateFrictionModel()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<FrictionModel>( "friction model" );
    }

    [MenuItem( "Assets/AGXUnity/Cable Properties", priority = 620 )]
    public static Object CreateCableProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<CableProperties>( "cable properties" );
    }

    [MenuItem( "Assets/AGXUnity/Cable Damage Properties", priority = 620 )]
    public static Object CreateCableDamageProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<CableDamageProperties>( "cable damage properties" );
    }

    [MenuItem( "Assets/AGXUnity/Geometry Contact Merge Split Thresholds", priority = 640 )]
    public static Object CreateGeometryContactMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<GeometryContactMergeSplitThresholds>( "contact merge split thresholds" );
    }

    [MenuItem( "Assets/AGXUnity/Constraint Merge Split Thresholds", priority = 640 )]
    public static Object CreateConstraintMergeSplitThresholds()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<ConstraintMergeSplitThresholds>( "constraint merge split thresholds" );
    }

    [MenuItem( "Assets/AGXUnity/Two Body Tire Properties", priority = 660 )]
    public static Object CreateTwoBodyTireProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<TwoBodyTireProperties>("two body tire properties");
    }

    [MenuItem( "Assets/AGXUnity/Solver Settings", priority = 680 )]
    public static Object CrateSolverSettings()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<SolverSettings>( "solver settings" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Properties", priority = 700 )]
    public static Object CrateDeformableTerrainProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainProperties>( "deformable terrain properties" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Material", priority = 700 )]
    public static Object CrateDeformableTerrainMaterial()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainMaterial>( "deformable terrain material" );
    }

    [MenuItem( "Assets/AGXUnity/Deformable Terrain Shovel Settings", priority = 700 )]
    public static Object CrateDeformableTerrainShovelSettings()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<DeformableTerrainShovelSettings>( "deformable terrain shovel settings" );
    }

    [MenuItem( "Assets/AGXUnity/Track Properties", priority = 720 )]
    public static Object CreateTrackProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<TrackProperties>( "track properties" );
    }

    [MenuItem( "Assets/AGXUnity/Track Internal Merge Properties", priority = 720 )]
    public static Object CreateTrackInternalMergeProperties()
    {
      return Selection.activeObject = Utils.AssetFactory.Create<TrackInternalMergeProperties>( "track internal merge properties" );
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected URDF [instance]", validate = true, priority = 550 )]
    public static bool IsUrdfSelected()
    {
      return IO.URDF.Reader.GetSelectedUrdfFiles().Length > 0;
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected URDF [instance]", priority = 550 )]
    public static GameObject[] InstantiateSelectedUrdfFiles()
    {
      var urdfFilePaths = IO.URDF.Reader.GetSelectedUrdfFiles( true );
      var instances = IO.URDF.Reader.Instantiate( urdfFilePaths, null, false );
      if ( instances.Length > 0 )
        Selection.objects = instances;
      return instances;
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected URDF [prefab]...", validate = true, priority = 550 )]
    public static bool IsUrdfSelectedAsPrefab()
    {
      return IsUrdfSelected();
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected URDF [prefab]...", priority = 550 )]
    public static GameObject[] SelectedUrdfFilesAsPrefab()
    {
      var urdfFilePaths = IO.URDF.Reader.GetSelectedUrdfFiles( true );
      var instances = IO.URDF.Reader.Instantiate( urdfFilePaths, null, false );
      if ( instances.Length == 0 )
        return instances;

      var prefabs = new List<GameObject>();
      foreach ( var instance in instances ) {
        var directory = IO.URDF.Prefab.OpenFolderPanel( $"Prefab and assets directory for: {instance.name}" );
        if ( string.IsNullOrEmpty( directory ) ) {
          Debug.Log( $"Ignoring URDF prefab {instance.name}." );
          continue;
        }
        var model = AGXUnity.IO.URDF.Utils.GetElement<AGXUnity.IO.URDF.Model>( instance );
        var prefab = IO.URDF.Prefab.Create( model,
                                            instance,
                                            directory );
        if ( prefab != null )
          prefabs.Add( prefab );

        Object.DestroyImmediate( instance );
      }

      return prefabs.ToArray();
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected STL [instance]", validate = true, priority = 551 )]
    public static bool IsStlSelected()
    {
      return IO.Utils.GetSelectedFiles( ".stl", false ).Length > 0;
    }

    [MenuItem( "Assets/AGXUnity/Import/Selected STL [instance]", priority = 551 )]
    public static void ReadSelectedStl()
    {
      var selectedStlPaths = IO.Utils.GetSelectedFiles( ".stl", true );
      var createdParents = new List<GameObject>();
      using ( new Utils.UndoCollapseBlock( $"Instantiating {selectedStlPaths.Length} STL files" ) ) {
        foreach ( var selectedStlPath in selectedStlPaths ) {
          try {
            createdParents.AddRange( AGXUnity.IO.StlFileImporter.Instantiate( selectedStlPath,
                                                                              obj => Undo.RegisterCreatedObjectUndo( obj, "Created " +
                                                                                                                          obj.GetType().Name ) ) );
          }
          catch ( Exception e ) {
            Debug.LogException( e );
            continue;
          }
        }
      }
      if ( createdParents.Count > 0 )
        Selection.objects = createdParents.ToArray();
    }

    [MenuItem( "Assets/Import AGX Dynamics file [prefab]", validate = true )] // <- Remove this for AGXUnity -> Import.
    [MenuItem( "Assets/AGXUnity/Import/Selected AGX Dynamics file [prefab]", validate = true, priority = 552 )]
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

    [MenuItem( "Assets/Import AGX Dynamics file [prefab]" )] // <- Remove this for AGXUnity -> Import.
    [MenuItem( "Assets/AGXUnity/Import/Selected AGX Dynamics file [prefab]", priority = 552 )]
    public static void GenerateAGXFileAsPrefab()
    {
      IO.AGXFileImporter.Import( Selection.objects );
    }
  }
}
