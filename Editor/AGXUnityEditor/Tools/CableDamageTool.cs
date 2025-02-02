﻿using AGXUnity;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( CableDamageProperties ) )]
  [CanEditMultipleObjects]
  public class CableDamagePropertiesTool : CustomTargetTool
  {
    public CableDamageProperties CableDamageProperties { get { return Targets[ 0 ] as CableDamageProperties; } }

    public CableDamagePropertiesTool( Object[] targets )
      : base( targets )
    {
    }

    private static GUIContent[] empty = new GUIContent[] { new GUIContent( "" ), new GUIContent( "" ),  new GUIContent( "" ) };

    public override void OnPreTargetMembersGUI()
    {
      var skin = InspectorEditor.Skin;

      GUIContent[] labels = new GUIContent[]
      {
        new GUIContent( "Bend" ),
        new GUIContent( "Twist" ),
        new GUIContent( "Stretch" )
      };
      InspectorGUI.MultiFieldColumnLabels( GUI.MakeLabel( "Movement weights", 12, true ), labels );

      var deformation = InspectorGUI.MultiFloatField(
        new GUIContent("Deformation", "The weight of the directional deformations to the overall cable damage calculation"),
        empty,
        new float[]{CableDamageProperties.BendDeformation, CableDamageProperties.TwistDeformation, CableDamageProperties.StretchDeformation});

      var rate =        InspectorGUI.MultiFloatField(
        new GUIContent("Rate", "The weight of the directional rate of deformation to the overall cable damage calculation"),
        empty,
        new float[]{CableDamageProperties.BendRate, CableDamageProperties.TwistRate, CableDamageProperties.StretchRate});

      var tension =     InspectorGUI.MultiFloatField(
        new GUIContent("Tension", "The weight of the directional tensions to the overall cable damage calculation"),
        empty,
        new float[]{CableDamageProperties.BendTension, CableDamageProperties.TwistTension, CableDamageProperties.StretchTension});

      GUILayout.Space( 5 );

      var threshold =   InspectorGUI.MultiFloatField(
        new GUIContent("Threshold", "The threshold over which the individual directional contributions should be considered for the overall cable damage calculation"),
        empty,
        new float[]{CableDamageProperties.BendThreshold, CableDamageProperties.TwistThreshold, CableDamageProperties.StretchThreshold});

      GUILayout.Space( 10 );

      CableDamageProperties.NormalForce =   EditorGUILayout.FloatField(
        new GUIContent( "Normal Force", "The weight of the calculated normal force to the overall cable damage calculation" ),
        CableDamageProperties.NormalForce );

      CableDamageProperties.FrictionForce = EditorGUILayout.FloatField(
        new GUIContent( "Friction Force", "The weight of the calculated friction force to the overall cable damage calculation" ),
        CableDamageProperties.FrictionForce );

      CableDamageProperties.BendDeformation = deformation[ 0 ];
      CableDamageProperties.TwistDeformation = deformation[ 1 ];
      CableDamageProperties.StretchDeformation = deformation[ 2 ];

      CableDamageProperties.BendRate = rate[ 0 ];
      CableDamageProperties.TwistRate = rate[ 1 ];
      CableDamageProperties.StretchRate = rate[ 2 ];

      CableDamageProperties.BendTension = tension[ 0 ];
      CableDamageProperties.TwistTension = tension[ 1 ];
      CableDamageProperties.StretchTension = tension[ 2 ];

      CableDamageProperties.BendThreshold = threshold[ 0 ];
      CableDamageProperties.TwistThreshold = threshold[ 1 ];
      CableDamageProperties.StretchThreshold = threshold[ 2 ];

      GUILayout.Space( 5 );

      GUILayout.Label( "<b>Rendering options</b>", InspectorGUISkin.Instance.Label );
    }
  }
}
