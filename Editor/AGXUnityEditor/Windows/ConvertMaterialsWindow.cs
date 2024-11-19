using AGXUnity.Utils;
using AGXUnityEditor.UIElements;
using AGXUnityEditor.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Windows
{
  public class ConvertMaterialsWindow : EditorWindow
  {
    private enum MaterialStatus
    {
      Updatable,
      Compatible,
      NotAGXMaterial
    }

    // Uses ScriptableObject base class to support undo/redo
    private class MaterialData : ScriptableObject
    {
      public bool Convert;
      public Material Instance;
      public string Path;
      public MaterialStatus Status;
    }

    public static ConvertMaterialsWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindow<ConvertMaterialsWindow>( false,
                                                      "Convert custom materials",
                                                      true );
      return window;
    }

    private Dictionary<string, string> m_SurfaceToSG = new Dictionary<string, string>();
    private Dictionary<string, string> m_SGToSurface = new Dictionary<string, string>();

    private MaterialData[] m_materials;
    private Texture2D[] m_statusIcons;

    private List<VisualElement> m_tableRows = new List<VisualElement>();
    private MixedToggle m_toggleAll;

    private int m_selectedIndex = -1;
    private int m_hoverIndex = -1;

    // By default, trying to access the shader on a material will return a null shader
    // which references the error shader in the given pipeline. However, sometimes it
    // is useful to know the path of the shader which is being refrenced by the material.
    // One such case is in shader replacement where we need to know the original shader
    // to replace by an equivalent one which functions in the current RP.
    // 
    // This method accesses the serialized material directly and attempts to extract the
    // path to the referenced shader.
    private string GetShaderPath( Material mat )
    {
      // TODO: Add test that this method works properly in various cases.
      var so =  new SerializedObject( mat );
      var sp = so.FindProperty( "m_Shader" );
      sp.Next( true );
      var id = sp.intValue;

      AssetDatabase.TryGetGUIDAndLocalFileIdentifier( id, out string guid, out long _ );
      return AssetDatabase.GUIDToAssetPath( guid ); ;
    }

    private string GetShaderNameFromPath( string shaderPath )
    {
      var noExt = shaderPath.Replace( ".shadergraph", "" ).Replace( ".shader", "" );
      return System.IO.Path.GetRelativePath( IO.Utils.AGXUnityResourceDirectory, noExt );
    }

    private void GatherMaterials()
    {
      var assets = AssetDatabase.FindAssets( "t:Material", null );

      var RP = RenderingUtils.DetectPipeline();

      m_materials = assets.Select( a => {
        var path = AssetDatabase.GUIDToAssetPath( a );
        var mat = AssetDatabase.LoadAssetAtPath<Material>( path );
        var status = MaterialStatus.NotAGXMaterial;

        var shaderPath = GetShaderPath(mat);

        if ( m_SurfaceToSG.ContainsKey( shaderPath ) || m_SGToSurface.ContainsKey( shaderPath ) )
          status = mat.SupportsPipeline( RP ) ? MaterialStatus.Compatible : MaterialStatus.Updatable;

        var md = CreateInstance<MaterialData>();
        md.Convert = status == MaterialStatus.Updatable;
        md.Instance = mat;
        md.Path = path;
        md.Status = status;

        return md;
      } ).ToArray();
    }

    // TODO: Replace hover/select coloring with proper USS styling
    private void UpdateColors()
    {
      for ( int i = 0; i < m_tableRows.Count(); i++ ) {
        Color bgc = InspectorGUI.BackgroundColor;

        if ( i == m_selectedIndex )
          bgc = Color.Lerp( bgc, Color.blue, 0.1f );

        if ( i == m_hoverIndex )
          bgc = Color.Lerp( bgc, Color.white, 0.1f );

        if ( m_hoverIndex != i && m_selectedIndex != i )
          bgc = i % 2 == 0 ? ( bgc * 0.8f ) : bgc;

        m_tableRows[ i ].style.backgroundColor = bgc;
      }
    }

    private VisualElement TableRowUI( MaterialData mat )
    {
      var RP = RenderingUtils.DetectPipeline();

      var row = new VisualElement();
      row.SetPadding( 3, 3, 3, 0 );
      var ve = new VisualElement();

      ve.SetEnabled( mat.Status == MaterialStatus.Updatable );
      var index = m_tableRows.Count();
      row.RegisterCallback<MouseDownEvent>( mde => {
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = mat.Instance;
        m_selectedIndex = index;

        UpdateColors();
      } );
      row.RegisterCallback<MouseOverEvent>( mde => {
        m_hoverIndex = index;
        UpdateColors();
      } );
      row.RegisterCallback<MouseOutEvent>( mde => {
        m_hoverIndex = -1;
        UpdateColors();
      } );
      ve.style.flexDirection = FlexDirection.Row;

      var activeToggle = new Toggle() { value = mat.Convert  };
      activeToggle.RegisterValueChangedCallback( ce => mat.Convert = ce.newValue );
      activeToggle.style.width = 20;
      activeToggle.SetMargin( 0, 0, 0, StyleKeyword.Null );
      if ( mat.Status == MaterialStatus.Updatable )
        m_toggleAll.AddControlledToggle( activeToggle );

      var flex = new VisualElement();
      flex.style.flexDirection = FlexDirection.Row;
      flex.style.justifyContent = Justify.SpaceBetween;
      flex.style.flexGrow = 1;

      var nameLabel = new Label( mat.Instance.name );
      nameLabel.style.flexGrow = 0.3f;
      nameLabel.style.flexBasis = 0;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      nameLabel.style.marginRight = 2;

      var pathLabel = new Label( mat.Path );
      pathLabel.style.flexGrow = 0.6f;
      pathLabel.style.flexBasis = 0;
      pathLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      pathLabel.style.marginRight = 2;

      var StatusIcon = new Image( ) { image = m_statusIcons[ (int)mat.Status ] };
      StatusIcon.style.height = 20;
      StatusIcon.style.width = 20;

      ve.Add( activeToggle );
      flex.Add( nameLabel );
      flex.Add( pathLabel );
      flex.Add( StatusIcon );
      ve.Add( flex );
      row.Add( ve );

      m_tableRows.Add( row );

      return row;
    }

    private void OnEnable()
    {
      m_SGToSurface.Clear();
      m_SurfaceToSG.Clear();

      var shaderPath = IO.Utils.AGXUnityResourceDirectory + "/Shaders/";

      // Add convertable shaders to dictionary
      m_SurfaceToSG.Add( shaderPath + "Built-In/CableAndWire.shader", shaderPath + "Shader Graph/Cable and Wire.shadergraph" );
      m_SurfaceToSG.Add( shaderPath + "Built-In/UpsampledParticle.shader", shaderPath + "Shader Graph/Upsampled Particle.shadergraph" );

      // Add reverse mapping 
      foreach ( var (k, v) in m_SurfaceToSG )
        m_SGToSurface[ v ] = k;

      // Verify that each of the listed files actually exists
      foreach ( var shader in m_SGToSurface.Keys.Union( m_SurfaceToSG.Keys ) ) {
        var fullPath = System.IO.Path.GetFullPath(shader);
        Debug.Assert( System.IO.File.Exists( fullPath ) );
      }

      // TODO: Replace icons with custom icons
      m_statusIcons = new Texture2D[ 3 ];
      m_statusIcons[ (int)MaterialStatus.Updatable ]      = IconManager.GetIcon( "convertible_material" );
      m_statusIcons[ (int)MaterialStatus.Compatible ]     = IconManager.GetIcon( "compatible_material" );
      m_statusIcons[ (int)MaterialStatus.NotAGXMaterial ] = IconManager.GetIcon( "pass" );

      GatherMaterials();

      Undo.undoRedoPerformed += () => Repopulate( false );
    }

    private void CreateGUI()
    {
      rootVisualElement.SetPadding( 19, 15, 15, 15 );

      var RP = RenderingUtils.DetectPipeline();
      var RPLabel = new Label( $"Current Render Pipeline: <b>{RP}</b>" );
      RPLabel.style.marginBottom = 15;
      rootVisualElement.Add( RPLabel );

      var assetConverter = new VisualElement();
      assetConverter.style.height = 600;
      assetConverter.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
      assetConverter.SetBorderRadius( 5 );
      assetConverter.SetPadding( 5 );

      var description = new Label( "Converts material assets which use custom AGXUnity shaders between rendering pipelines." );
      description.style.whiteSpace = WhiteSpace.Normal;
      description.style.marginBottom = 10;
      assetConverter.Add( description );

      var header = new VisualElement();
      header.style.flexDirection = FlexDirection.Row;
      header.style.justifyContent = Justify.SpaceBetween;
      header.style.flexShrink = 0;
      m_toggleAll = new MixedToggle();
      header.Add( m_toggleAll );

      var numMats = new VisualElement();
      numMats.style.flexDirection = FlexDirection.Row;
      numMats.style.alignItems = Align.Center;
      numMats.style.unityTextAlign = TextAnchor.MiddleLeft;
      foreach ( var obj in Enum.GetValues( typeof( MaterialStatus ) ) ) {
        var status = (MaterialStatus)obj;
        var image = new Image() { image = m_statusIcons[ (int)status ] };
        image.style.width = 20;
        image.style.height = 20;

        numMats.Add( image );
        var lab = new Label() { text = $"{m_materials.Where( m => m.Status == status ).Count()}" };
        lab.style.width = 15;
        numMats.Add( lab );
      }
      header.Add( numMats );

      var scroll = new ScrollView();

      foreach ( var mat in m_materials )
        scroll.Add( TableRowUI( mat ) );

      UpdateColors();

      assetConverter.Add( header );
      assetConverter.Add( scroll );

      var footer = new VisualElement();
      footer.style.flexDirection = FlexDirection.Row;
      footer.style.justifyContent = Justify.SpaceBetween;
      footer.style.marginTop = 10;
      footer.style.flexShrink = 0;

      var convertButton = new Button() { text = "Convert Selected" };
      convertButton.clicked += ConvertSelected;

      var refreshButton = new Button();
      refreshButton.style.width = 40;
      refreshButton.style.marginLeft = 0;
      refreshButton.clicked += () => Repopulate();

      var refreshIcon =  new Image { image = IconManager.GetIcon( MiscIcon.Update ) };
      refreshIcon.style.flexBasis = 0;
      refreshIcon.style.flexGrow = 1;
      refreshButton.Add( refreshIcon );

      footer.Add( refreshButton );
      footer.Add( convertButton );
      assetConverter.Add( footer );

      rootVisualElement.Add( assetConverter );
    }

    private void Repopulate( bool gatherAssets = true )
    {
      if ( gatherAssets )
        GatherMaterials();
      m_tableRows.Clear();
      rootVisualElement.Clear();
      CreateGUI();
    }

    private void ConvertSelected()
    {
      using ( new UndoCollapseBlock( "Convert AGXUnity Materials" ) ) {
        foreach ( var mat in m_materials.Where( m => m.Status == MaterialStatus.Updatable ) ) {
          if ( !mat.Convert )
            continue;

          Undo.RecordObject( mat, mat.Path + "_Data" );
          Undo.RecordObject( mat.Instance, mat.Path );

          var shaderPath = GetShaderPath(mat.Instance);

          var repShader = "";
          if ( m_SurfaceToSG.ContainsKey( shaderPath ) )
            repShader = m_SurfaceToSG[ shaderPath ];
          else if ( m_SGToSurface.ContainsKey( shaderPath ) )
            repShader = m_SGToSurface[ shaderPath ];
          else
            Debug.LogError( $"Missing replacement shader for {shaderPath}" );

          Debug.Log( $"{mat.Instance.name}: Replace {shaderPath} with {repShader}" );
          var newShader = Resources.Load<Shader>( GetShaderNameFromPath(repShader) );
          mat.Instance.shader = newShader;

          mat.Status = MaterialStatus.Compatible;
          mat.Convert = false;

          EditorUtility.SetDirty( mat.Instance );
          EditorUtility.SetDirty( mat );
        }
      }

      AssetDatabase.SaveAssets();
      Repopulate( false );
    }
  }
}
