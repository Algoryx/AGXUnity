# Geometry


## Coordinatete frames and transformations

The simulation scenegraph has a 3d transformation hierarchy, where each node can be positioned in local coordinates relative to the coordinate frame of the parent. This is a common coordinate composition method found in many 3d graphics environments.

### Transform

This is a composite type for expressing affine 3d coordinate transformations. It consists of a position and a rotation. The rotation is typically expressed using Euler angles, or relative rotations using reference axes and other geometry.


### Frame

The frame type has a local and a world transform, and connections to parent and child frames.

When attached to a parent frame, the world transform is computed by applying the local transform in the world transform of the parent.

### Position

TODO

### Rotation

TODO
