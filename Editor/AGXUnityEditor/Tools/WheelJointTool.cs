using AGXUnity;
using AGXUnity.Model;
using AGXUnity.Utils;
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GUI = AGXUnity.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( WheelJoint ) )]
  public class WheelJointTool : CustomTargetTool
  {
    public WheelJoint WheelJoint => Targets[ 0 ] as WheelJoint;

    public ConstraintAttachmentFrameTool ConstraintAttachmentFrameTool { get; private set; }

    public Action<bool> OnFoldoutStateChange = delegate { };

    public WheelJointTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      var constraints = GetTargets<WheelJoint>();
      ConstraintAttachmentFrameTool = new ConstraintAttachmentFrameTool( constraints.Select( constraint => constraint.AttachmentPair ).ToArray(), constraints.ToArray() );
      AddChild( ConstraintAttachmentFrameTool );
    }

    public override void OnRemove()
    {
      RemoveAllChildren();
    }

    private EditorDataEntry GetDataEntry( string id ) => EditorData.Instance.GetData( WheelJoint, id, entry => entry.Bool = false );

    public override void OnPreTargetMembersGUI()
    {
      var skin           = InspectorEditor.Skin;
      var constraints    = GetTargets<WheelJoint>().ToArray();
      var refConstraint  = constraints[ 0 ];

      // Render AttachmentPair GUI.
      ConstraintAttachmentFrameTool.OnPreTargetMembersGUI();

      Undo.RecordObjects( constraints, "WheelJointTool" );

      EditorGUI.BeginChangeCheck();

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.CollisionsState != constraint.CollisionsState );
      var collisionsState = ConstraintTool.ConstraintCollisionsStateGUI( refConstraint.CollisionsState );
      EditorGUI.showMixedValue = false;

      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var constraint in constraints ) {
          constraint.CollisionsState = collisionsState;
          EditorUtility.SetDirty( constraint );
        }
      }
      EditorGUI.BeginChangeCheck();

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.SolveType != constraint.SolveType );
      var solveType = ConstraintTool.ConstraintSolveTypeGUI( refConstraint.SolveType );
      EditorGUI.showMixedValue = false;

      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var constraint in constraints ) {
          constraint.SolveType = solveType;
          EditorUtility.SetDirty( constraint );
        }
      }

      EditorGUI.BeginChangeCheck();
      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.ConnectedFrameNativeSyncEnabled != constraint.ConnectedFrameNativeSyncEnabled );
      var frameNativeSync = ConstraintTool.ConstraintConnectedFrameSyncGUI( refConstraint.ConnectedFrameNativeSyncEnabled );
      EditorGUI.showMixedValue = false;

      if ( EditorGUI.EndChangeCheck() ) {
        foreach ( var constraint in constraints ) {
          constraint.ConnectedFrameNativeSyncEnabled = frameNativeSync;
          EditorUtility.SetDirty( constraint );
        }
      }

      var constraintsParser = ( from constraint
                                 in constraints
                                select ConstraintUtils.ConstraintRowParser.Create( constraint.GetOrdinaryElementaryConstraints() ) ).ToArray();
      var allElementaryConstraints = constraints.SelectMany( constraint => constraint.GetOrdinaryElementaryConstraints() ).ToArray();
      Undo.RecordObjects( constraints, "WheelJointTool" );

      var ecRowDataWrappers = InvokeWrapper.FindFieldsAndProperties<ElementaryConstraintRowData>();
      foreach ( ConstraintUtils.ConstraintRowParser.RowType rowType in Enum.GetValues( typeof( ConstraintUtils.ConstraintRowParser.RowType ) ) ) {
        if ( !InspectorGUI.Foldout( GetDataEntry( "ec_" + rowType.ToString() ),
                                    GUI.MakeLabel( rowType.ToString() + " properties", true ) ) ) {
          continue;
        }

        using ( InspectorGUI.IndentScope.Single ) {
          var refTransOrRotRowData = constraintsParser[ 0 ][ rowType ];
          foreach ( var wrapper in ecRowDataWrappers ) {
            if ( !InspectorEditor.ShouldBeShownInInspector( wrapper.Member, null ) )
              continue;

            for ( int i = 0; i < 3; ++i ) {
              var rowDataInstances = ( from constraintParser
                                        in constraintsParser
                                       where constraintParser[ rowType ][ i ] != null
                                       select constraintParser[ rowType ][ i ].RowData ).ToArray();

              using ( new GUI.EnabledBlock( refTransOrRotRowData[ i ] != null ) ) {
                var labelContent = i == 0 ? InspectorGUI.MakeLabel( wrapper.Member ) : null;
                var fieldContent = GUI.MakeLabel( RowLabels[ i ], RowColors[ i ] );
                if ( wrapper.IsType<float>() ) {
                  EditorGUI.showMixedValue = !wrapper.AreValuesEqual( rowDataInstances );
                  EditorGUI.BeginChangeCheck();
                  var value = InspectorGUI.CustomFloatField( labelContent,
                                                              fieldContent,
                                                              wrapper.Get<float>( refTransOrRotRowData[ i ]?.RowData ) );
                  if ( EditorGUI.EndChangeCheck() ) {
                    foreach ( var constraintParser in constraintsParser )
                      wrapper.ConditionalSet( constraintParser[ rowType ][ i ]?.RowData, value );
                  }
                }
                else if ( wrapper.IsType<RangeReal>() ) {
                  EditorGUI.showMixedValue = rowDataInstances.Any( rowData => !Equals( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Min,
                                                                                       wrapper.Get<RangeReal>( rowData ).Min ) ) ||
                                             rowDataInstances.Any( rowData => !Equals( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Max,
                                                                                       wrapper.Get<RangeReal>( rowData ).Max ) );
                  var rangeChangeData = InspectorGUI.RangeRealField( labelContent,
                                                                      wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ),
                                                                      GUI.MakeLabel( RowLabels[ i ], RowColors[ i ] ) );
                  if ( rangeChangeData.MinChanged || rangeChangeData.MaxChanged ) {
                    foreach ( var constraintParser in constraintsParser ) {
                      var range = wrapper.Get<RangeReal>( constraintParser[ rowType ][ i ].RowData );
                      if ( rangeChangeData.MinChanged )
                        range.Min = rangeChangeData.Min;
                      if ( rangeChangeData.MaxChanged )
                        range.Max = rangeChangeData.Max;

                      // Validation of Min > Max has to go somewhere else because if e.g.,
                      // Min = 50 and the user wants to type Max = 200 we're receiving
                      // Max = 2 as the user types.

                      wrapper.ConditionalSet( constraintParser[ rowType ][ i ].RowData, range );
                    }
                  }
                }
              }
              EditorGUI.showMixedValue = false;
            }
          } // For type wrappers.
        } // Indentation.
      } // For Translational, Rotational.

      var controllers = WheelJoint.GetElementaryConstraintControllers();
      Action<string> ctrlGUI = (nativeName) => ControllerGUI( controllers.Where( c => c.NativeName == nativeName ).First(), constraints );

      if ( InspectorGUI.Foldout( GetDataEntry( "Steering" ), GUI.MakeLabel( "Steering", true ) ) ) {
        using var _ = new InspectorGUI.IndentScope();
        ctrlGUI( "RSt" );
        ctrlGUI( "MSt" );
        ctrlGUI( "LSt" );
        ctrlGUI( "ESt" );
      }
      if ( InspectorGUI.Foldout( GetDataEntry( "Wheel Axle" ), GUI.MakeLabel( "Wheel Axle", true ) ) ) {
        using var _ = new InspectorGUI.IndentScope();
        ctrlGUI( "RWh" );
        ctrlGUI( "MWh" );
        ctrlGUI( "LWh" );
        ctrlGUI( "EWh" );
      }
      if ( InspectorGUI.Foldout( GetDataEntry( "Suspension" ), GUI.MakeLabel( "Suspension", true ) ) ) {
        using var _ = new InspectorGUI.IndentScope();
        ctrlGUI( "RSu" );
        ctrlGUI( "MSu" );
        ctrlGUI( "LSu" );
        ctrlGUI( "ESu" );
      }
    }

    private void ControllerGUI( ElementaryConstraintController refController, WheelJoint[] constraints )
    {
      var icon = refController.NativeName.EndsWith("Su") ? GUI.Symbols.ArrowRight.ToString() : GUI.Symbols.CircleArrowAcw.ToString();
      var controllerName    = ConstraintUtils.FindName( refController );
      if ( controllerName.EndsWith( " Controller" ) )
        controllerName = controllerName.Remove( controllerName.LastIndexOf( " Controller" ) );
      var controllerLabel   = GUI.MakeLabel( $"{icon} {controllerName}", true );
      if ( !InspectorGUI.Foldout( GetDataEntry( refController.NativeName ), controllerLabel ) ) {
        return;
      }
      var controllers = ( from constraint
                          in constraints
                          from controller
                          in constraint.GetElementaryConstraintControllers()
                          where controller.NativeName == refController.NativeName
                          select controller ).ToArray();
      using ( InspectorGUI.IndentScope.Single ) {
        InspectorEditor.DrawMembersGUI( controllers, constraints );
        InspectorEditor.DrawMembersGUI( controllers, constraints, controller => ( controller as ElementaryConstraint ).RowData[ 0 ] );
      }
    }

    private static string[] RowLabels = new string[] { "U", "V", "N" };
    private static Color[] RowColors = new Color[]
    {
      Color.Lerp( Color.red, Color.white, 0.55f ),
      Color.Lerp( Color.green, Color.white, 0.55f ),
      Color.Lerp( Color.blue, Color.white, 0.55f )
    };
  }
}
