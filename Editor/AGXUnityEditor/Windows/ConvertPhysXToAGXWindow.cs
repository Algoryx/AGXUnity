using AGXUnity.Utils;
using AGXUnityEditor.Utils;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using AGXUnityEditor.UIElements;
using System;
using agxPowerLine;
using AGXUnity.Model;
using UnityEditor.VersionControl;
using agx;
using System.Collections;

namespace AGXUnityEditor.Windows
{
  public class ConvertPhysXToAGXWindow : EditorWindow
  {
    private enum PhysXType
    {
      Collider,
      RigidBody,
      Constraint
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
      //public string Path;
      public Type type;
      public List<AssetData> dependentAssets;
      public bool rootAsset; // Used by nested RBs
      public PhysXType physXType;
    }

    public static ConvertPhysXToAGXWindow Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindow<ConvertPhysXToAGXWindow>( false,
                                                      "Convert Components from PhysX to AGX",
                                                      true );
      return window;
    }

    private Texture2D[] m_statusIcons;

    private SortType m_sortType = SortType.Name;
    private int m_selectedIndex = -1;
    private int m_hoverIndex = -1;

    private List<VisualElement> m_tableRows = new List<VisualElement>();
    private MixedToggle m_toggleAll;

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
      //var assets = AssetDatabase.FindAssets( "t:Material", null );
      m_physXAssets = new List<AssetData>();
      foreach (var type in m_basicColliderTypes)
        m_physXAssets.AddRange(FindColliderAssetData(type, PhysXType.Collider));

      var terrains = FindColliderAssetData(typeof(TerrainCollider), PhysXType.Collider);
      for (int i = terrains.Count; --i >= 0;)
      {
        if (terrains[i].gameObject.GetComponent<DeformableTerrain>() != null)
          terrains.RemoveAt(i);
      }
      m_physXAssets.AddRange(terrains);

      m_physXAssets.AddRange(FindRigidbodyData());

      //m_physXObjects.AddRange(FindAssetData(typeof(Constraint)));

