using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using AGXUnity.Utils;
using NodeType = AGXUnityEditor.IO.InputAGXFileTreeNode.NodeType;
using Node = AGXUnityEditor.IO.InputAGXFileTreeNode;

namespace AGXUnityEditor.IO
{
  public class InputAGXFileTree
  {
    public static bool IsCableRigidBody( agx.RigidBody rb )
    {
      return agxCable.Cable.getCableForBody( rb ) != null;
    }

    public static bool IsWireRigidBody( agx.RigidBody rb )
    {
      return agxWire.Wire.isLumpedNode( rb );
    }

    public static bool IsValid( agx.RigidBody rb )
    {
      return rb != null &&
             !IsWireRigidBody( rb ) &&
             !IsCableRigidBody( rb );
    }

    public static bool IsValid( agxCollide.Geometry geometry )
    {
      return geometry != null &&
             agxWire.Wire.getWire( geometry ) == null &&
             agxCable.Cable.getCableForGeometry( geometry ) == null;
    }

    public static bool IsValid( agx.Constraint constraint )
    {
      return constraint != null &&
             constraint.getNumBodies() > 0ul;
    }

    public Node[] Roots { get { return m_roots.ToArray(); } }

    public Node[] Constraints { get { return m_constraintRoot.Children; } }

    public Node[] Materials { get { return m_materialRoot.Children; } }

    public Node[] ContactMaterials { get { return m_contactMaterialRoot.Children; } }

    public InputAGXFileTree()
    {
    }

    public Node GetNode( agx.Uuid uuid )
    {
      Node node;
      if ( m_nodeCache.TryGetValue( uuid, out node ) )
        return node;
      return null;
    }

    public agx.Frame GetAssembly( agx.Uuid uuid )
    {
      agx.Frame frame;
      if ( m_assemblies.TryGetValue( uuid, out frame ) )
        return frame;
      return null;
    }

    public agx.RigidBody GetRigidBody( agx.Uuid uuid )
    {
      agx.RigidBody rb;
      if ( m_bodies.TryGetValue( uuid, out rb ) )
        return rb;
      return null;
    }

    public agxCollide.Geometry GetGeometry( agx.Uuid uuid )
    {
      agxCollide.Geometry geometry;
      if ( m_geometries.TryGetValue( uuid, out geometry ) )
        return geometry;
      return null;
    }

    public agxCollide.Shape GetShape( agx.Uuid uuid )
    {
      agxCollide.Shape shape;
      if ( m_shapes.TryGetValue( uuid, out shape ) )
        return shape;
      return null;
    }

    public agx.Constraint GetConstraint( agx.Uuid uuid )
    {
      agx.Constraint constraint;
      if ( m_constraints.TryGetValue( uuid, out constraint ) )
        return constraint;
      return null;
    }

    public agxWire.Wire GetWire( agx.Uuid uuid )
    {
      agxWire.Wire wire;
      if ( m_wires.TryGetValue( uuid, out wire ) )
        return wire;
      return null;
    }

    public agxCable.Cable GetCable( agx.Uuid uuid )
    {
      agxCable.Cable cable;
      if ( m_cables.TryGetValue( uuid, out cable ) )
        return cable;
      return null;
    }

    public agx.Material GetMaterial( agx.Uuid uuid )
    {
      agx.Material material;
      if ( m_materials.TryGetValue( uuid, out material ) )
        return material;
      return null;
    }

    public agx.ContactMaterial GetContactMaterial( agx.Uuid uuid )
    {
      agx.ContactMaterial contactMaterial;
      if ( m_contactMaterials.TryGetValue( uuid, out contactMaterial ) )
        return contactMaterial;
      return null;
    }

    public agx.ObserverFrame GetObserverFrame( agx.Uuid uuid )
    {
      agx.ObserverFrame observerFrame;
      if ( m_observerFrames.TryGetValue( uuid, out observerFrame ) )
        return observerFrame;
      return null;
    }

