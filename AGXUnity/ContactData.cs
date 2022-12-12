using System.Runtime.InteropServices;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Applied force from a solved contact point.
  /// </summary>
  public struct ContactPointForceData
  {
    /// <summary>
    /// Normal force given in world coordinate frame.
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// Tangential (friction) force in the primary direction, given in world coordinate frame.
    /// </summary>
    public Vector3 PrimaryTangential;

    /// <summary>
    /// Tangential (friction) force in the secondary direction, given in world coordinate frame.
    /// </summary>
    public Vector3 SecondaryTangential;

    /// <summary>
    /// True if this contact point has been solved as an impacting point.
    /// </summary>
    public bool IsImpacting;

    /// <summary>
    /// Total tangential (friction) force, i.e., primary + secondary force, given in world coordinate frame.
    /// </summary>
    public Vector3 Tangential => PrimaryTangential + SecondaryTangential;

    /// <summary>
    /// Total force, i.e., normal + primary + secondary force, given in world coordinate frame.
    /// </summary>
    public Vector3 Total => Normal + Tangential;

    /// <summary>
    /// Create given native contact point.
    /// </summary>
    /// <param name="gcPoint">Native contact point.</param>
    /// <returns>Contact point force data from the given native contact point.</returns>
    public static ContactPointForceData Create( agxCollide.ContactPoint gcPoint )
    {
      return new ContactPointForceData()
      {
        Normal              = gcPoint.getNormalForce().ToHandedVector3(),
        PrimaryTangential   = (float)gcPoint.getTangentialForceUMagnitude() * gcPoint.tangentU.ToHandedVector3(),
        SecondaryTangential = (float)gcPoint.getTangentialForceVMagnitude() * gcPoint.tangentV.ToHandedVector3(),
        IsImpacting         = gcPoint.hasState( agxCollide.ContactPoint.ContactPointState.IMPACTING )
      };
    }
  }

  /// <summary>
  /// Data of a contacting point. The force is only available when the contacts
  /// has been solved.
  /// </summary>
  public struct ContactPointData
  {
    /// <summary>
    /// Position of this contact point given in world coordinate frame.
    /// </summary>
    public Vector3 Position;

    /// <summary>
    /// Contact normal of this contact point given in world coordinate frame.
    /// </summary>
    public Vector3 Normal;

    /// <summary>
    /// Primary tangent (friction) direction of this contact point given in world coordinate frame.
    /// </summary>
    public Vector3 PrimaryTangent;

    /// <summary>
    /// Secondary tangent (friction) direction of this contact point given in world coordinate frame.
    /// </summary>
    public Vector3 SecondaryTangent;

    /// <summary>
    /// Target surface velocity of this contact point given in world coordinate frame.
    /// </summary>
    public Vector3 SurfaceVelocity;

    /// <summary>
    /// Penetration depth of this contact point.
    /// </summary>
    public float Depth;

    /// <summary>
    /// Enabled state of this contact point.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// True if the contact force of this contact point is available.
    /// </summary>
    public bool HasForce => m_force != null;

    /// <summary>
    /// Contact point force data, available when this contact point has been solved.
    /// </summary>
    public ContactPointForceData Force
    {
      get
      {
        if ( HasForce )
          return m_force.Value;
        return s_emptyForceData;
      }
    }

    /// <summary>
    /// Copies data from the native contact point.
    /// </summary>
    /// <param name="gcPoint">Native contact point.</param>
    /// <param name="hasForce">True if the force data should be read from the native contact point.</param>
    public void From( agxCollide.ContactPoint gcPoint, bool hasForce )
    {
      Position         = gcPoint.point.ToHandedVector3();
      Normal           = gcPoint.normal.ToHandedVector3();
      PrimaryTangent   = gcPoint.tangentU.ToHandedVector3();
      SecondaryTangent = gcPoint.tangentV.ToHandedVector3();
      SurfaceVelocity  = gcPoint.velocity.ToHandedVector3();
      Depth            = (float)gcPoint.depth;
      Enabled          = gcPoint.enabled;

      if ( hasForce )
        m_force = ContactPointForceData.Create( gcPoint );
      else
        m_force = null;
    }

    /// <summary>
    /// Synchronizes the native contact point given any value has been updated.
    /// </summary>
    /// <param name="gcPoint">Native contact point to update.</param>
    public void Synchronize( agxCollide.ContactPoint gcPoint )
    {
      gcPoint.setPoint( Position.ToHandedVec3() );
      gcPoint.setNormal( Normal.ToHandedVec3f() );
      gcPoint.setTangentU( PrimaryTangent.ToHandedVec3f() );
      gcPoint.setTangentV( SecondaryTangent.ToHandedVec3f() );
      gcPoint.setVelocity( SurfaceVelocity.ToHandedVec3f() );
      gcPoint.setDepth( Depth );
      gcPoint.setEnabled( Enabled );
    }

    private ContactPointForceData? m_force;
    private static readonly ContactPointForceData s_emptyForceData = new ContactPointForceData();
  }

  /// <summary>
  /// Contact data with a set of contact points and interacting
  /// components. The components, if found, is in the order in
  /// which they appear in the native geometry contact.
  /// </summary>
  public struct ContactData
  {
    /// <summary>
    /// First interacting component - null if not found.
    /// </summary>
    public ScriptComponent Component1;

    /// <summary>
    /// Second interacting component - null of not found.
    /// </summary>
    public ScriptComponent Component2;

    /// <summary>
    /// True if this contact is enabled, false if disabled.
    /// </summary>
    public bool Enabled;

    /// <summary>
    /// Array of contact points belonging to this contact.
    /// </summary>
    public RefArraySegment<ContactPointData> Points;

    /// <summary>
    /// Access to the first geometry in the contact. This geometry
    /// belongs to Component1.
    /// </summary>
    public agxCollide.Geometry Geometry1
    {
      get
      {
        return m_geometry1 ??
               ( m_geometry1Handle.Handle == System.IntPtr.Zero ?
                  null :
                  m_geometry1 = new agxCollide.Geometry( m_geometry1Handle.Handle, false ) );
      }
      set
      {
        m_geometry1Handle = agxCollide.Geometry.getCPtr( value );
        m_geometry1 = null;
      }
    }


    /// <summary>
    /// Access to the second geometry in the contact. This geometry
    /// belongs to Component2.
    /// </summary>
    public agxCollide.Geometry Geometry2
    {
      get
      {
        return m_geometry2 ??
               ( m_geometry2Handle.Handle == System.IntPtr.Zero ?
                  null :
                  m_geometry2 = new agxCollide.Geometry( m_geometry2Handle.Handle, false ) );
      }
      set
      {
        m_geometry2Handle = agxCollide.Geometry.getCPtr( value );
        m_geometry2 = null;
      }
    }


    /// <summary>
    /// True if contact point force data is available.
    /// </summary>
    public bool HasContactPointForceData
    {
      get
      {
        return Points.Count > 0 && Points[ 0 ].HasForce;
      }
    }

    /// <summary>
    /// Total normal force of this contact given in world coordinate frame.
    /// </summary>
    public Vector3 TotalNormalForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalNormalForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalNormalForce += point.Force.Normal;
        return totalNormalForce;
      }
    }

    /// <summary>
    /// Total primary tangential (friction) force of this contact, given in world coordinate frame.
    /// </summary>
    public Vector3 TotalPrimaryTangentialForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalPrimaryTangentialForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalPrimaryTangentialForce += point.Force.PrimaryTangential;
        return totalPrimaryTangentialForce;
      }
    }

    /// <summary>
    /// Total secondary tangential (friction) force of this contact, given in world coordinate frame.
    /// </summary>
    public Vector3 TotalSecondaryTangentialForce
    {
      get
      {
        if ( !HasContactPointForceData )
          return Vector3.zero;

        var totalSecondaryTangentialForce = Vector3.zero;
        foreach ( ref var point in Points )
          totalSecondaryTangentialForce += point.Force.SecondaryTangential;
        return totalSecondaryTangentialForce;
      }
    }

    /// <summary>
    /// Total tangential (friction) force of this contact, given in world coordinate frame.
    /// </summary>
    public Vector3 TotalTangentialForce => TotalPrimaryTangentialForce + TotalSecondaryTangentialForce;

    /// <summary>
    /// Total (normal and tangential) force of this contact, given in world coordinate frame.
    /// </summary>
    public Vector3 TotalForce => TotalNormalForce + TotalTangentialForce;

    /// <summary>
    /// Invalidates the geometries so that they cannot be accessed any more.
    /// </summary>
    public void InvalidateGeometries()
    {
      Geometry1 = null;
      Geometry2 = null;
    }

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

    private HandleRef m_geometry1Handle;
    private agxCollide.Geometry m_geometry1;
    private HandleRef m_geometry2Handle;
    private agxCollide.Geometry m_geometry2;
  }

  public struct SeparationData
  {
    /// <summary>
    /// First interacting component - null if not found.
    /// </summary>
    public ScriptComponent Component1;

    /// <summary>
    /// Second interacting component - null of not found.
    /// </summary>
    public ScriptComponent Component2;

    /// <summary>
    /// Access to the first geometry in the contact. This geometry
    /// belongs to Component1.
    /// </summary>
    public agxCollide.Geometry Geometry1
    {
      get
      {
        return m_geometry1 ??
               ( m_geometry1Handle.Handle == System.IntPtr.Zero ?
                  null :
                  m_geometry1 = new agxCollide.Geometry( m_geometry1Handle.Handle, false ) );
      }
      set
      {
        m_geometry1Handle = agxCollide.Geometry.getCPtr( value );
        m_geometry1 = null;
      }
    }


    /// <summary>
    /// Access to the second geometry in the contact. This geometry
    /// belongs to Component2.
    /// </summary>
    public agxCollide.Geometry Geometry2
    {
      get
      {
        return m_geometry2 ??
               ( m_geometry2Handle.Handle == System.IntPtr.Zero ?
                  null :
                  m_geometry2 = new agxCollide.Geometry( m_geometry2Handle.Handle, false ) );
      }
      set
      {
        m_geometry2Handle = agxCollide.Geometry.getCPtr( value );
        m_geometry2 = null;
      }
    }

    /// <summary>
    /// Invalidates the geometries so that they cannot be accessed any more.
    /// </summary>
    public void InvalidateGeometries()
    {
      Geometry1 = null;
      Geometry2 = null;
    }

    public override string ToString()
    {
      var c1Name = Component1 != null ?
                     Component1.name + $" <{Component1.GetType().FullName}>" :
                     "null";
      var c2Name = Component2 != null ?
                     Component2.name + $" <{Component2.GetType().FullName}>" :
                     "null";
      var result = $"{c1Name} <-/-> {c2Name}";
      return result;
    }

    private HandleRef m_geometry1Handle;
    private agxCollide.Geometry m_geometry1;
    private HandleRef m_geometry2Handle;
    private agxCollide.Geometry m_geometry2;
  }
}
