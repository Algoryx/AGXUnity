using System.ComponentModel;
using UnityEngine;

namespace AGXUnity.Rendering
{
  /// <summary>
  /// At paly, this manager adds ObserverFrameRenderer components to every GameObject with an ObserverFrame 
  /// component that does not already have a renderer attached.
  /// </summary>
  [AddComponentMenu( "" )]
  public class ObserverFrameRendererManager : UniqueGameObject<ObserverFrameRendererManager>
  {

    // Mimic settings in ObserverFrameRenderer component
    [Description("Whether to use Unity's gizmos which are pickable and stripped out of builds or " +
                 "lines which are not pickable and are included in builds")]
    public ObserverFrameRenderer.DrawMode FrameDrawMode;

    [ClampAboveZeroInInspector]
    public float Size = 1.0f;

    [FloatSliderInInspector(0, 1)]
    public float Alpha = 1.0f;

    public bool RightHanded = false;

    public int LineDivisions = 1;

    protected override bool Initialize()
    {
      // Loop over each GameObject with an ObserverFrame and add Renderer if it does not already have one
      foreach ( var GO in FindObjectsOfType<ObserverFrame>() )
        if ( GO.GetComponent<ObserverFrameRenderer>() == null ) {
          var renderer = GO.gameObject.AddComponent<ObserverFrameRenderer>();

          // Copy options to renderer
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
