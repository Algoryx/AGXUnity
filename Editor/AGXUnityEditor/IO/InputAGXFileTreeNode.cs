using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AGXUnityEditor.IO
{
  public class InputAGXFileTreeNode
  {
    public enum NodeType
    {
      Placeholder,
      Shape,
      Geometry,
      RigidBody,
      Assembly,
      Constraint,
      Wire,
      Cable,
      Material,
      ContactMaterial,
      GroupId,
      ObserverFrame
    }

    public NodeType Type { get; set; }

    public agx.Uuid Uuid { get; set; }

    public InputAGXFileTreeNode Parent { get; private set; }

    public InputAGXFileTreeNode[] Children { get { return m_children.ToArray(); } }

    public InputAGXFileTreeNode[] References { get { return m_references.ToArray(); } }

    public GameObject GameObject { get; set; }

    public ScriptableObject Asset { get; set; }

    public object Object { get; set; }

    public string Name { get; set; }

    public void AddChild( InputAGXFileTreeNode child )
    {
      if ( child == null ) {
        Debug.LogWarning( "Trying to add null child to parent: " + Type + ", (UUID: " + Uuid.str() + ")" );
        return;
      }

      if ( child.Parent != null ) {
        Debug.LogError( "Node already have a parent." );
        return;
      }

      child.Parent = this;
      m_children.Add( child );
    }

    public InputAGXFileTreeNode[] GetChildren( NodeType type )
    {
      return ( from node in m_children where node.Type == type select node ).ToArray();
    }

    public void AddReference( InputAGXFileTreeNode reference )
    {
      if ( reference == null || m_references.Contains( reference ) )
        return;

      m_references.Add( reference );
      reference.m_references.Add( this );
    }

    public InputAGXFileTreeNode[] GetReferences( NodeType type )
    {
      return ( from node in m_references where node.Type == type select node ).ToArray();
    }

    private List<InputAGXFileTreeNode> m_children   = new List<InputAGXFileTreeNode>();
    private List<InputAGXFileTreeNode> m_references = new List<InputAGXFileTreeNode>();
  }
}
