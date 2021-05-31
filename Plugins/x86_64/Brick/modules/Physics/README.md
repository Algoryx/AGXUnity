# Physics

## Components

The `Physics.Component` type is the fundamental building block for modelling high level physics components. Sub-types typically contain a set of child components and connections between them.


## Connectors and attachments

Connectors (`Physics.Connector`) define interactions between physics components. The connectors use attachment frames to match and align the connected components, and to set up physics constraints to be part of the simulation.

For example, a `SimpleCarWheelConnector` can define how to connect a wheel/tire to a wheel axle of a car model, using a hinge constraint.

An attachment (`Physics.Attachment`) is defined on individual components/bodies and defines how the body can be attached to other components. The attachment contains:

1. **Frame**: The local coordinates of the attachment
2. **Type**: Used to constrain the types of connections that the attachment is compatible with. Eg a car door should be attached to the door hinges, and not to the wheel axle, even if both are rotational attachments.
3. **Name**: Attachments have names for optional, automatic connections by matching names. And to convey the semantic meaning of an attachment.

The connectors are defined as logical connections between attachments, forming a connection graph.

The positioning of the connected components is computed using a positioning algorithm, that allows logical connections to be specified without explicitly aligning all the coordinate frames and bodies.

::: warning TODO
More documentation on positioning algorithm
:::


## Rigid bodies

A rigid body (`Physics.RigidBody`) is an entity in 3D space with physical properties such as mass and inertia tensor, as well as a frame which describes its position and orientation in space. A rigid body can have different motion control modes, which control the dynamics of the body:

- **DYNAMICS** - The rigid body's transform is updated given applied forces, which can be from e.g. collisions, constraints and explicitly applied forces (such as gravity).
- **STATIC** - The body is not affected by forces and will never update its position. The body is considered to have infinite mass.
- **KINEMATICS** - The body is not affected by forces, but can have an explicit velocity and angular velocity set, which will be used to integrate positions and orientations during simulation.

For more information on rigid bodies, see the AGX Dynamics User Manual.


## Geometries

A geometry (`Physics.Geometry`) can be assigned to a rigid body to give it a geometrical representation. This representation can be used for both collision detection and visualization. A geometry can get its shape either from a primitive, such as a box or a sphere, or from a custom mesh imported from file. For a complete list of primitives and their attributes, see the AGX Dynamics User Manual.


## Materials

A material (`Physics.Material`) contains definitions of friction, restitution, density and other parameters that describe the physical surface and bulk properties of an object. Materials are assigned to Geometries.

### Surface properties

The surface properties are used to determine how contact interactions with other geometries affect the involved bodies. There are three properties under this category:

- **roughness** – Corresponds to friction coefficient, the higher value, the higher friction.
- **adhesion** – Determines a force used for keeping colliding objects together.
- **viscosity** – Defines the compliance for friction constraints. It defines how “wet” a surface is.

### Bulk properties

The bulk properties define internal properties of an object, such as its density and stiffness. The properties in this category are:

- **density** - Can be used to set the mass and inertia tensor of the body.
- **youngsModulus** - This specifies the stiffness of the contact between two interacting geometries/bodies.
- **viscosity** - Used for calculating restitution.
- **damping** - Used for controlling how much time it should take to restore the collision penetration. A larger value will result in “softer” contacts.


<!-- ## Mechanics -->

<!-- ## FMI Export -->
