using agxopenplx;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using System;
using System.Linq;
using UnityEngine;

namespace AGXUnity.IO.OpenPLX
{
  public class VisualsMapper
  {
    private MapperData Data;
    private MapperOptions Options;

    private Material ColorBlitMat;

    public VisualsMapper( MapperData cache, MapperOptions options )
    {
      Data = cache;
      Options = options;

      ColorBlitMat = new Material( Shader.Find( "AGXUnity/ColorAlphaBlitter" ) );
    }

    private struct TextureData
    {
      public int width;
      public int height;
      public int format;
      public byte[] data;
    };

    Tuple<GameObject, bool> MapCachedVisual( agxCollide.Shape shape, agx.AffineMatrix4x4 transform, openplx.Visuals.Geometries.Geometry visual )
    {
      GameObject go = Data.CreateGameObject();

      var rd      = shape.getRenderData();

      var filter = go.AddComponent<MeshFilter>();
      var renderer = go.AddComponent<MeshRenderer>();

      renderer.enabled = rd.getShouldRender();

      // TODO: Should these be cached? Can they?
      var mesh = AGXMeshToUnityMesh(rd.getVertexArray(),rd.getIndexArray());
      if ( Options.HideMeshesInHierarchy )
        mesh.hideFlags = HideFlags.HideInHierarchy;
      mesh.name = visual.getName();
      Data.MappedMeshes.Add( mesh );
      filter.mesh = mesh;

      var rm = rd.getRenderMaterial();
      if ( rm != null ) {
        if ( !Data.NativeMappedRenderMaterialCache.TryGetValue( rm.getHash(), out Material mat ) ) {
          mat = new Material( Shader.Find( "Standard" ) );
          mat.RestoreLocalDataFrom( rm );
          if ( rm.getName() != "" )
            mat.name = $"{rm.getName()}#{rm.getHash()}";
          else
            mat.name = rm.getHash().ToString();
          if ( Options.HideVisualMaterialsInHierarchy )
            mat.hideFlags = HideFlags.HideInHierarchy;
          Data.NativeMappedRenderMaterialCache[ rm.getHash() ] = mat;
          Data.MappedMaterials.Add( mat );
        }

        renderer.material = mat;
        return Tuple.Create( go, true );
      }

      return Tuple.Create( go, false );
    }

    public GameObject MapVisualNode( openplx.Visuals.Node node )
    {
      GameObject go;
      if ( node is openplx.Visuals.Geometries.Geometry geom )
        go = MapVisualGeometry( geom );
      else
        go = Data.CreateOpenPLXObject( node.getName() );

      Utils.MapLocalTransform( go.transform, node.local_transform() );

      foreach ( var subnode in node.getValues<openplx.Visuals.Node>() )
        Utils.AddChild( go, MapVisualNode( subnode ), Data.ErrorReporter, subnode );

      return go;
    }

    GameObject MapVisualGeometry( openplx.Visuals.Geometries.Geometry visual )
    {
      GameObject go = null;
      bool cachedMat = false;
      var uuid_annots = visual.findAnnotations("uuid");
      foreach ( var uuid_annot in uuid_annots ) {
        if ( uuid_annot.isString() ) {
          var uuid = uuid_annot.asString();
          var shape = Data.AgxCache.readCollisionShapeAndTransformCS( uuid );
          if ( shape != null )
            (go, cachedMat) = MapCachedVisual( shape.first, shape.second, visual );
        }
      }

      if ( go == null ) {
        go = visual switch
        {
          openplx.Visuals.Geometries.Box box => GameObject.CreatePrimitive( PrimitiveType.Cube ),
          openplx.Visuals.Geometries.Cylinder cyl => GameObject.CreatePrimitive( PrimitiveType.Cylinder ),
          openplx.Visuals.Geometries.ExternalTriMeshGeometry etmg => MapExternalTriMesh( etmg ),
          openplx.Visuals.Geometries.Base64TriMeshGeometry itmg => MapBase64TriMesh( itmg ),
          openplx.Visuals.Geometries.ConvexMesh cm => MapConvex( cm ),
          openplx.Visuals.Geometries.Sphere sphere => GameObject.CreatePrimitive( PrimitiveType.Sphere ),
          _ => null
        };

        switch ( visual ) {
          case openplx.Visuals.Geometries.Box box:
            go.transform.localScale = box.size().ToVector3();
            break;
          case openplx.Visuals.Geometries.Cylinder cyl:
            go.transform.localScale = new Vector3( (float)cyl.radius(), (float)cyl.height()/2, (float)cyl.radius() );
            break;
          case openplx.Visuals.Geometries.Sphere sphere:
            go.transform.localScale = Vector3.one * (float)sphere.radius();
            break;
          default:
            break;
        }
      }

      if ( go == null ) {
        // TODO: ExternalTriMeshes can fail if their paths are not valid dont report them as unimplemented.
        if ( visual is openplx.Visuals.Geometries.ExternalTriMeshGeometry )
          return null;

        return Utils.ReportUnimplemented<GameObject>( visual, Data.ErrorReporter );
      }

      Data.RegisterOpenPLXObject( visual.getName(), go );
      if ( !cachedMat ) {
        // TODO: Find a better way to check whether to use material
        // When visuals are imported in the editor, the resulting object might be a prefab instance to another asset
        // TODO: This is only relevant when using editor assets which is currently disabled
#if false
        if ( visual.material().GetType() != typeof( openplx.Visuals.Materials.Material ) )
#endif
        //foreach(var mat in visual.getValues<openplx.Visuals.Materials.>())
        foreach ( var renderer in go.GetComponentsInChildren<MeshRenderer>() )
          renderer.material = MapVisualMaterial( visual.material() );
      }

      return go;
    }

