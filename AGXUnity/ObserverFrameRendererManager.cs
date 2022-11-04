using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  public class ObserverFrameRendererManager : UniqueGameObject<ObserverFrameRendererManager>
  {
    public Rendering.ObserverFrameRenderer.DrawMode FrameDrawMode;

    [ClampAboveZeroInInspector]
    public float Size = 1.0f;

    [FloatSliderInInspector(0, 1)]
    public float Alpha = 1.0f;

    public bool RightHanded = false;

    public int LineDivisions = 1;

    protected override bool Initialize()
    {
      foreach ( var GO in FindObjectsOfType<ObserverFrame>() )
        if ( GO.GetComponent<Rendering.ObserverFrameRenderer>() == null ) {
          var renderer = GO.gameObject.AddComponent<Rendering.ObserverFrameRenderer>();
          renderer.FrameDrawMode = FrameDrawMode;
          renderer.Size = Size;
          renderer.Alpha = Alpha;
          renderer.RightHanded = RightHanded;
          renderer.LineDivisions = LineDivisions;
        }

      return true;
    }
  }
}
