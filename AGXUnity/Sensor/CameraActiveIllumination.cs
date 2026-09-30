using AGXUnity.Util;
using UnityEngine;
using UnityEngine.Rendering;

namespace AGXUnity.Sensor
{
  [System.Serializable]
  public class Illuminator : Subcomponent<CameraSensor, agxSensor.ICameraActiveIllumination>
  {
    public enum Type
    {
      Spot,
      Point
    }

    [SerializeField]
    [Tooltip( "The native and Unity light type." )]
    private Type m_illuminatorType = Type.Spot;

    [SerializeField]
    private Color m_color = Color.white;

    [SerializeField]
    [Min( 0.0f )]
    private float m_intensity = 10.0f;

    private bool HasAngles => m_illuminatorType == Type.Spot;

    [SerializeField]
    [Tooltip( "Inner and outer cone angles in degrees. Used by spot illuminators." )]
    [DynamicallyShowInInspector(nameof(HasAngles))]
    private Vector2 m_coneAngles = new Vector2( 100.0f, 120.0f );

    [SerializeField]
    [Min( 0.0f )]
    private float m_flashDuration = Mathf.Infinity;

    public Type IlluminatorType
    {
      get => m_illuminatorType;
      set => PropertyUtil.Assign( ref m_illuminatorType, value, this );
    }

    public Color Color
    {
      get => m_color;
      set => PropertyUtil.Assign( ref m_color, value, this );
    }

    public float Intensity
    {
      get => m_intensity;
      set => PropertyUtil.Assign( ref m_intensity, Mathf.Max( 0.0f, value ), this );
    }

    public Vector2 ConeAngles
    {
      get => m_coneAngles;
      set => PropertyUtil.Assign( ref m_coneAngles, value, this );
    }

    public float FlashDuration
    {
      get => m_flashDuration;
      set => PropertyUtil.Assign( ref m_flashDuration, Mathf.Max( 0.0f, value ), this );
    }

    internal Light UnityLight { get; set; }

    internal bool NativeMatchesConfiguration =>
      Native == null ||
      IlluminatorType == Type.Spot && Native is agxSensor.CameraSpotlight ||
      IlluminatorType == Type.Point && Native is agxSensor.CameraPointLight;

    private float LastFlash { get; set; } = float.NegativeInfinity;

    protected override agxSensor.ICameraActiveIllumination InitializeNative()
    {
      agxSensor.ICameraActiveIllumination native = null;
      if ( IlluminatorType == Type.Spot ) {
        native = new agxSensor.CameraSpotlight(
          new agx.AffineMatrix4x4(),
          Intensity,
          new agx.Vec3( Color.r, Color.g, Color.b ),
          new agx.RangeReal( ConeAngles.x, ConeAngles.y ),
          FlashDuration );
      }
      else {
        native = new agxSensor.CameraPointLight(
          new agx.AffineMatrix4x4(),
          Intensity,
          new agx.Vec3( Color.r, Color.g, Color.b ),
          FlashDuration );
      }

      CameraBackend.Instance.MapLight( native, this );
      SynchronizeConfiguration();
      return native;
    }

    internal void SynchronizeConfiguration()
    {
      if ( Native != null && !NativeMatchesConfiguration ) {
        Parent?.SynchronizeNative();
        return;
      }

      var nativeColor = new agx.Vec3( Color.r, Color.g, Color.b );
      if ( Native is agxSensor.CameraSpotlight spotlight ) {
        spotlight.setColor( nativeColor );
        spotlight.setIntensity( Intensity );
        spotlight.setConeAngle( new agx.RangeReal( ConeAngles.x, ConeAngles.y ) );
        spotlight.setFlashDuration( FlashDuration );
      }
      else if ( Native is agxSensor.CameraPointLight pointLight ) {
        pointLight.setColor( nativeColor );
        pointLight.setIntensity( Intensity );
        pointLight.setFlashDuration( FlashDuration );
      }

      SynchronizeUnityLight();
    }

    protected override void NativeSync()
    {
      SynchronizeConfiguration();
    }

    internal void SynchronizeUnityLight()
    {
      if ( UnityLight == null )
        return;

      UnityLight.type = IlluminatorType == Type.Spot ? LightType.Spot : LightType.Point;
      UnityLight.color = Color;
      UnityLight.range = 100.0f;
      if ( IlluminatorType == Type.Spot ) {
        UnityLight.spotAngle = ConeAngles.y;
        UnityLight.innerSpotAngle = ConeAngles.x;
      }

      UpdateLightIntensity();
    }

    protected override void DisposeNative()
    {
      if ( Native != null )
        CameraBackend.Instance.UnmapLight( Native );
      UnityLight = null;
    }

    public void UpdateLightIntensity()
    {
      if ( UnityLight != null && float.IsFinite( FlashDuration ) && LastFlash < Time.time )
        UnityLight.enabled = false;
    }

    public void Flash()
    {
      if ( Parent == null || UnityLight == null )
        return;

      var baseIntensity = LightUnitUtils.ConvertIntensity( UnityLight,
                                                          Intensity,
                                                          LightUnit.Lumen,
                                                          LightUnitUtils.GetNativeLightUnit( UnityLight.type ) );

      // Scaling based on flash duration throughout camera frame (t0.1, exponential falloff).
      float flashScale = 1.0f;
      if ( float.IsFinite( FlashDuration ) && FlashDuration > 0.0f ) {
        float scaledTime = 2.302585093f * Parent.Photodetector.ShutterSpeed / FlashDuration;
        flashScale = ( 1.0f - Mathf.Exp( -scaledTime ) ) / scaledTime;
      }

      UnityLight.intensity = baseIntensity * flashScale;
      UnityLight.enabled = true;
      LastFlash = Time.time;
    }
  }
}