    UnityEngine.Mesh AGXMeshToUnityMesh( agx.Vec3Vector vertices, agx.UInt32Vector indices, agx.Vec2Vector uvs = null )
    {
      var outMesh = new UnityEngine.Mesh();
      Vector3[] uVertices = new Vector3[vertices.Count];
      for ( int i = 0; i < vertices.Count; i++ )
        uVertices[ i ].Set( (float)-vertices[ i ].x, (float)vertices[ i ].y, (float)vertices[ i ].z );
      outMesh.vertices = uVertices;
      if ( vertices.Count > UInt16.MaxValue )
        outMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

      int[] uIndices = new int[indices.Count];
      for ( int i = 0; i < indices.Count; i += 3 ) {
        uIndices[ i ]     = (int)indices[ i ];
        uIndices[ i + 1 ] = (int)indices[ i + 2 ];
        uIndices[ i + 2 ] = (int)indices[ i + 1 ];
      }
      outMesh.SetIndices( uIndices, MeshTopology.Triangles, 0 );

      if ( uvs != null ) {
        Vector2[] uUvs = new Vector2[uvs.Count];
        for ( int i = 0; i < uvs.Count; i++ )
          uUvs[ i ].Set( (float)uvs[ i ].x, (float)uvs[ i ].y );
        outMesh.SetUVs( 0, uUvs );
      }

      outMesh.RecalculateBounds();
      outMesh.RecalculateNormals();
      return outMesh;
    }

    TextureData MapTextureData( openplx.Visuals.Textures.TextureData texData )
    {
      int format = (int)texData.format();
      int width = (int)texData.width();
      int height = (int)texData.height();
      int channels = format + 1;

      var numBytes = height * width * channels;
      byte[] srcData = System.Convert.FromBase64String( texData.data() );
      byte[] data = new byte[numBytes];
      for ( int y = 0; y < height; y++ ) {
        int srcOffset = y * width * channels;
        int dstOffset = (height - y - 1) * width * channels;
        for ( int i = 0; i < width * channels; i++ )
          data[ dstOffset + i ] = srcData[ srcOffset + i ];
      }

      TextureData resultData = new TextureData();
      resultData.format = format;
      resultData.width = width;
      resultData.height = height;
      resultData.data = data;
      return resultData;
    }

    void ApplySwizzle( ref TextureData data, openplx.Visuals.Textures.Texture oTex )
    {
      if ( !oTex.HasTrait<openplx.Visuals.Textures.Swizzle>() )
        return;

      string swizzle = oTex.getDynamic("swizzle").asString();

      int channels = data.format + 1;
      int numBytes = data.width * data.height * channels;

      var allowedSwizzles = new char[]{'r', 'g', 'b', 'a' }.Take( channels );

      if ( swizzle.Any( c => !allowedSwizzles.Contains( c ) ) ) {
        var errorData = BaseError.CreateErrorData(oTex);
        Data.ErrorReporter.reportError( new agxopenplx.InvalidSwizzle( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, swizzle ) );
        return;
      }
      else {
        // Swizzle Logic
        var swizzled = new byte[(numBytes / channels) * swizzle.Length];

        var swizzleMap = new int[swizzle.Length];
        int i = 0;
        foreach ( char c in swizzle ) {
          swizzleMap[ i++ ] = c switch
          {
            'r' => 0,
            'g' => 1,
            'b' => 2,
            'a' => 3,
            _ => 0
          };
        }

        i = 0;
        for ( int p = 0; p < numBytes / channels; p++ ) {
          foreach ( var m in swizzleMap )
            swizzled[ i++ ] = data.data[ p * channels + m ];
        }

        data.data = swizzled;

        data.format = swizzle.Length - 1;
      }
    }

