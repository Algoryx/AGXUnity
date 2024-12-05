using AGXUnityEditor.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AGXUnityEditor.UIElements;
using System;

namespace AGXUnityEditor.Windows
{
  public class ConvertPhysXToAGXWindow : EditorWindow
  {
    private enum PhysXType
    {
      Collider,
      RigidBody,
      Constraint,
      PhysicsMaterial
    }

    private enum SortType
    {
      Name,
      Type
    }


    // Uses ScriptableObject base class to support undo/redo
    private class AssetData : ScriptableObject
    {
      public bool Updatable;
      public bool Convert;
      public GameObject gameObject;
      public Type type;
      public List<AssetData> dependentAssets;
      public bool rootAsset; // Used by nested RBs
      public PhysXType physXType;
    }

    private class PrefabData : ScriptableObject
    {
      public string Name;
      public string Path;
      public int ConvertibleCount;
      public bool Convert;
      public bool Updatable;

      // Factory method to create an instance of PrefabData
      public static PrefabData CreateInstance( string name, string path, int convertibleCount, bool selected = false, bool updatable = true )
      {
        var instance = ScriptableObject.CreateInstance<PrefabData>();
        instance.Name = name;
        instance.Path = path;
        instance.ConvertibleCount = convertibleCount;
        instance.Convert = selected;
        instance.Updatable = updatable;
        return instance;
      }
    }

    public static ConvertPhysXToAGXWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindow<ConvertPhysXToAGXWindow>( false,
                                                      "Convert Components from PhysX to AGX",
                                                      true );
      window.Repopulate();
      return window;
    }

    private Texture2D[] m_statusIcons;

    private SortType m_sortType = SortType.Name;
    private int m_selectedIndex = -1;
    private int m_hoverIndex = -1;

    private List<VisualElement> m_tableRows = new List<VisualElement>();
    private MixedToggle m_toggleAllSceneObjects;
    private MixedToggle m_toggleAllPrefabObjects;

    private Type[] m_basicColliderTypes = new Type[]{
                                    typeof(BoxCollider),
                                    typeof(SphereCollider),
                                    typeof(CapsuleCollider),
                                    typeof(MeshCollider),
                                    typeof(WheelCollider)};

    private Type[] m_constraintTypes = new Type[]{
                                    typeof(HingeJoint),
                                    typeof(FixedJoint),
                                    typeof(ConfigurableJoint),
                                    typeof(CharacterJoint),
                                    typeof(SpringJoint)};

    private List<AssetData> m_physXAssets;
    private void GatherSceneObjects()
    {
      m_physXAssets = GatherObjects();

      SortAssets();
    }

    private List<AssetData> GatherObjects( GameObject prefabObject = null )
    {
      var convertibleObjects = new List<AssetData>();
      foreach ( var type in m_basicColliderTypes )
        convertibleObjects.AddRange( FindColliderAssetData( type, PhysXType.Collider, prefabObject ) );

      foreach ( var type in m_constraintTypes )
        convertibleObjects.AddRange( FindConstraintAssetData( type, PhysXType.Constraint, prefabObject ) );

#if UNITY_2023_1_OR_NEWER
      bool foundTerrainPager = FindObjectsByType( typeof( AGXUnity.Model.DeformableTerrainPager ), FindObjectsInactive.Include, FindObjectsSortMode.None ).Length > 0;
#else
      bool foundTerrainPager = FindObjectsOfType( typeof( AGXUnity.Model.DeformableTerrainPager ), true ).Length > 0;
#endif

      if (foundTerrainPager)
      {
        Debug.Log("Convert PhysX to AGX Tool: Found at least one Deformable Terrain Pager, assuming Terrain Colliders to be part of that system. If there are separate terrains that should have Terrain Colliders, add manually.");
      }
      else
      {
        var terrains = FindColliderAssetData(typeof(TerrainCollider), PhysXType.Collider);
        for (int i = terrains.Count; --i >= 0;)
        {
          if (terrains[i].gameObject.GetComponent<AGXUnity.Model.DeformableTerrain>() != null)
            terrains.RemoveAt(i);
        }
        convertibleObjects.AddRange(terrains);
      }

      convertibleObjects.AddRange( FindRigidbodyData( prefabObject ) );

      return convertibleObjects;
    }

