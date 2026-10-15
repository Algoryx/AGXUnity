namespace AGXUnity.Rendering
{
  /// <summary>Stable, rendering-only partition of native UInt32 particle identities.</summary>
  public static class SoilParticleSelection
  {
    public static bool IsIndividualParticle( uint id, double fraction )
    {
      unchecked {
        id ^= id >> 16;
        id *= 0x7feb352dU;
        id ^= id >> 15;
        id *= 0x846ca68bU;
        id ^= id >> 16;
      }
      return (double)id / 4294967296.0 < fraction;
    }

    internal static bool Finite( float value ) => !float.IsNaN( value ) && !float.IsInfinity( value );
    internal static float Positive( float value, float previous ) => Finite( value ) && value > 0 ? value : previous;
    internal static float Range( float value, float minimum, float maximum, float previous ) =>
      Finite( value ) && value >= minimum && value <= maximum ? value : previous;
  }
}