    void ApplySampler( Texture2D texture, openplx.Visuals.Textures.Texture oTex, System.Action<Vector2, Vector2> onOffsetAndScale = null )
    {
      if ( !oTex.HasTrait<openplx.Visuals.Textures.Sampler>() )
        return;

      texture.filterMode = oTex.getDynamic( "filter_mode" ).asInt() switch
      {
        0 => UnityEngine.FilterMode.Point,
        1|2 => UnityEngine.FilterMode.Bilinear,
        _ => UnityEngine.FilterMode.Bilinear,
      };
      texture.wrapMode = oTex.getDynamic( "wrap_mode" ).asInt() switch
      {
        0 => TextureWrapMode.Repeat,
        1 => TextureWrapMode.Clamp,
        2 => TextureWrapMode.Mirror,
        _ => TextureWrapMode.Repeat,
      };

      if ( onOffsetAndScale != null ) {
        Vector2 offset = new Vector2((float)oTex.getDynamic("offset_u").asReal(), (float)oTex.getDynamic("offset_v").asReal());
        Vector2 scale = new Vector2((float)oTex.getDynamic("scale_u").asReal(), (float)oTex.getDynamic("scale_v").asReal());

        onOffsetAndScale( offset, scale );
      }
      else {
        // TODO: Warn that something is afoot
      }
    }

    TextureFormat MapTextureFormat( int format )
    {
      return format switch
      {
        0 => TextureFormat.R8,
        1 => TextureFormat.RG16,
        2 => TextureFormat.RGB24,
        3 => TextureFormat.RGBA32,
        _ => TextureFormat.RGBA32
      };
    }

