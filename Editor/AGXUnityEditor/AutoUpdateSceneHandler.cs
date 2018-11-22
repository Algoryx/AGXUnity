using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

namespace AGXUnityEditor
{
  public static class AutoUpdateSceneHandler
  {
    public static void HandleUpdates( UnityEngine.SceneManagement.Scene scene )
    {
      // Ignoring updates when the editor presses play.
      if ( EditorApplication.isPlaying )
        return;

      VerifyOnSelectionTarget( scene );
      VerifyShapeVisualsMaterial( scene );
      VerifySimulationInstance( scene );
    }

    /// <summary>
    /// * Verifies version of OnSelectionProxy and patches it if Target == null.
    /// * Verifies so that our shapes doesn't have multiple debug rendering components.
    /// </summary>
    private static void VerifyOnSelectionTarget( UnityEngine.SceneManagement.Scene scene )
    {
      AGXUnity.Collide.Shape[] shapes = UnityEngine.Object.FindObjectsOfType<AGXUnity.Collide.Shape>();
      foreach ( var shape in shapes ) {
        OnSelectionProxy selectionProxy = shape.GetComponent<OnSelectionProxy>();
        if ( selectionProxy != null && selectionProxy.Target == null )
          selectionProxy.Component = shape;

        AGXUnity.Rendering.ShapeDebugRenderData[] data = shape.GetComponents<AGXUnity.Rendering.ShapeDebugRenderData>();
        if ( data.Length > 1 ) {
          Debug.Log( "Shape has several ShapeDebugRenderData. Removing/resetting.", shape );
          foreach ( var instance in data )
            Component.DestroyImmediate( instance );
          data = null;
        }
      }
    }

    /// <summary>
    /// Shape visual components with sharedMaterial == null are assigned default material.
    /// </summary>
    private static void VerifyShapeVisualsMaterial( UnityEngine.SceneManagement.Scene scene )
    {
      AGXUnity.Rendering.ShapeVisual[] shapeVisuals = UnityEngine.Object.FindObjectsOfType<AGXUnity.Rendering.ShapeVisual>();
      foreach ( var shapeVisual in shapeVisuals ) {
        var renderers = shapeVisual.GetComponentsInChildren<MeshRenderer>();
        foreach ( var renderer in renderers ) {
          if ( renderer.sharedMaterial == null ) {
            renderer.sharedMaterial = Manager.GetOrCreateShapeVisualDefaultMaterial();

            Debug.Log( "Shape visual with null material. Assigning default.", shapeVisual );

            if ( !EditorApplication.isPlaying )
              UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty( scene );
          }
        }
      }
    }

    private static void VerifySimulationInstance( UnityEngine.SceneManagement.Scene scene )
    {
      var simulation = UnityEngine.Object.FindObjectOfType<Simulation>();
      if ( simulation == null )
        return;

      var guid   = AssetDatabase.AssetPathToGUID( IO.Utils.AGXUnitySourceDirectory + "/Simulation.cs" );
      var script = IO.Utils.FindScriptInSceneFile( scene.path, guid, false ).FirstOrDefault();
      if ( script == null )
        return;

      HandleSolverSettings( scene, simulation, script );
      HandleSimulationStepMode( scene, simulation, script );
    }

    private static void HandleSolverSettings( UnityEngine.SceneManagement.Scene scene,
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

    private static void HandleSimulationStepMode( UnityEngine.SceneManagement.Scene scene,
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
  }
}
