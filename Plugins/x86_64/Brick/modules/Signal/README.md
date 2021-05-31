# Signal

The Signal module allows getting and setting values of another component during runtime. It could be setting the desired velocity of a velocity-controlled motor, or getting the position of a body, for example.

All signals are either of type `Input<T>` or `Output<T>`, where `T` corresponds to the the data type that the signal can get or set. These two types inherit from a base type called `SignalBase`. This is because it currently isn't possible to gather all objects of a type when it is templated (in C#).

A signal in itself does not do anything, but it requires a "consumer" to actually request an interaction with it. Consumers can be for example something that logs the signal to file, sets a signal given a keyboard interaction, or publishes the signal to a ROS topic. The Signal module includes a `FileLogger` consumer, which logs an output signal to file.

## Adding a new signal

All signals must explicitly defined in a .yml-file and a corresponding implementation file generated. To create a new signal, follow these steps:

1. Add a new .yml file for the Signal, extending either Signal.Input<T> or Signal.Output<T> (with T being the signal type, i.e. Vec3, Real, etc)
2. Run brick build modules/Signal
3. Create a corresponding .cs file in cs/brick/Signal (if it wasn't automatically created in step 2), and implement the GetData method.
    1. For inputs, also implement the SetData method.
4. Make sure that the value of the component that the signal should get or set from is synced during the simulation (in e.g. BrickSimulation.cs).