    Texture2D MapColorTexture( openplx.Visuals.Optics.VisibleBand band )
    {
      bool hasColor = band.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.BaseColor>();
      bool hasTransparency = band.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.Transparency>();

      if ( !hasColor && !hasTransparency )
        return null;

      Texture2D colorTex = null;
      openplx.Visuals.Textures.Texture colorOTex = null;

      if ( hasColor ) {
        var tex = band.getDynamic( "base_color_map" ).asObject() as openplx.Visuals.Textures.DefaultTexture;
        if ( tex is openplx.Visuals.Textures.Texture oTex ) {
          colorOTex = oTex;
          var data = MapTextureData( oTex.data() );
          var pixelData = data.data;

          ApplySwizzle( ref data, oTex );

          var format = MapTextureFormat(data.format);

          // If base color map is not RGBA, the channels are expanded into an RGBA texture as that is what the shaders expect
          if ( format == TextureFormat.RG16 ) {
            byte[] expanded = new byte[data.width * data.height * 4];
            for ( int i = 0; i < data.width * data.height; i++ ) {
              expanded[ i*4 + 0 ] = pixelData[ i * 2 ];
              expanded[ i*4 + 1 ] = pixelData[ i * 2 ];
              expanded[ i*4 + 2 ] = pixelData[ i * 2 ];
              expanded[ i*4 + 3 ] = pixelData[ i * 2 + 1 ];
            }
            pixelData = expanded;
            format = TextureFormat.RGBA32;
          }
          else if ( format == TextureFormat.R8 || format == TextureFormat.RGB24 ) {
            byte[] expanded = new byte[data.width * data.height * 4];
            bool hasGB = format == TextureFormat.RGB24;
            int channels = hasGB ? 3 : 1;
            for ( int i = 0; i < data.width * data.height; i++ ) {
              expanded[ i*4 + 0 ] = pixelData[ i * channels ];
              expanded[ i*4 + 1 ] = pixelData[ i * channels + ( hasGB ? 1 : 0 ) ];
              expanded[ i*4 + 2 ] = pixelData[ i * channels + ( hasGB ? 2 : 0 ) ];
              expanded[ i*4 + 3 ] = 255;
            }
            pixelData = expanded;
            format = TextureFormat.RGBA32;
          }

          colorTex = new Texture2D( data.width, data.height, format, false, false );
          colorTex.SetPixelData<byte>( pixelData, 0 );
          colorTex.Apply();

          ApplySampler( colorTex, oTex, ( offset, scale ) => {
            ColorBlitMat.SetTextureOffset( "_ColorTex", offset );
            ColorBlitMat.SetTextureScale( "_ColorTex", scale );
          } );
        }
      }

      if ( hasTransparency ) {
        var oTex = band.getDynamic( "alpha_map" ).asObject() as openplx.Visuals.Textures.DefaultTexture;
        var alphaTex = MapTexture( oTex, ( offset, scale ) => {
          ColorBlitMat.SetTextureOffset( "_AlphaTex", offset );
          ColorBlitMat.SetTextureScale( "_AlphaTex", scale );
        }, false );

        // If no alpha map is specified, default should be the alpha channel of the base color texture if present
        if ( alphaTex != null ) {
          ColorBlitMat.SetTexture( "_AlphaTex", alphaTex );
          ColorBlitMat.SetTexture( "_ColorTex", colorTex );

          Vector2Int blittedSize = new Vector2Int(
            Mathf.Max(
              colorTex != null ? colorTex.width : 1,
              alphaTex != null ? alphaTex.width : 1 ),
            Mathf.Max(
              colorTex != null ? colorTex.height : 1,
              alphaTex != null ? alphaTex.height : 1 )
            );

          Texture2D destTex = new Texture2D( blittedSize.x, blittedSize.y, TextureFormat.RGBA32, false, false );
          RenderTexture renderTex = new RenderTexture( blittedSize.x, blittedSize.y, 32, RenderTextureFormat.ARGB32 );
          RenderTexture.active = renderTex;
          Graphics.Blit( null, renderTex, ColorBlitMat );
          destTex.ReadPixels( new Rect( 0, 0, blittedSize.x, blittedSize.y ), 0, 0, false );
          destTex.Apply( false, false );

          var alphaOTex = oTex as openplx.Visuals.Textures.Texture;

          // Use sampler state from Color texture if present or alpha map otherwise
          if ( colorOTex != null && colorOTex.HasTrait<openplx.Visuals.Textures.Sampler>() )
            ApplySampler( destTex, colorOTex );
          else if ( alphaOTex != null && alphaOTex.HasTrait<openplx.Visuals.Textures.Sampler>() )
            ApplySampler( destTex, alphaOTex );

          if (
            alphaOTex != null &&
            alphaOTex.HasTrait<openplx.Visuals.Textures.Sampler>() &&
            colorOTex != null &&
            colorOTex.HasTrait<openplx.Visuals.Textures.Sampler>() ) {
            var equal =
              alphaOTex.getDynamic( "filter_mode" ).asInt() == colorOTex.getDynamic( "filter_mode" ).asInt() &&
              alphaOTex.getDynamic( "wrap_mode" ).asInt() == colorOTex.getDynamic( "wrap_mode" ).asInt() &&
              alphaOTex.getDynamic( "offset_u" ).asReal() == colorOTex.getDynamic( "offset_u" ).asReal() &&
              alphaOTex.getDynamic( "offset_v" ).asReal() == colorOTex.getDynamic( "offset_v" ).asReal();

            if ( !equal ) {
              Data.ErrorReporter.reportError( new IncompatibleSamplersError( band ) );
              return null;
            }
          }

          colorTex = destTex;
        }
      }

      if ( colorTex == null )
        return null;

      var dummy = band.getDynamic( "base_color_map" ).asObject() as openplx.Visuals.Textures.DefaultTexture;
      colorTex.name = band.getName() + "base_color_alpha_map";
      Data.TextureCache[ dummy ] = colorTex;
      if ( Options.HideTexturesInHierarchy )
        colorTex.hideFlags = HideFlags.HideInHierarchy;
      return colorTex;
    }

