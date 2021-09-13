using System;
using System.Collections.Generic;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  public struct RefArraySegment<T>
    where T : struct
  {
    public T[] Array { get; private set; }

    public int Offset { get; private set; }

    public int Count { get; private set; }

    public ref T this[ int index ]
    {
      get
      {
        return ref Array[ Offset + index ];
      }
    }

    public RefArraySegment( T[] array, int offset, int count )
    {
      Array = array;
      Offset = offset;
      Count = count;
    }

    public Enumerator GetEnumerator()
    {
      return new Enumerator()
      {
        Segment = this
      };
    }

    public struct Enumerator
    {
      public RefArraySegment<T> Segment
      {
        get { return m_segment; }
        set
        {
          m_segment = value;
          m_index = 0;
        }
      }

      public bool MoveNext()
      {
        return m_index < Segment.Count;
      }

      public ref T Current
      {
        get
        {
          return ref Segment[ m_index++ ];
        }
      }

      private int m_index;
      private RefArraySegment<T> m_segment;
    }
  }

  public struct ContactPointForceData
  {
    public Vector3 Normal;
    public Vector3 PrimaryTangential;
    public Vector3 SecondaryTangential;
    public bool IsImpacting;

    public Vector3 Tangential { get { return PrimaryTangential + SecondaryTangential; } }
  }

  public struct ContactPointData
  {
    public Vector3 Position;
    public Vector3 Normal;
    public Vector3 SurfaceVelocity;
    public ContactPointForceData? Force;
    public float Depth;
    public bool Enabled;

    public void From( agxCollide.ContactPoint gcPoint, bool hasForce )
    {
      Position        = gcPoint.point.ToHandedVector3();
      Normal          = gcPoint.normal.ToHandedVector3();
      SurfaceVelocity = gcPoint.velocity.ToHandedVector3();
      Depth           = (float)gcPoint.depth;
      Enabled         = gcPoint.enabled;

      if ( hasForce ) {
        Force = new ContactPointForceData()
        {
          Normal = gcPoint.getNormalForce().ToHandedVector3(),
          PrimaryTangential = (float)gcPoint.getTangentialForceUMagnitude() * gcPoint.tangentU.ToHandedVector3(),
          SecondaryTangential = (float)gcPoint.getTangentialForceVMagnitude() * gcPoint.tangentV.ToHandedVector3(),
          IsImpacting = gcPoint.hasState( agxCollide.ContactPoint.ContactPointState.IMPACTING )
        };
      }
      else
        Force = null;
    }

    public void To( agxCollide.ContactPoint gcPoint )
    {
      gcPoint.setPoint( Position.ToHandedVec3() );
      gcPoint.setNormal( Normal.ToHandedVec3f() );
      gcPoint.setVelocity( SurfaceVelocity.ToHandedVec3f() );
      gcPoint.setDepth( Depth );
      gcPoint.setEnabled( Enabled );
    }
  }

  public struct ContactData
  {
    public ScriptComponent Component1;
    public ScriptComponent Component2;
    public RefArraySegment<ContactPointData> Points;

    public bool HasContactPointForceData
    {
      get
      {
        return Points.Count > 0 && Points[ 0 ].Force.HasValue;
      }
    }

    public Vector3 TotalNormalForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalNormalForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalNormalForce += point.Force.Value.Normal;
        return totalNormalForce;
      }
    }

    public Vector3 TotalPrimaryTangentialForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalPrimaryTangentialForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalPrimaryTangentialForce += point.Force.Value.PrimaryTangential;
        return totalPrimaryTangentialForce;
      }
    }

    public Vector3 TotalSecondaryTangentialForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalSecondaryTangentialForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalSecondaryTangentialForce += point.Force.Value.SecondaryTangential;
        return totalSecondaryTangentialForce;
      }
    }

    public Vector3 TotalTangentialForce => TotalPrimaryTangentialForce + TotalSecondaryTangentialForce;

    public override string ToString()
    {
      var c1Name = Component1 != null ?
                     Component1.name + $" <{Component1.GetType().FullName}>" :
                     "null";
      var c2Name = Component2 != null ?
                     Component2.name + $" <{Component2.GetType().FullName}>" :
                     "null";
      var result = $"{c1Name} <-> {c2Name}:\n";
      result += $"    #points: {Points.Count}\n";
      var pointIndex = 0;
      foreach ( var p in Points ) {
        result += $"        [{pointIndex++}]: p = {p.Position}, n = {p.Normal}, enabled = {p.Enabled}";
        if ( pointIndex < Points.Count )
          result += '\n';
      }
      return result;
    }
  }

  // TODO: Move this to a separate file.
  public class GeometryContactHandler
  {
    public class AccessScope : IDisposable
    {
      public GeometryContactHandler Handler { get; private set; }

      public AccessScope( GeometryContactHandler handler )
      {
        Handler = handler;
      }

      public void Dispose()
      {
        Handler.ReturnNativeData();
        Handler = null;
      }
    }

    public List<agxCollide.GeometryContact> GeometryContacts { get; private set; } = new List<agxCollide.GeometryContact>();

    public RefArraySegment<ContactData> ContactData { get; private set; }

    public int GetIndex( agxCollide.GeometryContact geometryContact, bool returnProxyIfRedundant = true )
    {
      if ( m_geometryContactIndexTable.TryGetValue( geometryContact, out var index ) ) {
        if ( returnProxyIfRedundant )
          geometryContact.ReturnToPool();
        return index;
      }

      index = GeometryContacts.Count;
      GeometryContacts.Add( geometryContact );

      var gcPoints = geometryContact.points();
      m_numContactPoints += (int)gcPoints.size();
      gcPoints.ReturnToPool();

      m_geometryContactIndexTable.Add( geometryContact, index );

      return index;
    }

    public AccessScope GenerateContactData( Func<agxCollide.Geometry, ScriptComponent> geometryToComponent,
                                            bool hasForce )
    {
      if ( m_contactDataCache == null || GeometryContacts.Count > m_contactDataCache.Length )
        m_contactDataCache = new ContactData[ GeometryContacts.Count ];
      if ( m_contactPointDataCache == null || m_numContactPoints > m_contactPointDataCache.Length )
        m_contactPointDataCache = new ContactPointData[ m_numContactPoints ];

      ContactData = new RefArraySegment<ContactData>( m_contactDataCache,
                                                      0,
                                                      GeometryContacts.Count );

      int contactPointStartIndex = 0;
      for ( int contactIndex = 0; contactIndex < GeometryContacts.Count; ++contactIndex ) {
        var gc = GeometryContacts[ contactIndex ];
        ref var contactData = ref m_contactDataCache[ contactIndex ];

        var g1 = gc.geometry( 0u );
        contactData.Component1 = geometryToComponent( g1 );

        var g2 = gc.geometry( 1u );
        contactData.Component2 = geometryToComponent( g2 );

        var gcPoints = gc.points();
        var gcNumPoints = (int)gcPoints.size();
        for ( int pointIndex = 0; pointIndex < gcNumPoints; ++pointIndex ) {
          var gcPoint = gcPoints.at( (uint)pointIndex );

          m_contactPointDataCache[ contactPointStartIndex + pointIndex ].From( gcPoint, hasForce );

          gcPoint.ReturnToPool();
        }

        contactData.Points = new RefArraySegment<ContactPointData>( m_contactPointDataCache,
                                                                    contactPointStartIndex,
                                                                    gcNumPoints );
        contactPointStartIndex += gcNumPoints;

        g2.ReturnToPool();
        g1.ReturnToPool();
        gcPoints.ReturnToPool();
      }

      return new AccessScope( this );
    }

    public void ReturnNativeData()
    {
      m_geometryContactIndexTable.Clear();
      m_numContactPoints = 0;
      GeometryContacts.ForEach( gc => gc.ReturnToPool() );
      GeometryContacts.Clear();
    }

    private Dictionary<agxCollide.GeometryContact, int> m_geometryContactIndexTable = new Dictionary<agxCollide.GeometryContact, int>();
    private ContactData[] m_contactDataCache = null;
    private ContactPointData[] m_contactPointDataCache = null;
    private int m_numContactPoints = 0;
  }
}
