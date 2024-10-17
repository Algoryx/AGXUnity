using AGXUnity;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( FrictionModel ) )]
  public class FrictionModelTool : CustomTargetTool
  {
    public FrictionModel FrictionModel { get { return Targets[ 0 ] as FrictionModel; } }

    public FrictionModelTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPostTargetMembersGUI()
    {
      if ( FrictionModel.Type != FrictionModel.EType.ConstantNormalForceBoxFriction )
        return;

      EditorGUI.showMixedValue = ShowMixed( ( fm1, fm2 ) => !AGXUnity.Utils.Math.Approximately( fm1.NormalForceMagnitude,
                                                                                                fm2.NormalForceMagnitude ) );
      EditorGUI.BeginChangeCheck();
      var normalForce = Mathf.Max( EditorGUILayout.FloatField( GUI.MakeLabel( "Normal Force Magnitude" ),
                                                               FrictionModel.NormalForceMagnitude ),
                                   0.0f );
      if ( EditorGUI.EndChangeCheck() )
        foreach ( var fm in GetTargets<FrictionModel>() )
          fm.NormalForceMagnitude = normalForce;

      EditorGUI.showMixedValue = ShowMixed( ( fm1, fm2 ) => fm1.ScaleNormalForceWithDepth != fm2.ScaleNormalForceWithDepth );

      EditorGUI.BeginChangeCheck();
      var scaleNormalForceWithDepth = InspectorGUI.Toggle( GUI.MakeLabel( "Scale Normal Force With Depth" ),
                                                           FrictionModel.ScaleNormalForceWithDepth );
      if ( EditorGUI.EndChangeCheck() )
        foreach ( var fm in GetTargets<FrictionModel>() )
          fm.ScaleNormalForceWithDepth = scaleNormalForceWithDepth;

      EditorGUI.showMixedValue = false;
    }

    private bool ShowMixed( System.Func<FrictionModel, FrictionModel, bool> validator )
    {
      foreach ( var fm in GetTargets<FrictionModel>() )
        if ( validator( FrictionModel, fm ) )
          return true;
      return false;
    }
  }
}
