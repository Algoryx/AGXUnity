using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Script asset manager keeping track of initialized script assets.
  /// Components with a reference to a script asset can use the same
  /// semantics, like obj.GetInitialized<MyScriptAsset>(), since this
  /// manager is holding the state of the script asset.
  /// </summary>
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#assets" )]
  public class ScriptAssetManager : UniqueGameObject<ScriptAssetManager>
  {
    /// <summary>
    /// Initialized script assets.
    /// </summary>
    private HashSet<ScriptAsset> m_activeScriptAssets = new HashSet<ScriptAsset>();

    /// <summary>
    /// State of a script asset instance.
    /// </summary>
    public enum InitializationState
    {
      NotInitialized,
      Initialized
    }

    /// <summary>
    /// When someone calls GetInitialized of a script asset, we're receiving
    /// a call to this method - finding and returning the current state of
    /// the script.
    /// </summary>
    /// <param name="scriptAsset">Script asset to find state for.</param>
    /// <returns>Current state of the script asset.</returns>
    public InitializationState Report( ScriptAsset scriptAsset )
    {
      if ( scriptAsset == null )
        throw new ArgumentNullException( "scriptAsset" );

      if ( m_activeScriptAssets.Contains( scriptAsset ) )
        return InitializationState.Initialized;

      // Adding the script asset to active but it hasn't been
      // initialized yet - assuming script asset base class
      // to unregister itself if initialization fails.
      m_activeScriptAssets.Add( scriptAsset );

      return InitializationState.NotInitialized;
    }

    /// <summary>
    /// Unregister initialized script asset.
    /// </summary>
    /// <param name="scriptAsset"></param>
    public void Unregister( ScriptAsset scriptAsset )
    {
      m_activeScriptAssets.Remove( scriptAsset );
    }

    protected override bool Initialize()
    {
      return true;
    }

    /// <summary>
    /// Calls Destroy on all active script assets.
    /// </summary>
    protected override void OnDestroy()
    {
      foreach ( var scriptAsset in m_activeScriptAssets )
        scriptAsset.Destroy();

      m_activeScriptAssets.Clear();

      base.OnDestroy();
    }
  }
}
