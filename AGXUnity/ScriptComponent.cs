﻿using System;
using System.Reflection;
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
  public abstract class ScriptComponent : MonoBehaviour
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

    /// <summary>
    /// Returns native simulation object unless the scene is being
    /// destructed.
    /// </summary>
    /// <returns>Native simulation object if not being destructed.</returns>
    public agxSDK.Simulation GetSimulation()
    {
      Simulation simulation = Simulation.Instance;
      return simulation ? simulation.Native : null;
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
      State = States.AWAKE;
      OnAwake();
    }

    /// <summary>
    /// On first call, all ScriptComponent objects will get Initialize callback.
    /// NOTE: Implement "Initialize" rather than "Start".
    /// </summary>
    protected void Start()
    {
      InitializeCallback();

      IsSynchronizingProperties = true;
      Utils.PropertySynchronizer.Synchronize( this );
      IsSynchronizingProperties = false;
    }

    protected virtual void OnAwake() { }

    protected virtual void OnEnable() { }

    protected virtual void OnDisable() { }

    protected virtual void OnDestroy()
    {
      NativeHandler.Instance.Unregister( this );

      State = States.DESTROYED;
    }

    protected virtual void OnApplicationQuit() { }
  }
}
