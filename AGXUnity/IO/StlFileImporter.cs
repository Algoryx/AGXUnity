using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

using Object = UnityEngine.Object;

namespace AGXUnity.IO
{
  /// <summary>
  /// Read and/or instantiates STL files, binary or ASCII, as meshes/GameoObjects.
  /// </summary>
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#stl-import" )]
  public class StlFileImporter
  {
    /// <summary>
    /// Default angle where normal angles below this angle is
    /// interpreted as a smooth surface. Default: 26 degrees.
    /// </summary>
    public static readonly float DefaultNormalAngleThreshold = 26.0f;

    /// <summary>
    /// Read meshes from STL file where the STL file is either binary of ASCII.
    /// This method throws if the file isn't found or when the file contains errors.
    /// </summary>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <returns>Array of meshes read from the given STL file.</returns>
    public static Mesh[] Read( string stlFile ) => Read( stlFile, DefaultNormalAngleThreshold );

    /// <summary>
    /// Read meshes from STL file where the STL file is either binary of ASCII.
    /// This method throws if the file isn't found or when the file contains errors.
    /// </summary>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <param name="normalSmoothAngleThreshold">Angle where normal angles below this angle is
    ///                                          interpreted as a smooth surface.</param>
    /// <returns>Array of meshes read from the given STL file.</returns>
    public static Mesh[] Read( string stlFile,
                               float normalSmoothAngleThreshold )
    {
      var stlFileInfo = new FileInfo( stlFile );
      if ( !stlFileInfo.Exists )
        throw new Exception( $"{stlFile} doesn't exist." );
      if ( stlFileInfo.Extension.ToLower() != ".stl" )
        throw new Exception( $"Unknown STL extension: {stlFileInfo.Extension}" );

      byte[] buffer = null;
      using ( var stream = stlFileInfo.OpenRead() ) {
        if ( stream.Length > int.MaxValue )
          throw new Exception( $"{stlFile} is too large - maximum supported size is 2048 Mb." );

        buffer = new byte[ stream.Length ];
        stream.Read( buffer, 0, (int)stream.Length );
      }

      //var asciiMatch = "solid";
      //var solidStr   = System.Text.Encoding.ASCII.GetString( buffer, 0, asciiMatch.Length );
      if ( !IsBinary( buffer, 0, 256 ) )
        return ReadAscii( buffer, normalSmoothAngleThreshold );
      else
        return ReadBinary( buffer, normalSmoothAngleThreshold );
    }

    /// <summary>
    /// Read meshes from STL file and instantiate game objects with UnityEngine.MeshRenderer
    /// and UnityEngine.MeshFilter. <seealso cref="Read(string)"/>
    /// </summary>
    /// <remarks>
    /// If the STL file contains several meshes, a parent game object is created without
    /// any UnityEngine.MeshFilter or UnityEngine.MeshRenderer and the meshes are instead
    /// added as children.
    /// </remarks>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <returns>Array of parent game objects.</returns>
    public static GameObject[] Instantiate( string stlFile ) => Instantiate( stlFile, DefaultNormalAngleThreshold, null );

    /// <summary>
    /// Read meshes from STL file and instantiate game objects with UnityEngine.MeshRenderer
    /// and UnityEngine.MeshFilter. <seealso cref="Read(string)"/>
    /// </summary>
    /// <remarks>
    /// If the STL file contains several meshes, a parent game object is created without
    /// any UnityEngine.MeshFilter or UnityEngine.MeshRenderer and the meshes are instead
    /// added as children.
    /// </remarks>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <param name="onCreate">Callback when objects are created.</param>
    /// <returns>Array of parent game objects.</returns>
    public static GameObject[] Instantiate( string stlFile, Action<Object> onCreate ) => Instantiate( stlFile, DefaultNormalAngleThreshold, onCreate );

    /// <summary>
    /// Read meshes from STL file and instantiate game objects with UnityEngine.MeshRenderer
    /// and UnityEngine.MeshFilter. <seealso cref="Read(string)"/>
    /// </summary>
    /// <remarks>
    /// If the STL file contains several meshes, a parent game object is created without
    /// any UnityEngine.MeshFilter or UnityEngine.MeshRenderer and the meshes are instead
    /// added as children.
    /// </remarks>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <param name="normalSmoothAngleThreshold">Angle where normal angles below this angle is
    ///                                          interpreted as a smooth surface.</param>
    /// <returns>Array of parent game objects.</returns>
    public static GameObject[] Instantiate( string stlFile, float normalSmoothAngleThreshold ) => Instantiate( stlFile, normalSmoothAngleThreshold, null );

