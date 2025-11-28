using AGXUnity.Utils;
using System;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Script asset base class. Script assets are scripts but is part
  /// of the assets hierarchy in the editor and NOT part of a game object.
  /// 
  /// E.g., ShapeMaterial is a script asset, enabling the user to have a
  /// set of materials, shared by many shapes. When the user changes
  /// values in the ShapeMaterial (in their assets), all components with
  /// reference to this script asset receives the new values.
  /// 
  /// Since script assets are unique objects in Unity, this class uses
  /// ScriptAssetManager to have a "component like" behavior in the
  /// implementations.
  /// </summary>
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#assets" )]
  public abstract class ScriptAsset : ScriptableObject, IPropertySynchronizable
  {
    private const int CurrentSerializationVersion = 1;

    // Serialization version is currently unused
#pragma warning disable 0414
    [SerializeField]
    private int m_serializationVersion = CurrentSerializationVersion;
#pragma warning restore 0414

    /// <summary>
    /// True when the property synchronizer is running during (post) initialize.
    /// </summary>
    [HideInInspector]
    public bool IsSynchronizingProperties { get; private set; } = false;

    public static T Create<T>() where T : ScriptAsset
    {
      return Create( typeof( T ) ) as T;
    }

    public static ScriptAsset Create( Type type )
    {
      var instance = CreateInstance( type ) as ScriptAsset;
      instance.Construct();

      return instance;
    }

    /// <summary>
    /// Initializes any native objects. Use this method when components are initialized.
    /// </summary>
    /// <returns>This object, initialized.</returns>
    public T GetInitialized<T>() where T : ScriptAsset
    {
      if ( ScriptAssetManager.Instance == null )
        throw new AGXUnity.Exception( "Script asset manager seems to be destroyed. Is your script trying to access an initialized script asset outside of start/initialize -> destroy?" );

      var state = ScriptAssetManager.Instance.Report( this );
      if ( state == ScriptAssetManager.InitializationState.NotInitialized ) {
        NativeHandler.Instance.MakeMainThread();

        if ( Initialize() ) {
          IsSynchronizingProperties = true;
          Utils.PropertySynchronizer.Synchronize( this );
          IsSynchronizingProperties = false;
        }
        else {
          Debug.LogError( "Unable to initialize script asset: " + this.name, this );
          ScriptAssetManager.Instance.Unregister( this );
          return null;
        }
      }

      return this as T;
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
    /// Cached synchronized properties.
    /// </summary>
    [HideInInspector]
    [NonSerialized]
    public Utils.PropertySynchronizer.FieldPropertyPair[] SynchronizedProperties = null;

    /// <summary>
    /// Scene is taken down. Destroy/unreference any native object(s).
    /// </summary>
    public abstract void Destroy();

    /// <summary>
    /// Construct method called when a new instance of this object has been
    /// instantiated. This is like the default constructor but it's possible
    /// to create new instances from within this method.
    /// </summary>
    protected abstract void Construct();

    /// <summary>
    /// Initialize native objects. Properties will be synchronized after this call.
    /// </summary>
    /// <returns></returns>
    protected abstract bool Initialize();
  }

  /// <summary>
  /// Extension methods for ScriptAsset.
  /// This is used for template inference in classes that derive from ScriptAsset.
  /// </summary>
  public static partial class ScriptAssetExtensions
  {
    /// <summary>
    /// Shorthand for the GetInitialized method which does not require specifying the asset type.
    /// </summary>
    /// <typeparam name="T">The asset type, deduced from instance.</typeparam>
    /// <param name="inst">Asset instance to initialize.</param>
    /// <returns></returns>
    public static T GetInitialized<T>( this T inst ) where T : ScriptAsset
    {
      return inst.GetInitialized<T>();
    }
  }
}
