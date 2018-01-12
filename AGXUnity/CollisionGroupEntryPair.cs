using System;

namespace AGXUnity
{
  /// <summary>
  /// Object containing two collision group entries that are
  /// disabled against each other.
  /// </summary>
  [Serializable]
  public class CollisionGroupEntryPair
  {
    public CollisionGroupEntry First = new CollisionGroupEntry();
    public CollisionGroupEntry Second = new CollisionGroupEntry();
  }
}
