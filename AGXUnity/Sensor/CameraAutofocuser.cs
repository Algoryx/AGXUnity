using AGXUnity.Util;
using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using static UnityEngine.Rendering.RenderPipeline;

#if HAS_URP
using UnityEngine.Rendering.Universal;
#endif

namespace AGXUnity.Sensor
{
  [Serializable]
  public class CameraAutofocuser : Subcomponent<CameraLens>
  {
    public enum Mode
    {
      Progressive,
      Instant
    }

    [SerializeField]
    public Mode AutofocusMode = Mode.Progressive;

    private RenderTexture Depth { get; set; }
    private ComputeShader DepthSampler;
    private int DepthSamplerKernel;
    private ComputeBuffer DepthSamplerBuffer;
    private Camera m_camera;
    private bool m_disposed = true;
    private float m_targetFocusDistance;
    private float m_autofocusPeriod = 0.1f;
    private float m_lastFocus = float.NegativeInfinity;

    [SerializeField]
    [Min( 0.0f )]
    private float m_minimumFocusDistance = 0.1f;
    public float MinimumFocusDistance
    {
      get => m_minimumFocusDistance;
      set => PropertyUtil.Assign( ref m_minimumFocusDistance, value, this );
    }

    public float FocusDistance { get; private set; }

    internal void Activate( Camera cam, float initialFocusDistance )
    {
      m_camera = cam;
      FocusDistance = initialFocusDistance;

      if ( m_disposed ) {
        var desc = new RenderTextureDescriptor( 256, 256 )
        {
          graphicsFormat = GraphicsFormat.None,
          depthStencilFormat = GraphicsFormat.D32_SFloat,
          msaaSamples = 1
        };
        Depth = new RenderTexture( desc );
        Depth.Create();

        DepthSampler ??= Resources.Load<ComputeShader>( "Shaders/Compute/CameraAutofocusDistance" );
        DepthSamplerKernel = DepthSampler.FindKernel( "SampleDepth" );
        DepthSamplerBuffer ??= new ComputeBuffer( 1, sizeof( float ) );

        DepthSampler.SetBuffer( DepthSamplerKernel, "Result", DepthSamplerBuffer );
        DepthSampler.SetTexture( DepthSamplerKernel, "DepthTexture", Depth, 0, RenderTextureSubElement.Depth );
      }

      m_disposed = false;
    }

    internal void Dispose()
    {
      if ( m_disposed )
        return;

      m_disposed = true;
      Depth?.Release();
      DepthSamplerBuffer?.Dispose();
      DepthSamplerBuffer = null;
    }

    internal void Update()
    {
      if ( m_camera == null || m_disposed )
        return;

      if ( Time.time > m_lastFocus + m_autofocusPeriod ) {

#if HAS_URP
        var urpData = m_camera.GetUniversalAdditionalCameraData();

        bool prePP = urpData.renderPostProcessing;
        bool preShadows = urpData.renderShadows;
        bool preReqColor = urpData.requiresColorTexture;
        bool preReqDepth = urpData.requiresDepthTexture;

        urpData.renderPostProcessing = false;
        urpData.renderShadows = false;
        urpData.requiresColorTexture = false;
        urpData.requiresDepthTexture = false;
#endif

        CameraClearFlags preClear = m_camera.clearFlags;
        DepthTextureMode preDepthMode = m_camera.depthTextureMode;

        m_camera.clearFlags = CameraClearFlags.SolidColor;
        m_camera.depthTextureMode = DepthTextureMode.None;

        var request = new StandardRequest() { destination = Depth };
        m_camera.SubmitRenderRequest( request );

#if HAS_URP
        urpData.renderPostProcessing = prePP;
        urpData.renderShadows = preShadows;
        urpData.requiresColorTexture = preReqColor;
        urpData.requiresDepthTexture = preReqDepth;
#endif

        m_camera.clearFlags = preClear;
        m_camera.depthTextureMode = preDepthMode;

        Vector4 zBufferParams;
        float n = m_camera.nearClipPlane;
        float f = m_camera.farClipPlane;

        if ( SystemInfo.usesReversedZBuffer ) {
          float x = -1.0f + f / n;
          zBufferParams = new Vector4( x, 1.0f, x / f, 1.0f / f );
        }
        else {
          float x = 1.0f - f / n;
          float y = f / n;
          zBufferParams = new Vector4( x, y, x / f, y / f );
        }

        DepthSampler.SetVector( "ZBufferParams", zBufferParams );
        DepthSampler.Dispatch( DepthSamplerKernel, 1, 1, 1 );

        AsyncGPUReadback.Request( DepthSamplerBuffer, req => {
          if ( req.hasError || m_disposed )
            return;
          m_targetFocusDistance = req.GetData<float>()[ 0 ];
        } );
        m_lastFocus = Time.time;
      }

      if ( AutofocusMode == Mode.Instant )
        FocusDistance = Mathf.Max( MinimumFocusDistance, m_targetFocusDistance );
      else if ( AutofocusMode == Mode.Progressive )
        FocusDistance = Mathf.Max( MinimumFocusDistance, Mathf.Lerp( FocusDistance, m_targetFocusDistance, 0.1f ) );
    }

    protected override void NativeSync()
    {
      Parent?.SynchronizeNative();
    }
  }
}
