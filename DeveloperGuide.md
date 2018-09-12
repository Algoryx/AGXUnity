## AGXUnity.ScriptComponent extends UnityEngine.MonoBehaviour
Since we’re managing native objects and MonoBehaviour in general has a bit undeterministic behavior, all objects handling native objects should inherit from ScriptComponent rather than MonoBehaviour.
There’re also native objects that depends on other native objects. One example of this is constraints. The native constraints has to have native instances to the rigid bodies when the constraints are instantiated. To enable this, ScriptComponent has two important methods. “Initialize” where all native objects should be instantiated and “GetInitialized” which guarantees that the returned component is initialized with a valid native instance.

```c#
public abstract class ScriptComponent : MonoBehaviour
{
  ...
  /// <summary>
  /// Initialize internal and/or native objects.
  /// </summary>
  /// <returns>true if successfully initialized</returns>
  protected virtual bool Initialize();
  ...
  /// <summary>
  /// Makes sure this component is returned fully initialized, if
  /// e.g., your component depends on native objects in this.
  /// </summary>
  /// <typeparam name="T">Type of this component.</typeparam>
  /// <returns>This component fully initialized, or null if failed.</returns>
  public T GetInitialized<T>() where T : ScriptComponent;
}
```

Example usage – create a (native) lock joint between two game objects containing rigid body components:

```c#
protected override bool Initialize()
{
  // GetInitializedComponent is an AGXUnity extension to UnityEngine.GameObject.
  // GetInitializedComponent == go.GetComponent<RigidBody>().GetInitialized<RigidBody>().
  AGXUnity.RigidBody rb1 = m_gameObject1.GetInitializedComponent<AGXUnity.RigidBody>();
  AGXUnity.RigidBody rb2 = m_gameObject2.GetInitializedComponent<AGXUnity.RigidBody>();
  if ( rb1 == null || rb2 == null )
    throw new NullReferenceException();

  m_lock = new agx.LockJoint( rb1.Native, rb2.Native );
  GetSimulation().add( m_lock );

  return base.Initialize();
}
```