    /// <summary>
    /// Read meshes from STL file and instantiate game objects with UnityEngine.MeshRenderer
    /// and UnityEngine.MeshFilter. <seealso cref="Read(string)"/>
    /// </summary>
    /// <remarks>
    /// If the STL file contains several meshes, a parent game object is created without
    /// any UnityEngine.MeshFilter or UnityEngine.MeshRenderer and the meshes are instead
    /// added as children.
    /// </remarks>
    /// <param name="stlFile">STL file, including relative path to current context.</param>
    /// <param name="normalSmoothAngleThreshold">Angle where normal angles below this angle is
    ///                                          interpreted as a smooth surface.</param>
    /// <param name="onCreate">Callback when objects are created.</param>
    /// <returns>Array of parent game objects.</returns>
    public static GameObject[] Instantiate( string stlFile,
                                            float normalSmoothAngleThreshold,
                                            Action<Object> onCreate )
    {
      var createdParents = new List<GameObject>();
      var meshes = Read( stlFile, normalSmoothAngleThreshold );
      if ( meshes.Length == 0 ) {
        Debug.LogWarning( $"{stlFile} contained 0 meshes." );
        return createdParents.ToArray();
      }

      var filename = new FileInfo( stlFile).Name;
      filename = filename.Substring( 0, filename.LastIndexOf( '.' ) );
      var parent = meshes.Length > 1 ?
                      new GameObject( Factory.CreateName( filename ) ) :
                      null;
      if ( parent != null ) {
        createdParents.Add( parent );
        onCreate?.Invoke( parent );
      }

      foreach ( var mesh in meshes ) {
        var go                  = new GameObject( Factory.CreateName( filename ) );
        var filter              = go.AddComponent<MeshFilter>();
        var renderer            = go.AddComponent<MeshRenderer>();
        filter.sharedMesh       = mesh;
        renderer.sharedMaterial = Rendering.ShapeVisual.DefaultMaterial;
        if ( parent != null )
          go.transform.parent = parent.transform;
        else
          createdParents.Add( go );

        onCreate?.Invoke( go );
        onCreate?.Invoke( filter );
        onCreate?.Invoke( renderer );
      }

      return createdParents.ToArray();
    }

    /// <summary>
    /// Parse read bytes from a STL ASCII file.
    /// </summary>
    /// <param name="bytes">Read bytes.</param>
    /// <returns>Meshes parsed from <paramref name="bytes"/>.</returns>
    public static Mesh[] ReadAscii( byte[] bytes ) => ReadAscii( bytes, DefaultNormalAngleThreshold );

