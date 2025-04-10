﻿using AGXUnity;
using AGXUnity.Utils;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AGXUnityEditor
{
  public static class AutoUpdateSceneHandler
  {
    public static void HandleUpdates( Scene scene )
    {
      // Ignoring updates when the editor presses play.
      if ( EditorApplication.isPlaying )
        return;

      VerifyOnSelectionTarget( scene );
      VerifySimulationInstance( scene );
      VerifyCableWireRendering( scene );
      VerifyTerrainMaterials();
    }

    /// <summary>
    /// * Verifies prefab instance doesn't contain WireRenderer with SegmentSpawner as child.
    /// * Verifies prefab instance doesn't contain CableRenderer with SegmentSpawner as child.
    /// </summary>
    /// <param name="instance">Prefab instance.</param>
    /// <returns>true if changes were made, otherwise false</returns>
    public static bool VerifyPrefabInstance( GameObject instance )
    {
      var isDisconnected =
#if UNITY_2022_1_OR_NEWER
        false;
#else
        PrefabUtility.IsDisconnectedFromPrefabAsset( instance );
#endif

      // Patching the instance when we've a disconnected prefab - otherwise patch
      // the source asset object.
      var objectToCheck = isDisconnected ?
                            instance :
                            (GameObject)PrefabUtility.GetCorrespondingObjectFromSource( instance );

      if ( !HandleSegmentSpawner( objectToCheck ) )
        return false;

      try {
        if ( !isDisconnected ) {
          PrefabUtility.SaveAsPrefabAssetAndConnect( objectToCheck,
                                                     AssetDatabase.GetAssetPath( objectToCheck ),
                                                     InteractionMode.UserAction );
        }
      }
      catch ( System.ArgumentException ) {
        // Silencing Unity (2018.3) bug where everything seems right but we
        // get an ArgumentException thrown at us:
        //    https://forum.unity.com/threads/creating-prefabs-from-models-by-script.606760/
      }

      return true;
    }

    /// <summary>
    /// * Verifies version of OnSelectionProxy and patches it if Target == null.
    /// * Verifies so that our shapes doesn't have multiple debug rendering components.
    /// </summary>
    private static void VerifyOnSelectionTarget( Scene scene )
    {
#if UNITY_2022_2_OR_NEWER
      var shapes = Object.FindObjectsByType<AGXUnity.Collide.Shape>(FindObjectsSortMode.None);
#else
      var shapes = Object.FindObjectsOfType<AGXUnity.Collide.Shape>();
#endif
      foreach ( var shape in shapes ) {
        OnSelectionProxy selectionProxy = shape.GetComponent<OnSelectionProxy>();
        if ( selectionProxy != null && selectionProxy.Target == null )
          selectionProxy.Component = shape;

        var data = shape.GetComponents<AGXUnity.Rendering.ShapeDebugRenderData>();
        if ( data.Length > 1 ) {
          Debug.Log( "Shape has several ShapeDebugRenderData. Removing/resetting.", shape );
          foreach ( var instance in data )
            Component.DestroyImmediate( instance );
          data = null;
        }
      }
    }

    private static void VerifySimulationInstance( Scene scene )
    {
#if UNITY_6000_0_OR_NEWER
      var simulation = Object.FindAnyObjectByType<Simulation>( FindObjectsInactive.Include );
#else
      var simulation = Object.FindObjectOfType<Simulation>(true);
#endif
      if ( simulation == null )
        return;

      var guid   = AssetDatabase.AssetPathToGUID( IO.Utils.AGXUnitySourceDirectory + "/Simulation.cs" );
      var script = IO.Utils.FindScriptInSceneFile( scene.path, guid, false ).FirstOrDefault();
      if ( script == null )
        return;

      HandleSolverSettings( scene, simulation, script );
      HandleSimulationStepMode( scene, simulation, script );
    }

    private static void VerifyCableWireRendering( Scene scene )
    {
      var containsChanges = false;
      System.Action<GameObject> checkGameObject = go =>
      {
        var root = PrefabUtility.GetOutermostPrefabInstanceRoot( go );
        if ( root != null )
          containsChanges = VerifyPrefabInstance( root ) || containsChanges;
        else
          containsChanges = HandleSegmentSpawner( go ) || containsChanges;
      };

#if UNITY_2022_2_OR_NEWER
      var cables = Object.FindObjectsByType<Cable>(FindObjectsSortMode.None);
      var wires  = Object.FindObjectsByType<Wire>(FindObjectsSortMode.None);
#else
      var cables = Object.FindObjectsOfType<Cable>();
      var wires  = Object.FindObjectsOfType<Wire>();
#endif

      foreach ( var cable in cables )
        checkGameObject( cable.gameObject );

      foreach ( var wire in wires )
        checkGameObject( wire.gameObject );

      if ( containsChanges ) {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( scene );

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }
    }

    private static void HandleSolverSettings( Scene scene,
                                              Simulation simulation,
                                              IO.Utils.YamlObject script )
    {
      var isUpToDate = !script.Fields.ContainsKey( "m_warmStartingDirectContacts" );

      // Flag scene dirty when we have the old version in the file.
      if ( !isUpToDate )
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( scene );

      if ( simulation.SolverSettings != null || isUpToDate )
        return;

      var warmStartingContacts = false;
      var numRestingIterations = -1;
      var numDryFrictionIterations = -1;
      try {
        if ( !script.Fields[ "m_warmStartingDirectContacts" ].TryGet( out warmStartingContacts ) )
          warmStartingContacts = false;
        if ( !script.Fields[ "m_numRestingIterations" ].TryGet( out numRestingIterations ) )
          numRestingIterations = -1;
        if ( !script.Fields[ "m_numDryFrictionIterations" ].TryGet( out numDryFrictionIterations ) )
          numDryFrictionIterations = -1;
      }
      catch ( System.Exception ) {
        return;
      }

      // Ignore if default values were used.
      if ( warmStartingContacts == false && numRestingIterations < 0 && numDryFrictionIterations < 0 )
        return;

      var createSettings = EditorUtility.DisplayDialog( "Solver settings asset",
                                                        "Solver settings moved from Simulation to SolverSettings asset.\n\nCreate new solver settings asset with previous values?",
                                                        "Yes",
                                                        "No" );
      if ( !createSettings )
        return;

      var path = EditorUtility.SaveFilePanel( "Create new Solver Settings",
                                              "Assets",
                                              "solver settings.asset",
                                              "asset" );
      if ( path == string.Empty )
        return;

      var info = new FileInfo( path );
      var relPath = IO.Utils.MakeRelative( path, Application.dataPath );
      var solverSettings = AGXUnity.ScriptAsset.Create<AGXUnity.SolverSettings>();
      solverSettings.name = info.Name;
      AssetDatabase.CreateAsset( solverSettings, relPath + ( info.Extension == ".asset" ? "" : ".asset" ) );
      AssetDatabase.SaveAssets();
      AssetDatabase.Refresh();

      solverSettings.WarmStartDirectContacts = warmStartingContacts;
      if ( numRestingIterations >= 0 )
        solverSettings.RestingIterations = numRestingIterations;
      if ( numDryFrictionIterations >= 0 )
        solverSettings.DryFrictionIterations = numDryFrictionIterations;
      simulation.SolverSettings = solverSettings;
    }

    private static void HandleSimulationStepMode( Scene scene,
                                                  Simulation simulation,
                                                  IO.Utils.YamlObject script )
    {
      if ( !script.Fields.ContainsKey( "m_enableAutomaticStepping" ) )
        return;

      var enableAutoStepping = true;
      script.Fields[ "m_enableAutomaticStepping" ].TryGet( out enableAutoStepping );

      simulation.AutoSteppingMode = enableAutoStepping ?
                                      Simulation.AutoSteppingModes.FixedUpdate :
                                      Simulation.AutoSteppingModes.Disabled;
      UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( scene );
    }

    private static bool HandleSegmentSpawner( GameObject go )
    {
      if ( go == null )
        return false;

      var cRenderers = go.GetComponentsInChildren<AGXUnity.Rendering.CableRenderer>();
      var wRenderers = go.GetComponentsInChildren<AGXUnity.Rendering.WireRenderer>();
      var toRemove   = new List<GameObject>();
      toRemove.AddRange( from cRenderer
                         in cRenderers
                         where cRenderer.transform.Find( "RenderSegments" ) != null
                         select cRenderer.transform.Find( "RenderSegments" ).gameObject );
      toRemove.AddRange( from wRenderer
                         in wRenderers
                         where wRenderer.transform.Find( "RenderSegments" ) != null
                         select wRenderer.transform.Find( "RenderSegments" ).gameObject );
      if ( toRemove.Count == 0 )
        return false;

      foreach ( var obj in toRemove )
        GameObject.DestroyImmediate( obj, true );

      return true;
    }

    private enum LegacyDeformableTerrainMaterialPreset
    {
      gravel_1,
      sand_1,
      dirt_1
    }

    /// <summary>
    /// Updating from MaterialPreset enum to named + MaterialsLibrary directory.
    /// </summary>
    private static void VerifyTerrainMaterials()
    {
      if ( EditorData.Instance.GetStaticData( "DeformableTerrainMaterial_PresetName_Update" ).Bool )
        return;

      var terrainMaterials = IO.Utils.FindAssetsOfType<AGXUnity.Model.DeformableTerrainMaterial>( "Assets" );
      var terrainMaterialUpdated = false;
      foreach ( var terrainMaterial in terrainMaterials ) {
        var asset = IO.Utils.ParseAsset( AssetDatabase.GetAssetPath( terrainMaterial ) );
        int presetValue = -1;
        if ( asset.Fields.ContainsKey( "m_preset" ) && asset.Fields[ "m_preset" ].TryGet( out presetValue ) ) {
          var presetName = ( (LegacyDeformableTerrainMaterialPreset)presetValue ).ToString();
          Debug.Log( $"{"DeformableTerrainMaterial".Color( Color.green )}: Updating {terrainMaterial.name} to use preset name \"{presetName}\"" );
          terrainMaterial.SetPresetName( presetName );
          EditorUtility.SetDirty( terrainMaterial );
          terrainMaterialUpdated = true;
        }
      }
      if ( terrainMaterialUpdated ) {
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
      }

      EditorData.Instance.GetStaticData( "DeformableTerrainMaterial_PresetName_Update" ).Bool = true;
    }
  }
}
