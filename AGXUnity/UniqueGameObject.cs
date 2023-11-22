using UnityEngine;

using SceneManager = UnityEngine.SceneManagement.SceneManager;
using Scene = UnityEngine.SceneManagement.Scene;

namespace AGXUnity
{
  /// <summary>
  /// Singleton like object that is created when Instance is called.
  /// It's important that callers to Instance properly checks
  /// HasInstance and/or IsDestroyed when the caller is being
  /// destroyed, e.g., during scene unload or from OnDestroy.
  /// </summary>
  /// <typeparam name="T">Type of subclass.</typeparam>
  public class UniqueGameObject<T> : ScriptComponent
    where T : ScriptComponent
  {
    /// <summary>
    /// Get found instance or finds instance in the current scene or
    /// creates a new instance in the scene.
    /// </summary>
    public static T Instance
    {
      get
      {
        return s_instance != null ?
                 s_instance :
                 FindOrCreateInstance();
      }
      private set
      {
        s_instance = value;
        s_wasCreated |= value != null;
      }
    }

    /// <summary>
    /// True if the instance has been found or been created.
    /// False if destroyed or no one has called Instance.
    /// </summary>
    public static bool HasInstance => s_instance != null;

    /// <summary>
    /// True if this object has been destroyed. Calling Instance
    /// after this will create a new instance so if IsDestroyed == true,
    /// don't call Instance from OnDestroy.
    /// </summary>
    public static bool IsDestroyed => s_wasCreated && s_instance == null;

    /// <summary>
    /// True if an instance has been created or there is an instance
    /// in the loaded scenes. This is used by implicit dependent scripts
    /// on certain managers, e.g., WindAndWaterParameters shouldn't
    /// instantiate a dependent WindAndWaterManager if the user hasn't
    /// explicitly enabled a WindAndWaterManager in a loaded scene.
    /// </summary>
    public static bool HasInstanceInScene
    {
      get
      {
        return s_instance != null || ( FindOrCreateInstance( true ) != null );
      }
    }

    protected sealed override void OnAwake()
    {
      // In adaptive scene loading it's possible that we
      // have multiple instances of some managers. It's
      // likely they communicate with the same simulation
      // instance or may operate separately as long as no
      // external script depends on calls to Instance.
      // E.g., ContactMaterialManager and CollisionGroupsManager
      // may have several instances. Simulation and
      // ScriptAssetManager may not, so either they has
      // to be in the main scene or in no scene at all.
      if ( s_instance == null )
        Instance = this as T;
    }

    protected override void OnDestroy()
    {
      if ( s_instance == this )
        Instance = null;

      base.OnDestroy();
    }

    /// <summary>
    /// Invalid to call this method if s_instance != null. Finds
    /// instance in scene or creates a new game object with component T.
    /// </summary>
    /// <param name="onlyFind">
    /// True if only FindObjectOfType should be called and if not found,
    /// return null. False to find or create a new instance.
    /// </param>
    /// <returns>
    /// Found or new instance. Null if <paramref name="onlyFind"/> == true and
    /// FindObjectOfType returns null.
    /// </returns>
    private static T FindOrCreateInstance( bool onlyFind = false )
    {
      if ( s_instance != null )
        Debug.LogError( $"{s_instance.name}: Invalid to call FindOrCreateInstance called with non-null s_instance." );

      // It's important that the caller knows something in which context
      // the Instance call is made in so that we're not recreating this
      // object in, e.g., OnDestroy/Unloading of as scene.
      s_wasCreated = false;

      Instance = FindObjectOfType<T>();
      if ( !onlyFind && s_instance == null )
        Instance = new GameObject( typeof( T ).FullName ).AddComponent<T>();

      if ( s_instance != null ) {
        s_instance.transform.hideFlags = HideFlags.NotEditable;
        s_wasCreated = true;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
      }

      return s_instance;
    }

    private static void OnSceneUnloaded( Scene scene )
    {
      s_wasCreated = false;
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private static T s_instance = null;
    private static bool s_wasCreated = false;
  }
}