    private void SortAssets()
    {
      switch ( m_sortType ) {
        case SortType.Name:
          m_physXAssets.Sort( ( n1, n2 ) => n1.gameObject.name.CompareTo( n2.gameObject.name ) );
          break;

        case SortType.Type:
          // This is default
          break;
      }
    }

    private List<PrefabData> m_prefabPhysXAssets;
    private void GatherPrefabData()
    {
      m_prefabPhysXAssets = new List<PrefabData>();

      string[] prefabPaths = AssetDatabase.FindAssets("t:Prefab")
                                        .Select(AssetDatabase.GUIDToAssetPath)
                                        .ToArray();

      foreach ( var prefabPath in prefabPaths ) {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if ( prefab == null )
          continue;

        int count = 0;
        count += prefab.GetComponentsInChildren<Collider>( true ).Length;
        count += prefab.GetComponentsInChildren<Rigidbody>( true ).Length;
        count += prefab.GetComponentsInChildren<Joint>( true ).Length;

        if ( count > 0 ) {
          var prefabData = PrefabData.CreateInstance(prefab.name, prefabPath, count);
          m_prefabPhysXAssets.Add( prefabData );
        }
      }

      // The aptly named Physic Material apparently changed its name to the less Danish-English Physics Material
#if UNITY_6000_0_OR_NEWER
      string[] pmPaths = AssetDatabase.FindAssets("t:PhysicsMaterial")
                                        .Select(AssetDatabase.GUIDToAssetPath)
                                        .ToArray();
      foreach ( var pmPath in pmPaths ) {
        PhysicsMaterial physicMaterial = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);

        var prefabData = PrefabData.CreateInstance(physicMaterial.name, pmPath, 1, updatable: false);
        m_prefabPhysXAssets.Add( prefabData );
      }
#else
      string[] pmPaths = AssetDatabase.FindAssets("t:PhysicMaterial")
                                        .Select(AssetDatabase.GUIDToAssetPath)
                                        .ToArray();
      foreach ( var pmPath in pmPaths ) {
        PhysicsMaterial physicMaterial = AssetDatabase.LoadAssetAtPath<PhysicMaterial>(pmPath);

        var prefabData = PrefabData.CreateInstance(physicMaterial.name, pmPath, 1, updatable: false);
        m_prefabPhysXAssets.Add( prefabData );
      }
#endif