    /// <summary>
    /// Parse read bytes from a STL ASCII file.
    /// </summary>
    /// <param name="bytes">Read bytes.</param>
    /// <param name="normalSmoothAngleThreshold">Angle where normal angles below this angle is
    ///                                          interpreted as a smooth surface.</param>
    /// <returns>Meshes parsed from <paramref name="bytes"/>.</returns>
    public static Mesh[] ReadAscii( byte[] bytes,
                                    float normalSmoothAngleThreshold )
    {
      var meshes = new List<Mesh>();

      using ( var memStream = new MemoryStream( bytes ) )
      using ( var strStream = new StreamReader( memStream ) ) {
        string line = string.Empty;
        MeshData meshData = null;
        int lineCount = 0;
        while ( ( line = strStream.ReadLine()?.Trim() ) != null ) {
          ++lineCount;

          var lineElements = line.SplitSpace();
          if ( lineElements.Length == 0 )
            continue;
          else if ( lineElements[ 0 ] == "endsolid" ) {
            if ( meshData == null || !meshData.IsValid )
              throw new Exception( $"Found \"endsolid\" on line {lineCount} but the mesh isn't valid." );

            meshes.Add( (Mesh)meshData );

            continue;
          }
          else if ( lineElements[ 0 ] == "solid" ) {
            if ( meshData != null && meshData.IsValid )
              throw new Exception( $"Expecting \"endsolid\" before \"solid\" on line {lineCount}." );

            meshData = new MeshData( 512, normalSmoothAngleThreshold );
            var unitIndex = lineElements.IndexOf( str => str.StartsWith( "unit" ) );
            // Assuming unit is default.
            if ( unitIndex < 0 )
              continue;

            var unit = string.Empty;
            // Space between unit and =, e.g., "stl unit = MM" or "stl unit =MM".
            if ( unitIndex + 1 < lineElements.Length && lineElements[ unitIndex + 1 ].StartsWith( "=" ) ) {
              // "unit = MM"
              if ( lineElements[ unitIndex + 1 ].Length == 1 && unitIndex + 2 < lineElements.Length )
                unit = lineElements[ unitIndex + 2 ].Trim( '\"', '>' );
              // "unit =MM"
              else if ( lineElements[ unitIndex + 1 ].Length > 1 )
                unit = lineElements[ unitIndex + 1 ].Trim( '=', '\"', '>' );
            }
            // No space between unit and =, e.g., "stl unit=MM" or "stl unit= MM".
            else if ( lineElements[ unitIndex ].Contains( '=' ) ) {
              // "unit=MM"
              if ( unitIndex + 1 == lineElements.Length )
                unit = lineElements[ unitIndex ].Trim( '\"', '>' ).Split( '=' )[ 1 ];
              // "unit= MM"
              else
                unit = lineElements[ unitIndex + 1 ].Trim( '\"', '>' );
            }

            meshData.SetUnit( unit );

            continue;
          }
          // Ignoring "outer loop" and "endloop", errors are handled in "facet" and "endfacet".
          else if ( line == "outer loop" || lineElements[ 0 ] == "endloop" )
            continue;

          var isBeginTriangle = lineElements[ 0 ] == "facet";
          var isVertex        = lineElements[ 0 ] == "vertex";
          var isEndTriangle   = lineElements[ 0 ] == "endfacet";
          if ( isBeginTriangle && lineElements.Length != 5 )
            throw new Exception( $"Unexpected \"facet\" on line {lineCount}: {line}" );
          if ( isVertex && lineElements.Length != 4 )
            throw new Exception( $"Unexpected \"vertex\" on line {lineCount}: {line}" );
          if ( isEndTriangle && ( meshData == null || !meshData.IsTriangleDataComplete ) )
            throw new Exception( $"Unexpected \"endfacet\" on line {lineCount} (missing \"facet normal\" or \"vertex\"): {line}" );

          // Reading normal: "facet normal 0 -0.624695047554391 -0.780868809443057"
          if ( isBeginTriangle )
            meshData.BeginTriangle( lineElements.ParseVector3( 2 ).ToLeftHanded() );
          // Reading vertex: "vertex -19.6885398409434 1.02256672804167 74.933542863845"
          else if ( isVertex )
            meshData.AddTriangleVertex( lineElements.ParseVector3( 1 ).ToLeftHanded() );
          // Reading endfacet: "endfacet" expecting 3 "vertex" and 1 "facet" before this.
          else if ( isEndTriangle ) {
            var numVertices = meshData.EndTriangle();
            if ( numVertices != 3 )
              throw new Exception( $"Expecting 3 \"vertex\" within \"outer loop\" and \"endloop\" - " +
                                   $"got {numVertices} at \"endfacet\" on line {lineCount}." );
          }
        }
      }

      return meshes.ToArray();
    }

    /// <summary>
    /// Parse read bytes from a STL binary file.
    /// </summary>
    /// <param name="bytes">Read bytes.</param>
    /// <returns>Array of meshes parsed from <paramref name="bytes"/>.</returns>
    public static Mesh[] ReadBinary( byte[] bytes ) => ReadBinary( bytes, DefaultNormalAngleThreshold );