      SortAssets();
    }

    private void SortAssets()
    {
      switch (m_sortType)
      {
        case SortType.Name: 
          m_physXAssets.Sort( ( n1, n2 ) =>  n1.gameObject.name.CompareTo(n2.gameObject.name ));
          break;
        
        case SortType.Type:
          // This is default
          break;
      }
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
    private List<AssetData> FindRigidbodyData()
    {
      var components = FindObjectsOfType(typeof(Rigidbody), true);

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
      foreach (var rb in allRBs)
      {
        if (rb.rbChildren.Count > 0)
        {
          rb.root = true;
          rbsThatAreRBChildren.AddRange(rb.rbChildren);
        }
      }
      foreach (var rb in allRBs)
      {
        if (rbsThatAreRBChildren.Contains(rb.rigidbody))
          rb.root = false;
      }

      return allRBs.Select( rb =>
      {
        var data = CreateInstance<AssetData>();
        data.Convert = false;
        data.type = typeof(Rigidbody);
        data.physXType = PhysXType.RigidBody;
        data.gameObject = rb.rigidbody.gameObject;
        data.Updatable = true;
        data.rootAsset = rb.root;

        // AssetData for colliders in RB
        data.dependentAssets = FindColliderAssetDataInGameObjects(rb.ownChildren);

        return data;
      } ).ToList();
    }

    private List<AssetData> FindColliderAssetDataInGameObjects(List<Transform> transforms)
    {
      var physXAssets = new List<AssetData>();
      foreach (var type in m_basicColliderTypes)
      {
        List<GameObject> colliders = new List<GameObject>();
        foreach (var t in transforms)
        {
          var components = t.GetComponents(type);
          foreach (var c in components)
            colliders.Add(c.gameObject);
        }
        physXAssets.AddRange(
          colliders.Select( a =>
          {
            var data = CreateInstance<AssetData>();
            data.Convert = false;
            data.type = type;
            data.physXType = PhysXType.Collider;
            data.gameObject = a;
            data.Updatable = type != typeof(WheelCollider);

            return data;
          } ).ToList());
      }

      // Debug.Log("Number of assetDatas: " + physXAssets.Count);

      return physXAssets;
    }

    private List<AssetData> FindColliderAssetData(Type type, PhysXType physXType)
    {
      var components = FindObjectsOfType(type, true);

      return components.Where( o => ((Collider)o).attachedRigidbody == null )
                       .Select( o =>
      {
        var data = CreateInstance<AssetData>();
        data.Convert = false;
        data.type = type;
        data.physXType = physXType;
        data.gameObject = ((UnityEngine.Component)o).gameObject;
        data.Updatable = type != typeof(WheelCollider);

        return data;
      } ).ToList();
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

    private VisualElement TableRowUI( AssetData asset )
    {

      var row = new VisualElement();
      row.SetPadding(3,3,3,0);
            var ve = new VisualElement();

      ve.SetEnabled( asset.Updatable );
      var index = m_tableRows.Count();
      row.RegisterCallback<MouseDownEvent>( mde =>
      {
        EditorUtility.FocusProjectWindow();
        Selection.activeObject = asset.gameObject;
        m_selectedIndex = index;

        UpdateColors();
      } );
      row.RegisterCallback<MouseOverEvent>( mde =>
      {
        m_hoverIndex = index;
        UpdateColors();
      } );
      row.RegisterCallback<MouseOutEvent>( mde =>
      {
        m_hoverIndex = -1;
        UpdateColors();
      } );
      ve.style.flexDirection = FlexDirection.Row;

      var activeToggle = new Toggle() { value = asset.Convert  };
      activeToggle.RegisterValueChangedCallback( ce => asset.Convert = ce.newValue );
      activeToggle.style.width = 20;
      activeToggle.SetMargin( 0, 0, 0, StyleKeyword.Null );
      if ( asset.Updatable)
        m_toggleAll.AddControlledToggle( activeToggle );

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
      if (asset.dependentAssets == null)
        componentLabel = new Label( asset.type.ToString() );
      else
        componentLabel = new Label( asset.type.ToString() + " - Colliders: " + asset.dependentAssets.Count);
      componentLabel.style.flexGrow = 0.6f;
      componentLabel.style.flexBasis = 0;
      componentLabel.style.overflow = Overflow.Hidden;
      nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
      componentLabel.style.marginRight = 2;

      ve.Add( activeToggle );
      flex.Add( nameLabel );
      flex.Add( TypeIcon );
      flex.Add( componentLabel );
      ve.Add( flex );
      row.Add( ve );

      m_tableRows.Add( row );

      return row;
    }

    private void OnEnable()
    {
      GatherSceneObjects();

      Undo.undoRedoPerformed += () => Repopulate(false);
    }

    private void CreateGUI()
    {
      rootVisualElement.SetPadding( 19, 15, 15, 15 );

      var RPLabel = new Label( $"This utility attempts to convert PhysX Components to corresponding AGX Components." );
      RPLabel.style.marginBottom = 15;
      rootVisualElement.Add( RPLabel );

      // TODO starting here with just colliders, moving on from there to RBs. Think about how the different lists will look
      var assetConverter = new VisualElement();
      assetConverter.style.height = 600;
      assetConverter.SetBorder( 2, Color.Lerp( InspectorGUI.BackgroundColor, Color.black, 0.2f ) );
      assetConverter.SetBorderRadius( 5 );
      assetConverter.SetPadding( 5 );

      var description = new Label( "PhysXObjects in Scene. GameObjects with multiple matching Components can appear on multiple lines." );
      description.style.whiteSpace = WhiteSpace.Normal;
      description.style.marginBottom = 10;
      assetConverter.Add( description );

//      var sortMenu = EditorGUILayout.EnumFlagsField( AGXUnity.Utils.GUI.MakeLabel( "Properties" ),
//                                                                                   m_sortType,
//                                                                                   InspectorEditor.Skin.Popup );
//      var sortMenu = new EnumField("Test", m_sortType);
//      sortMenu.
//      //sortMenu.style.marginBottom = 15;
//      rootVisualElement.Add( sortMenu );

      var header = new VisualElement();
      header.style.flexDirection = FlexDirection.Row;
      header.style.justifyContent = Justify.SpaceBetween;
      header.style.flexShrink = 0;
      m_toggleAll = new MixedToggle();
      header.Add( m_toggleAll );

      var numAssets = new VisualElement();
      numAssets.style.flexDirection = FlexDirection.Row;
      numAssets.style.alignItems = Align.Center;
      numAssets.style.unityTextAlign = TextAnchor.MiddleLeft;
  
      numAssets.Add(new Label() { text = "Upgradable assets: " });

      var image = new Image() { image = m_statusIcons[ 0 ] };
      image.style.width = 20;
      image.style.height = 20;
      numAssets.Add( image );

      var lab = new Label() { text = $" {(m_physXAssets != null ? m_physXAssets.Where( a => a.Updatable).Count() : 0)}" };
      lab.style.width = 15;
      numAssets.Add( lab );
      header.Add( numAssets );

      var scroll = new ScrollView();

      foreach ( var asset in m_physXAssets )
        scroll.Add( TableRowUI( asset ) );

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
      refreshButton.clicked += () => Repopulate( );

      var refreshIcon =  new Image { image = IconManager.GetIcon( MiscIcon.Update ) };
      refreshIcon.style.flexBasis = 0;
      refreshIcon.style.flexGrow = 1;
      refreshButton.Add( refreshIcon );

      footer.Add( refreshButton );
      footer.Add( convertButton );
      assetConverter.Add( footer );

      rootVisualElement.Add( assetConverter );
    }

    private void Repopulate(bool gatherAssets = true)
    {
      GatherSceneObjects();
      m_tableRows.Clear();

      rootVisualElement.Clear();
      CreateGUI();
    }

    private void ConvertSelected()
    {
      using ( new UndoCollapseBlock("Convert Selected PhysX Assets") ) {
        foreach(var asset in m_physXAssets ) { // .Where(m => m.Status == MaterialStatus.Updatable )
          if ( !asset.Convert )
            continue;

          if (asset.physXType == PhysXType.Collider)
            ConvertCollider(asset);

          if (asset.physXType == PhysXType.RigidBody)
          {
            ConvertRigidbody(asset);
            foreach (var dependentCollider in asset.dependentAssets)
              ConvertCollider(dependentCollider);
          }

//          EditorUtility.SetDirty( asset );
//          EditorUtility.SetDirty( asset.gameObject.GetComponent(asset.type) );
        }
      }

      AssetDatabase.SaveAssets();
      Repopulate(false);
    }


    private void ConvertRigidbody(AssetData asset)
    {
      var physXRb = asset.gameObject.GetComponent<Rigidbody>();
      var agxRB = Undo.AddComponent<AGXUnity.RigidBody>(asset.gameObject);

      agxRB.MassProperties.Mass.UseDefault = false;
      agxRB.MassProperties.Mass.Value = physXRb.mass;

      agxRB.LinearVelocityDamping = new Vector3(physXRb.drag, physXRb.drag, physXRb.drag);
      agxRB.AngularVelocityDamping = new Vector3(physXRb.angularDrag, physXRb.angularDrag, physXRb.angularDrag);

      if (!physXRb.automaticCenterOfMass)
      {
        agxRB.MassProperties.CenterOfMassOffset.UseDefault = false;
        agxRB.MassProperties.CenterOfMassOffset.Value = physXRb.centerOfMass;
      }

      if (!physXRb.automaticInertiaTensor)
      {
        agxRB.MassProperties.InertiaDiagonal.UseDefault = false;
        agxRB.MassProperties.InertiaDiagonal.Value = physXRb.centerOfMass;
      }

      if (physXRb.isKinematic)
        agxRB.MotionControl = agx.RigidBody.MotionControl.KINEMATICS;

      if (asset.rootAsset)
        asset.gameObject.AddComponent<AGXUnity.ArticulatedRoot>();

      Undo.DestroyObjectImmediate(physXRb);
    }

    private void ConvertCollider(AssetData asset)
    {
      // We need to create a child gameObject with the AGX collider to account for the "Center" property
      var newObject = new GameObject();

      Undo.RegisterCreatedObjectUndo(newObject, newObject.name);
      newObject.transform.parent = asset.gameObject.transform;
      
      newObject.transform.localRotation = Quaternion.identity;
      newObject.transform.localScale = Vector3.one; // AGX colliders ignore transform scale, except MeshColliders

      newObject.isStatic = asset.gameObject.isStatic;

      var localScale = asset.gameObject.transform.localScale;

      switch (asset.type.ToString()) // Can't switch on type, workaround with strings
      {
        case "UnityEngine.SphereCollider": 
          var physXSphere = asset.gameObject.GetComponent<SphereCollider>();
          var agxSphere = Undo.AddComponent<AGXUnity.Collide.Sphere>(newObject);

          newObject.name = "AGXUnity.Collide.Sphere";
          newObject.transform.localPosition = physXSphere.center;

          // SphereCollider radius is old radius times largest of absolute value of localScale axes
          agxSphere.Radius = physXSphere.radius * Mathf.Max(new float[]{
                                                    Mathf.Abs(localScale.x), 
                                                    Mathf.Abs(localScale.y),
                                                    Mathf.Abs(localScale.z)});

          agxSphere.IsSensor = physXSphere.isTrigger;

          Undo.DestroyObjectImmediate(physXSphere);
          break;

        case "UnityEngine.BoxCollider": 
          var physXBox = asset.gameObject.GetComponent<BoxCollider>();
          var agxBox = Undo.AddComponent<AGXUnity.Collide.Box>(newObject);

          newObject.name = "AGXUnity.Collide.Box";
          newObject.transform.localPosition = physXBox.center;

          agxBox.HalfExtents = new Vector3(physXBox.size.x / 2f * Mathf.Abs(localScale.x),
                                           physXBox.size.y / 2f * Mathf.Abs(localScale.y),
                                           physXBox.size.z / 2f * Mathf.Abs(localScale.z));
          
          agxBox.IsSensor = physXBox.isTrigger;

          Undo.DestroyObjectImmediate(physXBox);
          break;        

        case "UnityEngine.CapsuleCollider": 
          var physXCapsule = asset.gameObject.GetComponent<CapsuleCollider>();
          var agxCapsule = Undo.AddComponent<AGXUnity.Collide.Capsule>(newObject);

          newObject.name = "AGXUnity.Collide.Capsule";
          newObject.transform.localPosition = physXCapsule.center;

          // Capsule radius is old radius times abs largest xz-localScale axis. PhysX capsule height is including caps, agx is without
          newObject.transform.localScale = Vector3.one;
          var radius = physXCapsule.radius * Mathf.Max(new float[]{
                                                      Mathf.Abs(localScale.x), 
                                                      Mathf.Abs(localScale.z)});
          agxCapsule.Radius = radius;
          agxCapsule.Height = (physXCapsule.height - radius * 2) * Mathf.Abs(localScale.y);

          agxCapsule.IsSensor = physXCapsule.isTrigger;

          Undo.DestroyObjectImmediate(physXCapsule);
          break;        

        case "UnityEngine.MeshCollider": 
          var physXMesh = asset.gameObject.GetComponent<MeshCollider>();
          var agxMesh = Undo.AddComponent<AGXUnity.Collide.Mesh>(newObject);

          newObject.name = "AGXUnity.Collide.Mesh";
          newObject.transform.localPosition = Vector3.zero;

          agxMesh.AddSourceObject(physXMesh.sharedMesh);

          agxMesh.IsSensor = physXMesh.isTrigger;

          Undo.DestroyObjectImmediate(physXMesh);
          break;

        case "UnityEngine.TerrainCollider": 
          Undo.DestroyObjectImmediate(newObject);
          Undo.AddComponent<AGXUnity.Model.DeformableTerrain>(asset.gameObject);
        break;
      }
    }
  }
}