      m_prefabPhysXAssets = m_prefabPhysXAssets.OrderBy( p => p.Name ).ToList();
    }

    private class RBList
    {
      public Rigidbody rigidbody;
      public List<Rigidbody> rbChildren;
      public List<Transform> ownChildren;
      public bool root;
    }

    // Get RBs. We need to keep track of:
    // - Which RBs have children but are not themselves children of RBs
    // - Which colliders belong to this RB (and not to a child RB)
    private List<AssetData> FindRigidbodyData( GameObject prefabObject = null )
    {
      bool sceneMode = prefabObject == null;

      UnityEngine.Object[] components;
      if ( sceneMode ) {
#if UNITY_2023_1_OR_NEWER
        components = FindObjectsByType( typeof( Rigidbody ), FindObjectsInactive.Include, FindObjectsSortMode.None );
#else
        components = FindObjectsOfType(typeof(RigidBody), true);
#endif
      }
      else {
        components = prefabObject.GetComponentsInChildren<Rigidbody>( true );
      }

      var allRBs = components.Select( rb =>
      {
        var entry = new RBList();
        entry.rigidbody = (Rigidbody)rb;
        entry.root = false;
        var go = ((UnityEngine.Component)rb).gameObject;
        entry.rbChildren = go.GetComponentsInChildren<Rigidbody>().ToList();
        entry.rbChildren.Remove(entry.rigidbody);

        entry.ownChildren = go.GetComponentsInChildren<Transform>().ToList();
        foreach (var childRB in entry.rbChildren)
        {
          var grandChildren = childRB.gameObject.GetComponentsInChildren<Transform>();
          foreach (var grandChild in grandChildren)
            entry.ownChildren.Remove(grandChild);
          entry.ownChildren.Remove(childRB.gameObject.transform);
        }

        return entry;
      }).ToList();

      // Find root RBs
      var rbsThatAreRBChildren = new List<Rigidbody>();
      foreach ( var rb in allRBs ) {
        if ( rb.rbChildren.Count > 0 ) {
          rb.root = true;
          rbsThatAreRBChildren.AddRange( rb.rbChildren );
        }
      }
      foreach ( var rb in allRBs ) {
        if ( rbsThatAreRBChildren.Contains( rb.rigidbody ) )
          rb.root = false;
      }

      return allRBs.Select( rb => {
        var data = CreateInstance<AssetData>();
        data.Convert = !sceneMode;
        data.type = typeof( Rigidbody );
        data.physXType = PhysXType.RigidBody;
        data.gameObject = rb.rigidbody.gameObject;
        data.Updatable = true;
        data.rootAsset = rb.root;

        // AssetData for colliders in RB
        data.dependentAssets = FindColliderAssetDataInGameObjects( rb.ownChildren, !sceneMode );

        return data;
      } ).ToList();
    }

    private List<AssetData> FindColliderAssetDataInGameObjects( List<Transform> transforms, bool convert )
    {
      var physXAssets = new List<AssetData>();
      foreach ( var type in m_basicColliderTypes ) {
        List<GameObject> colliders = new List<GameObject>();
        foreach ( var t in transforms ) {
          var components = t.GetComponents(type);
          foreach ( var c in components )
            colliders.Add( c.gameObject );
        }
        physXAssets.AddRange(
          colliders.Select( a => {
            var data = CreateInstance<AssetData>();
            data.Convert = convert;
            data.type = type;
            data.physXType = PhysXType.Collider;
            data.gameObject = a;
            data.Updatable = type != typeof( WheelCollider );

            return data;
          } ).ToList() );
      }

      return physXAssets;
    }

    private List<AssetData> FindColliderAssetData( Type type, PhysXType physXType, GameObject prefabObject = null )
    {
      bool sceneMode = prefabObject == null;

      UnityEngine.Object[] components;
      if ( sceneMode ) {
#if UNITY_2023_1_OR_NEWER
        components = FindObjectsByType( type, FindObjectsInactive.Include, FindObjectsSortMode.None );
#else
        components = FindObjectsOfType(type, true);
#endif
      }
      else {
        components = prefabObject.GetComponentsInChildren( type, true );
      }

      return components.Where( o => ( (Collider)o ).attachedRigidbody == null )
                       .Select( o => {
                         var data = CreateInstance<AssetData>();
                         data.Convert = !sceneMode;
                         data.type = type;
                         data.physXType = physXType;
                         data.gameObject = ( (UnityEngine.Component)o ).gameObject;
                         data.Updatable = type != typeof( WheelCollider );

                         return data;
                       } ).ToList();
    }

    private List<AssetData> FindConstraintAssetData( Type type, PhysXType physXType, GameObject prefabObject = null )
    {
      bool sceneMode = prefabObject == null;

      UnityEngine.Object[] components;
      if ( sceneMode ) {
#if UNITY_2023_1_OR_NEWER
        components = FindObjectsByType( type, FindObjectsInactive.Include, FindObjectsSortMode.None );
#else
        components = FindObjectsOfType( type, true );
#endif
      }
      else {
        components = prefabObject.GetComponentsInChildren( type, true );
      }

      return components.Select( o => {
                         var data = CreateInstance<AssetData>();
                         data.Convert = false;
                         data.type = type;
                         data.physXType = physXType;
                         data.gameObject = ( (UnityEngine.Component)o ).gameObject;
                         data.Updatable = false;

                         return data;
                       } ).ToList();
    }

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

    private void OnEnable()
    {
      GatherSceneObjects();

      m_statusIcons = new Texture2D[ 3 ];
      m_statusIcons[ 0 ] = IconManager.GetIcon( "convertible_material" );
      m_statusIcons[ 1 ] = IconManager.GetIcon( "compatible_material" );
      m_statusIcons[ 2 ] = IconManager.GetIcon( "pass" );

      Undo.undoRedoPerformed += () => Repopulate( false );
      EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    private void OnDisable()
    {
      Undo.undoRedoPerformed -= () => Repopulate( false );
      EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
    }

    private void OnPlayModeStateChanged( PlayModeStateChange state )
    {
      if ( state == PlayModeStateChange.EnteredPlayMode || state == PlayModeStateChange.ExitingPlayMode ) {
        Repaint();
      }

      if ( state == PlayModeStateChange.ExitingPlayMode ) {
        Repopulate(); // Rebuild UI and data when leaving Play mode
      }
    }

    private VisualElement CreateSceneRowUI( AssetData asset )
    {
      var row = new VisualElement();
      row.SetPadding( 3, 3, 3, 0 );
      row.SetEnabled( asset.Updatable );

      var index = m_tableRows.Count();
      row.RegisterCallback<MouseDownEvent>( mde => {
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset.gameObject;
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
      row.style.flexDirection = FlexDirection.Row;

      var toggleConversion = new Toggle() { value = asset.Convert  };
      toggleConversion.RegisterValueChangedCallback( ce => asset.Convert = ce.newValue );
      toggleConversion.style.width = 20;
      toggleConversion.SetMargin( 0, 0, 0, StyleKeyword.Null );
      if ( asset.Updatable )
        m_toggleAllSceneObjects.AddControlledToggle( toggleConversion );

      var flex = new VisualElement();
      flex.style.flexDirection = FlexDirection.Row;
      flex.style.justifyContent = Justify.SpaceBetween;
      flex.style.flexGrow = 1;

      var nameLabel = new Label( asset.gameObject.name );
      nameLabel.style.flexGrow = 0.3f;
      nameLabel.style.flexBasis = 0;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      nameLabel.style.marginRight = 2;

      // TODO this could probably be faster if we saved the image somewhere but then we would have to do a nice lookup
      var TypeIcon = new Image( ) { image = EditorGUIUtility.ObjectContent(null, asset.type).image };
      TypeIcon.style.height = 20;
      TypeIcon.style.width = 20;

      Label componentLabel = null;
      if ( asset.dependentAssets == null )
        componentLabel = new Label( asset.type.ToString() );
      else
        componentLabel = new Label( asset.type.ToString() + " - Colliders: " + asset.dependentAssets.Count );
      componentLabel.style.flexGrow = 0.6f;
      componentLabel.style.flexBasis = 0;
      componentLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      componentLabel.style.marginRight = 2;

      row.Add( toggleConversion );
      flex.Add( nameLabel );
      flex.Add( TypeIcon );
      flex.Add( componentLabel );
      row.Add( flex );

      m_tableRows.Add( row );

      return row;
    }

    private VisualElement CreatePrefabRowUI( PrefabData prefabData )
    {
      int rowIndex = m_tableRows.Count;
      var row = new VisualElement();
      row.style.flexDirection = FlexDirection.Row;
      row.style.justifyContent = Justify.SpaceBetween;
      row.SetPadding( 3, 3, 3, 0 );
      row.SetEnabled( prefabData.Updatable );

      var index = m_tableRows.Count();
      row.RegisterCallback<MouseOverEvent>( _ => {
        m_hoverIndex = index;
        UpdateColors();
      } );
      row.RegisterCallback<MouseOutEvent>( _ => {
        m_hoverIndex = -1;
        UpdateColors();
      } );
      row.RegisterCallback<MouseDownEvent>( _ => {
        m_selectedIndex = index;
        var prefabAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(prefabData.Path);
        if ( prefabAsset != null ) {
          EditorGUIUtility.PingObject( prefabAsset );
        }
      } );

      var toggleConversion = new Toggle { value = prefabData.Convert };
      toggleConversion.style.width = 20;
      toggleConversion.SetMargin( 0, 5, 0, StyleKeyword.Null );
      toggleConversion.RegisterValueChangedCallback( ce => prefabData.Convert = ce.newValue );
      if ( prefabData.Updatable )
        m_toggleAllPrefabObjects.AddControlledToggle( toggleConversion );

      var nameLabel = new Label(prefabData.Name);
      nameLabel.style.flexGrow = 0.3f;
      nameLabel.style.flexBasis = 0;
      nameLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      nameLabel.style.marginRight = 5;

      var pathLabel = new Label(prefabData.Path);
      pathLabel.style.flexGrow = 0.4f;
      pathLabel.style.flexBasis = 0;
      pathLabel.style.overflow = Overflow.Hidden;
      pathLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      pathLabel.style.marginRight = 5;

      var countLabel = new Label($"Components: {prefabData.ConvertibleCount}");
      countLabel.style.flexGrow = 0.2f;
      countLabel.style.flexBasis = 0;
      countLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

      row.Add( toggleConversion );
      row.Add( nameLabel );
      row.Add( pathLabel );
      row.Add( countLabel );

      m_tableRows.Add( row );

      return row;
    }

    private void CreateGUI()
    {
      rootVisualElement.SetPadding( 19, 15, 15, 15 );

      var RPLabel = new Label($"Convenience utility attempting to convert simple PhysX assets to corresponding AGX assets. Intended for colliders and rigid bodies. Refresh the view after the scene changes.");
      RPLabel.style.whiteSpace = WhiteSpace.Normal; // Enables text wrapping
      RPLabel.style.flexGrow = 1; // Ensures the label grows with its container
      RPLabel.style.flexShrink = 1; // Allows it to shrink if needed
      RPLabel.style.overflow = Overflow.Hidden; // Prevents text from overflowing the container
      RPLabel.style.marginBottom = 5;
      rootVisualElement.Add( RPLabel );

      // Actions for both boxes
      var footer = new VisualElement();
      footer.style.flexDirection = FlexDirection.Row;
      footer.style.justifyContent = Justify.SpaceBetween;
      footer.style.marginBottom = 10;
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
      rootVisualElement.Add( footer );


      // Scene Object Converter Box
      var sceneConverter = CreateSceneObjectBox();
      rootVisualElement.Add( sceneConverter );

      // Prefab Converter Box
      var prefabConverter = CreatePrefabObjectBox();
      rootVisualElement.Add( prefabConverter );
    }


    private VisualElement CreateSceneObjectBox()
    {
      var sceneBox = new VisualElement();

      sceneBox.style.height = 300;
      sceneBox.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
      sceneBox.SetBorderRadius( 5 );
      sceneBox.SetPadding( 5 );

      var description = new Label( "PhysX GameObjects in the open main scene. GameObjects with multiple matching Components can appear on multiple lines." );
      description.style.whiteSpace = WhiteSpace.Normal;
      description.style.marginBottom = 10;
      sceneBox.Add( description );

      var header = new VisualElement();
      header.style.flexDirection = FlexDirection.Row;
      header.style.justifyContent = Justify.SpaceBetween;
      header.style.flexShrink = 0;
      m_toggleAllSceneObjects = new MixedToggle();
      header.Add( m_toggleAllSceneObjects );

      var numAssets = new VisualElement();
      numAssets.style.flexDirection = FlexDirection.Row;
      numAssets.style.alignItems = Align.Center;
      numAssets.style.unityTextAlign = TextAnchor.MiddleLeft;
      numAssets.style.flexGrow = 1;
      numAssets.style.justifyContent = Justify.FlexEnd;
      numAssets.Add( new Label() { text = "Upgradable assets: " } );

      var image = new Image() { image = m_statusIcons[ 0 ] };
      image.style.width = 20;
      image.style.height = 20;
      image.style.marginLeft = 5;
      numAssets.Add( image );

      var lab = new Label() { text = $" {(m_physXAssets != null ? m_physXAssets.Where( a => a.Updatable).Count() : 0)}" };
      lab.style.width = 15;
      numAssets.Add( lab );
      header.Add( numAssets );

      // Create table rows
      var scrolledTable = new ScrollView();
      foreach ( var asset in m_physXAssets )
        scrolledTable.Add( CreateSceneRowUI( asset ) );
      UpdateColors();

      sceneBox.Add( header );
      sceneBox.Add( scrolledTable );

      return sceneBox;
    }

    private VisualElement CreatePrefabObjectBox()
    {
      var prefabBox = new VisualElement();
      prefabBox.style.height = 300;
      prefabBox.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
      prefabBox.SetBorderRadius( 5 );
      prefabBox.SetPadding( 5 );

      if ( EditorApplication.isPlayingOrWillChangePlaymode ) {
        var warningLabel = new Label("Prefab conversion not available in Play mode.");
        warningLabel.style.color = Color.red;
        warningLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        warningLabel.style.marginTop = 10;
        warningLabel.style.marginBottom = 10;
        prefabBox.Add( warningLabel );
        return prefabBox; // Return early since prefab conversion is disabled in Play mode
      }

      var description = new Label("Prefabs containing PhysX eligible components. Note: Conversion on prefabs cannot be undone!");
      description.style.whiteSpace = WhiteSpace.Normal;
      description.style.marginBottom = 10;
      prefabBox.Add( description );


      var header = new VisualElement();
      header.style.flexDirection = FlexDirection.Row;
      header.style.justifyContent = Justify.SpaceBetween;
      header.style.flexShrink = 0;
      header.style.overflow = Overflow.Hidden;

      m_toggleAllPrefabObjects = new MixedToggle();
      m_toggleAllPrefabObjects.style.flexGrow = 0;
      m_toggleAllPrefabObjects.style.flexShrink = 0;
      header.Add( m_toggleAllPrefabObjects );

      var numAssets = new VisualElement();
      numAssets.style.flexDirection = FlexDirection.Row;
      numAssets.style.alignItems = Align.Center;
      numAssets.style.unityTextAlign = TextAnchor.MiddleLeft;
      numAssets.style.flexGrow = 1;
      numAssets.style.justifyContent = Justify.FlexEnd;
      numAssets.Add( new Label() { text = "Upgradable prefabs: " } );

      var image = new Image() { image = m_statusIcons[ 0 ] };
      image.style.width = 20;
      image.style.height = 20;
      image.style.marginLeft = 5;
      numAssets.Add( image );

      var label = new Label() { text = $" {(m_prefabPhysXAssets != null ? m_prefabPhysXAssets.Where( a => a.Updatable).Count() : 0)}" };
      label.style.flexGrow = 0; 
      label.style.flexShrink = 0;
      numAssets.Add( label );

      header.Add( numAssets );

      prefabBox.Add( header );

      var scrolledTable = new ScrollView();
      if (m_prefabPhysXAssets != null)
        foreach ( var prefabData in m_prefabPhysXAssets ) // Populate rows with prefabs
          scrolledTable.Add( CreatePrefabRowUI( prefabData ) );

      prefabBox.Add( scrolledTable );
      return prefabBox;
    }

    private void Repopulate( bool gatherAssets = true )
    {
      GatherSceneObjects();
      GatherPrefabData();
      m_tableRows.Clear();

      rootVisualElement.Clear();
      CreateGUI();
    }

    private void ConvertObjects( List<AssetData> assets )
    {
      foreach ( var asset in assets ) {
        if ( !asset.Convert || !asset.Updatable )
          continue;

        if ( asset.physXType == PhysXType.Collider )
          ConvertCollider( asset );

        if ( asset.physXType == PhysXType.RigidBody ) {
          ConvertRigidbody( asset );
          foreach ( var dependentCollider in asset.dependentAssets )
            ConvertCollider( dependentCollider );
        }

        EditorUtility.SetDirty( asset );
      }
    }

    private void ConvertSelected()
    {
      using ( new UndoCollapseBlock( "Convert Selected PhysX Assets" ) ) {
        ConvertObjects( m_physXAssets );
      }

      using ( new UndoCollapseBlock( "Convert Selected Prefab Assets" ) ) {
        foreach ( var prefab in m_prefabPhysXAssets ) {
          if ( !prefab.Convert )
            continue;

          GameObject prefabObject = PrefabUtility.LoadPrefabContents(prefab.Path);
          if ( prefabObject == null )
            continue;

          var objects = GatherObjects(prefabObject);
          try {
            ConvertObjects( objects );

            PrefabUtility.SaveAsPrefabAsset(prefabObject, prefab.Path);
          }
          finally {
            PrefabUtility.UnloadPrefabContents( prefabObject );
          }

          Debug.Log( $"Converted prefab: {prefab.Name}, objects: {objects.Count}" );
        }
      }

      AssetDatabase.SaveAssets();
      Repopulate( false );
    }

    private void ConvertRigidbody( AssetData asset )
    {
      var physXRb = asset.gameObject.GetComponent<Rigidbody>();
      var agxRB = Undo.AddComponent<AGXUnity.RigidBody>(asset.gameObject);

      agxRB.MassProperties.Mass.UseDefault = false;
      agxRB.MassProperties.Mass.Value = physXRb.mass;

#if UNITY_6000_0_OR_NEWER
      var linearVelocityDamping = physXRb.linearDamping;
      var angularVelocityDamping = physXRb.angularDamping;
#else
      var linearVelocityDamping = physXRb.drag;
      var angularVelocityDamping = physXRb.angularDrag;
#endif
      agxRB.LinearVelocityDamping = new Vector3( linearVelocityDamping, linearVelocityDamping, linearVelocityDamping );
      agxRB.AngularVelocityDamping = new Vector3( angularVelocityDamping, angularVelocityDamping, angularVelocityDamping );

      if ( !physXRb.automaticCenterOfMass ) {
        agxRB.MassProperties.CenterOfMassOffset.UseDefault = false;
        agxRB.MassProperties.CenterOfMassOffset.Value = physXRb.centerOfMass;
      }

      if ( !physXRb.automaticInertiaTensor ) {
        agxRB.MassProperties.InertiaDiagonal.UseDefault = false;
        agxRB.MassProperties.InertiaDiagonal.Value = physXRb.centerOfMass;
      }

      if ( physXRb.isKinematic )
        agxRB.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      if ( asset.rootAsset )
        asset.gameObject.AddComponent<AGXUnity.ArticulatedRoot>();

      Undo.DestroyObjectImmediate( physXRb );
    }

    private void ConvertCollider( AssetData asset )
    {
      // We need to create a child gameObject with the AGX collider to account for the "Center" property
      var newObject = new GameObject();

      Undo.RegisterCreatedObjectUndo( newObject, newObject.name );
      newObject.transform.parent = asset.gameObject.transform;

      newObject.transform.localRotation = Quaternion.identity;
      newObject.transform.localScale = Vector3.one; // AGX colliders ignore transform scale, except MeshColliders

      newObject.isStatic = asset.gameObject.isStatic;

      var colliderScale = asset.gameObject.transform.lossyScale;

      switch ( asset.type.ToString() ) // Can't switch on type, workaround with strings
      {
        case "UnityEngine.SphereCollider":
          var physXSphere = asset.gameObject.GetComponent<SphereCollider>();
          var agxSphere = Undo.AddComponent<AGXUnity.Collide.Sphere>(newObject);

          newObject.name = "AGXUnity.Collide.Sphere";
          newObject.transform.localPosition = physXSphere.center;

          // SphereCollider radius is old radius times largest of absolute value of localScale axes
          agxSphere.Radius = physXSphere.radius * Mathf.Max( new float[]{
                                                    Mathf.Abs(colliderScale.x),
                                                    Mathf.Abs(colliderScale.y),
                                                    Mathf.Abs(colliderScale.z)} );

          agxSphere.IsSensor = physXSphere.isTrigger;

          Undo.DestroyObjectImmediate( physXSphere );
          break;

        case "UnityEngine.BoxCollider":
          var physXBox = asset.gameObject.GetComponent<BoxCollider>();
          var agxBox = Undo.AddComponent<AGXUnity.Collide.Box>(newObject);

          newObject.name = "AGXUnity.Collide.Box";
          newObject.transform.localPosition = physXBox.center;

          agxBox.HalfExtents = new Vector3( physXBox.size.x / 2f * Mathf.Abs( colliderScale.x ),
                                           physXBox.size.y / 2f * Mathf.Abs( colliderScale.y ),
                                           physXBox.size.z / 2f * Mathf.Abs( colliderScale.z ) );

          agxBox.IsSensor = physXBox.isTrigger;

          Undo.DestroyObjectImmediate( physXBox );
          break;

        case "UnityEngine.CapsuleCollider":
          var physXCapsule = asset.gameObject.GetComponent<CapsuleCollider>();
          var agxCapsule = Undo.AddComponent<AGXUnity.Collide.Capsule>(newObject);

          newObject.name = "AGXUnity.Collide.Capsule";
          newObject.transform.localPosition = physXCapsule.center;

          // Capsule radius is old radius times abs largest xz-localScale axis. PhysX capsule height is including caps, agx is without
          newObject.transform.localScale = Vector3.one;
          var radius = physXCapsule.radius * Mathf.Max(new float[]{
                                                      Mathf.Abs(colliderScale.x),
                                                      Mathf.Abs(colliderScale.z)});
          agxCapsule.Radius = radius;
          agxCapsule.Height = ( physXCapsule.height - radius * 2 ) * Mathf.Abs( colliderScale.y );

          agxCapsule.IsSensor = physXCapsule.isTrigger;

          Undo.DestroyObjectImmediate( physXCapsule );
          break;

        case "UnityEngine.MeshCollider":
          var physXMesh = asset.gameObject.GetComponent<MeshCollider>();
          var agxMesh = Undo.AddComponent<AGXUnity.Collide.Mesh>(newObject);//

          newObject.name = "AGXUnity.Collide.Mesh";
          newObject.transform.localPosition = Vector3.zero;

          if (physXMesh.sharedMesh.isReadable)
          {
            agxMesh.AddSourceObject( physXMesh.sharedMesh );
          }
          else
          {
            Mesh readableMesh = new Mesh();
            readableMesh.vertices = physXMesh.sharedMesh.vertices;
            readableMesh.triangles = physXMesh.sharedMesh.triangles;
            agxMesh.AddSourceObject( readableMesh );
            
            Undo.RegisterCompleteObjectUndo(readableMesh, "Create Readable Mesh");
          }

          agxMesh.IsSensor = physXMesh.isTrigger;

          Undo.DestroyObjectImmediate( physXMesh );
          break;

        case "UnityEngine.TerrainCollider":
          Undo.DestroyObjectImmediate( newObject );
          var terrain = asset.gameObject.GetComponent<Terrain>();
          if (terrain.leftNeighbor != null || terrain.rightNeighbor != null || 
                terrain.topNeighbor != null || terrain.bottomNeighbor != null)
            Undo.AddComponent<AGXUnity.Model.DeformableTerrainPager>( asset.gameObject );
          else
            Undo.AddComponent<AGXUnity.Model.DeformableTerrain>( asset.gameObject );
          break;
      }
    }
  }
}