    /// <summary>
    /// Parse read bytes from a STL binary file.
    /// </summary>
    /// <param name="bytes">Read bytes.</param>
    /// <param name="normalSmoothAngleThreshold">Angle where normal angles below this angle is
    ///                                          interpreted as a smooth surface.</param>
    /// <returns>Array of meshes parsed from <paramref name="bytes"/>.</returns>
    public static Mesh[] ReadBinary( byte[] bytes,
                                     float normalSmoothAngleThreshold )
    {
      // Specification from https://en.wikipedia.org/wiki/STL_(file_format).
      // -------------------------------------------------------------------
      // UINT8[80] – Header
      // UINT32 – Number of triangles
      // 
      // foreach triangle
      // REAL32[3] – Normal vector
      // REAL32[3] – Vertex 1
      // REAL32[3] – Vertex 2
      // REAL32[3] – Vertex 3
      // UINT16 – Attribute byte count
      // end
      // -------------------------------------------------------------------

      var meshes = new List<Mesh>();
      using ( var memStream = new MemoryStream( bytes ) )
      using ( var binStream = new BinaryReader( memStream ) ) {
        binStream.ReadBytes( 80 );
        int numTriangles = (int)binStream.ReadUInt32();
        var meshData  = new MeshData( 3 * numTriangles, normalSmoothAngleThreshold );
        var vertexBuffer = new Vector3[ 3 ];
        for ( int globalTriangleNumber = 0; globalTriangleNumber < numTriangles; ++globalTriangleNumber ) {
          var normal = binStream.ReadVector3().ToLeftHanded();
          for ( int triangleVertexIndex = 0; triangleVertexIndex < 3; ++triangleVertexIndex )
            vertexBuffer[ triangleVertexIndex ] = binStream.ReadVector3().ToLeftHanded();
          meshData.Add( normal, vertexBuffer );

          // TODO STL: Parse color if bit index 15 is set (or not in some implementations) ("hack").
          binStream.ReadUInt16();

          if ( meshData.Vertices.Count >= ushort.MaxValue - 3 )
            meshes.Add( (Mesh)meshData );
        }
        if ( meshData.IsValid )
          meshes.Add( (Mesh)meshData );
      }

      return meshes.ToArray();
    }

    /// <summary>
    /// True if the bytes in given range is interpreted as binary content.
    /// </summary>
    /// <param name="bytes">Bytes buffer.</param>
    /// <param name="startIndex">Start index in buffer.</param>
    /// <param name="count">Number of elements to check.</param>
    /// <returns>True if the content is interpreted as binary - otherwise false.</returns>
    public static bool IsBinary( byte[] bytes, int startIndex, int count )
    {
      count = System.Math.Min( startIndex + count, bytes.Length );
      return System.Text.Encoding.ASCII.GetString( bytes, startIndex, count ).Any( c => char.IsControl( c ) &&
                                                                                        c != '\r' &&
                                                                                        c != '\n' &&
                                                                                        c != '\t' );
    }

    private class MeshData
    {
      public enum VertexUnit
      {
        Mm,
        Cm,
        Dm,
        M
      }

      public List<Vector3> TriangleNormals    = null;
      public List<Vector3> Vertices           = null;
      public List<int> Triangles              = null;
      public float NormalSmoothAngleThreshold = DefaultNormalAngleThreshold;
      public VertexUnit Unit                  = VertexUnit.M;

      public static explicit operator Mesh( MeshData meshData )
      {
        if ( !meshData.IsValid )
          throw new Exception( $"Mesh data isn't valid with #vertices = {meshData.Vertices.Count}, " +
                               $"#triangle indices = {meshData.Triangles.Count} and " +
                               $"#triangle normals = {meshData.TriangleNormals.Count}." );

        var mesh = new Mesh();
        mesh.SetVertices( meshData.Vertices );
        mesh.SetTriangles( meshData.Triangles, 0 );
        mesh.normals = meshData.CalculateNormals();
        mesh.RecalculateTangents();
#if UNITY_2019_1_OR_NEWER
        mesh.Optimize();
#endif

        meshData.TriangleNormals.Clear();
        meshData.Vertices.Clear();
        meshData.Triangles.Clear();
        meshData.m_sharedVerticesMap.Clear();
        meshData.m_triangleData = TriangleData.Invalid;

        return mesh;
      }

      public bool IsValid
      {
        get
        {
          return Vertices.Count > 0 &&
                 Vertices.Count == Triangles.Count &&
                 TriangleNormals.Count == Vertices.Count / 3;
        }
      }

      public bool IsTriangleDataComplete { get { return m_triangleData.IsComplete; } }

