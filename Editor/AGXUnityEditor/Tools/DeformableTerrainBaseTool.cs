﻿using AGXUnity;
using AGXUnity.Model;
using UnityEditor;
using UnityEngine;
using static AGXUnityEditor.InspectorGUI;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( DeformableTerrainBase ) )]
  public class DeformableTerrainBaseTool : CustomTargetTool
  {
    public DeformableTerrainBase DeformableTerrainBase { get { return Targets[ 0 ] as DeformableTerrainBase; } }

    public DeformableTerrainBaseTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( NumTargets > 1 )
        return;

      RenderMaterialHandles();

      ToolArrayGUI( this, DeformableTerrainBase.MaterialPatches, "Material Patches" );
    }

    protected void RenderMaterialHandles()
    {
      if ( Foldout( EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainBaseMaterialHandles" ), GUI.MakeLabel( "Internal Material Handles" ) ) ) {
        EditorGUI.indentLevel = 1;

        WarningLabel( "The parameters of these materials will be overrwritten depending on the TerrainMaterial set. " +
                      "The purpose of these materials is to have a handle to create contact materials between terrain objects and external objects." );

        Undo.RecordObject( DeformableTerrainBase, "Set Surface Material" );
        var surfaceMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainSurfaceMaterial" );
        var surfaceMatRes = FoldoutObjectField( GUI.MakeLabel( "Surface Material" ),
                                                DeformableTerrainBase.Material,
                                                typeof( ShapeMaterial ),
                                                surfaceMatData,
                                                false ) as ShapeMaterial;
        if ( surfaceMatRes != DeformableTerrainBase.Material )
          DeformableTerrainBase.Material = surfaceMatRes;


        Undo.RecordObject( DeformableTerrainBase, "Set Particle Material" );
        var particleMatData = EditorData.Instance.GetData( DeformableTerrainBase, "DeformableTerrainParticleMaterial" );
        var particleMatRes = FoldoutObjectField(  GUI.MakeLabel( "Particle Material" ),
                                                  DeformableTerrainBase.ParticleMaterial,
                                                  typeof( ShapeMaterial ),
                                                  particleMatData,
                                                  false ) as ShapeMaterial;

        if ( particleMatRes != DeformableTerrainBase.ParticleMaterial )
          DeformableTerrainBase.ParticleMaterial = particleMatRes;
        EditorGUI.indentLevel = 0;
      }
    }
  }
}
