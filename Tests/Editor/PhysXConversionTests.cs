using AGXUnityEditor.Utils;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AGXUnityTesting.Editor
{
  public class PhysXConversionTests
  {
    private Scene m_scene;
    private string m_assets;
    private readonly List<GameObject> m_objects = new List<GameObject>();
    private int m_undoGroup;

    [SetUp]
    public void SetUp()
    {
      m_scene = SceneManager.GetActiveScene();
      Undo.IncrementCurrentGroup();
      m_undoGroup = Undo.GetCurrentGroup();
    }

    [TearDown]
    public void TearDown()
    {
      Undo.RevertAllDownToGroup( m_undoGroup );
      foreach ( var instance in m_objects )
        if ( instance != null )
          UnityEngine.Object.DestroyImmediate( instance );
      m_objects.Clear();
      if ( !string.IsNullOrEmpty( m_assets ) )
        AssetDatabase.DeleteAsset( m_assets );
      m_assets = null;
    }

    private GameObject Object( string name = "Conversion test", Transform parent = null )
    {
      var result = new GameObject( name );
      m_objects.Add( result );
      SceneManager.MoveGameObjectToScene( result, m_scene );
      if ( parent != null )
        result.transform.SetParent( parent, false );
      return result;
    }

    private string AssetPath( string name )
    {
      if ( m_assets == null ) {
        string folder = "PhysXConversionTestAssets" + Guid.NewGuid().ToString( "N" );
        AssetDatabase.CreateFolder( "Assets/AGXUnity/Tests/Editor", folder );
        m_assets = "Assets/AGXUnity/Tests/Editor/" + folder;
      }
      return m_assets + "/" + name;
    }

    private PhysXConversion.Candidate Candidate( Component component ) =>
      PhysXConversion.Discover( m_objects.Where( o => o != null && o.transform.parent == null ) ).Single( c => c.Source == component );

    [Test]
    public void ConvertingSecondColliderPreservesFirstAndSupportsUndoRedo()
    {
      var root = Object();
      var first = root.AddComponent<BoxCollider>();
      var second = root.AddComponent<BoxCollider>();
      second.center = new Vector3( 1, 2, 3 );
      second.size = new Vector3( 2, 4, 6 );
      second.enabled = false;
      second.isTrigger = true;
      root.layer = 2;
      root.transform.localScale = new Vector3( 2, 3, 4 );
      var result = PhysXConversion.ConvertScene( new Component[] { second } );
      Assert.That( result.Errors, Is.Empty );
      Assert.That( result.Converted, Is.EqualTo( 1 ) );
      Assert.That( root.GetComponents<BoxCollider>(), Is.EqualTo( new[] { first } ) );
      var shape = root.GetComponentInChildren<AGXUnity.Collide.Box>( true );
      Assert.That( shape.HalfExtents, Is.EqualTo( new Vector3( 2, 6, 12 ) ) );
      Assert.That( shape.transform.localPosition, Is.EqualTo( new Vector3( 1, 2, 3 ) ) );
      Assert.That( shape.enabled, Is.False );
      Assert.That( shape.IsSensor, Is.True );
      Assert.That( shape.gameObject.layer, Is.EqualTo( 2 ) );
      Undo.PerformUndo();
      Assert.That( root.GetComponents<BoxCollider>().Length, Is.EqualTo( 2 ) );
      Assert.That( root.GetComponentInChildren<AGXUnity.Collide.Box>( true ), Is.Null );
      Undo.PerformRedo();
      Assert.That( root.GetComponents<BoxCollider>().Length, Is.EqualTo( 1 ) );
      shape = root.GetComponentInChildren<AGXUnity.Collide.Box>( true );
      Assert.That( shape.HalfExtents, Is.EqualTo( new Vector3( 2, 6, 12 ) ) );
      Assert.That( shape.enabled, Is.False );
    }

    [TestCase( 0, 4, 4 )]
    [TestCase( 1, 4, 10 )]
    [TestCase( 2, 3, 18 )]
    public void CapsulePreservesAxisAndScaledDimensions( int axis, float radius, float height )
    {
      var root = Object();
      root.transform.localScale = new Vector3( -2, 3, 4 );
      var source = root.AddComponent<CapsuleCollider>();
      source.direction = axis;
      source.radius = 1;
      source.height = 6;
      Assert.That( PhysXConversion.ConvertScene( new Component[] { source } ).Errors, Is.Empty );
      var shape = root.GetComponentInChildren<AGXUnity.Collide.Capsule>();
      Assert.That( shape.Radius, Is.EqualTo( radius ).Within( 1.0E-5f ) );
      Assert.That( shape.Height, Is.EqualTo( height ).Within( 1.0E-5f ) );
      Vector3 expectedAxis = axis == 0 ? Vector3.right : axis == 1 ? Vector3.up : Vector3.forward;
      Assert.That( Vector3.Distance( shape.transform.localRotation * Vector3.up, expectedAxis ), Is.LessThan( 1.0E-5f ) );
    }

    [Test]
    public void SquashedCapsuleBecomesSphere()
    {
      var root = Object();
      root.transform.localScale = new Vector3( 4, 1, 4 );
      var source = root.AddComponent<CapsuleCollider>();
      source.radius = 1;
      source.height = 2;
      Assert.That( PhysXConversion.ConvertScene( new Component[] { source } ).Errors, Is.Empty );
      Assert.That( root.GetComponentInChildren<AGXUnity.Collide.Capsule>(), Is.Null );
      Assert.That( root.GetComponentInChildren<AGXUnity.Collide.Sphere>().Radius, Is.EqualTo( 4 ) );
    }

    [Test]
    public void NestedInactiveBodiesAndCollidersConvertTogetherAndUndoRoot()
    {
      var parent = Object( "Parent" );
      var parentBody = parent.AddComponent<Rigidbody>();
      var ownChild = Object( "Own collider", parent.transform );
      var ownCollider = ownChild.AddComponent<BoxCollider>();
      ownChild.SetActive( false );
      var child = Object( "Child body", parent.transform );
      var childBody = child.AddComponent<Rigidbody>();
      var childCollider = child.AddComponent<SphereCollider>();
      child.SetActive( false );
      Assert.That( Candidate( parentBody ).Colliders, Is.EqualTo( new[] { ownCollider } ) );
      Assert.That( Candidate( childBody ).Colliders, Is.EqualTo( new[] { childCollider } ) );
      var result = PhysXConversion.ConvertScene( new Component[] { childBody } );
      Assert.That( result.Errors, Is.Empty );
      Assert.That( result.Converted, Is.EqualTo( 4 ) );
      Assert.That( parent.GetComponent<AGXUnity.ArticulatedRoot>(), Is.Not.Null );
      Assert.That( child.GetComponent<AGXUnity.RigidBody>(), Is.Not.Null );
      Assert.That( ownChild.GetComponentInChildren<AGXUnity.Collide.Box>( true ), Is.Not.Null );
      Undo.PerformUndo();
      Assert.That( parent.GetComponent<AGXUnity.ArticulatedRoot>(), Is.Null );
      Assert.That( parent.GetComponent<AGXUnity.RigidBody>(), Is.Null );
      Assert.That( parent.GetComponent<Rigidbody>(), Is.Not.Null );
      Assert.That( child.GetComponent<Rigidbody>(), Is.Not.Null );
    }

    [Test]
    public void UnsupportedWheelBlocksWholeScenePlanWithoutCreatingChildren()
    {
      var root = Object();
      var body = root.AddComponent<Rigidbody>();
      root.AddComponent<WheelCollider>();
      var other = Object( "Unrelated" );
      var box = other.AddComponent<BoxCollider>();
      var result = PhysXConversion.ConvertScene( new Component[] { body, box } );
      Assert.That( result.Errors, Is.Not.Empty );
      Assert.That( body, Is.Not.Null );
      Assert.That( box, Is.Not.Null );
      Assert.That( root.transform.childCount, Is.Zero );
      Assert.That( other.transform.childCount, Is.Zero );
    }

    [Test]
    public void IncomingJointBlocksConnectedBody()
    {
      var root = Object();
      var body = root.AddComponent<Rigidbody>();
      var other = Object( "Joint owner" );
      other.AddComponent<Rigidbody>();
      other.AddComponent<FixedJoint>().connectedBody = body;
      Assert.That( Candidate( body ).Supported, Is.False );
      Assert.That( PhysXConversion.ConvertScene( new Component[] { body } ).Errors, Is.Not.Empty );
      Assert.That( root.GetComponent<AGXUnity.RigidBody>(), Is.Null );
    }

    [Test]
    public void NestedUnsupportedBodyBlocksEligibilityOfParent()
    {
      var parent = Object();
      var body = parent.AddComponent<Rigidbody>();
      var child = Object( "Unsupported child", parent.transform );
      child.AddComponent<Rigidbody>();
      child.AddComponent<WheelCollider>();
      Assert.That( Candidate( body ).Supported, Is.False );
    }

    [Test]
    public void MissingMeshIsRejectedBeforeMutation()
    {
      var root = Object();
      var mesh = root.AddComponent<MeshCollider>();
      Assert.That( PhysXConversion.ConvertScene( new Component[] { mesh } ).Errors, Is.Not.Empty );
      Assert.That( root.transform.childCount, Is.Zero );
      Assert.That( mesh, Is.Not.Null );
    }

    [Test]
    public void ShearedPrimitiveIsRejected()
    {
      var parent = Object();
      parent.transform.localScale = new Vector3( 2, 1, 1 );
      var child = Object( "Sheared", parent.transform );
      child.transform.localRotation = Quaternion.Euler( 0, 0, 45 );
      var box = child.AddComponent<BoxCollider>();
      Assert.That( Candidate( box ).Supported, Is.False );
    }

    [Test]
    public void RotatedInertiaAndUnscaledCustomCenterOfMassArePreserved()
    {
      var root = Object();
      root.transform.localScale = Vector3.one * 3;
      var body = root.AddComponent<Rigidbody>();
      body.centerOfMass = new Vector3( 1, 2, 3 );
      body.inertiaTensor = new Vector3( 1, 3, 5 );
      body.inertiaTensorRotation = Quaternion.Euler( 0, 0, 45 );
      Assert.That( PhysXConversion.ConvertScene( new Component[] { body } ).Errors, Is.Empty );
      var target = root.GetComponent<AGXUnity.RigidBody>();
      Assert.That( target.MassProperties.CenterOfMassOffset.Value, Is.EqualTo( new Vector3( 1, 2, 3 ) ) );
      var diagonal = target.MassProperties.InertiaDiagonal.Value;
      var offDiagonal = target.MassProperties.InertiaOffDiagonal.Value;
      Assert.That( diagonal.x, Is.EqualTo( 2 ).Within( 1.0E-5f ) );
      Assert.That( diagonal.y, Is.EqualTo( 2 ).Within( 1.0E-5f ) );
      Assert.That( diagonal.z, Is.EqualTo( 5 ).Within( 1.0E-5f ) );
      Assert.That( offDiagonal.x, Is.EqualTo( 1 ).Within( 1.0E-5f ) );
    }

    [Test]
    public void PrefabConversionSavesReloadsAndDoesNotTouchSceneObjects()
    {
      var root = Object();
      root.AddComponent<Rigidbody>().mass = 7;
      root.AddComponent<BoxCollider>().center = Vector3.up;
      var path = AssetPath( "Body.prefab" );
      PrefabUtility.SaveAsPrefabAsset( root, path );
      int scenesBefore = SceneManager.sceneCount;
      var result = PhysXConversion.ConvertPrefab( path );
      Assert.That( result.Errors, Is.Empty );
      Assert.That( result.Converted, Is.EqualTo( 2 ) );
      Assert.That( SceneManager.sceneCount, Is.EqualTo( scenesBefore ) );
      Assert.That( root.GetComponent<Rigidbody>(), Is.Not.Null );
      var loaded = PrefabUtility.LoadPrefabContents( path );
      try {
        Assert.That( loaded.GetComponent<Rigidbody>(), Is.Null );
        Assert.That( loaded.GetComponent<BoxCollider>(), Is.Null );
        Assert.That( loaded.GetComponent<AGXUnity.RigidBody>().MassProperties.Mass.Value, Is.EqualTo( 7 ) );
        Assert.That( loaded.GetComponentInChildren<AGXUnity.Collide.Box>().transform.localPosition, Is.EqualTo( Vector3.up ) );
      }
      finally { PrefabUtility.UnloadPrefabContents( loaded ); }
      Assert.That( PhysXConversion.ConvertPrefab( path ).Converted, Is.Zero );
    }

    [Test]
    public void ReadableMeshAssetReferenceSurvivesPrefabReload()
    {
      var mesh = new Mesh {
        vertices = new[] { Vector3.zero, Vector3.right, Vector3.up },
        triangles = new[] { 0, 1, 2 }
      };
      var meshPath = AssetPath( "Source.asset" );
      AssetDatabase.CreateAsset( mesh, meshPath );
      var root = Object();
      root.AddComponent<MeshCollider>().sharedMesh = mesh;
      var path = AssetPath( "Mesh.prefab" );
      PrefabUtility.SaveAsPrefabAsset( root, path );
      Assert.That( PhysXConversion.ConvertPrefab( path ).Errors, Is.Empty );
      var loaded = PrefabUtility.LoadPrefabContents( path );
      try {
        Assert.That( loaded.GetComponentInChildren<AGXUnity.Collide.Mesh>().SourceObjects.Single(), Is.EqualTo( mesh ) );
      }
      finally { PrefabUtility.UnloadPrefabContents( loaded ); }
    }

    [Test]
    public void PrefabTerrainIsScopedAndConvertedToDeformableTerrain()
    {
      var data = new TerrainData { heightmapResolution = 33, size = new Vector3( 32, 10, 32 ) };
      AssetDatabase.CreateAsset( data, AssetPath( "Terrain.asset" ) );
      var root = Object( "Terrain" );
      root.AddComponent<Terrain>().terrainData = data;
      root.AddComponent<TerrainCollider>().terrainData = data;
      var path = AssetPath( "Terrain.prefab" );
      PrefabUtility.SaveAsPrefabAsset( root, path );
      var unrelated = Object( "Scene collider" ).AddComponent<BoxCollider>();
      var asset = AssetDatabase.LoadAssetAtPath<GameObject>( path );
      var candidates = PhysXConversion.Discover( new[] { asset } );
      Assert.That( candidates.Count, Is.EqualTo( 1 ) );
      Assert.That( candidates[ 0 ].Source, Is.TypeOf<TerrainCollider>() );
      Assert.That( PhysXConversion.ConvertPrefab( path ).Errors, Is.Empty );
      var loaded = PrefabUtility.LoadPrefabContents( path );
      try {
        Assert.That( loaded.GetComponent<AGXUnity.Model.DeformableTerrain>(), Is.Not.Null );
        Assert.That( loaded.GetComponent<AGXUnity.Collide.HeightField>(), Is.Null );
        Assert.That( loaded.GetComponent<TerrainCollider>(), Is.Null );
      }
      finally { PrefabUtility.UnloadPrefabContents( loaded ); }
      Assert.That( root.GetComponent<TerrainCollider>(), Is.Not.Null );
      Assert.That( unrelated, Is.Not.Null );
    }

    [Test]
    public void ExistingAgxBodyIsRejected()
    {
      var root = Object();
      var body = root.AddComponent<Rigidbody>();
      root.AddComponent<AGXUnity.RigidBody>();
      Assert.That( Candidate( body ).Supported, Is.False );
      Assert.That( PhysXConversion.ConvertScene( new Component[] { body } ).Errors, Is.Not.Empty );
      Assert.That( body, Is.Not.Null );
    }

    [Test]
    public void ThreeNestedBodiesHaveOnlyOneArticulatedRoot()
    {
      var top = Object( "Top" );
      top.AddComponent<Rigidbody>();
      var middle = Object( "Middle", top.transform );
      var middleBody = middle.AddComponent<Rigidbody>();
      Object( "Bottom", middle.transform ).AddComponent<Rigidbody>();
      Assert.That( PhysXConversion.ConvertScene( new Component[] { middleBody } ).Errors, Is.Empty );
      Assert.That( top.GetComponentsInChildren<AGXUnity.ArticulatedRoot>( true ).Length, Is.EqualTo( 1 ) );
      Assert.That( top.GetComponent<AGXUnity.ArticulatedRoot>(), Is.Not.Null );
    }

    [TestCase( false )]
    [TestCase( true )]
    public void NestedPrefabAndVariantConversionPreserveSourcePrefab( bool variant )
    {
      var original = Object( "Original" );
      original.AddComponent<BoxCollider>();
      var sourcePath = AssetPath( "Original.prefab" );
      var sourceAsset = PrefabUtility.SaveAsPrefabAsset( original, sourcePath );
      var instance = (GameObject)PrefabUtility.InstantiatePrefab( sourceAsset, m_scene );
      m_objects.Add( instance );
      var root = variant ? instance : Object( "Container" );
      if ( !variant )
        instance.transform.SetParent( root.transform, false );
      var targetPath = AssetPath( "Target.prefab" );
      PrefabUtility.SaveAsPrefabAsset( root, targetPath );
      var result = PhysXConversion.ConvertPrefab( targetPath );
      Assert.That( result.Errors, Is.Empty );
      Assert.That( result.Converted, Is.EqualTo( 1 ) );
      var loaded = PrefabUtility.LoadPrefabContents( targetPath );
      try {
        Assert.That( loaded.GetComponentInChildren<BoxCollider>(), Is.Null );
        Assert.That( loaded.GetComponentInChildren<AGXUnity.Collide.Box>(), Is.Not.Null );
      }
      finally { PrefabUtility.UnloadPrefabContents( loaded ); }
      Assert.That( AssetDatabase.LoadAssetAtPath<GameObject>( sourcePath ).GetComponent<BoxCollider>(), Is.Not.Null );
      Assert.That( AssetDatabase.LoadAssetAtPath<GameObject>( sourcePath ).GetComponentInChildren<AGXUnity.Collide.Box>(), Is.Null );
    }

    [Test]
    public void PrefabSkipsUnsupportedBodyButConvertsIndependentCollider()
    {
      var root = Object();
      var bodyObject = Object( "Unsupported body", root.transform );
      bodyObject.AddComponent<Rigidbody>();
      bodyObject.AddComponent<WheelCollider>();
      Object( "Supported collider", root.transform ).AddComponent<BoxCollider>();
      var path = AssetPath( "Partial.prefab" );
      PrefabUtility.SaveAsPrefabAsset( root, path );
      var result = PhysXConversion.ConvertPrefab( path );
      Assert.That( result.Errors, Is.Empty );
      Assert.That( result.Converted, Is.EqualTo( 1 ) );
      Assert.That( result.Skipped, Is.EqualTo( 2 ) );
      var loaded = PrefabUtility.LoadPrefabContents( path );
      try {
        Assert.That( loaded.GetComponentInChildren<Rigidbody>(), Is.Not.Null );
        Assert.That( loaded.GetComponentInChildren<WheelCollider>(), Is.Not.Null );
        Assert.That( loaded.GetComponentInChildren<BoxCollider>(), Is.Null );
        Assert.That( loaded.GetComponentInChildren<AGXUnity.Collide.Box>(), Is.Not.Null );
      }
      finally { PrefabUtility.UnloadPrefabContents( loaded ); }
    }

    [Test]
    public void PagerOnlyBlocksItsConnectedTerrains()
    {
      var data = new TerrainData { heightmapResolution = 33, size = new Vector3( 32, 10, 32 ) };
      AssetDatabase.CreateAsset( data, AssetPath( "Terrain.asset" ) );
      var first = Object( "Pager terrain" );
      var firstTerrain = first.AddComponent<Terrain>();
      firstTerrain.terrainData = data;
      first.AddComponent<TerrainCollider>().terrainData = data;
      first.AddComponent<AGXUnity.Model.DeformableTerrainPager>();
      var neighbor = Object( "Pager neighbor" );
      var neighborTerrain = neighbor.AddComponent<Terrain>();
      neighborTerrain.terrainData = data;
      var neighborCollider = neighbor.AddComponent<TerrainCollider>();
      neighborCollider.terrainData = data;
      firstTerrain.SetNeighbors( null, null, neighborTerrain, null );
      neighborTerrain.SetNeighbors( firstTerrain, null, null, null );
      var unrelated = Object( "Separate terrain" );
      unrelated.AddComponent<Terrain>().terrainData = data;
      var unrelatedCollider = unrelated.AddComponent<TerrainCollider>();
      unrelatedCollider.terrainData = data;
      Assert.That( Candidate( neighborCollider ).Supported, Is.False );
      Assert.That( Candidate( unrelatedCollider ).Supported, Is.True );
    }

    [Test]
    public void RigidbodyCollisionDisablingIsTransferredToShapes()
    {
      var root = Object();
      var body = root.AddComponent<Rigidbody>();
      body.detectCollisions = false;
      root.AddComponent<BoxCollider>();
      Assert.That( PhysXConversion.ConvertScene( new Component[] { body } ).Errors, Is.Empty );
      var shape = root.GetComponentInChildren<AGXUnity.Collide.Box>();
      Assert.That( shape.enabled, Is.True );
      Assert.That( shape.CollisionsEnabled, Is.False );
    }

    [Test]
    public void DestroyedSelectionIsReportedAsStale()
    {
      var root = Object();
      var body = root.AddComponent<Rigidbody>();
      var available = PhysXConversion.Discover( new[] { root } );
      UnityEngine.Object.DestroyImmediate( body );
      var plan = PhysXConversion.BuildPlan( available, new Component[] { body } );
      Assert.That( plan.CanExecute, Is.False );
      Assert.That( plan.Errors, Is.Not.Empty );
    }
  }
}