    public void Parse( agxSDK.Simulation simulation, AGXFileInfo fileInfo )
    {
      if ( simulation == null )
        throw new ArgumentNullException( "simulation", "agxSDK.Simulation instance is null." );

      if ( m_roots.Count > 0 )
        throw new AGXUnity.Exception( "Calling InputAGXFileTree::Parse multiple times is not supported." );

      // RigidBody nodes.
      foreach ( var nativeRb in simulation.getRigidBodies() ) {
        if ( !IsValid( nativeRb.get() ) )
          continue;

        // TODO: Recursive assembly creation.
        var assemblyNode = TryGetOrCreateAssembly( nativeRb.getFrame() );
        var rbNode       = GetOrCreateRigidBody( nativeRb.get(), assemblyNode == null );
        if ( assemblyNode != null )
          assemblyNode.AddChild( rbNode );

        foreach ( var nativeGeometry in nativeRb.getGeometries() )
          Parse( nativeGeometry.get(), rbNode );
      }

      // Free Geometry nodes.
      foreach ( var nativeGeometry in simulation.getGeometries() ) {
        if ( !IsValid( nativeGeometry.get() ) )
          continue;

        // We already have a node for this from reading bodies.
        if ( nativeGeometry.getRigidBody() != null ) {
          if ( !m_nodeCache.ContainsKey( nativeGeometry.getUuid() ) )
            Debug.LogWarning( "Geometry with rigid body ignored but isn't in present in the tree. Name: " + nativeGeometry.getName() );
          continue;
        }

        // TODO: Recursive assembly creation.
        Parse( nativeGeometry.get(), TryGetOrCreateAssembly( nativeGeometry.getFrame() ) );
      }

      // Constraint nodes.
      foreach ( var nativeConstraint in simulation.getConstraints() ) {
        if ( !IsValid( nativeConstraint.get() ) )
          continue;

        var nativeRb1 = nativeConstraint.getBodyAt( 0 );
        var nativeRb2 = nativeConstraint.getBodyAt( 1 );
        if ( !IsValid( nativeRb1 ) )
          continue;
        if ( nativeRb2 != null && !IsValid( nativeRb2 ) )
          continue;

        var rb1Node = nativeRb1 != null ? GetNode( nativeRb1.getUuid() ) : null;
        var rb2Node = nativeRb2 != null ? GetNode( nativeRb2.getUuid() ) : null;

        // Could be ignored bodies due to Wire and Cable.
        if ( rb1Node == null && rb2Node == null )
          continue;

        var constraintNode = GetOrCreateConstraint( nativeConstraint.get() );
        if ( rb1Node != null )
          constraintNode.AddReference( rb1Node );
        if ( rb2Node != null )
          constraintNode.AddReference( rb2Node );
      }

      var wires = agxWire.Wire.findAll( simulation );
      foreach ( var wire in wires ) {
        // TODO: Handle wires in assemblies?
        var wireNode = GetOrCreateWire( wire );
        if ( wire.getMaterial() != null ) {
          var materialNode = GetOrCreateMaterial( wire.getMaterial() );
          wireNode.AddReference( materialNode );
        }
      }

      var cables = agxCable.Cable.getAll( simulation );
      foreach ( var cable in cables ) {
        var cableNode = GetOrCreateCable( cable );
        if ( cable.getMaterial() != null ) {
          var materialNode = GetOrCreateMaterial( cable.getMaterial() );
          cableNode.AddReference( materialNode );
        }

        var groupsCollection = cable.findGroupIdCollection();
        foreach ( var name in groupsCollection.getNames() )
          cableNode.AddReference( new Node() { Type = NodeType.GroupId, Object = name } );
        foreach ( var id in groupsCollection.getIds() )
          cableNode.AddReference( new Node() { Type = NodeType.GroupId, Object = id.ToString() } );

        var it = cable.getSegments().begin();
        while ( !it.EqualWith( cable.getSegments().end() ) ) {
          var constraint = it.getConstraint();
          if ( constraint != null && GetConstraint( constraint.getUuid() ) != null )
            Debug.LogWarning( "Cable constraint has a constraint node in the simulation tree." );
          foreach ( var attachment in it.getAttachments() )
            if ( attachment.getConstraint() != null && GetConstraint( attachment.getConstraint().getUuid() ) != null )
              Debug.LogWarning( "Cable attachment has a constraint node in the simulation tree." );
          it.inc();
        }
      }

      var mm = simulation.getMaterialManager();
      foreach ( var m1 in m_materials.Values ) {
        foreach ( var m2 in m_materials.Values ) {
          var cm = mm.getContactMaterial( m1, m2 );
          if ( cm == null )
            continue;

          var cmNode = GetOrCreateContactMaterial( cm );
          cmNode.AddReference( GetNode( m1.getUuid() ) );
          cmNode.AddReference( GetNode( m2.getUuid() ) );
        }
      }

      foreach ( var observerFrame in simulation.getObserverFrames() ) {
        if ( observerFrame.getRigidBody() == null )
          continue;
        var rbNode = GetNode( observerFrame.getRigidBody().getUuid() );
        if ( rbNode == null )
          continue;

        var observerFrameNode = GetOrCreateObserverFrame( observerFrame.get() );
        observerFrameNode.AddReference( rbNode );
      }

      // Generating wires, cables and constraints last when all bodies has been generated.
      m_roots.Add( m_wireRoot );
      m_roots.Add( m_cableRoot );
      m_roots.Add( m_constraintRoot );
      m_roots.Add( m_observerFrameRoot );
      // Generating assets last since we have to know the references.
      // Materials aren't parsed, they are generated on the fly when
      // objects references them.
      m_roots.Add( m_materialRoot );
      m_roots.Add( m_contactMaterialRoot );
    }

