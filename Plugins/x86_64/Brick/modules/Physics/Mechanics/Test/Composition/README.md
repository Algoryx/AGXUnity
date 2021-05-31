# Composition

The Composition folder in the Example module both work as an example on how to model mechanics by composition, and for unit tests of the Positioning Algorithm.

The `Components` folder contain Brick files declaring Components and Parts for building a Crane. The `Connections` folder include helper models for connecting the Crane parts and Cylinder with axle connections.

The `Crane.yml` file define a crane in two steps. `CraneParts` collects all parts for the crane. `Crane` connects the parts.

## Initializing Simulation
The `Crane.yml` file include two scenes using the `Crane` Component.

1. The `CraneAngleWorld` Component define the crane position using angle between the `Column` and the `Boom` as a variable.
2. The `CraneCylinderWorld` Component define the crane position using the length of the cylinder