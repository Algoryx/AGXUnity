
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Sensor
{
  [System.Serializable]
  public class Illuminator
  {
    public agxSensor.ICameraActiveIllumination Native { get; private set; }

    public enum Type
    {
      Spot,
      Point
    };

    [field: SerializeField]
    public Type IlluminatorType { get; set; } = Type.Spot;

    [field: SerializeField]
    public Color Color { get; set; } = Color.white;

    [field: SerializeField]
    public float Intensity { get; set; } = 10;

    private bool HasAngle => IlluminatorType == Type.Spot;

    [field: SerializeField]
    [DynamicallyShowInInspector( nameof( HasAngle ) )]
    public Vector2 ConeAngles { get; set; } = new Vector2( 20, 25 );

    [field: SerializeField]
    public float FlashDuration { get; set; } = Mathf.Infinity;

    [HideInInspector]
    public CameraSensor Parent { get; private set; }

    [HideInInspector]
    public Light UnityLight { get; internal set; }

    private float LastFlash { get; set; } = float.NegativeInfinity;

    internal void Initialize( CameraSensor parent )
    {
      Parent = parent;
      if ( IlluminatorType == Type.Spot ) {
        Native = new agxSensor.CameraSpotlight(
          new agx.AffineMatrix4x4(),
          Intensity,
          new agx.Vec3( Color.r, Color.g, Color.b ),
          new agx.RangeReal( ConeAngles.x, ConeAngles.y ),
          FlashDuration );
      }
      else if ( IlluminatorType == Type.Point ) {
        Native = new agxSensor.CameraPointLight(
          new agx.AffineMatrix4x4(),
          Intensity,
          new agx.Vec3( Color.r, Color.g, Color.b ),
          FlashDuration );
      }
    }

    public void UpdateLightIntensity()
    {
      if ( float.IsFinite( FlashDuration ) && LastFlash < Time.time ) {
        UnityLight.enabled = false;
      }
    }

    public void Flash()
    {
      var baseIntensity = LightUnitUtils.ConvertIntensity( UnityLight, Intensity, LightUnit.Lumen, LightUnitUtils.GetNativeLightUnit( UnityLight.type ) );

      // Scaling based on flash duration throughout camera frame (t0.1, exponential falloff)
      float flashScale = 1.0f;
      if ( float.IsFinite( FlashDuration ) ) {
        float scaledTime = 2.302585093f * Parent.ShutterSpeed / FlashDuration;
        flashScale = ( 1.0f - Mathf.Exp( -scaledTime ) ) / scaledTime;
      }

      UnityLight.intensity = baseIntensity * flashScale;
      UnityLight.enabled = true;
      LastFlash = Time.time;
    }
  }
}
