using System;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity
{
  public class Route<T> : ScriptComponent, IEnumerable<T>
    where T : RouteNode
  {
    public class ValidatedNode
    {
      public T Node = null;
      public bool Valid = true;
      public string ErrorString = string.Empty;
    }

    public class ValidatedRoute : IEnumerable<ValidatedNode>
    {
      public bool Valid = true;
      public string ErrorString = string.Empty;
      public List<ValidatedNode> Nodes = new List<ValidatedNode>();

      public IEnumerator<ValidatedNode> GetEnumerator()
      {
        return Nodes.GetEnumerator();
      }

      System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
      {
        return GetEnumerator();
      }
    }

    public virtual ValidatedRoute GetValidated()
    {
      ValidatedRoute validatedRoute = new ValidatedRoute();
      foreach ( var node in this )
        validatedRoute.Nodes.Add( new ValidatedNode() { Node = node, Valid = true } );

      return validatedRoute;
    }

    /// <summary>
    /// Route node list.
    /// </summary>
    [SerializeField]
    private List<T> m_nodes = new List<T>();

    /// <summary>
    /// Callback fired when a node has been added/inserted into this route.
    /// Signature: OnNodeAdded( NodeT addedNode, int indexOfAddedNode ).
    /// </summary>
    public Action<T, int> OnNodeAdded = delegate { };

    /// <summary>
    /// Callback fired when a node has been removed from this route.
    /// Signature: OnNodeRemoved( WireRouteNode removedNode, int indexOfRemovedNode ).
    /// </summary>
    public Action<T, int> OnNodeRemoved = delegate { };

    /// <summary>
    /// Number of nodes in route.
    /// </summary>
    public int NumNodes { get { return m_nodes.Count; } }

    /// <summary>
    /// Get node at index <paramref name="index"/>.
    /// </summary>
    /// <param name="index">Index of node.</param>
    /// <returns>Node given index.</returns>
    public T this[ int index ]
    {
      get { return m_nodes[ index ]; }
    }

    /// <summary>
    /// Calculates and returns the total length of this route.
    /// </summary>
    public float TotalLength
    {
      get
      {
        float totalLength = 0.0f;
        for ( int i = 1; i < NumNodes; ++i )
          totalLength += Vector3.Distance( this[ i - 1 ].Position, this[ i ].Position );

        return totalLength;
      }
    }

    /// <summary>
    /// Finds index of the node in the list.
    /// </summary>
    /// <param name="node">Node to find index of.</param>
    /// <returns>Index of the node in route list - -1 if not found.</returns>
    public int IndexOf( T node )
    {
      return m_nodes.IndexOf( node );
    }

    /// <summary>
    /// Add new node to route.
    /// </summary>
    /// <param name="node">Node to add.</param>
    /// <returns>True if the node is added, false if null or already present.</returns>
    public bool Add( T node )
    {
      if ( node == null || m_nodes.Contains( node ) )
        return false;

      return TryInsertAtIndex( m_nodes.Count, node );
    }

    /// <summary>
    /// Insert node before another given node, already present in route.
    /// </summary>
    /// <param name="nodeToInsert">Node to insert.</param>
    /// <param name="beforeThisNode">Insert <paramref name="nodeToInsert"/> before this node.</param>
    /// <returns>True if inserted, false if null or already present.</returns>
    public bool InsertBefore( T nodeToInsert, T beforeThisNode )
    {
      if ( nodeToInsert == null || beforeThisNode == null )
        return false;

      return TryInsertAtIndex( m_nodes.IndexOf( beforeThisNode ), nodeToInsert );
    }

    /// <summary>
    /// Insert node after another given node, already present in route.
    /// </summary>
    /// <param name="nodeToInsert">Node to insert.</param>
    /// <param name="afterThisNode">Insert <paramref name="nodeToInsert"/> before this node.</param>
    /// <returns>True if inserted, false if null or already present.</returns>
    public bool InsertAfter( T nodeToInsert, T afterThisNode )
    {
      if ( nodeToInsert == null || afterThisNode == null || !m_nodes.Contains( afterThisNode ) )
        return false;

      return TryInsertAtIndex( m_nodes.IndexOf( afterThisNode ) + 1, nodeToInsert );
    }

    /// <summary>
    /// Remove node from route.
    /// </summary>
    /// <param name="node">Node to remove.</param>
    /// <returns>True if removed, otherwise false.</returns>
    public bool Remove( T node )
    {
      int index = IndexOf( node );
      if ( index < 0 || index >= m_nodes.Count )
        return false;

      m_nodes.RemoveAt( index );

      OnNodeRemoved( node, index );

      return true;
    }

    /// <summary>
    /// Complete clear of this route.
    /// </summary>
    public virtual void Clear()
    {
      m_nodes.Clear();
    }

    protected override bool Initialize()
    {
      foreach ( var node in this )
        node.GetInitialized<T>();

      return true;
    }

    protected override void OnDestroy()
    {
      foreach ( var node in this )
        node.OnDestroy();

      base.OnDestroy();
    }

    protected virtual void Reset()
    {
      hideFlags |= HideFlags.HideInInspector;
    }

    private bool TryInsertAtIndex( int index, T node )
    {
      // According to List documentation having index == m_nodes.Count is
      // valid and the new node will be added to the list.
      if ( index < 0 || index > m_nodes.Count || m_nodes.Contains( node ) )
        return false;

      m_nodes.Insert( index, node );

      OnNodeAdded( node, index );

      return true;
    }

    public IEnumerator<T> GetEnumerator()
    {
      return m_nodes.GetEnumerator();
    }

    System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
    {
      return GetEnumerator();
    }
  }
}
