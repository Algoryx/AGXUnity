using AGXUnity.Model;
using AGXUnity.Utils;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Rendering
{
  [RequireComponent( typeof( DeformableTerrainWheel ) )]
  [DisallowMultipleComponent]
  [AddComponentMenu( "AGXUnity/Rendering/Deformable Terrain Wheel Debug Renderer" )]
  public class DeformableTerrainWheelDebugRenderer : ScriptComponent
  {
    [field: SerializeField]
    [Tooltip( "Render sampled regression-plane terrain points." )]
    public bool RenderSamplePoints { get; set; } = true;

    [field: SerializeField]
    [Tooltip( "Render regression plane outlines, axes, normals, force-feedback plane, and angle vectors." )]
    public bool RenderLines { get; set; } = true;

    [field: SerializeField]
    [Min( 0.0f )]
    [Tooltip( "Radius of sampled terrain point gizmos." )]
    public float SamplePointRadius { get; set; } = 0.03f;

    [field: SerializeField]
    [Min( 0.0f )]
    [Tooltip( "Width of rendered debug line boxes." )]
    public float LineWidth { get; set; } = 0.02f;

    [field: SerializeField]
    [Min( 1 )]
    [Tooltip( "Regression grid point count along the wheel forward direction. The native default is 9." )]
    public int GridPointCountX { get; set; } = 9;

    [field: SerializeField]
    [Min( 1 )]
    [Tooltip( "Regression grid point count along the wheel lateral direction. The native default is 5." )]
    public int GridPointCountY { get; set; } = 5;

    protected override bool Initialize()
    {
      m_wheel = GetComponent<DeformableTerrainWheel>()?.GetInitialized<DeformableTerrainWheel>();
      if ( m_wheel == null || m_wheel.Native == null ) {
        Debug.LogWarning( "DeformableTerrainWheelDebugRenderer requires an initialized DeformableTerrainWheel.", this );
        return false;
      }

      return true;
    }

    protected override void OnDisable()
    {
      m_lines.Clear();
      m_spheres.Clear();
      base.OnDisable();
    }

    private void LateUpdate()
    {
      Build();
    }

    private void OnDrawGizmos()
    {
      if ( Application.isPlaying )
        Build();

      if ( RenderLines ) {
        foreach ( var line in m_lines )
          DrawLineBox( line );
      }

      if ( RenderSamplePoints ) {
        foreach ( var sphere in m_spheres ) {
          Gizmos.color = sphere.Color;
          Gizmos.DrawSphere( sphere.Position, SamplePointRadius );
        }
      }
    }

    private void Build()
    {
      m_lines.Clear();
      m_spheres.Clear();

      var wheel = m_wheel?.Native;
      if ( wheel == null )
        return;

      var terrain = wheel.getActiveTerrain();
      if ( terrain == null ) {
        m_hasValidContactFrame = false;
        return;
      }

      var parameters = wheel.getRegressionPlanesParameters();
      if ( parameters == null || !parameters.isValid() )
        return;

      UpdateForwardSign( wheel, parameters );

      var wheelTransform = wheel.getWheelShape().getTransform();
      var terrainUp = terrain.getUpDirection();
      var gridOrigin = GetGridOrigin( wheel, wheelTransform );
      var gridAxes = agxTerrain.TerrainWheelPhysics.computeGridCoordinateAxes( terrainUp, wheelTransform, m_forwardSign );
      var template = CreateGridTemplate( wheel, terrain, parameters );
      if ( template.Count == 0 )
        return;

      var surfacePoints = new List<agx.Vec3>( template.Count );
      var acceptedLocalPoints = new List<agx.Vec3>( template.Count );
      foreach ( var localPoint in template ) {
        var world = gridOrigin + localPoint.x * gridAxes.forwardAxis + localPoint.y * gridAxes.lateralAxis;
        if ( !InterpolateTerrainHeight( terrain, world, out var height ) )
          continue;

        world += ( terrain.getPosition() * terrainUp + height - world * terrainUp ) * terrainUp;
        surfacePoints.Add( world );
        acceptedLocalPoints.Add( localPoint );

        if ( RenderSamplePoints )
          m_spheres.Add( new SphereData( world.ToHandedVector3(), DebugColorTriplets[ 0 ].Point ) );
      }

      if ( surfacePoints.Count < 3 )
        return;

      var plane = FitPlane( surfacePoints, acceptedLocalPoints, gridOrigin, gridAxes.forwardAxis, gridAxes.lateralAxis, gridAxes.upAxis );
      if ( plane.Normal.length() <= 0.0 )
        return;

      DrawRegressionPlane( template, gridOrigin, gridAxes, plane, wheel );
      DrawForceFeedbackPlane( wheel, parameters, plane );
      DrawAngleLines( wheel, parameters, terrain, plane.Normal );

      m_previousPlane = plane;
      m_hasValidContactFrame = true;
    }

    private void UpdateForwardSign( agxTerrain.TerrainWheel wheel, agxTerrain.RegressionPlanesParameters parameters )
    {
      var quantities = wheel.getQuantities();
      var radius = wheel.getRadius();
      var vx = quantities.getWheelFrameVel().x;
      var omegaY = quantities.getWheelFrameAngVel().y;
      var minVxAngEquiv = parameters.m_forwardDirectionVxAngularEquivalentThreshold;
      var minOmegaY = parameters.m_forwardDirectionOmegaYThreshold;

      if ( System.Math.Abs( vx ) < minVxAngEquiv * radius && System.Math.Abs( omegaY ) < minOmegaY )
        return;

      m_forwardSign = SignInt( agxTerrain.TerrainWheelPhysics.computeSlipVelocity( radius, vx, omegaY ) );
    }

    private agx.Vec3 GetGridOrigin( agxTerrain.TerrainWheel wheel, agx.AffineMatrix4x4 wheelTransform )
    {
      var wheelPosition = wheel.getWheelBody().getPosition();
      if ( !m_hasValidContactFrame )
        return wheelPosition;

      var wheelAxis = wheelTransform.transformVector( agx.Vec3.Y_AXIS() );
      return agxTerrain.TerrainWheelPhysics.computeCenterOfPlaneAtWheel( m_previousPlane.Normal,
                                                                         m_previousPlane.AveragePoint,
                                                                         wheelPosition,
                                                                         wheelAxis );
    }

    private List<agx.Vec3> CreateGridTemplate( agxTerrain.TerrainWheel wheel,
                                               agxTerrain.Terrain terrain,
                                               agxTerrain.RegressionPlanesParameters parameters )
    {
      var points = new List<agx.Vec3>();

      var centers = parameters.m_gridsCenterPoint;
      var centerScales = parameters.m_gridsCenterPointScale;
      var steps = parameters.m_gridsStepSize;
      var stepScales = parameters.m_gridsStepSizeScale;
      var shapes = parameters.m_gridsShape;
      if ( centers == null || centerScales == null || steps == null || stepScales == null ||
           shapes == null || centers.Count == 0 || centerScales.Count == 0 ||
           steps.Count == 0 || stepScales.Count == 0 || shapes.Count == 0 )
        return points;

      var center = centers[ 0 ];
      var centerScale = centerScales[ 0 ];
      var step = steps[ 0 ];
      var stepScale = stepScales[ 0 ];
      var shape = shapes[ 0 ];

      var cx = ComputeScaledLength( center.x, centerScale[ 0 ], wheel, terrain );
      var cy = ComputeScaledLength( center.y, centerScale[ 1 ], wheel, terrain );
      var dx = ComputeScaledLength( step.x, stepScale[ 0 ], wheel, terrain );
      var dy = ComputeScaledLength( step.y, stepScale[ 1 ], wheel, terrain );
      if ( !( dx > 0.0 ) )
        dx = terrain.getElementSize();
      if ( !( dy > 0.0 ) )
        dy = terrain.getElementSize();

      var nx = System.Math.Max( 1, GridPointCountX );
      var ny = System.Math.Max( 1, GridPointCountY );
      var spanX = dx * ( nx - 1 );
      var spanY = dy * ( ny - 1 );
      var startX = -0.5 * spanX;
      var startY = -0.5 * spanY;
      var a = 0.5 * spanX + 1.0e-6;
      var b = 0.5 * spanY + 1.0e-6;
      var isEllipse = shape == agxTerrain.RegressionPlanesParameters.GridShape.ELLIPSE;

      for ( int ix = 0; ix < nx; ++ix ) {
        var x = nx == 1 ? 0.0 : startX + ix * dx;
        for ( int iy = 0; iy < ny; ++iy ) {
          var y = ny == 1 ? 0.0 : startY + iy * dy;

          if ( isEllipse && !InsideEllipse( x, y, a, b ) )
            continue;

          points.Add( new agx.Vec3( cx + x, cy + y, 0.0 ) );
        }
      }

      return points;
    }

    private static bool InsideEllipse( double x, double y, double a, double b )
    {
      var aZero = !( a > 0.0 );
      var bZero = !( b > 0.0 );
      if ( aZero && bZero )
        return x * x + y * y <= 1.0e-12;
      if ( aZero )
        return System.Math.Abs( y ) <= b;
      if ( bZero )
        return System.Math.Abs( x ) <= a;

      var nx = x / a;
      var ny = y / b;
      return nx * nx + ny * ny <= 1.0;
    }

    private static double ComputeScaledLength( double value,
                                               agxTerrain.RegressionPlanesParameters.GridScale scale,
                                               agxTerrain.TerrainWheel wheel,
                                               agxTerrain.Terrain terrain )
    {
      switch ( scale ) {
        case agxTerrain.RegressionPlanesParameters.GridScale.RADIUS:
          return value * wheel.getRadius();
        case agxTerrain.RegressionPlanesParameters.GridScale.WIDTH:
          return value * wheel.getWidth();
        case agxTerrain.RegressionPlanesParameters.GridScale.ELEMENT_SIZE:
          return value * terrain.getElementSize();
        case agxTerrain.RegressionPlanesParameters.GridScale.UNITY:
        default:
          return value;
      }
    }

    private static bool InterpolateTerrainHeight( agxTerrain.Terrain terrain, agx.Vec3 worldPosition, out double height )
    {
      height = 0.0;

      if ( !terrain.isWorldPositionWithinBounds( worldPosition ) )
        return false;

      var local = terrain.transformPointToTerrain( worldPosition );
      var width = (int)terrain.getResolutionX();
      var depth = (int)terrain.getResolutionY();
      var elementSize = terrain.getElementSize();
      var halfWidth = 0.5 * ( width - 1 ) * elementSize;
      var halfDepth = 0.5 * ( depth - 1 ) * elementSize;
      var iF = ( local.x + halfWidth ) / elementSize;
      var jF = ( local.y + halfDepth ) / elementSize;
      var i0 = Mathf.FloorToInt( (float)iF );
      var j0 = Mathf.FloorToInt( (float)jF );
      var i1 = i0 + 1;
      var j1 = j0 + 1;

      if ( i0 < 0 || j0 < 0 || i1 >= width || j1 >= depth )
        return false;

      var I00 = new agx.Vec2i( i0, j0 );
      var I10 = new agx.Vec2i( i1, j0 );
      var I01 = new agx.Vec2i( i0, j1 );
      var I11 = new agx.Vec2i( i1, j1 );
      if ( !terrain.isIndexWithinBounds( I00 ) || !terrain.isIndexWithinBounds( I10 ) ||
           !terrain.isIndexWithinBounds( I01 ) || !terrain.isIndexWithinBounds( I11 ) )
        return false;

      var fx = iF - i0;
      var fy = jF - j0;
      var h00 = terrain.getHeight( I00, false );
      var h10 = terrain.getHeight( I10, false );
      var h01 = terrain.getHeight( I01, false );
      var h11 = terrain.getHeight( I11, false );

      if ( fx + fy <= 1.0 ) {
        height = ( 1.0 - fx - fy ) * h00 + fx * h10 + fy * h01;
      }
      else {
        height = ( fx + fy - 1.0 ) * h11 + ( 1.0 - fx ) * h01 + ( 1.0 - fy ) * h10;
      }

      return true;
    }

    private static PlaneData FitPlane( List<agx.Vec3> surfacePoints,
                                       List<agx.Vec3> localPoints,
                                       agx.Vec3 origin,
                                       agx.Vec3 forward,
                                       agx.Vec3 lateral,
                                       agx.Vec3 up )
    {
      var average = new agx.Vec3( 0.0, 0.0, 0.0 );
      foreach ( var point in surfacePoints )
        average += point;
      average /= surfacePoints.Count;

      double sX = 0.0, sY = 0.0, sZ = 0.0;
      double sXX = 0.0, sXY = 0.0, sYY = 0.0, sXZ = 0.0, sYZ = 0.0;
      var count = System.Math.Min( surfacePoints.Count, localPoints.Count );
      for ( int i = 0; i < count; ++i ) {
        var x = localPoints[ i ].x;
        var y = localPoints[ i ].y;
        var z = ( surfacePoints[ i ] - origin ) * up;
        sX += x;
        sY += y;
        sZ += z;
        sXX += x * x;
        sXY += x * y;
        sYY += y * y;
        sXZ += x * z;
        sYZ += y * z;
      }

      var normal = up;
      if ( Solve3x3( sXX, sXY, sX,
                     sXY, sYY, sY,
                     sX, sY, count,
                     sXZ, sYZ, sZ,
                     out var a, out var b, out _ ) )
        normal = ( up - a * forward - b * lateral ).normal();

      return new PlaneData { Normal = normal, AveragePoint = average };
    }

    private static bool Solve3x3( double a00, double a01, double a02,
                                  double a10, double a11, double a12,
                                  double a20, double a21, double a22,
                                  double b0, double b1, double b2,
                                  out double x0, out double x1, out double x2 )
    {
      x0 = x1 = x2 = 0.0;
      var det = a00 * ( a11 * a22 - a12 * a21 ) -
                a01 * ( a10 * a22 - a12 * a20 ) +
                a02 * ( a10 * a21 - a11 * a20 );
      if ( System.Math.Abs( det ) < 1.0e-12 )
        return false;

      x0 = ( b0 * ( a11 * a22 - a12 * a21 ) -
             a01 * ( b1 * a22 - a12 * b2 ) +
             a02 * ( b1 * a21 - a11 * b2 ) ) / det;
      x1 = ( a00 * ( b1 * a22 - a12 * b2 ) -
             b0 * ( a10 * a22 - a12 * a20 ) +
             a02 * ( a10 * b2 - b1 * a20 ) ) / det;
      x2 = ( a00 * ( a11 * b2 - b1 * a21 ) -
             a01 * ( a10 * b2 - b1 * a20 ) +
             b0 * ( a10 * a21 - a11 * a20 ) ) / det;
      return true;
    }

    private void DrawRegressionPlane( List<agx.Vec3> localPoints,
                                      agx.Vec3 gridOrigin,
                                      agxTerrain.TerrainWheelPhysics.GridAxes gridAxes,
                                      PlaneData plane,
                                      agxTerrain.TerrainWheel wheel )
    {
      if ( !RenderLines )
        return;

      var minX = double.MaxValue;
      var maxX = double.MinValue;
      var minY = double.MaxValue;
      var maxY = double.MinValue;
      foreach ( var point in localPoints ) {
        minX = System.Math.Min( minX, point.x );
        maxX = System.Math.Max( maxX, point.x );
        minY = System.Math.Min( minY, point.y );
        maxY = System.Math.Max( maxY, point.y );
      }

      var axes = agxTerrain.TerrainWheelPhysics.computeGridCoordinateAxes( plane.Normal, wheel.getWheelShape().getTransform(), m_forwardSign );
      var halfX = 0.5 * System.Math.Max( 0.0, maxX - minX );
      var halfY = 0.5 * System.Math.Max( 0.0, maxY - minY );
      DrawRectangle( plane.AveragePoint, axes.forwardAxis, axes.lateralAxis, halfX, halfY, DebugColorTriplets[ 0 ].Line );

      DrawLine( gridOrigin, gridOrigin + gridAxes.upAxis, DebugColorTriplets[ 0 ].Line );
      DrawLine( gridOrigin, gridOrigin + gridAxes.lateralAxis, DebugColorTriplets[ 0 ].Line );
      DrawLine( gridOrigin, gridOrigin + gridAxes.forwardAxis, DebugColorTriplets[ 0 ].Line );

      DrawLine( plane.AveragePoint, plane.AveragePoint + plane.Normal * 3.0, DebugColorTriplets[ 0 ].Vector );

      var wheelDirection = wheel.getWheelBody().getPosition() - plane.AveragePoint;
      var signDirection = wheelDirection * axes.forwardAxis >= 0.0 ? 1.0 : -1.0;
      DrawLine( plane.AveragePoint,
                plane.AveragePoint + axes.forwardAxis * signDirection * wheelDirection.length(),
                DebugColorTriplets[ 0 ].Vector );
    }

    private void DrawForceFeedbackPlane( agxTerrain.TerrainWheel wheel,
                                         agxTerrain.RegressionPlanesParameters parameters,
                                         PlaneData plane )
    {
      if ( !RenderLines )
        return;

      var terrain = wheel.getActiveTerrain();
      var lengths = parameters.m_forceFeedbackHeightFieldLengths;
      var scales = parameters.m_forceFeedbackHeightFieldLengthsScale;
      var sizeX = ComputeScaledLength( lengths.x, scales[ 0 ], wheel, terrain );
      var sizeY = ComputeScaledLength( lengths.y, scales[ 1 ], wheel, terrain );
      var wheelTransform = wheel.getWheelShape().getTransform();
      var wheelAxis = wheelTransform.transformVector( agx.Vec3.Y_AXIS() );
      var center = agxTerrain.TerrainWheelPhysics.computeCenterOfPlaneAtWheel( plane.Normal,
                                                                               plane.AveragePoint,
                                                                               wheel.getWheelBody().getPosition(),
                                                                               wheelAxis,
                                                                               true );
      var axes = agxTerrain.TerrainWheelPhysics.computeGridCoordinateAxes( plane.Normal, wheelTransform, m_forwardSign );

      var corners = RectangleCorners( center, axes.forwardAxis, axes.lateralAxis, 0.5 * sizeX, 0.5 * sizeY );
      DrawPolyline( corners, DebugColorForceFeedbackHeightFieldLine );
      DrawLine( center, center + plane.Normal.normal() * 3.25, DebugColorForceFeedbackHeightFieldVector );

      var p = m_forwardSign == 1 ? corners[ 2 ] : corners[ 3 ];
      DrawLine( p - axes.forwardAxis * 0.4, p + axes.forwardAxis * 0.4, DebugColorForceFeedbackAlignment );
      DrawLine( p - axes.lateralAxis * 0.4, p + axes.lateralAxis * 0.4, DebugColorForceFeedbackAlignment );
      DrawLine( p - axes.upAxis * 0.4, p + axes.upAxis * 0.4, DebugColorForceFeedbackAlignment );
    }

    private void DrawAngleLines( agxTerrain.TerrainWheel wheel,
                                 agxTerrain.RegressionPlanesParameters parameters,
                                 agxTerrain.Terrain terrain,
                                 agx.Vec3 regressionPlaneNormal )
    {
      if ( !RenderLines )
        return;

      var quantities = wheel.getQuantities();
      var referenceNormal = parameters.m_overrideRearAndFrontAngleReferenceWithZ ?
                              terrain.getUpDirection() :
                              regressionPlaneNormal;
      var axes = agxTerrain.TerrainWheelPhysics.computeGridCoordinateAxes( -referenceNormal,
                                                                           wheel.getWheelShape().getTransform(),
                                                                           m_forwardSign );
      var up = axes.upAxis;
      var forward = -axes.forwardAxis;
      var center = wheel.getWheelBody().getPosition();
      var radius = wheel.getRadius();
      var frontAngle = quantities.getFrontAngle();
      var rearAngle = quantities.getRearAngle();

      var frontEnd = center + 3.0 * radius * forward * System.Math.Sin( frontAngle ) +
                    3.0 * radius * up * System.Math.Cos( frontAngle );
      var rearEnd = center + 1.5 * radius * forward * System.Math.Sin( rearAngle ) +
                   1.5 * radius * up * System.Math.Cos( rearAngle );
      var referenceEnd = center + 2.25 * radius * up;

      DrawLine( center, frontEnd, DebugColorRearAndFrontAngles );
      DrawLine( center, rearEnd, DebugColorRearAndFrontAngles );
      DrawLine( center, referenceEnd, DebugColorRearAndFrontAngles );
    }

    private void DrawRectangle( agx.Vec3 center, agx.Vec3 xAxis, agx.Vec3 yAxis, double halfX, double halfY, Color color )
    {
      DrawPolyline( RectangleCorners( center, xAxis, yAxis, halfX, halfY ), color );
    }

    private static agx.Vec3[] RectangleCorners( agx.Vec3 center, agx.Vec3 xAxis, agx.Vec3 yAxis, double halfX, double halfY )
    {
      return new[]
      {
        center - halfX * xAxis - halfY * yAxis,
        center + halfX * xAxis - halfY * yAxis,
        center + halfX * xAxis + halfY * yAxis,
        center - halfX * xAxis + halfY * yAxis
      };
    }

    private void DrawPolyline( agx.Vec3[] points, Color color )
    {
      for ( int i = 0; i < points.Length; ++i )
        DrawLine( points[ i ], points[ ( i + 1 ) % points.Length ], color );
    }

    private void DrawLine( agx.Vec3 start, agx.Vec3 end, Color color )
    {
      m_lines.Add( new LineData( start.ToHandedVector3(), end.ToHandedVector3(), color ) );
    }

    private void DrawLineBox( LineData line )
    {
      var direction = line.End - line.Start;
      var length = direction.magnitude;
      if ( length <= Mathf.Epsilon )
        return;

      var previousMatrix = Gizmos.matrix;
      Gizmos.color = line.Color;
      Gizmos.matrix = Matrix4x4.TRS( 0.5f * ( line.Start + line.End ),
                                     Quaternion.FromToRotation( Vector3.forward, direction ),
                                     Vector3.one );
      Gizmos.DrawCube( Vector3.zero, new Vector3( LineWidth, LineWidth, length ) );
      Gizmos.matrix = previousMatrix;
    }

    private static int SignInt( double value )
    {
      return value < 0.0 ? -1 : 1;
    }

    private struct PlaneData
    {
      public agx.Vec3 Normal;
      public agx.Vec3 AveragePoint;
    }

    private readonly struct LineData
    {
      public LineData( Vector3 start, Vector3 end, Color color )
      {
        Start = start;
        End = end;
        Color = color;
      }

      public readonly Vector3 Start;
      public readonly Vector3 End;
      public readonly Color Color;
    }

    private readonly struct SphereData
    {
      public SphereData( Vector3 position, Color color )
      {
        Position = position;
        Color = color;
      }

      public readonly Vector3 Position;
      public readonly Color Color;
    }

    private readonly struct ColorTriplet
    {
      public ColorTriplet( Color point, Color line, Color vector )
      {
        Point = point;
        Line = line;
        Vector = vector;
      }

      public readonly Color Point;
      public readonly Color Line;
      public readonly Color Vector;
    }

    private static readonly ColorTriplet[] DebugColorTriplets =
    {
      new ColorTriplet( Color.magenta, new Color( 0.5f, 0.0f, 0.5f ), new Color( 0.5f, 0.0f, 0.5f ) )
    };

    private static readonly Color DebugColorForceFeedbackHeightFieldLine = new Color( 0.2f, 0.8f, 0.2f );
    private static readonly Color DebugColorForceFeedbackHeightFieldVector = new Color( 0.2f, 0.8f, 0.2f );
    private static readonly Color DebugColorRearAndFrontAngles = new Color( 0.98f, 0.98f, 0.82f );
    private static readonly Color DebugColorForceFeedbackAlignment = new Color( 0.53f, 0.81f, 0.98f );

    private readonly List<LineData> m_lines = new List<LineData>();
    private readonly List<SphereData> m_spheres = new List<SphereData>();
    private DeformableTerrainWheel m_wheel = null;
    private PlaneData m_previousPlane;
    private bool m_hasValidContactFrame = false;
    private int m_forwardSign = 1;
  }
}