      public MeshData( int expectedNumVertices,
                       float normalSmoothAngleThreshold,
                       VertexUnit unit = VertexUnit.M )
      {
        TriangleNormals            = new List<Vector3>( expectedNumVertices / 3 );
        Vertices                   = new List<Vector3>( expectedNumVertices );
        Triangles                  = new List<int>( expectedNumVertices );
        NormalSmoothAngleThreshold = normalSmoothAngleThreshold;
      }

      public void Add( Vector3 normal, Vector3[] vertices )
      {
        TriangleNormals.Add( normal );
        // Reverse order applying Unity mesh triangle format.
        AddVertex( vertices[ 2 ] );
        AddVertex( vertices[ 1 ] );
        AddVertex( vertices[ 0 ] );
      }

      public void SetUnit( string unit )
      {
        if ( string.IsNullOrEmpty( unit ) )
          return;
        unit = unit.ToLower().FirstCharToUpperCase();
        foreach ( VertexUnit unitEnum in System.Enum.GetValues( typeof( VertexUnit ) ) ) {
          if ( unitEnum.ToString() == unit ) {
            Unit = unitEnum;
            return;
          }
        }
      }

      public void BeginTriangle( Vector3 normal )
      {
        m_triangleData = TriangleData.Create( normal );
      }

      public void AddTriangleVertex( Vector3 vertex )
      {
        m_triangleData.Vertices[ m_triangleData.Index++ ] = vertex;
      }

      public int EndTriangle()
      {
        if ( !m_triangleData.IsComplete )
          return System.Math.Max( m_triangleData.Index, 0 );

        Add( m_triangleData.Normal, m_triangleData.Vertices );

        m_triangleData = TriangleData.Invalid;

        return 3;
      }

      private void AddVertex( Vector3 vertex )
      {
        var vertexIndex = Vertices.Count;
        if ( !m_sharedVerticesMap.TryGetValue( vertex, out var sharedVertices ) ) {
          sharedVertices = new List<int>();
          m_sharedVerticesMap.Add( vertex, sharedVertices );
        }
        sharedVertices.Add( vertexIndex );

        Triangles.Add( vertexIndex );
        Vertices.Add( ConvertVertex( vertex ) );
      }

      private Vector3 ConvertVertex( Vector3 vertex )
      {
        return Unit == VertexUnit.M ?
                vertex :
               Unit == VertexUnit.Dm ?
                 1.0E-1f * vertex :
               Unit == VertexUnit.Cm ?
                 1.0E-2f * vertex :
                 1.0E-3f * vertex;
      }

      private Vector3[] CalculateNormals()
      {
        var normals      = new Vector3[ Vertices.Count ];
        var dotThreshold = Mathf.Cos( NormalSmoothAngleThreshold * Mathf.Deg2Rad );
        foreach ( var sharedVertexList in m_sharedVerticesMap.Values ) {
          for ( int i1 = 0; i1 < sharedVertexList.Count; ++i1 ) {
            var vertex1Index    = sharedVertexList[ i1 ];
            var triangle1Index  = vertex1Index / 3;
            var triangle1Normal = TriangleNormals[ triangle1Index ];
            var normal          = triangle1Normal;
            for ( int i2 = 0; i2 < sharedVertexList.Count; ++i2 ) {
              // Normal added above.
              if ( i1 == i2 )
                continue;
              var vertex2Index    = sharedVertexList[ i2 ];
              var triangle2Index  = vertex2Index / 3;
              var triangle2Normal = TriangleNormals[ triangle2Index ];
              var dot             = Vector3.Dot( triangle1Normal, triangle2Normal );
              if ( dot > dotThreshold )
                normal += triangle2Normal;
            }
            normals[ vertex1Index ] = normal.normalized;
          }
        }

        return normals;
      }

      private struct TriangleData
      {
        public static TriangleData Invalid { get { return new TriangleData() { Vertices = null, Index = -1 }; } }

        public static TriangleData Create( Vector3 normal )
        {
          return new TriangleData()
          {
            Normal   = normal,
            Vertices = new Vector3[ 3 ],
            Index    = 0
          };
        }

        public Vector3 Normal;
        public Vector3[] Vertices;
        public int Index;

        public bool IsComplete { get { return Index == 3 && Vertices != null; } }
      }

      private Dictionary<Vector3, List<int>> m_sharedVerticesMap = new Dictionary<Vector3, List<int>>();
      private TriangleData m_triangleData;
    }
  }
}
