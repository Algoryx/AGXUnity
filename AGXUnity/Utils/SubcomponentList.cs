using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AGXUnity.Util
{
  public interface ISubcomponentList : IEnumerable
  {
    event Action OnChange;

    void Add( object item );
    bool Remove( object item );
    void Clear();
    bool Contains( object item );
    void AddDefault();
  }

  /// <summary>
  /// Serializable collection for subcomponents owned by a parent object.
  /// </summary>
  /// <remarks>
  /// Besides exposing a read-only list, this class keeps mutation behavior such
  /// as ownership validation and change notifications consistent for all
  /// subcomponent collections.
  /// </remarks>
  [Serializable]
  public class SubcomponentList<T, ParentT> :
    IReadOnlyList<T>,
    ISubcomponentList
    where T : Subcomponent<ParentT>, new()
    where ParentT : class
  {
    [SerializeField]
    private List<T> m_backing = new List<T>();

    public event Action OnChange;

    public int Count => m_backing.Count;

    public T this[ int index ] => m_backing[ index ];

    public void Add( T item )
    {
      Insert( Count, item );
    }

    public void AddDefault()
    {
      Add( new T() );
    }

    public void Insert( int index, T item )
    {
      ValidateInsert( index, item, nameof( item ) );
      m_backing.Insert( index, item );
      OnChange?.Invoke();
    }

    public bool Remove( T item )
    {
      if ( !m_backing.Remove( item ) )
        return false;

      OnChange?.Invoke();
      return true;
    }

    public void Move( int fromIndex, int toIndex )
    {
      ValidateExistingIndex( fromIndex, nameof( fromIndex ) );
      ValidateExistingIndex( toIndex, nameof( toIndex ) );
      if ( fromIndex == toIndex )
        return;

      var item = m_backing[ fromIndex ];
      m_backing.RemoveAt( fromIndex );
      m_backing.Insert( toIndex, item );
      OnChange?.Invoke();
    }

    public void Clear()
    {
      if ( Count == 0 )
        return;

      m_backing.Clear();
      OnChange?.Invoke();
    }

    public bool Contains( T item ) => m_backing.Contains( item );

    internal List<T> GetAttachedItems( ParentT parent,
                                       string itemDescription,
                                       UnityEngine.Object logContext )
    {
      var result = new List<T>();
      var unique = new HashSet<T>();
      foreach ( var item in m_backing ) {
        if ( item == null ) {
          Debug.LogWarning( $"The collection contains a null {itemDescription}. It will be ignored.",
                            logContext );
          continue;
        }
        if ( !unique.Add( item ) ) {
          Debug.LogWarning( $"The collection contains the same {itemDescription} more than once. " +
                            "Duplicate entries will be ignored.",
                            logContext );
          continue;
        }
        if ( !item.Attach( parent ) )
          continue;

        result.Add( item );
      }

      return result;
    }

    private void ValidateInsert( int index, T item, string parameterName )
    {
      if ( item == null )
        throw new ArgumentNullException( parameterName );
      if ( index < 0 || index > Count )
        throw new ArgumentOutOfRangeException( nameof( index ) );
      if ( Contains( item ) )
        throw new InvalidOperationException( "The same subcomponent cannot be added more than once." );
      if ( item.Parent != null )
        throw new InvalidOperationException( "The subcomponent is already owned by another component." );
    }

    private void ValidateExistingIndex( int index, string parameterName )
    {
      if ( index < 0 || index >= Count )
        throw new ArgumentOutOfRangeException( parameterName );
    }

    public IEnumerator<T> GetEnumerator() => m_backing.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    void ISubcomponentList.Add( object item )
    {
      if ( item == null )
        throw new ArgumentNullException( nameof( item ) );
      if ( item is not T typedItem )
        throw new ArgumentException( $"The item must be of type {typeof( T ).FullName}.", nameof( item ) );
      Add( typedItem );
    }

    bool ISubcomponentList.Remove( object item ) => item is T typedItem && Remove( typedItem );

    bool ISubcomponentList.Contains( object item ) => item is T typedItem && Contains( typedItem );
  }
}
