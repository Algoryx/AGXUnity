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
  /// <summary>Selection and reporting UI; conversion policy lives in PhysXConversion.</summary>
  public class ConvertPhysXToAGXWindow : EditorWindow
  {
    [SerializeField] private List<Component> m_selectedSources = new List<Component>();
    [SerializeField] private List<string> m_selectedPrefabs = new List<string>();
    [SerializeField] private string m_report = "";
    [SerializeField] private string m_sort = "Name";
    private List<PhysXConversion.Candidate> m_candidates = new List<PhysXConversion.Candidate>();
    private List<PhysXConversion.PrefabInfo> m_prefabs = new List<PhysXConversion.PrefabInfo>();
    private bool m_sceneDirty;
    private bool m_converting;
    private Button m_convertButton;
    private TextField m_reportField;
    private readonly Dictionary<Type, Texture> m_icons = new Dictionary<Type, Texture>();

    public static ConvertPhysXToAGXWindow Open()
    {
      var window = GetWindow<ConvertPhysXToAGXWindow>( false, "Convert PhysX to AGX", true );
      window.minSize = new Vector2( 600, 500 );
      return window;
    }

    private void OnEnable()
    {
      Undo.undoRedoPerformed += OnUndoRedo;
      EditorApplication.hierarchyChanged += OnHierarchyChanged;
      EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    private void OnDisable()
    {
      Undo.undoRedoPerformed -= OnUndoRedo;
      EditorApplication.hierarchyChanged -= OnHierarchyChanged;
      EditorApplication.playModeStateChanged -= OnPlayModeChanged;
    }

    private void OnUndoRedo() => m_sceneDirty = true;
    private void OnHierarchyChanged() => m_sceneDirty = true;
    private void OnPlayModeChanged( PlayModeStateChange state ) => Refresh( false );

    private void Update()
    {
      if ( m_sceneDirty && !m_converting )
        Refresh( false );
    }

    private void CreateGUI() => Refresh( true );

    private void Refresh( bool prefabs )
    {
      m_sceneDirty = false;
      m_candidates = PhysXConversion.Discover( PhysXConversion.SceneRoots() );
      if ( prefabs )
        m_prefabs = PhysXConversion.DiscoverPrefabs();
      // Preserve identities during refresh, including Undo/Redo. Stale selections
      // cannot execute: every conversion is preflighted against fresh discovery.
      Draw();
    }

    private void Draw()
    {
      rootVisualElement.Clear();
      rootVisualElement.style.paddingLeft = 12;
      rootVisualElement.style.paddingRight = 12;
      rootVisualElement.style.paddingTop = 12;
      rootVisualElement.style.paddingBottom = 12;
      var separator = new VisualElement();
      separator.style.height = 2;
      separator.style.flexShrink = 0;
      separator.style.backgroundColor = InspectorGUISkin.BrandColor;
      separator.style.marginBottom = 8;
      rootVisualElement.Add( separator );

      var instructions = new HelpBox(
        "Convert colliders and rigidbodies in loaded scenes or prefabs. Nested rigidbodies are converted together. " +
        "Terrain conversion creates DeformableTerrain. Unity layers are copied, but collision filtering, physics callbacks " +
        "and script references require migration. Materials, joints and wheels are not converted.", HelpBoxMessageType.Info );
      // Keep the explanatory text at its measured height. Only the lists should
      // shrink when the window is resized, otherwise HelpBox text escapes its border.
      instructions.style.flexShrink = 0;
      var instructionLabel = instructions.Q<Label>();
      instructionLabel.style.whiteSpace = WhiteSpace.Normal;
      instructionLabel.style.minWidth = 0;
      rootVisualElement.Add( instructions );

      var actions = new VisualElement();
      actions.style.flexDirection = FlexDirection.Row;
      actions.style.alignItems = Align.Center;
      actions.style.flexShrink = 0;
      actions.style.marginTop = 8;
      actions.style.marginBottom = 8;
      actions.Add( new Button( () => Refresh( true ) ) { text = "Refresh" } );
      var sorting = new PopupField<string>( "Sort", new List<string> { "Name", "Type" }, m_sort );
      sorting.style.flexGrow = 1;
      sorting.style.minWidth = 100;
      sorting.labelElement.style.minWidth = 0;
      sorting.labelElement.style.width = 32;
      sorting.RegisterValueChangedCallback( e => { m_sort = e.newValue; Draw(); } );
      actions.Add( sorting );
      actions.Add( new Button( Preview ) { text = "Preview Selected" } );
      m_convertButton = new Button( ConvertSelected ) { text = "Convert Selected" };
      actions.Add( m_convertButton );
      rootVisualElement.Add( actions );

      bool editMode = !EditorApplication.isPlayingOrWillChangePlaymode;
      if ( !editMode ) {
        var warning = new HelpBox( "Conversion is only available in Edit mode.", HelpBoxMessageType.Warning );
        warning.style.flexShrink = 0;
        rootVisualElement.Add( warning );
      }

      var sceneHeader = Header( $"Loaded scenes — {m_candidates.Count( c => c.Supported )} eligible entries", () => {
        var eligible = m_candidates.Where( c => c.Supported ).Select( c => c.Source ).ToArray();
        SelectAll( m_selectedSources, eligible );
        Draw();
      } );
      sceneHeader.SetEnabled( editMode );
      var sceneItems = ( m_sort == "Type" ? m_candidates.OrderBy( c => c.Description ).ThenBy( c => c.Source.gameObject.name ) :
                                           m_candidates.OrderBy( c => c.Source.gameObject.name ).ThenBy( c => c.Description ) ).ToList();
      var sceneList = CreateList( sceneItems, 26, ( row, index ) => BindSceneRow( row, sceneItems[ index ] ) );
      sceneList.SetEnabled( editMode );
      rootVisualElement.Add( Section( sceneHeader, sceneList ) );

      var prefabHeader = Header( $"Prefabs — {m_prefabs.Count} containing PhysX components", () => {
        SelectAll( m_selectedPrefabs, m_prefabs.Where( p => p.Error == null && p.SupportedCount > 0 )
                                             .Select( p => AssetDatabase.AssetPathToGUID( p.Path ) ).ToArray() );
        Draw();
      } );
      prefabHeader.SetEnabled( editMode );
      var prefabList = CreateList( m_prefabs, 42, ( row, index ) => BindPrefabRow( row, m_prefabs[ index ] ) );
      prefabList.SetEnabled( editMode );
      rootVisualElement.Add( Section( prefabHeader, prefabList ) );

      m_reportField = new TextField( "Preview / results" ) { multiline = true, isReadOnly = true, value = m_report };
      m_reportField.style.height = 110;
      m_reportField.style.flexShrink = 0;
      rootVisualElement.Add( m_reportField );
      UpdateConvertButton();
    }

    private static ListView CreateList( System.Collections.IList items, int rowHeight, Action<VisualElement, int> bind )
    {
      var list = new ListView( items, rowHeight, () => new VisualElement(), bind );
      list.selectionType = SelectionType.None;
      list.showAlternatingRowBackgrounds = AlternatingRowBackground.ContentOnly;
      list.style.flexGrow = 1;
      list.style.flexBasis = 0;
      list.style.minHeight = 0;
      return list;
    }

    private static VisualElement Section( VisualElement header, ListView list )
    {
      var section = new VisualElement();
      section.style.flexGrow = 1;
      section.style.flexBasis = 0;
      section.style.minHeight = 80;
      section.style.marginBottom = 8;
      section.style.overflow = Overflow.Hidden;
      section.SetBorder( 1, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.4f ) );
      section.SetBorderRadius( 3 );
      section.Add( header );
      section.Add( list );
      return section;
    }

    private static VisualElement Header( string text, Action selectAll )
    {
      var header = new VisualElement();
      header.style.flexDirection = FlexDirection.Row;
      header.style.alignItems = Align.Center;
      header.style.flexShrink = 0;
      header.SetPadding( 3, 4, 3, 6 );
      var label = new Label( text );
      label.style.flexGrow = 1;
      label.style.minWidth = 0;
      label.style.whiteSpace = WhiteSpace.Normal;
      header.Add( label );
      header.Add( new Button( selectAll ) { text = "Select / clear eligible" } );
      return header;
    }

    private static void SelectAll<T>( List<T> selection, T[] eligible )
    {
      bool clear = eligible.All( selection.Contains );
      foreach ( var id in eligible ) {
        selection.Remove( id );
        if ( !clear )
          selection.Add( id );
      }
    }

    private void BindSceneRow( VisualElement row, PhysXConversion.Candidate candidate )
    {
      row.Clear();
      row.SetPadding( 1, 6, 1, 6 );
      row.style.overflow = Overflow.Hidden;
      var source = candidate.Source;
      var line = new VisualElement();
      line.style.flexDirection = FlexDirection.Row;
      line.style.alignItems = Align.Center;
      line.style.height = 24;
      line.style.flexShrink = 0;
      var toggle = new Toggle { value = m_selectedSources.Contains( source ) };
      toggle.SetEnabled( candidate.Supported || toggle.value );
      toggle.RegisterValueChangedCallback( e => {
        m_selectedSources.Remove( source );
        if ( e.newValue )
          m_selectedSources.Add( source );
        toggle.SetEnabled( candidate.Supported || e.newValue );
        UpdateConvertButton();
      } );
      toggle.SetMargin( 0 );
      toggle.style.width = 20;
      toggle.style.flexShrink = 0;
      line.Add( toggle );
      if ( !m_icons.TryGetValue( source.GetType(), out var icon ) ) {
        icon = EditorGUIUtility.ObjectContent( null, source.GetType() ).image;
        m_icons[ source.GetType() ] = icon;
      }
      var image = new Image { image = icon };
      image.style.width = 16;
      image.style.height = 16;
      image.style.flexShrink = 0;
      image.style.marginRight = 4;
      line.Add( image );
      var objectButton = new Button( () => { Selection.activeObject = source; EditorGUIUtility.PingObject( source ); } )
      {
        text = source.gameObject.name + " — " + candidate.Description
      };
      CompactObjectButton( objectButton );
      line.Add( objectButton );
      var details = string.Join( " ", candidate.Errors.Concat( candidate.Warnings ).Distinct() );
      row.tooltip = details;
      if ( !string.IsNullOrEmpty( details ) ) {
        var status = new Label( candidate.Supported ? "Warnings" : "Unsupported" ) { tooltip = details };
        status.style.marginLeft = 6;
        status.style.flexShrink = 0;
        status.style.color = candidate.Supported ? InspectorGUISkin.BrandColor : new Color( 0.9f, 0.4f, 0.3f );
        line.Add( status );
      }
      row.Add( line );
    }

    private void BindPrefabRow( VisualElement row, PhysXConversion.PrefabInfo prefab )
    {
      row.Clear();
      row.SetPadding( 1, 6, 1, 6 );
      row.style.overflow = Overflow.Hidden;
      string guid = AssetDatabase.AssetPathToGUID( prefab.Path );
      var line = new VisualElement();
      line.style.flexDirection = FlexDirection.Row;
      line.style.alignItems = Align.Center;
      line.style.height = 24;
      line.style.flexShrink = 0;
      var toggle = new Toggle { value = m_selectedPrefabs.Contains( guid ) };
      toggle.SetEnabled( ( prefab.Error == null && prefab.SupportedCount > 0 ) || toggle.value );
      toggle.RegisterValueChangedCallback( e => {
        m_selectedPrefabs.Remove( guid );
        if ( e.newValue )
          m_selectedPrefabs.Add( guid );
        toggle.SetEnabled( ( prefab.Error == null && prefab.SupportedCount > 0 ) || e.newValue );
        UpdateConvertButton();
      } );
      toggle.SetMargin( 0 );
      toggle.style.width = 20;
      toggle.style.flexShrink = 0;
      line.Add( toggle );
      var prefabButton = new Button( () => EditorGUIUtility.PingObject( AssetDatabase.LoadAssetAtPath<GameObject>( prefab.Path ) ) )
      {
        text = $"{prefab.Name} — {prefab.SupportedCount} eligible / {prefab.UnsupportedCount} unsupported components"
      };
      CompactObjectButton( prefabButton );
      line.Add( prefabButton );
      row.Add( line );
      AddDetails( row, prefab.Error ?? prefab.Path, prefab.Error != null );
    }

    private static void CompactObjectButton( Button button )
    {
      button.style.flexGrow = 1;
      button.style.minWidth = 0;
      button.style.height = 22;
      button.SetMargin( 0 );
      button.style.unityTextAlign = TextAnchor.MiddleLeft;
      button.style.overflow = Overflow.Hidden;
      button.style.textOverflow = TextOverflow.Ellipsis;
    }

    private static void AddDetails( VisualElement row, string text, bool error )
    {
      var label = new Label( text ) { tooltip = text };
      label.style.whiteSpace = WhiteSpace.NoWrap;
      label.style.overflow = Overflow.Hidden;
      label.style.textOverflow = TextOverflow.Ellipsis;
      label.style.height = 14;
      label.style.flexShrink = 0;
      label.SetMargin( 0, 0, 0, 20 );
      label.style.fontSize = 11;
      if ( error )
        label.style.color = new Color( 0.9f, 0.4f, 0.3f );
      row.Add( label );
    }

    private Component[] SelectedSources() => m_candidates.Where( c => c.Source != null && m_selectedSources.Contains( c.Source ) )
                                                         .Select( c => c.Source ).ToArray();

    private string[] SelectedPrefabPaths() => m_selectedPrefabs.Select( AssetDatabase.GUIDToAssetPath ).Where( p => !string.IsNullOrEmpty( p ) ).ToArray();

    private void UpdateConvertButton()
    {
      m_convertButton?.SetEnabled( !EditorApplication.isPlayingOrWillChangePlaymode &&
                                   ( SelectedSources().Length > 0 || SelectedPrefabPaths().Length > 0 ) );
    }

    private List<string> Preflight( out List<string> errors )
    {
      errors = new List<string>();
      var lines = new List<string>();
      var selected = SelectedSources();
      if ( selected.Length > 0 ) {
        var plan = PhysXConversion.BuildPlan( PhysXConversion.Discover( PhysXConversion.SceneRoots() ), selected );
        errors.AddRange( plan.Errors );
        lines.Add( $"Scenes: {plan.ComponentCount} components, including body dependencies. Undo is available." );
        lines.AddRange( plan.Warnings.Distinct() );
      }
      foreach ( var path in SelectedPrefabPaths() ) {
        var root = AssetDatabase.LoadAssetAtPath<GameObject>( path );
        if ( root == null || PrefabUtility.IsPartOfImmutablePrefab( root ) || !path.StartsWith( "Assets/", StringComparison.Ordinal ) ) {
          errors.Add( path + ": Prefab is missing, immutable, or outside Assets." );
          continue;
        }
        var candidates = PhysXConversion.Discover( new[] { root } );
        var plan = PhysXConversion.BuildPlan( candidates, candidates.Where( c => c.Supported ).Select( c => c.Source ) );
        errors.AddRange( plan.Errors.Select( e => path + ": " + e ) );
        lines.Add( $"{path}: {plan.ComponentCount} components; {candidates.Where( c => !c.Supported ).Sum( c => c.ComponentCount )} unsupported components skipped." );
        lines.AddRange( plan.Warnings.Distinct() );
        lines.AddRange( candidates.Where( c => !c.Supported ).SelectMany( c => c.Errors ).Distinct().Select( e => "Skipped: " + e ) );
      }
      if ( SelectedPrefabPaths().Length > 0 )
        lines.Add( "Prefabs are saved directly and cannot be undone. Nested prefabs and variants are changed through overrides." );
      lines.Add( "Unity collision filtering, physics callbacks and script references require migration." );
      return lines;
    }

    private void Preview()
    {
      var lines = Preflight( out var errors );
      SetReport( string.Join( "\n", errors.Select( e => "Blocked: " + e ).Concat( lines ) ) );
    }

    private void SetReport( string report )
    {
      m_report = report;
      m_reportField?.SetValueWithoutNotify( report );
    }

    private void ConvertSelected()
    {
      if ( EditorApplication.isPlayingOrWillChangePlaymode )
        return;
      var preview = Preflight( out var errors );
      SetReport( string.Join( "\n", errors.Select( e => "Blocked: " + e ).Concat( preview ) ) );
      if ( errors.Count > 0 )
        return;
      if ( !EditorUtility.DisplayDialog( "Convert selected PhysX components", m_report, "Convert", "Cancel" ) )
        return;
      m_converting = true;
      var results = new List<string>();
      try {
        var selected = SelectedSources();
        if ( selected.Length > 0 )
          results.Add( "Scenes: " + PhysXConversion.ConvertScene( selected ) );
        var paths = SelectedPrefabPaths();
        for ( int i = 0; i < paths.Length; ++i ) {
          if ( EditorUtility.DisplayCancelableProgressBar( "Convert PhysX prefabs", paths[ i ], (float)i / paths.Length ) ) {
            results.Add( "Cancelled remaining prefabs. Completed conversions have been saved." );
            break;
          }
          results.Add( paths[ i ] + ": " + PhysXConversion.ConvertPrefab( paths[ i ] ) );
        }
        SetReport( string.Join( "\n", results ) );
        Debug.Log( "PhysX to AGX conversion\n" + m_report );
      }
      catch ( Exception exception ) {
        SetReport( string.Join( "\n", results ) + "\nConversion failed: " + exception.Message );
        Debug.LogException( exception );
      }
      finally {
        EditorUtility.ClearProgressBar();
        m_converting = false;
        Refresh( true );
      }
    }
  }
}
