using System;
using System.IO;
using AGXUnity.Model;
using AGXUnity.Rendering;
using AGXUnity.Rendering.URP;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Rendering
{
  public static class SoilSurfaceExampleBuilder
  {
    public const string Destination = "Assets/AGXUnity/Samples/SoilSurface";

    [MenuItem( "AGXUnity/Examples/Create Soil Surface Grading Example" )]
    public static void CreateExample()
    {
      if ( File.Exists( Destination + "/SoilSurfaceGrading.unity" ) ) {
        Debug.Log( "Soil surface example already exists: " + Destination );
        return;
      }
      Directory.CreateDirectory( Destination );
      AssetDatabase.Refresh();
      var previousScene = SceneManager.GetActiveScene();
      var scene = EditorSceneManager.NewScene( NewSceneSetup.EmptyScene, NewSceneMode.Additive );
      SceneManager.SetActiveScene( scene );
      try {
        var sourcePipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
        var rendererData = sourcePipeline != null && sourcePipeline.rendererDataList.Length > 0 ?
          Object.Instantiate( sourcePipeline.rendererDataList[0] ) as UniversalRendererData : ScriptableObject.CreateInstance<UniversalRendererData>();
        if ( rendererData == null ) throw new InvalidOperationException( "Example requires a Universal Renderer." );
        rendererData.name = "Soil Surface Forward+";
        rendererData.renderingMode = RenderingMode.ForwardPlus;
        rendererData.depthPrimingMode = DepthPrimingMode.Disabled;
        // Keep the example self-contained without modifying the source features.
        for ( int i = 0; i < rendererData.rendererFeatures.Count; ++i )
          rendererData.rendererFeatures[i] = Object.Instantiate( rendererData.rendererFeatures[i] );
        var feature = ScriptableObject.CreateInstance<TerrainParticleSurfaceFeature>();
        feature.name = "Terrain Particle Surface";
        rendererData.rendererFeatures.Insert( 0, feature );
        AssetDatabase.CreateAsset( rendererData, Destination + "/SoilRenderer.asset" );
        AssetDatabase.AddObjectToAsset( feature, rendererData );
        foreach ( var copied in rendererData.rendererFeatures )
          if ( copied != feature ) AssetDatabase.AddObjectToAsset( copied, rendererData );
        var pipeline = sourcePipeline != null ? Object.Instantiate( sourcePipeline ) : UniversalRenderPipelineAsset.Create( rendererData );
        pipeline.name = "Soil Surface URP";
        pipeline.msaaSampleCount = 1; pipeline.supportsCameraDepthTexture = true;
        var serialized = new SerializedObject( pipeline );
        var list = serialized.FindProperty( "m_RendererDataList" );
        list.arraySize = 1; list.GetArrayElementAtIndex( 0 ).objectReferenceValue = rendererData;
        serialized.FindProperty( "m_DefaultRendererIndex" ).intValue = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset( pipeline, Destination + "/SoilPipeline.asset" );

        var rock = CreateRock();
        AssetDatabase.CreateAsset( rock, Destination + "/JaggedRock.asset" );
        var soil = CreateSoilTexture();
        AssetDatabase.CreateAsset( soil, Destination + "/SoilTexture.asset" );
        var rockMaterial = Lit( "Rock", new Color( 0.3f, 0.27f, 0.21f ) );
        rockMaterial.enableInstancing = true;
        AssetDatabase.CreateAsset( rockMaterial, Destination + "/Rock.mat" );
        var terrainMaterial = new Material( Shader.Find( "Universal Render Pipeline/Terrain/Lit" ) ) { name = "Terrain" };
        AssetDatabase.CreateAsset( terrainMaterial, Destination + "/Terrain.mat" );

        var data = new TerrainData { heightmapResolution = 65, size = new Vector3( 12, 6, 12 ) };
        var heights = new float[65,65];
        for ( int z = 0; z < 65; ++z )
          for ( int x = 0; x < 65; ++x ) {
            float px = x / 64.0f, pz = z / 64.0f;
            heights[z,x] = (1.1f + 1.4f * pz + 0.3f * Mathf.Sin( px * Mathf.PI ) * Mathf.Sin( pz * Mathf.PI )) / 6;
          }
        data.SetHeights( 0, 0, heights );
        var layer = new TerrainLayer { diffuseTexture = soil, tileSize = new Vector2( 0.25f, 0.25f ) };
        AssetDatabase.CreateAsset( layer, Destination + "/SoilLayer.terrainlayer" );
        data.terrainLayers = new[] { layer };
        AssetDatabase.CreateAsset( data, Destination + "/GradingTerrain.asset" );
        var ground = Terrain.CreateTerrainGameObject( data );
        ground.name = "Deformable grading slope";
        ground.GetComponent<Terrain>().materialTemplate = terrainMaterial;
        var terrain = ground.AddComponent<DeformableTerrain>();
        terrain.MaximumDepth = 1;
        var surface = ground.AddComponent<TerrainParticleSurfaceRenderer>();
        surface.ParticleMesh = rock; surface.ParticleMaterial = rockMaterial; surface.SoilTexture = soil;
        surface.IndividualParticleFraction = 0.05f;
        // Connect nearby physical spheres without enlarging their outer envelope.
        surface.SupportRadiusScale = 1.5f; surface.Smoothing = 1;
        surface.SurfaceInput = TerrainParticleSurfaceRenderer.SurfaceInputMode.Mesh;

        var failure = new GameObject( "Release soil along grading slope" );
        failure.transform.position = new Vector3( 6, 2.4f, 8.5f );
        var failureShape = failure.AddComponent<AGXUnity.Collide.Box>();
        failureShape.HalfExtents = new Vector3( 2.2f, 0.7f, 1.6f );
        failureShape.CollisionsEnabled = false;
        failure.AddComponent<DeformableTerrainFailureVolume>().Add( terrain );

        var blade = GameObject.CreatePrimitive( PrimitiveType.Cube );
        blade.name = "Thin grading shovel";
        Object.DestroyImmediate( blade.GetComponent<Collider>() );
        blade.transform.position = new Vector3( 6, 1.95f, 7 );
        blade.transform.localScale = Vector3.one;
        blade.GetComponent<MeshFilter>().sharedMesh = CreateBoxMesh( new Vector3( 3.5f, 0.8f, 0.08f ) );
        AssetDatabase.CreateAsset( blade.GetComponent<MeshFilter>().sharedMesh, Destination + "/BladeMesh.asset" );
        blade.GetComponent<MeshRenderer>().sharedMaterial = Lit( "Steel", new Color( 0.12f, 0.25f, 0.34f ) );
        AssetDatabase.CreateAsset( blade.GetComponent<MeshRenderer>().sharedMaterial, Destination + "/Steel.mat" );
        var body = blade.AddComponent<AGXUnity.RigidBody>();
        body.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;
        blade.AddComponent<AGXUnity.Collide.Box>().HalfExtents = new Vector3( 1.75f, 0.4f, 0.04f );
        var shovel = blade.AddComponent<DeformableTerrainShovel>();
        shovel.TopEdge = AGXUnity.Line.Create( blade, new Vector3( -1.75f, 0.4f, 0 ), new Vector3( 1.75f, 0.4f, 0 ) );
        shovel.CuttingEdge = AGXUnity.Line.Create( blade, new Vector3( -1.75f, -0.4f, 0 ), new Vector3( 1.75f, -0.4f, 0 ) );

        var sun = new GameObject( "Sun" ).AddComponent<Light>();
        sun.type = LightType.Directional; sun.intensity = 1.8f; sun.shadows = LightShadows.Soft;
        sun.transform.rotation = Quaternion.Euler( 45, -30, 0 );
        var fill = new GameObject( "Additional warm fill" ).AddComponent<Light>();
        fill.type = LightType.Point; fill.range = 15; fill.intensity = 15;
        fill.color = new Color( 1, 0.75f, 0.45f ); fill.transform.position = new Vector3( 4, 4, 5 );
        var camera = new GameObject( "Main Camera" ).AddComponent<Camera>();
        camera.tag = "MainCamera"; camera.transform.position = new Vector3( 12, 7, 2 );
        camera.transform.LookAt( new Vector3( 6, 1.8f, 7 ) ); camera.nearClipPlane = 0.1f;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color( 0.14f, 0.18f, 0.24f );
        camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        var example = new GameObject( "Soil Surface Comparison" ).AddComponent<SoilSurfaceExample>();
        example.Renderer = surface; example.Blade = body; example.ExamplePipeline = pipeline;
        EditorSceneManager.SaveScene( scene, Destination + "/SoilSurfaceGrading.unity" );
        AssetDatabase.SaveAssets();
      }
      finally {
        EditorSceneManager.CloseScene( scene, true );
        if ( previousScene.IsValid() ) SceneManager.SetActiveScene( previousScene );
      }
      Debug.Log( "Created " + Destination + "/SoilSurfaceGrading.unity. Open it and enter Play for the rolling soil comparison." );
    }

    public static Material Lit( string name, Color color )
    {
      var material = new Material( Shader.Find( "Universal Render Pipeline/Lit" ) ) { name = name };
      material.SetColor( "_BaseColor", color ); material.SetFloat( "_Smoothness", 0.2f );
      return material;
    }

    public static Mesh CreateRock()
    {
      var mesh = Object.Instantiate( Resources.Load<Mesh>( "Debug/Models/Icosahedron" ) );
      mesh.name = "Jagged unit diameter rock";
      var vertices = mesh.vertices;
      float maximum = 0;
      for ( int i = 0; i < vertices.Length; ++i ) {
        var p = vertices[i];
        vertices[i] *= 0.85f + 0.15f * Mathf.Sin( p.x * 19 + p.y * 31 + p.z * 11 );
        maximum = Mathf.Max( maximum, vertices[i].magnitude );
      }
      for ( int i = 0; i < vertices.Length; ++i ) vertices[i] *= 0.5f / maximum;
      mesh.vertices = vertices; mesh.RecalculateNormals(); mesh.RecalculateBounds();
      return mesh;
    }

    public static Texture2D CreateSoilTexture()
    {
      var texture = new Texture2D( 128, 128, TextureFormat.RGBA32, true ) { name = "Soil grain", wrapMode = TextureWrapMode.Repeat };
      var pixels = new Color[128*128];
      for ( uint i = 0; i < pixels.Length; ++i ) {
        uint h = i * 747796405u + 2891336453u; h = ((h >> (int)((h >> 28) + 4)) ^ h) * 277803737u;
        float value = 0.6f + 0.4f * ((h ^ (h >> 22)) / 4294967296.0f);
        pixels[i] = new Color( value * 0.72f, value * 0.5f, value * 0.28f );
      }
      texture.SetPixels( pixels ); texture.Apply();
      return texture;
    }

    private static Mesh CreateBoxMesh( Vector3 size )
    {
      var cube = GameObject.CreatePrimitive( PrimitiveType.Cube );
      var mesh = Object.Instantiate( cube.GetComponent<MeshFilter>().sharedMesh );
      Object.DestroyImmediate( cube );
      var vertices = mesh.vertices;
      for ( int i = 0; i < vertices.Length; ++i ) vertices[i] = Vector3.Scale( vertices[i], size );
      mesh.vertices = vertices; mesh.RecalculateBounds();
      return mesh;
    }
  }
}