    Texture2D MapTexture( openplx.Visuals.Textures.DefaultTexture baseTex, System.Action<Vector2, Vector2> onOffsetAndScale = null, bool colorTexture = true )
    {
      if ( Data.TextureCache.TryGetValue( baseTex, out Texture2D cached ) )
        return cached;

      Texture2D result = null;

      if ( baseTex is openplx.Visuals.Textures.Texture oTex ) {
        var data = MapTextureData( oTex.data() );
        var pixelData = data.data;

        ApplySwizzle( ref data, oTex );

        result = new Texture2D( data.width, data.height, MapTextureFormat( data.format ), false, !colorTexture );
        result.SetPixelData<byte>( data.data, 0 );
        result.Apply();

        ApplySampler( result, oTex, onOffsetAndScale );
      }
      else
        return null;

      result.name = baseTex.getName();

      Data.TextureCache[ baseTex ] = result;
      if ( Options.HideTexturesInHierarchy )
        result.hideFlags = HideFlags.HideInHierarchy;

      return result;
    }

    public enum SurfaceType
    {
      Opaque,
      Transparent
    }

    public enum BlendMode
    {
      Alpha,
      Premultiply,
      Additive,
      Multiply
    }

    public Material MapVisualMaterial( openplx.Physics.Optics.Material mat )
    {
      if ( !mat.HasTrait<openplx.Visuals.Optics.VisualMaterial>() )
        return null;

      var visible = mat.getDynamic("visible");

      var visibleBand = visible.asObject() as openplx.Visuals.Optics.VisibleBand;

      if ( Data.RenderMaterialCache.TryGetValue( mat, out var renderMat ) )
        return renderMat;

      renderMat = RenderingUtils.CreateDefaultMaterial();
      renderMat.name = mat.getName();

      Color color = new Color(1,1,1,1); // Color property is set from BaseColor and Transparency

      // BaseColor 
      if ( visibleBand.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.BaseColor>() ) {
        var tint = visibleBand.getDynamic( "base_color_tint" ).asObject() as openplx.Visuals.Properties.Color;
        color.r = Mathf.Sqrt( (float)tint.r() );
        color.g = Mathf.Sqrt( (float)tint.g() );
        color.b = Mathf.Sqrt( (float)tint.b() );
      }

      // Opacity
      if ( visibleBand.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.Transparency>() ) {
        // The presence of the Transparency trait signals that we need to enable transparent render mode for this material
        color.a = (float)visibleBand.getDynamic( "alpha" ).asReal();
        renderMat.SetFloat( "_Surface", (float)SurfaceType.Transparent );
        renderMat.SetFloat( "_Blend", (float)BlendMode.Alpha );
        renderMat.SetOverrideTag( "RenderType", "Transparent" );
        renderMat.SetInt( "_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha );
        renderMat.SetInt( "_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha );
        renderMat.SetInt( "_ZWrite", 1 );
        renderMat.DisableKeyword( "_ALPHATEST_ON" );
        renderMat.EnableKeyword( "_ALPHABLEND_ON" );
        renderMat.DisableKeyword( "_ALPHAPREMULTIPLY_ON" );
      }

      renderMat.color = color;
      renderMat.mainTexture = MapColorTexture( visibleBand ); // Alpha map is baked into a color texture if present

      // Normals
      if ( visibleBand.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.Normals>() ) {
        var normalTex = visibleBand.getDynamic( "normal_map" ).asObject() as openplx.Visuals.Textures.DefaultTexture;
        renderMat.SetTexture( "_BumpMap", MapTexture( normalTex, ( offset, scale ) => {
          renderMat.SetTextureOffset( "_BumpMap", offset );
          renderMat.SetTextureScale( "_BumpMap", scale );
        }, false ) );
      }

      // Metallic
      if ( visibleBand.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.Metallic>() ) {
        float metallic = (float)visibleBand.getDynamic("metallic").asReal();
        renderMat.SetFloat( "_Metallic", metallic );

        var metallicTex = visibleBand.getDynamic("metallic_map").asObject() as openplx.Visuals.Textures.DefaultTexture;
        renderMat.SetTexture( "_Metallic_Map", MapTexture( metallicTex, ( offset, scale ) => {
          renderMat.SetTextureOffset( "_Metallic_Map", offset );
          renderMat.SetTextureScale( "_Metallic_Map", scale );
        }, false ) );
      }

      // Roughness
      if ( visibleBand.HasTrait<openplx.Visuals.Optics.SurfaceFeatures.Roughness>() ) {
        float roughness = (float)visibleBand.getDynamic("roughness").asReal();
        renderMat.SetFloat( "_Smoothness", 1-roughness );

        var roughnessTex = visibleBand.getDynamic("roughness_map").asObject() as openplx.Visuals.Textures.DefaultTexture;
        renderMat.SetTexture( "_RoughnessMap", MapTexture( roughnessTex, ( offset, scale ) => {
          renderMat.SetTextureOffset( "_RoughnessMap", offset );
          renderMat.SetTextureScale( "_RoughnessMap", scale );
        }, false ) );
      }

      Data.RenderMaterialCache[ mat ] = renderMat;
      Data.MappedMaterials.Add( renderMat );
      if ( Options.HideVisualMaterialsInHierarchy )
        renderMat.hideFlags = HideFlags.HideInHierarchy;
      return renderMat;
    }
    GameObject MapExternalTriMesh( openplx.Visuals.Geometries.ExternalTriMeshGeometry objGeom )
    {
      string path = objGeom.path();

      GameObject go = Data.CreateGameObject();

      //if ( !VerifyAssetPath( path, objGeom ) )
      //  return go;

      // TODO: Unity's default importer is inconsistent about up axis, causing the rotation of the imported object to change depending on the asset.
      // As far as I know, there's no way to know which axis is used as the up axis by the importer and thus we dont know what rotation to apply to the mesh.
#if false
      var assetPath = "Assets/" + System.IO.Path.GetRelativePath(Application.dataPath,path).Replace('\\','/');

      var prefab = (GameObject)UnityEditor.PrefabUtility.InstantiatePrefab( UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>( assetPath ) );

      var rot = Quaternion.FromToRotation( Vector3.up, Vector3.forward );
      var m = new Matrix4x4();
      m.SetTRS( Vector3.zero, rot, Vector3.one );
      prefab.transform.localPosition = m.MultiplyPoint( prefab.transform.localPosition );
      //prefab.transform.localRotation = rot * prefab.transform.localRotation;
      prefab.transform.SetParent( go.transform, false );
#else
      var mf = go.AddComponent<MeshFilter>();
      var mr = go.AddComponent<MeshRenderer>();

      var mesh = new UnityEngine.Mesh();

      if ( Options.HideVisualMeshesInHierarchy )
        mesh.hideFlags = HideFlags.HideInHierarchy;

      var source = agxUtil.agxUtilSWIG.createRenderData(path, new agx.Matrix3x3(objGeom.scale().ToVec3()));
      mesh = AGXMeshToUnityMesh( source.getVertexArray(), source.getIndexArray(), source.getTexCoordArray() );
      mesh.name = objGeom.getName() + "_mesh";
      Data.MappedMeshes.Add( mesh );

      if ( mesh == null ) {
        var errorData = BaseError.CreateErrorData( objGeom );
        Data.ErrorReporter.reportError( new InvalidObjFile( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, path ) );
      }
      else
        mf.mesh = mesh;
#endif
      go.transform.localScale = objGeom.scale().ToVector3();

      return go;
    }

    GameObject MapBase64TriMesh( openplx.Visuals.Geometries.Base64TriMeshGeometry objGeom )
    {
      GameObject go = Data.CreateGameObject();

      var mf = go.AddComponent<MeshFilter>();
      var mr = go.AddComponent<MeshRenderer>();

      var mesh = new UnityEngine.Mesh();
      mesh.name = objGeom.getName() + "_mesh";
      Data.MappedMeshes.Add( mesh );

      var numVertices = objGeom.numVertices();
      var numIndices = objGeom.numIndices();

      bool hasPositions = false, hasNormals = false;
      int uvSet = 0;

      var attrTypes = new openplx.Visuals.Geometries.VertexAttributeType();
      foreach ( var attr in objGeom.attributes() ) {
        if ( attr.type() == attrTypes.Position() ) {
          if ( hasPositions )
            continue; // TODO: Warn
          var vertexBytes = Convert.FromBase64String(attr.data());
          if ( vertexBytes.Length / sizeof( float ) / 3.0f != numVertices ) {
            var errorData = BaseError.CreateErrorData(attr);
            Data.ErrorReporter.reportError( new agxopenplx.MeshAttributeVertexCountMismatch( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, objGeom, attr ) );
            continue;
          }

          var vertices = new Vector3[numVertices];
          for ( int i = 0; i < numVertices; i++ ) {
            vertices[ i ].x = -BitConverter.ToSingle( vertexBytes, i * 3 * sizeof( float ) );
            vertices[ i ].y =  BitConverter.ToSingle( vertexBytes, ( i * 3 + 1 ) * sizeof( float ) );
            vertices[ i ].z =  BitConverter.ToSingle( vertexBytes, ( i * 3 + 2 ) * sizeof( float ) );
          }
          mesh.vertices = vertices;
          hasPositions = true;
        }
        else if ( attr.type() == attrTypes.Normal() ) {
          if ( hasNormals )
            continue; // TODO: Warn;

          var normalBytes = Convert.FromBase64String(attr.data());
          if ( normalBytes.Length / sizeof( float ) / 3.0f != numVertices ) {
            var errorData = BaseError.CreateErrorData(attr);
            Data.ErrorReporter.reportError( new agxopenplx.MeshAttributeVertexCountMismatch( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, objGeom, attr ) );
            continue;
          }
          var normals = new Vector3[numVertices];
          for ( int i = 0; i < numVertices; i++ ) {
            normals[ i ].x = -BitConverter.ToSingle( normalBytes, i * 3 * sizeof( float ) );
            normals[ i ].y =  BitConverter.ToSingle( normalBytes, ( i * 3 + 1 ) * sizeof( float ) );
            normals[ i ].z =  BitConverter.ToSingle( normalBytes, ( i * 3 + 2 ) * sizeof( float ) );
          }
          mesh.normals = normals;
        }
        else if ( attr.type() == attrTypes.UV() ) {
          var uvBytes = Convert.FromBase64String(attr.data());
          if ( uvBytes.Length / sizeof( float ) / 3.0f != numVertices ) {
            var errorData = BaseError.CreateErrorData(attr);
            Data.ErrorReporter.reportError( new agxopenplx.MeshAttributeVertexCountMismatch( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, objGeom, attr ) );
            continue;
          }
          var uvs = new Vector2[numVertices];
          for ( int i = 0; i < numVertices; i++ ) {
            uvs[ i ].x = BitConverter.ToSingle( uvBytes, i * 3 * sizeof( float ) );
            uvs[ i ].y = BitConverter.ToSingle( uvBytes, ( i * 3 + 1 ) * sizeof( float ) );
          }
          mesh.SetUVs( uvSet++, uvs );
        }
      }

      var indexBytes = Convert.FromBase64String(objGeom.indexData());

      if ( !hasPositions || indexBytes.Length / sizeof( Int32 ) != numIndices ) {
        var errorData = BaseError.CreateErrorData(objGeom);
        Data.ErrorReporter.reportError( new agxopenplx.InvalidBase64Mesh( errorData.fromLine, errorData.fromColumn, errorData.toLine, errorData.toColumn, errorData.sourceID, objGeom ) );
      }

      var indices = new int[numIndices];
      for ( int i = 0; i < numIndices; i += 3 ) {
        indices[ i ] = BitConverter.ToInt32( indexBytes, ( i + 2 ) * sizeof( int ) );
        indices[ i + 1 ] = BitConverter.ToInt32( indexBytes, ( i + 1 ) * sizeof( int ) );
        indices[ i + 2 ] = BitConverter.ToInt32( indexBytes, i * sizeof( int ) );
      }

      if ( numVertices > ushort.MaxValue )
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
      mesh.triangles = indices;

      mf.mesh = mesh;

      // TODO: Add detailed option for visual mesh hiding
      if ( Options.HideVisualMeshesInHierarchy )
        mesh.hideFlags = HideFlags.HideInHierarchy;

      return go;
    }

    GameObject MapConvex( openplx.Visuals.Geometries.ConvexMesh convex )
    {
      var go = Data.CreateOpenPLXObject(convex.getName());
      var mr = go.AddComponent<MeshRenderer>();
      var mf = go.AddComponent<MeshFilter>();
      var mesh = MapConvex( convex.vertices(), convex.getName() );

      mf.mesh = mesh;
      return mf.gameObject;
    }

    UnityEngine.Mesh MapConvex( std.MathVec3Vector vertices, string name )
    {
      var mesh = new UnityEngine.Mesh();
      var source = agxUtil.agxUtilSWIG.createConvex(new agx.Vec3Vector(vertices.Select(v => v.ToVec3()).ToArray()));

      var md = source.getMeshData();
      mesh.vertices = md.getVertices().Select( v => v.ToHandedVector3() ).ToArray();
      mesh.SetIndices( md.getIndices().Select( i => (int)i ).ToArray(), MeshTopology.Triangles, 0 );

      mesh.name = name;
      Data.MappedMeshes.Add( mesh );

      return mesh;
    }
  }
}
