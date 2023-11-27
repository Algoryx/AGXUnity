using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "AGXUnity/Merge Split Properties" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#merge-split-properties" )]
  public class MergeSplitProperties : ScriptComponent
  {
    [SerializeField]
    private bool m_enableMerge = false;
    public bool EnableMerge
    {
      get { return m_enableMerge; }
      set
      {
        m_enableMerge = value;
        for ( int i = 0; i < m_natives.Count; ++i )
          m_natives[ i ].setEnableMerge( m_enableMerge );
      }
    }

    [SerializeField]
    private bool m_enableSplit = false;
    public bool EnableSplit
    {
      get { return m_enableSplit; }
      set
      {
        m_enableSplit = value;
        for ( int i = 0; i < m_natives.Count; ++i )
          m_natives[ i ].setEnableSplit( m_enableSplit );
      }
    }

    [SerializeField]
    private GeometryContactMergeSplitThresholds m_geometryContactThresholds = null;
    [AllowRecursiveEditing]
    public GeometryContactMergeSplitThresholds GeometryContactThresholds
    {
      get
      {
        if ( m_geometryContactThresholds == null )
          m_geometryContactThresholds = GeometryContactMergeSplitThresholds.DefaultResource;
        return m_geometryContactThresholds;
      }
      set
      {
        m_geometryContactThresholds = value ?? GeometryContactMergeSplitThresholds.DefaultResource;
        foreach ( var native in m_natives )
          native.setContactThresholds( m_geometryContactThresholds.GetInitialized<GeometryContactMergeSplitThresholds>().Native );
      }
    }

    [SerializeField]
    private ConstraintMergeSplitThresholds m_constraintThresholds = null;
    [AllowRecursiveEditing]
    public ConstraintMergeSplitThresholds ConstraintThresholds
    {
      get
      {
        if ( m_constraintThresholds == null )
          m_constraintThresholds = ConstraintMergeSplitThresholds.DefaultResource;
        return m_constraintThresholds;
      }
      set
      {
        m_constraintThresholds = value ?? ConstraintMergeSplitThresholds.DefaultResource;
        foreach ( var native in m_natives )
          native.setConstraintThresholds( m_constraintThresholds.GetInitialized<ConstraintMergeSplitThresholds>().Native );
      }
    }

    private List<agxSDK.MergeSplitProperties> m_natives = new List<agxSDK.MergeSplitProperties>();

    [HideInInspector]
    public agxSDK.MergeSplitProperties[] Natives { get { return m_natives.ToArray(); } }

    public bool RegisterNativeAndSynchronize( agxSDK.MergeSplitProperties properties )
    {
      if ( properties == null || m_natives.Contains( properties ) )
        return false;

      Add( properties );
      Utils.PropertySynchronizer.Synchronize( this );

      return true;
    }

    protected override bool Initialize()
    {
      m_natives.Clear();

      // Two Body tire does not currently handle AMOR very well so we need to avoid enabling merge for constraints which connect a rim and a tire of a TwoBodyTire
      // This is done by adding Rim/Tire pairs to a list and checking that Reference/Connected object is not a blacklisted pair before enabling merge
      var blacklist = new List<Tuple<RigidBody, RigidBody>>();

      var tires = GetComponentsInChildren<Model.TwoBodyTire>();
      foreach ( var tire in tires )
        if ( tire.GetInitialized<Model.TwoBodyTire>() != null )
          blacklist.Add( Tuple.Create( tire.TireRigidBody, tire.RimRigidBody ) );

      var bodies = GetComponentsInChildren<RigidBody>();
      foreach ( var rb in bodies )
        Add( agxSDK.MergeSplitHandler.getOrCreateProperties( rb.GetInitialized<RigidBody>().Native ) );

      var shapes = GetComponentsInChildren<Collide.Shape>();
      foreach ( var shape in shapes ) {
        var native = agxSDK.MergeSplitHandler.getProperties( shape.GetInitialized<Collide.Shape>().NativeGeometry );
        if ( native == null )
          Add( agxSDK.MergeSplitHandler.getOrCreateProperties( shape.NativeGeometry ) );
      }

      var constraints = GetComponentsInChildren<Constraint>();
      foreach ( var constraint in constraints )
        if ( constraint.GetInitialized<Constraint>() != null ) {
          var refRB = constraint.AttachmentPair.ReferenceObject.GetComponent<RigidBody>();
          var conRB = constraint.AttachmentPair.ConnectedObject.GetComponent<RigidBody>();
          if ( !blacklist.Any( pair =>
            ( pair.Item1 == refRB && pair.Item2 == conRB ) ||
            ( pair.Item2 == refRB && pair.Item1 == conRB ) ) )
            Add( agxSDK.MergeSplitHandler.getOrCreateProperties( constraint.Native ) );
        }

      var wires = GetComponentsInChildren<Wire>();
      foreach ( var wire in wires )
        if ( wire.GetInitialized<Wire>() != null )
          Add( agxSDK.MergeSplitHandler.getOrCreateProperties( wire.Native ) );

      var cables = GetComponentsInChildren<Cable>();
      foreach ( var cable in cables ) {
        if ( cable.GetInitialized<Cable>() == null )
          continue;

        var nativeCable = cable.Native;
        for ( var it = nativeCable.getSegments().begin(); !it.EqualWith( nativeCable.getSegments().end() ); it.inc() ) {
          var cableRb = it.getRigidBody();
          Add( agxSDK.MergeSplitHandler.getOrCreateProperties( cableRb ) );
          if ( it.getConstraint() != null )
            Add( agxSDK.MergeSplitHandler.getOrCreateProperties( it.getConstraint() ) );
        }
      }

      GeometryContactThresholds.GetInitialized<GeometryContactMergeSplitThresholds>();
      ConstraintThresholds.GetInitialized<ConstraintMergeSplitThresholds>();

      return true;
    }

    protected override void OnDestroy()
    {
      m_natives.Clear();

      base.OnDestroy();
    }

    private void Add( agxSDK.MergeSplitProperties native )
    {
      if ( m_natives.Contains( native ) ) {
        Debug.Log( "Native MergeSplitProperties already present in native list.", this );
        return;
      }

      m_natives.Add( native );
    }
  }
}