    private void Parse( agxCollide.Geometry geometry, Node parent )
    {
      var geometryNode = GetOrCreateGeometry( geometry, parent == null );
      if ( parent != null )
        parent.AddChild( geometryNode );

      foreach ( var shape in geometry.getShapes() ) {
        var shapeNode = GetOrCreateShape( shape.get() );
        geometryNode.AddChild( shapeNode );
      }

      if ( geometry.getMaterial() != null ) {
        var materialNode = GetOrCreateMaterial( geometry.getMaterial() );
        geometryNode.AddReference( materialNode );
      }

      var groupsCollection = geometry.findGroupIdCollection();
      foreach ( var name in groupsCollection.getNames() )
        geometryNode.AddReference( new Node() { Type = NodeType.GroupId, Object = name } );
      foreach ( var id in groupsCollection.getIds() )
        geometryNode.AddReference( new Node() { Type = NodeType.GroupId, Object = id.ToString() } );
    }

    private Node TryGetOrCreateAssembly( agx.Frame child )
    {
      agx.Frame parent = child != null ? child.getParent() : null;
      // If parent has a rigid body 'child' is probably a geometry.
      if ( parent == null || parent.getRigidBody() != null )
        return null;

      return GetOrCreateAssembly( parent );
    }

    private Node GetOrCreateAssembly( agx.Frame frame )
    {
      return GetOrCreateNode( NodeType.Assembly,
                              frame.getUuid(),
                              true,
                              () => m_assemblies.Add( frame.getUuid(), frame ) );
    }

    private Node GetOrCreateRigidBody( agx.RigidBody rb, bool isRoot )
    {
      return GetOrCreateNode( NodeType.RigidBody,
                              rb.getUuid(),
                              isRoot,
                              () => m_bodies.Add( rb.getUuid(), rb ) );
    }

    private Node GetOrCreateGeometry( agxCollide.Geometry geometry, bool isRoot )
    {
      return GetOrCreateNode( NodeType.Geometry,
                              geometry.getUuid(),
                              isRoot,
                              () => m_geometries.Add( geometry.getUuid(), geometry ) );
    }

    private Node GetOrCreateShape( agxCollide.Shape shape )
    {
      return GetOrCreateNode( NodeType.Shape,
                              shape.getUuid(),
                              false,
                              () => m_shapes.Add( shape.getUuid(), shape ) );
    }

    private Node GetOrCreateConstraint( agx.Constraint constraint )
    {
      return GetOrCreateNode( NodeType.Constraint,
                              constraint.getUuid(),
                              true,
                              () => m_constraints.Add( constraint.getUuid(), constraint ) );
    }

    private Node GetOrCreateWire( agxWire.Wire wire )
    {
      return GetOrCreateNode( NodeType.Wire,
                              wire.getUuid(),
                              true,
                              () => m_wires.Add( wire.getUuid(), wire ) );
    }

    private Node GetOrCreateCable( agxCable.Cable cable )
    {
      return GetOrCreateNode( NodeType.Cable,
                              cable.getUuid(),
                              true,
                              () => m_cables.Add( cable.getUuid(), cable ) );
    }

