using agx;
using System;
using UnityEngine;

namespace AGXUnity.Model
{
  public interface TrackNodeVariation
  {
    public float GetVariationForNode( float nodeLength, int i );
  }

  [Serializable]
  public class SinusoidalVariation : TrackNodeVariation
  {
    public float Amplitude;
    public float Period;

    public SinusoidalVariation( float amplitude = 0.1f, float period = 1.0f )
    {
      Amplitude = amplitude;
      Period = period;
    }

    public float GetVariationForNode( float nodeLength, int i )
    {
      var dist = nodeLength * (0.5 + i);
      float fraction = (float)(dist/Period);

      return Amplitude * Mathf.Sin( fraction * Mathf.PI );
    }
  }

  [Serializable]
  public class DiscretePulseVariation : TrackNodeVariation
  {
    public float Amplitude;
    public int Period;

    public DiscretePulseVariation( float amplitude = 0.1f, int period = 5 )
    {
      Amplitude = amplitude;
      Period = period;
    }

    public float GetVariationForNode( float nodeLength, int i )
    {
      return i % Period == 0 ? Amplitude : 0.0f;
    }
  }

  public static class VariationUtils
  {
    public static Tuple<Vec3, Vec3> ApplyVariations( TrackNodeVariation widthVar, TrackNodeVariation heightVar, Vec3 halfExtents, int nodeIdx )
    {
      var dh = (heightVar != null ? heightVar.GetVariationForNode( (float)halfExtents.z, nodeIdx ) : 0 );
      halfExtents.x += ( heightVar != null ? heightVar.GetVariationForNode( (float)halfExtents.z, nodeIdx ) : 0 );
      halfExtents.y += ( widthVar  != null ? widthVar.GetVariationForNode( (float)halfExtents.z, nodeIdx ) : 0 );

      var position = new Vec3(-dh, 0.0f, 0.0f);

      return Tuple.Create( halfExtents, position );
    }
  }
}
