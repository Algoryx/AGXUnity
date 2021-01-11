using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Rendering;
using AGXUnity.Collide;

namespace AGXUnityEditor.Utils
{
  /// <summary>
  /// Specialized class for DrawGizmoCallbackHandler to manage the colors of objects.
  /// </summary>
  public class ObjectsGizmoColorHandler
  {
    public enum SelectionType
    {
      ConstantColor,
      VaryingIntensity
    }

    private struct HSVDeltaData
    {
      public float DeltaHue;
      public float DeltaSaturation;
      public float DeltaValue;
      public float DeltaAlpha;

      public static HSVDeltaData SelectedRigidBody        { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = 0f, DeltaValue = 0.2f, DeltaAlpha = 0.1f }; } }
      public static HSVDeltaData SelectedShape            { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = 0f, DeltaValue = 1f, DeltaAlpha = 0.25f }; } }
      public static HSVDeltaData SelectedMeshFilter       { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = -0.1f, DeltaValue = 0f, DeltaAlpha = 0f }; } }

      public static HSVDeltaData ColorizedMeshFilter      { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = -0.1f, DeltaValue = 0f, DeltaAlpha = 0f }; } }

      public static HSVDeltaData HighlightedRigidBodyMax  { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = 0f, DeltaValue = 0.5f, DeltaAlpha = 0.3f }; } }
      public static HSVDeltaData HighlightedShapeMax      { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = 0f, DeltaValue = 0.5f, DeltaAlpha = 0.3f }; } }
      public static HSVDeltaData HighlightedMeshFilterMax { get { return new HSVDeltaData() { DeltaHue = 0f, DeltaSaturation = 0f, DeltaValue = 0.5f, DeltaAlpha = 0.3f }; } }
    }

    private class RigidBodyColorData
    {
      public class ColorizedMeshFilterData
      {
        public Color Color;
      }

      public Color Color;
      public bool Colorized = false;
      public ColorizedMeshFilterData MeshFiltersData = null;
    }

    private Dictionary<MeshFilter, Color> m_meshColors           = new Dictionary<MeshFilter, Color>();
    private Dictionary<RigidBody, RigidBodyColorData> m_rbColors = new Dictionary<RigidBody, RigidBodyColorData>();
    private UnityEngine.Random.State m_oldRandomState            = default( UnityEngine.Random.State );

    public Color ShapeColor          = new Color( 0.05f, 0.85f, 0.15f, 0.15f );
    public Color MeshFilterColor     = new Color( 0.6f, 0.6f, 0.6f, 0.15f );
    public float RigidBodyColorAlpha = 0.15f;
    public int RandomSeed            = 1024;

    public Dictionary<MeshFilter, Color> ColoredMeshFilters { get { return m_meshColors; } }
    public TimeInterpolator01 TimeInterpolator { get; private set; }

    public ObjectsGizmoColorHandler()
    {
      TimeInterpolator = new TimeInterpolator01( 4f, 2f );
    }

    public Color GetOrCreateColor( RigidBody rb )
    {
      if ( rb == null )
        throw new ArgumentNullException( "rb" );

      return GetOrCreateColorData( rb ).Color;
    }

    public Color Colorize( RigidBody rb )
    {
      if ( rb == null )
        throw new ArgumentNullException( "rb" );

      var colorData = GetOrCreateColorData( rb );
      if ( colorData.Colorized )
        return colorData.Color;

      foreach ( var shape in rb.Shapes ) {
        if ( !shape.IsEnabledInHierarchy )
          continue;

        var shapeFilters = ShapeDebugRenderData.GetMeshFilters( shape );
        foreach ( var shapeFilter in shapeFilters )
          m_meshColors.Add( shapeFilter, colorData.Color );
      }

      colorData.Colorized = true;

      return colorData.Color;
    }

    public Color ColorizeMeshFilters( RigidBody rb )
    {
      if ( rb == null )
        throw new ArgumentNullException( "rb" );

      var colorData = GetOrCreateColorData( rb );
      if ( colorData.MeshFiltersData != null )
        return colorData.MeshFiltersData.Color;

      var filters = rb.GetComponentsInChildren<MeshFilter>();
      Color filterColor = ChangeColorHSVDelta( colorData.Color, HSVDeltaData.ColorizedMeshFilter );

      colorData.MeshFiltersData = new RigidBodyColorData.ColorizedMeshFilterData() { Color = filterColor };

      foreach ( var filter in filters )
        m_meshColors[ filter ] = filterColor;

      return colorData.MeshFiltersData.Color;
    }

    public void Highlight( RigidBody rb, SelectionType selectionType )
    {
      if ( rb == null )
        return;

      Color rbColor = Colorize( rb );
      foreach ( var shape in rb.Shapes ) {
        if ( !shape.IsEnabledInHierarchy )
          continue;

        var shapeFilters = ShapeDebugRenderData.GetMeshFilters( shape );
        foreach ( var shapeFilter in shapeFilters )
          m_meshColors[ shapeFilter ] = selectionType == SelectionType.ConstantColor ?
                                          ChangeColorHSVDelta( rbColor, HSVDeltaData.SelectedRigidBody ) :
                                          TimeInterpolator.Lerp( rbColor, ChangeColorHSVDelta( rbColor, HSVDeltaData.HighlightedRigidBodyMax ) );
      }

      Color filterColor = ColorizeMeshFilters( rb );
      var filters = rb.GetComponentsInChildren<MeshFilter>();
      foreach ( var filter in filters )
        m_meshColors[ filter ] = selectionType == SelectionType.ConstantColor ?
                                   ChangeColorHSVDelta( filterColor, HSVDeltaData.SelectedMeshFilter ) :
                                   TimeInterpolator.Lerp( filterColor, ChangeColorHSVDelta( filterColor, HSVDeltaData.HighlightedRigidBodyMax ) );
    }

    public void Highlight( Shape shape, SelectionType selectionType )
    {
      if ( shape == null )
        return;

      RigidBody rb = shape.GetComponentInParent<RigidBody>();
      Color color = rb != null ? Colorize( rb ) : ShapeColor;

      var shapeFilters = ShapeDebugRenderData.GetMeshFilters( shape );
      foreach ( var shapeFilter in shapeFilters )
        m_meshColors[ shapeFilter ] = selectionType == SelectionType.ConstantColor ?
                                        ChangeColorHSVDelta( color, HSVDeltaData.SelectedShape ) :
                                        TimeInterpolator.Lerp( color, ChangeColorHSVDelta( color, HSVDeltaData.HighlightedShapeMax ) );
    }

    public void Highlight( MeshFilter filter, SelectionType selectionType )
    {
      if ( filter == null )
        return;

      RigidBody rb = filter.GetComponentInParent<RigidBody>();
      Color color;
      if ( rb != null ) {
        Colorize( rb );
        color = ColorizeMeshFilters( rb );
      }
      else
        color = MeshFilterColor;

      m_meshColors[ filter ] = selectionType == SelectionType.ConstantColor ?
                                 ChangeColorHSVDelta( color, HSVDeltaData.SelectedMeshFilter ) :
                                 TimeInterpolator.Lerp( color, ChangeColorHSVDelta( color, HSVDeltaData.HighlightedMeshFilterMax ) );
    }

    public AGXUnity.Utils.DisposableCallback BeginEndScope()
    {
      Begin();
      return new AGXUnity.Utils.DisposableCallback( End );
    }

    public void Begin()
    {
      if ( m_meshColors.Count > 0 || m_rbColors.Count > 0 ) {
        Debug.LogError( "Begin() called more than once before calling End()." );
        return;
      }

      m_oldRandomState = UnityEngine.Random.state;
      UnityEngine.Random.InitState( RandomSeed );
    }

    public void End()
    {
      m_meshColors.Clear();
      m_rbColors.Clear();

      UnityEngine.Random.state = m_oldRandomState;
      m_oldRandomState = default( UnityEngine.Random.State );
    }

    private RigidBodyColorData GetOrCreateColorData( RigidBody rb )
    {
      RigidBodyColorData colorData;
      if ( !m_rbColors.TryGetValue( rb, out colorData ) ) {
        Color color = new Color( UnityEngine.Random.value, UnityEngine.Random.value, UnityEngine.Random.value );

        color.a = RigidBodyColorAlpha;
        colorData = new RigidBodyColorData() { Color = color };

        m_rbColors.Add( rb, colorData );
      }

      return colorData;
    }

    /// <summary>
    /// Change color given delta in hue, saturation and value. All values are clamped between 0 and 1.
    ///   * Hue - change the actual color.
    ///   * Saturation - lowest value and the color is white, highest for clear color.
    ///   * Value - Brightness of the color, i.e., 0 for black and 1 for the actual color.
    /// </summary>
    private Color ChangeColorHSVDelta( Color color, HSVDeltaData data )
    {
      float h, s, v;
      Color.RGBToHSV( color, out h, out s, out v );

      h = Mathf.Clamp01( h + data.DeltaHue );
      // Decreasing saturation to make it more white.
      s = Mathf.Clamp01( s + data.DeltaSaturation );
      // Increasing value to make it more intense.
      v = Mathf.Clamp01( v + data.DeltaValue );

      Color newColor = Color.HSVToRGB( h, s, v );
      newColor.a     = Mathf.Clamp01( color.a + data.DeltaAlpha );
      return newColor;
    }
  }
}
