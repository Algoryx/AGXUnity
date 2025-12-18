using AGXUnity.Utils;
using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Base class for components. Many of our classes instantiates native objects
  /// during initialize and we've components that are dependent on other components
  /// native instances (e.g., RigidBody vs. Constraint). This base class facilitates
  /// cross dependencies enabling implementations to depend on each other in an
  /// otherwise random initialization order.
  /// </summary>
  /// <example>
  /// RigidBody rb = gameObject1.GetComponent{RigidBody}().GetInitialized{RigidBody}();
  /// // rb should have a native instance
  /// assert( rb.Native != null );
  /// </example>
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#components" )]
  public abstract class ScriptComponent : MonoBehaviour, IPropertySynchronizable, ISerializationCallbackReceiver
  {
    public enum States
    {
      CONSTRUCTED = 0,
      AWAKE,
      INITIALIZING,
      INITIALIZED,
      DESTROYED
    }

    [HideInInspector]
    public States State { get; private set; }

    protected ScriptComponent()
    {
      IsSynchronizingProperties = false;
    }

    // Version log:
    // 1 - Migrate shovel cutting direction to tooth direction
    private const int CurrentSerializationVersion = 1;

    [SerializeField]
    [HideInInspector]
    protected int m_serializationVersion = -1;

    public void OnBeforeSerialize()
    {
      m_serializationVersion = CurrentSerializationVersion;
    }

    public void OnAfterDeserialize()
    {
      if ( m_serializationVersion != CurrentSerializationVersion ) {
        if ( PerformMigration() ) {
          Debug.Log( $"Performed automatic migration of a component with type '{this.GetType()}'" );
#if UNITY_EDITOR
          // Ensure the migration is saved
          UnityEditor.EditorApplication.delayCall += () => {
            if ( this != null )
              UnityEditor.EditorUtility.SetDirty( this );
          };
#endif
        }
      }
    }

    /// <summary>
    /// Implement this to recieve a callback when an old version of a ScriptComponent is deserialized.
    /// </summary>
    /// <returns>True if migrations were made, false otherwise</returns>
    protected virtual bool PerformMigration() => false;

    /// <summary>
    /// Returns native simulation object unless the scene is being
    /// destructed.
    /// </summary>
    /// <returns>Native simulation object if not being destructed.</returns>
    public agxSDK.Simulation GetSimulation()
    {
      if ( Simulation.IsDestroyed )
        return null;

      return Simulation.Instance?.Native;
    }

    /// <summary>
    /// Makes sure this component is returned fully initialized, if
    /// e.g., your component depends on native objects in this.
    /// </summary>
    /// <typeparam name="T">Type of this component.</typeparam>
    /// <returns>This component fully initialized, or null if failed.</returns>
    public T GetInitialized<T>() where T : ScriptComponent
    {
      return (T)InitializeCallback();
    }

    /// <summary>
    /// True when the property synchronizer is running during (post) initialize.
    /// </summary>
    [HideInInspector]
    public bool IsSynchronizingProperties { get; private set; }

    public static event System.Action<ScriptComponent> OnInitialized;

    /// <summary>
    /// Cached synchronized properties.
    /// </summary>
    [HideInInspector]
    [NonSerialized]
    public Utils.PropertySynchronizer.FieldPropertyPair[] SynchronizedProperties = null;

    /// <summary>
    /// Internal method when initialize callback should be fired.
    /// </summary>
    protected ScriptComponent InitializeCallback()
    {
      if ( State == States.INITIALIZING )
        throw new Exception( "Initialize call when object is being initialized. Implement wait until initialized?" );

      // Supporting GetInitialized calls in, e.g., Awake.
      if ( State == States.CONSTRUCTED )
        Awake();

      if ( State == States.AWAKE ) {
        try {
          NativeHandler.Instance.MakeMainThread();
        }
        catch ( System.Exception ) {
          return null;
        }

        State = States.INITIALIZING;
        bool success = Initialize();
        State = success ? States.INITIALIZED : States.AWAKE;

        if ( success ) {
          IsSynchronizingProperties = true;
          Utils.PropertySynchronizer.Synchronize( this );
          IsSynchronizingProperties = false;

          if ( Application.isPlaying )
            m_uuidHash = Simulation.Instance.ContactCallbacks.Map( this );
        }

        OnInitialized?.Invoke( this );
      }

      return State == States.INITIALIZED ? this : null;
    }

    /// <summary>
    /// Initialize internal and/or native objects.
    /// </summary>
    /// <returns>true if successfully initialized</returns>
    protected virtual bool Initialize() { return true; }

    /// <summary>
    /// Register agx object method. Not possible to implement, use Initialize instead.
    /// </summary>
    protected void Awake()
    {
      if ( State == States.CONSTRUCTED ) {
        try {
          NativeHandler.Instance.MakeMainThread();
        }
        catch ( System.Exception ) {
          return;
        }

        State = States.AWAKE;
        OnAwake();
      }
    }

    /// <summary>
    /// On first call, all ScriptComponent objects will get Initialize callback.
    /// NOTE: Implement "Initialize" rather than "Start".
    /// </summary>
    protected void Start()
    {
      InitializeCallback();
    }

    protected virtual void OnAwake() { }

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() { }

    protected virtual void OnDestroy()
    {
      if ( Simulation.HasInstance )
        Simulation.Instance.ContactCallbacks.Unmap( m_uuidHash );

      NativeHandler.Instance.Unregister( this );

      State = States.DESTROYED;
    }

    protected virtual void OnApplicationQuit() { }

    /// <summary>
    /// This method is called by the <see cref="AGXUnityEditor.EditorUpdateManager"/> class on every editor update.
    /// Note that this is not called during play.
    /// </summary>
    public virtual void EditorUpdate() { }

    [NonSerialized]
    private uint m_uuidHash = 0u;
  }

  /// <summary>
  /// Extension methods for ScriptComponent.
  /// This is used for template inference in classes that derive from ScriptComponent.
  /// </summary>
  public static partial class ScriptComponentExtensions
  {
    /// <summary>
    /// Shorthand for the GetInitialized method which does not require specifying the component type.
    /// </summary>
    /// <typeparam name="T">The component type, deduced from instance.</typeparam>
    /// <param name="inst">Component instance to initialize.</param>
    /// <returns></returns>
    public static T GetInitialized<T>( this T inst ) where T : ScriptComponent
    {
      return inst.GetInitialized<T>();
    }
  }
}
