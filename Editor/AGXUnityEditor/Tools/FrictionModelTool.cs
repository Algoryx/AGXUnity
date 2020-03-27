using UnityEngine;
using UnityEditor;
using AGXUnity;

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
      var normalForce = Mathf.Max( EditorGUILayout.FloatField( GUI.MakeLabel( "Normal Force Magnitude" ),
                                                               FrictionModel.NormalForceMagnitude ),
                                   0.0f );
      if ( UnityEngine.GUI.changed )
        foreach ( var fm in GetTargets<FrictionModel>() )
          fm.NormalForceMagnitude = normalForce;
      UnityEngine.GUI.changed = false;

      EditorGUI.showMixedValue = ShowMixed( ( fm1, fm2 ) => fm1.ScaleNormalForceWithDepth != fm2.ScaleNormalForceWithDepth );

      var scaleNormalForceWithDepth = InspectorGUI.Toggle( GUI.MakeLabel( "Scale Normal Force With Depth" ),
                                                           FrictionModel.ScaleNormalForceWithDepth );
      if ( UnityEngine.GUI.changed )
        foreach ( var fm in GetTargets<FrictionModel>() )
          fm.ScaleNormalForceWithDepth = scaleNormalForceWithDepth;

      EditorGUI.showMixedValue = false;
      UnityEngine.GUI.changed = false;
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
