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
  public class UniqueGameObject<T> : ScriptComponent where T : ScriptComponent
  {
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

    public static bool HasInstance { get { return m_instance != null || (FindObjectOfType( typeof( T ) ) as T ) != null; } }

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

    protected override void OnAwake()
    {
      m_destroyed = false;

      base.OnAwake();
    }

    protected override void OnDestroy()
    {
      base.OnDestroy();

      m_destroyed = true;
    }

    private static void OnSceneUnloaded( Scene scene )
    {
      ResetDestroyedState();
      SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }
  }
}
