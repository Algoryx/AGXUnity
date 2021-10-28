using UnityEngine;

using SceneManager = UnityEngine.SceneManagement.SceneManager;
using Scene = UnityEngine.SceneManagement.Scene;

namespace AGXUnity
{
  /// <summary>
  /// Singleton like object that is created when Instance is called
  /// AND this object hasn't been destroyed in the Unity Engine.
  /// 
  /// This means that there can only be one instance of this type in
  /// a scene and that it is unsafe to use this object in the OnDestroy
  /// callbacks.
  /// 
  /// If this object has been destroyed, Instance will return null!
  /// </summary>
  /// <typeparam name="T">Type of subclass.</typeparam>
  public class UniqueGameObject<T> : ScriptComponent
    where T : ScriptComponent
  {
#if OLD_SINGLETON
    protected static T m_instance     = null;
    protected static bool m_destroyed = false;

    /// <summary>
    /// Returns an instance of this object if it hasn't been destroyed
    /// in the current context (Unity scene).
    /// </summary>
    /// <remarks>
    /// Note that this property may return null, and if it does, one
    /// probably wouldn't need the object anyway.
    /// </remarks>
    public static T Instance
    {
      get
      {
        if ( m_destroyed && !Application.isPlaying )
          ResetDestroyedState();

        var wasNull = m_instance == null;

        if ( !m_destroyed && m_instance == null && ( m_instance = FindObjectOfType( typeof( T ) ) as T ) == null ) {
          string name = ( typeof( T ).Namespace != null ? typeof( T ).Namespace + "." : "" ) + typeof( T ).Name;
          m_instance = ( new GameObject( name ) ).AddComponent<T>();
          m_instance.transform.hideFlags = HideFlags.NotEditable;
        }

        // When a scene has been unloaded it's safe to create
        // an instance of this singleton again.
        if ( wasNull && m_instance != null )
          SceneManager.sceneUnloaded += OnSceneUnloaded;

        return m_instance;
      }
    }

    // (FindObjectOfType( typeof( T ) ) as T ) != null
    public static bool HasInstance => m_instance != null;

    public static bool InstanceFound => m_instance != null || ( FindObjectOfType( typeof( T ) ) as T ) != null;

    public static bool IsDestroyed { get { return m_destroyed; } }

    /// <summary>
    /// Use with care. Reset the internal reset state so this
    /// object may be created again.
    /// 
    /// Only call/use this method when you are in the editor!
    /// </summary>
    public static void ResetDestroyedState()
    {
      m_destroyed = false;
    }

    protected virtual bool OnInitialize() => true;

    protected override void OnAwake()
    {
      m_destroyed = false;

      base.OnAwake();
    }

    protected sealed override bool Initialize()
    {
      // TODO: This isn't the right way to do it. Refactor this class!
      // https://gamedev.stackexchange.com/questions/116009/in-unity-how-do-i-correctly-implement-the-singleton-pattern
      int WARNING_READ_TODO;
      if ( !OnInitialize() )
        return false;

      if ( m_instance == null )
        m_instance = this as T;

      return true;
    }

    protected override void OnDestroy()
    {
      m_destroyed = true;

      base.OnDestroy();
    }

    private static void OnSceneUnloaded( Scene scene )
    {
      ResetDestroyedState();
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
#else
    public static T Instance
    {
      get
      {
        return s_instance ?? FindOrCreateInstance();
      }
      private set
      {
        s_instance = value;
        s_wasCreated |= value != null;
      }
    }

    public static bool HasInstance => s_instance != null;

    public static bool IsDestroyed => s_wasCreated && s_instance == null;

    public static bool HasInstanceInScene
    {
      get
      {
        return s_instance != null || ( FindOrCreateInstance( true ) != null );
      }
    }

    protected sealed override void OnAwake()
    {
      Instance = this as T;
    }

    protected override void OnDestroy()
    {
      Debug.Log( $"OnDestroy: {typeof( T ).FullName}, {s_instance != null}" );
      Instance = null;

      base.OnDestroy();
    }

    private static T FindOrCreateInstance( bool onlyFind = false )
    {
      if ( s_instance != null )
        Debug.LogError( $"{s_instance.name}: Invalid to call FindOrCreateInstance called with non-null s_instance." );

      Instance = FindObjectOfType<T>();
      Debug.Log( $"Found in scene: {typeof( T ).FullName}, {s_instance != null}" );
      if ( !onlyFind && s_instance == null ) {
        Instance = new GameObject( typeof( T ).FullName ).AddComponent<T>();
        Debug.Log( $"Create new: {typeof( T ).FullName}, {s_instance != null}" );
      }

      if ( s_instance != null ) {
        s_instance.transform.hideFlags = HideFlags.NotEditable;
        s_wasCreated = true;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
      }

      return s_instance;
    }

    private static void OnSceneUnloaded( Scene scene )
    {
      Debug.Log( "OnSceneUnloaded: " + scene.name );
      s_wasCreated = false;
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private static T s_instance = null;
    private static bool s_wasCreated = false;
#endif
  }
}
