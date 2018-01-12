using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using AGXUnity;

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
      foreach ( var obj in Selection.objects ) {
        var info = new IO.AGXFileInfo( AssetDatabase.GetAssetPath( obj ) );
        if ( info.Type != IO.AGXFileInfo.FileType.AGXBinary && info.Type != IO.AGXFileInfo.FileType.AGXAscii )
          continue;

        AssetPostprocessorHandler.ReadAGXFile( info );
      }
    }

    private static bool IsAGXFile( string path )
    {
      var fi = new System.IO.FileInfo( path );
      return fi.Exists && ( fi.Extension == ".agx" || fi.Extension == ".aagx" );
    }
  }
}