Each object is guaranteed to only receive one call to “Initialize”. The “Initialize” call is in general called during Unity “Start” phase.
After the “Initialize” call the object receives a “property synchronization” update.
Property synchronization
When data is changed in the editor or when scenes are restored from file (e.g., loading a scene) it’s convenient to handle the data flow in properties rather than fields and update phases. Read more about this in [Propagation of data with AGXUnityEditor.BaseEditor\<T\>.](#propagation)

As briefly described in previous section, ScriptComponents receives property synchronization update after the object has been initialized. Property synchronization is using reflection to match a private, serialized field to a public property with a matching name.

```c#
// Private, serialized field.
[SerializeField]
private Vector3 m_myVectorValue = Vector3.zero;
// Public property with matching name to m_myVectorValue.
public Vector3 MyVectorValue
{
  get { return m_myVectorValue; }
  set
  {
    m_myVectorValue = value;
    if ( something != null )
      something.SetValue( m_myVectorValue );
  }
}
```

The name of the property has to be the name of the field but without "m\_" and the first character capitalized. I.e., “MyVectorValue” is a match to “m\_myVectorValue”.
Whenever there’s a match, the property synchronization implementation performs obj.MyVectorValue = obj.m\_myVectorValue, i.e., invoking the “set” method, passing the private field as value.
The main benefit of this property synchronization is to write data to the native instances that were instantiated in the “Initialize” call. Consider the case where one presses “Play” in the editor or when a built application starts:

1. Serialized data is restored from file (to the serialized private and public fields).
2. ScriptComponent objects are initialized (“Initialize” is called) – native objects are created.
3. Property synchronization.

## Extending the Unity 3D editor
The AGXUnity plugin is linked to the native AGX Dynamics physics engine – having the simulated objects in the native environment. When values/properties etc. are changed from within the editor or a script, the data has to be propagated to the native environment – when needed.

The native objects are in general instantiated when Unity performs the “Start” calls. This means that all data has to be stored in the managed environment and then written down to the native environment when the managed object receives the “Start”/initialize call. Since all data is present in the Unity managed environment the serialization is trivial (automatic).


### <a name="propagation"> Propagation of data with AGXUnityEditor.BaseEditor\<T\></a>
The BaseEditor class is essential for the propagation of data from the managed to the native environment while using the editor. BaseEditor extends UnityEditor.Editor and it’s basically GUI code that you see in the “Inspector” tab in Unity.

Main features of the BaseEditor class is that it can:
- Visualize C# properties.
- Invoke “get” and “set” of C# properties when e.g., a value is changed.
- Invoke methods (when pressing a button).
- Handles custom attributes such as “This value must be larger than 0”.
- Like the default Unity editor class, visualize and change serializable fields.

Using properties instead of serializable fields makes it a lot easier to handle the data flow. Take for example the value of the mass of a rigid body.

```c#
// Public field, automatically serialized and
// visible in the editor.
public float Mass = 1.0f;
```

Having the mass as a field like this, we have to have at least one more line somewhere in the code where we assign it to the native rigid body instance.
Using properties instead will in general result in more code but enables a solid workflow, minimizing the risk of forgetting to propagate the data and maximizing the understanding when and how the data flows.

```c#
// Tell Unity we want serialization of this private field.
[SerializeField]
private float m_mass = 1.0f;
public float Mass
{
  get
  {
    return m_mass;
  }
  set
  {
    if ( value <= 0.0f )
      return;

    m_mass = value;

    // Synchronize to native instance if created.
    if ( m_native != null )
      m_native.getMassProperties().setMass( m_mass );
  }
}
```

Since “m\_mass” is private, it won’t be shown in the Inspector tab. Property “Mass” is public so it will be visualized and using BaseEditor the “set” method will be invoked when the value of the mass has been changed. If we have an instance of the native object, we can assign the new value directly.

### How to enable AGXUnityEditor.BaseEditor for an object
For Unity to use a custom editor to render the GUI under the “Inspector” tab, the class implementing the “OnInspectorGUI” method has to carry the attribute “CustomEditor”.
Consider the following, simple class that prints the input value to property “Test”:

```c#
public class TestComponent : MonoBehaviour
{
  [SerializeField]
  private float m_test = 0.5f;
  public float Test
  {
    get { return m_test; }
    set
    {
      Debug.Log( value );
      m_test = value;
    }
  }
}
```

**Note:** The type constraint is BaseEditor is UnityEngine.Object

Assigning the script to a game object a text field labeled “Text” will appear with value “0.5”. Using the default editor, changing the value, won’t show the debug print.

To enable the BaseEditor functionality, add a new script in any folder named “Editor”, with the following class:

```c#
using UnityEditor;

[CustomEditor( typeof( TestComponent ) ) ]
class TestComponentEditor : AGXUnityEditor.BaseEditor<TestComponent>
{
}
```

The TestComponent will be rendered the same in the Inspector tab, but when changing the value, the new value will be printed in the Console tab.

## Using EventListeners
EventListeners such as agxSDK.StepEventListener and agxSDK.ContactEventListeners has both a C# implementation (your code implementing the virtual methods) and a C++ implementation (the C# - C++ bridge). It is important to keep a reference to the C# object even though the listener object is added to a simulation.

Wrong way:

```c#
public class TestListener : ScriptComponent {
  class MyListener : agxSDK.StepEventListener
  {
    private override void pre(double t)
    {
    }
  }

  protected override bool Initialize()
  {
    // A C# instance is created, the C++ representation will be added to the simulation.
    // However, later when GC is run, the C# implementation will be deleted and C++ will try to call an object that does no longer exist!
    // At some point you will get:
    //    NullReferenceException: Object reference not set to an instance of an object
    //    at agxSDK.StepEventListener.SwigDirectorpost (Double arg0) [0x00000] in <filename unknown>:0     
    GetSimulation().add(new MyListener());
  }
}


```

Correct way:

```c#
public class TestListener : ScriptComponent {
  class MyListener : agxSDK.StepEventListener
  {
    private override void pre(double t)
    {
    }
  }

  private: agxSDK.StepEventListener m_listener = null;
  
  protected override bool Initialize()
  {
    // A C# instance is created, we will keep a reference to it!
    m_listener = new MyListener();
    GetSimulation().add(m_listener);
  }
}


```