    private Node GetOrCreateMaterial( agx.Material material )
    {
      return GetOrCreateNode( NodeType.Material,
                              material.getUuid(),
                              true,
                              () => m_materials.Add( material.getUuid(), material ) );
    }

    private Node GetOrCreateContactMaterial( agx.ContactMaterial contactMaterial )
    {
      return GetOrCreateNode( NodeType.ContactMaterial,
                              contactMaterial.getUuid(),
                              true,
                              () => m_contactMaterials.Add( contactMaterial.getUuid(), contactMaterial ) );
    }

    private Node GetOrCreateObserverFrame( agx.ObserverFrame observerFrame )
    {
      return GetOrCreateNode( NodeType.ObserverFrame,
                              observerFrame.getUuid(),
                              true,
                              () => m_observerFrames.Add( observerFrame.getUuid(), observerFrame ) );
    }

    private Node GetOrCreateNode( NodeType type, agx.Uuid uuid, bool isRoot, Action onCreate )
    {
      if ( m_nodeCache.ContainsKey( uuid ) )
        return m_nodeCache[ uuid ];

      onCreate();

      return CreateNode( type, uuid, isRoot );
    }

    private Node CreateNode( NodeType type, agx.Uuid uuid, bool isRoot )
    {
      Node node = new Node()
      {
        Type       = type,
        Uuid       = uuid
      };

      if ( isRoot ) {
        if ( type == NodeType.Constraint )
          m_constraintRoot.AddChild( node );
        else if ( type == NodeType.Material )
          m_materialRoot.AddChild( node );
        else if ( type == NodeType.ContactMaterial )
          m_contactMaterialRoot.AddChild( node );
        else if ( type == NodeType.Wire )
          m_wireRoot.AddChild( node );
        else if ( type == NodeType.Cable )
          m_cableRoot.AddChild( node );
        else if ( type == NodeType.ObserverFrame )
          m_observerFrameRoot.AddChild( node );
        else if ( m_roots.FindIndex( n => n.Uuid == uuid ) >= 0 )
          Debug.LogError( "Node already present as root." );
        else
          m_roots.Add( node );
      }

      m_nodeCache.Add( uuid, node );

      return node;
    }

    private Dictionary<agx.Uuid, Node>                m_nodeCache        = new Dictionary<agx.Uuid, Node>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.Frame>           m_assemblies       = new Dictionary<agx.Uuid, agx.Frame>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.RigidBody>       m_bodies           = new Dictionary<agx.Uuid, agx.RigidBody>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agxCollide.Geometry> m_geometries       = new Dictionary<agx.Uuid, agxCollide.Geometry>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agxCollide.Shape>    m_shapes           = new Dictionary<agx.Uuid, agxCollide.Shape>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.Constraint>      m_constraints      = new Dictionary<agx.Uuid, agx.Constraint>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agxWire.Wire>        m_wires            = new Dictionary<agx.Uuid, agxWire.Wire>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agxCable.Cable>      m_cables           = new Dictionary<agx.Uuid, agxCable.Cable>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.Material>        m_materials        = new Dictionary<agx.Uuid, agx.Material>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.ContactMaterial> m_contactMaterials = new Dictionary<agx.Uuid, agx.ContactMaterial>( new AGXUnity.IO.UuidComparer() );
    private Dictionary<agx.Uuid, agx.ObserverFrame>   m_observerFrames   = new Dictionary<agx.Uuid, agx.ObserverFrame>( new AGXUnity.IO.UuidComparer() );

    private List<Node> m_roots         = new List<Node>();
    private Node m_constraintRoot      = new Node() { Type = NodeType.Placeholder, Name = "Constraints" };
    private Node m_wireRoot            = new Node() { Type = NodeType.Placeholder, Name = "Wires" };
    private Node m_cableRoot           = new Node() { Type = NodeType.Placeholder, Name = "Cables" };
    private Node m_materialRoot        = new Node() { Type = NodeType.Placeholder, Name = "Shape materials" };
    private Node m_contactMaterialRoot = new Node() { Type = NodeType.Placeholder, Name = "Contact materials" };
    private Node m_observerFrameRoot   = new Node() { Type = NodeType.Placeholder, Name = "Observer frames" };
  }
}
