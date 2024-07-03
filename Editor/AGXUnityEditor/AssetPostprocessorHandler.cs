﻿using AGXUnity;
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

    string m_prefabToFix = "";

    private void UpdatePrefab()
    {
      var defaultMat = Object.Instantiate(ShapeVisual.DefaultMaterial);
      defaultMat.name = ShapeVisual.DefaultMaterialName;

      // Add or replace default shape material in prefab
      var objs = AssetDatabase.LoadAllAssetRepresentationsAtPath( m_prefabToFix );
      foreach ( var obj in objs.Where( o => o is Material && o.name == ShapeVisual.DefaultMaterialName ) )
        AssetDatabase.RemoveObjectFromAsset( obj );
      AssetDatabase.AddObjectToAsset( defaultMat, m_prefabToFix );

      var RP = RenderingUtils.DetectPipeline();
      using ( var editScope = new PrefabUtility.EditPrefabContentsScope( m_prefabToFix ) ) {
        var svs = editScope.prefabContentsRoot.GetComponentsInChildren<ShapeVisual>();
        foreach ( var sv in svs )
          foreach ( var mat in sv.GetMaterials() )
            if ( mat == null || !mat.SupportsPipeline( RP ) )
              sv.ReplaceMaterial( mat, defaultMat );
      }
      EditorApplication.update -= UpdatePrefab;
    }

    private static bool m_isProcessingPrefabInstance = false;

    private void OnPostprocessPrefab( GameObject gameObject )
    {
      // Since AGX materials are stored in the scene (in memory) these material instances cannot be added
      // to prefabs. This causes the prefabs to have 'None' materials.
      // One solution to this is to add the material as a subasset to the prefab.
      // However, we cant do that here as the prefab might not yet exist.
      // Instead we queue an editor update callback to do it.
      var svs = gameObject.GetComponentsInChildren<ShapeVisual>();
      if ( svs.Length == 0 )
        return;

      // We queue an update if any material is 'None' or if they have unsupported materials
      bool needsFixing = false;
      var RP = RenderingUtils.DetectPipeline();
      foreach ( var sv in svs )
        foreach ( var mat in sv.GetMaterials() )
          needsFixing |= mat == null || !mat.SupportsPipeline( RP );

      if ( needsFixing ) {
        m_prefabToFix = context.assetPath;
        EditorApplication.update += UpdatePrefab;
      }
      else {
        // If no update is needed we set the hideflags of the added material to hide it in the hierarchy. This cant be done in the prefab as the 
        // prefab code uses the hideflags internally: https://forum.unity.com/threads/prefab-sub-asset-hideflags-dont-persist.1324920/
        List<Object> objs = new List<Object>();
        context.GetObjects( objs );
        foreach ( var item in objs.Where( o => o is Material m && m.name == ShapeVisual.DefaultMaterialName ) )
          item.hideFlags = HideFlags.HideInHierarchy;
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
