using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#collision-groups-manager" )]
  public class CollisionGroupsManager : UniqueGameObject<CollisionGroupsManager>
  {
    /// <summary>
    /// List of disabled pairs paired with property DisabledPairs.
    /// </summary>
    [SerializeField]
    private List<CollisionGroupEntryPair> m_disabledPairs = new List<CollisionGroupEntryPair>();

    /// <summary>
    /// Get the list of disabled pairs.
    /// </summary>
    [HideInInspector]
    public CollisionGroupEntryPair[] DisabledPairs
    {
      get { return m_disabledPairs.ToArray(); }
    }

    /// <summary>
    /// Finds if group name tag 1 is enabled against group name tag 2.
    /// </summary>
    /// <param name="group1">First name tag.</param>
    /// <param name="group2">Second name tag.</param>
    /// <returns>True if <paramref name="group1"/> is enabled against <paramref name="group2"/>.</returns>
    public bool GetEnablePair( string group1, string group2 )
    {
      return m_disabledPairs.Find( pair => SymmetricMatch( pair, group1, group2 ) ) == null;
    }

    /// <summary>
    /// Enable/disable group name tag 1 against group name tag 2.
    /// </summary>
    /// <param name="group1">First name tag.</param>
    /// <param name="group2">Second name tag.</param>
    /// <param name="enable">True to enable (default enabled), false to disable.</param>
    public void SetEnablePair( string group1, string group2, bool enable )
    {
      if ( enable )
        RemoveFromDisabled( group1, group2 );
      else
        AddToDisabled( group1, group2 );
    }

    protected override bool Initialize()
    {
      var space = GetSimulation().getSpace();
      foreach ( var pair in m_disabledPairs )
        SetEnablePair( pair, false, space );

      return true;
    }

    private void AddToDisabled( string group1, string group2 )
    {
      // Already in the list.
      if ( !GetEnablePair( group1, group2 ) )
        return;

      m_disabledPairs.Add( new CollisionGroupEntryPair()
      {
        First = new CollisionGroupEntry() { Tag = group1 },
        Second = new CollisionGroupEntry() { Tag = group2 }
      } );

      if ( State == States.INITIALIZED )
        SetEnablePair( m_disabledPairs.Last(), false, GetSimulation().getSpace() );
    }

    private void RemoveFromDisabled( string group1, string group2 )
    {
      int index = m_disabledPairs.FindIndex( pair => SymmetricMatch( pair, group1, group2 ) );
      if ( index < 0 )
        return;

      if ( State == States.INITIALIZED )
        SetEnablePair( m_disabledPairs[ index ], true, GetSimulation().getSpace() );

      m_disabledPairs.RemoveAt( index );
    }

    private bool SymmetricMatch( CollisionGroupEntryPair entryPair, string group1, string group2 )
    {
      return ( entryPair.First.Tag == group1 && entryPair.Second.Tag == group2 ) ||
             ( entryPair.First.Tag == group2 && entryPair.Second.Tag == group1 );
    }

    private void SetEnablePair( CollisionGroupEntryPair pair, bool enable, agxCollide.Space space )
    {
      space.setEnablePair( pair.First.Tag.To32BitFnv1aHash(), pair.Second.Tag.To32BitFnv1aHash(), enable );
    }
  }
}
