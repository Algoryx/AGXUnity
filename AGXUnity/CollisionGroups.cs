using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  /// <summary>
  /// Component holding a list of name tags for collision groups.
  /// </summary>
  [AddComponentMenu( "AGXUnity/Collisions/Collision Groups" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#collision-groups" )]
  public class CollisionGroups : ScriptComponent
  {
    /// <summary>
    /// List of collision groups paired with property Groups.
    /// </summary>
    [SerializeField]
    private List<CollisionGroupEntry> m_groups = new List<CollisionGroupEntry>() { };

    /// <summary>
    /// Get current list of groups.
    /// </summary>
    public List<CollisionGroupEntry> Groups
    {
      get { return m_groups; }
    }

    /// <param name="tag">Name tag to check if it exist in the current set of groups.</param>
    /// <returns>True if the given name tag exists.</returns>
    public bool HasGroup( string tag )
    {
      return m_groups.Find( entry => entry.Tag == tag ) != null;
    }

    /// <summary>
    /// Add new group.
    /// </summary>
    /// <param name="tag">New group tag.</param>
    /// <param name="propagateToChildren">True if this tag should be propagated to all supported children.</param>
    /// <returns>True if the group was added - otherwise false (e.g., already exists).</returns>
    public bool AddGroup( string tag, bool propagateToChildren )
    {
      if ( HasGroup( tag ) )
        return false;

      m_groups.Add( new CollisionGroupEntry() { Tag = tag } );

      if ( State == States.INITIALIZED )
        AddGroup( m_groups.Last(), Find.LeafObjects( gameObject, propagateToChildren ) );

      return true;
    }

    /// <summary>
    /// Remove group.
    /// </summary>
    /// <param name="tag">Group to remove.</param>
    /// <returns>True if removed - otherwise false.</returns>
    public bool RemoveGroup( string tag )
    {
      int index = m_groups.FindIndex( entry => entry.Tag == tag );
      if ( index < 0 )
        return false;

      RemoveGroup( m_groups[ index ], Find.LeafObjects( gameObject, m_groups[ index ].PropagateToChildren ) );

      m_groups.RemoveAt( index );

      return true;
    }

    /// <summary>
    /// Initialize, finds supported object and executes addGroup to it.
    /// </summary>
    protected override bool Initialize()
    {
      if ( m_groups.Count == 0 )
        return base.Initialize();

      var data = new Find.LeafData[] { Find.LeafObjects( gameObject, false ), Find.LeafObjects( gameObject, true ) };
      foreach ( var entry in m_groups )
        AddGroup( entry, data[ Convert.ToInt32( entry.PropagateToChildren ) ] );

      return base.Initialize();
    }

    private void AddGroup( CollisionGroupEntry entry, Find.LeafData data )
    {
      foreach ( var shape in data.Shapes )
        if ( shape.GetInitialized<Collide.Shape>() != null )
          entry.AddTo( shape.NativeGeometry );

      foreach ( var wire in data.Wires )
        if ( wire.GetInitialized<Wire>() != null )
          entry.AddTo( wire.Native );

      foreach ( var cable in data.Cables )
        if ( cable.GetInitialized<Cable>() != null )
          entry.AddTo( cable.Native );

      foreach ( var track in data.Tracks )
        if ( track.GetInitialized<Model.Track>() != null )
          entry.AddTo( track.Native );

      foreach ( var terrain in data.Terrains )
        if ( terrain.GetInitialized<Model.DeformableTerrain>() != null )
          entry.AddTo( terrain.Native.getGeometry() );
    }

    private void RemoveGroup( CollisionGroupEntry entry, Find.LeafData data )
    {
      foreach ( var shape in data.Shapes )
        if ( shape.GetInitialized<Collide.Shape>() != null )
          entry.RemoveFrom( shape.NativeGeometry );

      foreach ( var wire in data.Wires )
        if ( wire.GetInitialized<Wire>() != null )
          entry.RemoveFrom( wire.Native );

      foreach ( var cable in data.Cables )
        if ( cable.GetInitialized<Cable>() != null )
          entry.RemoveFrom( cable.Native );

      foreach ( var track in data.Tracks )
        if ( track.GetInitialized<Model.Track>() != null )
          entry.RemoveFrom( track.Native );

      foreach ( var terrain in data.Terrains )
        if ( terrain.GetInitialized<Model.DeformableTerrain>() != null )
          entry.RemoveFrom( terrain.Native.getGeometry() );
    }
  }
}
