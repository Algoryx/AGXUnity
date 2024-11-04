using AGXUnity;
using AGXUnity.Collide;
using AGXUnity.Rendering;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AGXUnityEditor
{
  public class AssetPostprocessorHandler : AssetPostprocessor
  {
    static AssetPostprocessorHandler()
    {
      // Cache types which have the HideInInspector attribute for use in OnAGXPrefabAdddedToScene
      TypesHiddenInInspector = new List<System.Type>();
      foreach ( var a in System.AppDomain.CurrentDomain.GetAssemblies() )
        if ( !a.GetName().Name.StartsWith( "Unity" ) && !a.GetName().Name.StartsWith( "System" ) )
          foreach ( var t in a.DefinedTypes )
            if ( t.IsSubclassOf( typeof( MonoBehaviour ) ) && t.GetCustomAttribute<HideInInspector>() != null )
              TypesHiddenInInspector.Add( t );
    }

    static List<System.Type> TypesHiddenInInspector = new List<System.Type>();

    private class CollisionGroupEntryEqualityComparer : IEqualityComparer<CollisionGroupEntry>
    {
      public bool Equals( CollisionGroupEntry cg1, CollisionGroupEntry cg2 )
      {
        return cg1.Tag == cg2.Tag;
      }

      public int GetHashCode( CollisionGroupEntry entry )
      {
        return entry.Tag.GetHashCode();
      }
    }

    private static Material AddMaterial( Material material, string asset )
    {
      var defaultMat = Object.Instantiate(material);
      defaultMat.name = material.name;
      defaultMat.hideFlags |= HideFlags.HideInHierarchy;
      AssetDatabase.AddObjectToAsset( defaultMat, asset );

      return defaultMat;
    }

    private static bool m_isProcessingPrefabInstance = false;

    private static Material GetMaterialNoReplace( Object renderer )
    {
      return (Material)renderer.GetType().GetField( "m_material", BindingFlags.Instance | BindingFlags.NonPublic ).GetValue( renderer );
    }

    private void OnPostprocessPrefab( GameObject gameObject )
    {
      // HideFlags cannot be set in the asset directly since the prefab system uses them internally
      // Instead we set them on the imported prefab.
      List<Object> objList = new List<Object>();
      context.GetObjects( objList );
      foreach ( var obj in objList )
        if ( obj is Material mat && (
          mat.name == ShapeVisual.DefaultMaterialName ||
          mat.name == CableRenderer.DefaultMaterial().name ||
          mat.name == WireRenderer.DefaultMaterial().name ) )
          mat.hideFlags |= HideFlags.HideInHierarchy;

    }

    private static void PrefabCleanupMaterials( GameObject instance )
    {
      // Since AGX materials are stored in the scene (in memory) these material instances cannot be added
      // to prefabs. This causes the prefabs to have 'None' materials.
      // One solution to this is to add the material as a subasset to the prefab.
      // However, we cant do that here as the prefab might not yet exist.
      // Instead we queue an editor update callback to do it.

      // We queue an update if any material is 'None' or if they have unsupported materials
      var RP = RenderingUtils.DetectPipeline();
      bool changes = false;

      Material replacementVisualMat = null;
      Material replacementCableMat  = null;
      Material replacementWireMat   = null;

      var path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot( instance );

      using ( var prefab = new PrefabUtility.EditPrefabContentsScope( path ) ) {
        var svs = prefab.prefabContentsRoot.GetComponentsInChildren<ShapeVisual>();
        foreach ( var sv in svs ) {
          foreach ( var mat in sv.GetMaterials() ) {
            if ( mat == null || !mat.SupportsPipeline( RP ) || !EditorUtility.IsPersistent( mat ) ) {
              if ( replacementVisualMat == null )
                replacementVisualMat = AddMaterial( ShapeVisual.DefaultMaterial, path );
              sv.ReplaceMaterial( mat, replacementVisualMat );
              changes = true;
            }
          }
        }

        var cables = prefab.prefabContentsRoot.GetComponentsInChildren<CableRenderer>();
        foreach ( var cable in cables ) {
          var mat = GetMaterialNoReplace(cable);
          if ( mat == null || !mat.SupportsPipeline( RP ) || !EditorUtility.IsPersistent( mat ) ) {
            if ( replacementCableMat == null )
              replacementCableMat = AddMaterial( CableRenderer.DefaultMaterial(), path ); ;
            cable.Material = replacementCableMat;
            changes = true;
          }
        }

        var wires = prefab.prefabContentsRoot.GetComponentsInChildren<WireRenderer>();
        foreach ( var wire in wires ) {
          var mat = GetMaterialNoReplace(wire);
          if ( mat == null || !mat.SupportsPipeline( RP ) || !EditorUtility.IsPersistent( mat ) ) {
            if ( replacementWireMat == null )
              replacementWireMat = AddMaterial( WireRenderer.DefaultMaterial(), path ); ;
            wire.Material = replacementWireMat;
            changes = true;
          }
        }
      }

      if ( changes ) {
        foreach ( var cable in instance.GetComponentsInChildren<CableRenderer>() )
          if ( cable.Material == CableRenderer.DefaultMaterial() )
            PrefabUtility.RevertPropertyOverride( new SerializedObject( cable ).FindProperty( "m_Material" ), InteractionMode.AutomatedAction );

        foreach ( var wire in instance.GetComponentsInChildren<WireRenderer>() )
          if ( wire.Material == WireRenderer.DefaultMaterial() )
            PrefabUtility.RevertPropertyOverride( new SerializedObject( wire ).FindProperty( "m_Material" ), InteractionMode.AutomatedAction );

        var overrides = PrefabUtility.GetPropertyModifications( instance )
            .Where( mod =>
              mod.target == null ||
              mod.target.GetType() != typeof( MeshRenderer ) ||
              !mod.propertyPath.StartsWith( "m_Materials.Array.data" ) ||
              mod.objectReference.name != ShapeVisual.DefaultMaterialName
            )
            .ToArray();

        PrefabUtility.SetPropertyModifications( instance, overrides );
      }
    }

    /// <summary>
    /// Callback when a prefab is created from a scene game object <paramref name="go"/>,
    /// i.e., drag-dropped from hierarchy to the assets folder.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    public static void OnPrefabCreatedFromScene( GameObject instance )
    {
      // Avoiding recursion when we're manipulating the prefab from inside
      // this callback.
      if ( m_isProcessingPrefabInstance )
        return;

      PrefabCleanupMaterials( instance );

      var isAGXPrefab = instance.GetComponent<AGXUnity.IO.RestoredAGXFile>() != null;

      // Collect group ids that are disabled in the CollisionGroupsManager so that
      // when this prefab is added to a scene, the disabled collisions will be
      // added again.
      if ( !isAGXPrefab && CollisionGroupsManager.HasInstance ) {
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource( instance ) as GameObject;
        if ( prefab != null ) {
          try {
            m_isProcessingPrefabInstance = true;

            var groups = prefab.GetComponentsInChildren<CollisionGroups>();
            var tags   = ( from componentGroups
                           in groups
                           from tag
                           in componentGroups.Groups
                           select tag ).Distinct( new CollisionGroupEntryEqualityComparer() );
            var disabledPairs = from tag1
                                in tags
                                from tag2
                                in tags
                                where !CollisionGroupsManager.Instance.GetEnablePair( tag1.Tag, tag2.Tag )
                                select new AGXUnity.IO.GroupPair() { First = tag1.Tag, Second = tag2.Tag };
            if ( disabledPairs.Count() > 0 ) {
              var savedData = instance.GetComponent<AGXUnity.IO.SavedPrefabLocalData>();
              if ( savedData == null ) {
                savedData = instance.AddComponent<AGXUnity.IO.SavedPrefabLocalData>();
                PrefabUtility.ApplyAddedComponent( savedData, AssetDatabase.GetAssetPath( prefab ), InteractionMode.AutomatedAction );
              }
              foreach ( var disabledPair in disabledPairs )
                savedData.AddDisabledPair( disabledPair.First, disabledPair.Second );

              PrefabUtility.ApplyPrefabInstance( instance, InteractionMode.AutomatedAction );
            }
          }
          finally {
            m_isProcessingPrefabInstance = false;
          }
        }
      }

      if ( !isAGXPrefab && ContactMaterialManager.HasInstance ) {
        var prefab = PrefabUtility.GetCorrespondingObjectFromSource( instance ) as GameObject;
        if ( prefab != null ) {
          try {
            m_isProcessingPrefabInstance = true;

            var shapes    = prefab.GetComponentsInChildren<Shape>();
            var materials = ( from shape
                              in shapes
                              select shape.Material ).Distinct( );
            var contactMaterials = from cm
                                   in ContactMaterialManager.Instance.ContactMaterialEntries
                                   where materials.Contains(cm.ContactMaterial.Material1)
                                   where materials.Contains(cm.ContactMaterial.Material2)
                                   select cm;
            if ( contactMaterials.Count() > 0 ) {
              var savedData = instance.GetComponent<AGXUnity.IO.SavedPrefabLocalData>();
              if ( savedData == null ) {
                savedData = instance.AddComponent<AGXUnity.IO.SavedPrefabLocalData>();
                PrefabUtility.ApplyAddedComponent( savedData, AssetDatabase.GetAssetPath( prefab ), InteractionMode.AutomatedAction );
              }
              foreach ( var cm in contactMaterials ) {
                if ( cm.IsOriented )
                  Debug.LogWarning( $"Contact Material '{cm.ContactMaterial.name}' is oriented. Saving oriented materials in prefab is not currently supported. Contact material will be ignored." );
                else
                  savedData.AddContactMaterial( cm.ContactMaterial );
              }

              PrefabUtility.ApplyPrefabInstance( instance, InteractionMode.AutomatedAction );
            }
          }
          finally {
            m_isProcessingPrefabInstance = false;
          }
        }
      }
    }

    /// <summary>
    /// Callback when a prefab (likely) has been drag-dropped into a scene.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    public static void OnPrefabAddedToScene( GameObject instance )
    {
      if ( AutoUpdateSceneHandler.VerifyPrefabInstance( instance ) ) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      OnSavedPrefabAddedToScene( instance, instance.GetComponent<AGXUnity.IO.SavedPrefabLocalData>() );

      var fileInfo = new IO.AGXFileInfo( instance );
      if ( fileInfo.IsValid && fileInfo.Type == IO.AGXFileInfo.FileType.AGXPrefab )
        OnAGXPrefabAddedToScene( instance, fileInfo );
    }

    private static void OnAGXPrefabAddedToScene( GameObject instance, IO.AGXFileInfo fileInfo )
    {
      if ( fileInfo.ExistingPrefab == null ) {
        Debug.LogWarning( "Unable to load parent prefab from file: " + fileInfo.NameWithExtension );
        return;
      }

      Undo.SetCurrentGroupName( "Adding: " + instance.name + " to scene." );
      var grouId = Undo.GetCurrentGroup();

      // As of Unity 2022.1 the inspector breaks when setting hideflags while rendering a custom inspector
      // To circumvent this the hideflags are also set in the Reset method of the affected classes.
      // This however is not sufficient when importing .agx files as the agx file importer saves a prefab
      // which removes hideflags set on the object. (See https://forum.unity.com/threads/is-it-impossible-to-save-component-hideflags-in-a-prefab.976974/)
      // As an additional workaround we set the hideflags on the affected componentes when adding a prefab
      // to the scene instead.
      foreach ( var t in TypesHiddenInInspector ) {
        var components = instance.GetComponentsInChildren(t);
        foreach ( var comp in components )
          comp.hideFlags |= HideFlags.HideInInspector;
      }

      var contactMaterialManager = TopMenu.GetOrCreateUniqueGameObject<ContactMaterialManager>();
      Undo.RecordObject( contactMaterialManager, "Adding contact materials" );
      foreach ( var cm in IO.Utils.FindAssetsOfType<ContactMaterial>( fileInfo.DataDirectory ) )
        contactMaterialManager.Add( cm );

      var fileData = fileInfo.ExistingPrefab.GetComponent<AGXUnity.IO.RestoredAGXFile>();
      var collisionGroupsManager = TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>();
      Undo.RecordObject( collisionGroupsManager, "Adding disabled collision groups" );
      foreach ( var disabledPair in fileData.DisabledGroups )
        collisionGroupsManager.SetEnablePair( disabledPair.First, disabledPair.Second, false );

      var renderDatas = instance.GetComponentsInChildren<AGXUnity.Rendering.ShapeVisual>();
      Undo.RecordObjects( renderDatas, "Applying render data hide flags" );
      foreach ( var renderData in renderDatas ) {
        renderData.hideFlags |= HideFlags.NotEditable;
        renderData.transform.hideFlags |= HideFlags.NotEditable;
      }

      // TODO: Handle fileData.SolverSettings?

      Undo.CollapseUndoOperations( grouId );
    }

    private static void OnSavedPrefabAddedToScene( GameObject instance, AGXUnity.IO.SavedPrefabLocalData savedPrefabData )
    {
      if ( savedPrefabData == null || ( savedPrefabData.NumSavedDisabledPairs == 0 && savedPrefabData.NumSavedContactMaterials == 0 ) )
        return;

      Undo.SetCurrentGroupName( "Adding prefab data for " + instance.name + " to scene." );
      var grouId = Undo.GetCurrentGroup();
      foreach ( var disabledGroup in savedPrefabData.DisabledGroups )
        TopMenu.GetOrCreateUniqueGameObject<CollisionGroupsManager>().SetEnablePair( disabledGroup.First, disabledGroup.Second, false );
      foreach ( var contactMaterial in savedPrefabData.ContactMaterials )
        TopMenu.GetOrCreateUniqueGameObject<ContactMaterialManager>().Add( contactMaterial );
      Undo.CollapseUndoOperations( grouId );
    }
  }
}
