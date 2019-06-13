using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnityEditor.Utils.GUI;
using Object = UnityEngine.Object;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Constraint ) )]
  public class ConstraintTool : CustomTargetTool
  {
    public Constraint Constraint
    {
      get
      {
        return Targets[ 0 ] as Constraint;
      }
    }

    public ConstraintAttachmentFrameTool ConstraintAttachmentFrameTool { get; private set; }

    public Action<bool> OnFoldoutStateChange = delegate { };

    public ConstraintTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnAdd()
    {
      ConstraintAttachmentFrameTool = new ConstraintAttachmentFrameTool( GetTargets<Constraint>().Select( constraint => constraint.AttachmentPair ).ToArray() );
      AddChild( ConstraintAttachmentFrameTool );
    }

    public override void OnRemove()
    {
      RemoveAllChildren();
    }

    public override void OnPreTargetMembersGUI()
    {
      var skin           = InspectorEditor.Skin;
      var constraints    = GetTargets<Constraint>().ToArray();
      var refConstraint  = constraints[ 0 ];
      var differentTypes = false;
      for ( int i = 1; i < constraints.Length; ++i )
        differentTypes = differentTypes || refConstraint.Type != constraints[ i ].Type;

      GUILayout.Label( GUI.MakeLabel( ( differentTypes ?
                                        "Constraints" :
                                        refConstraint.Type.ToString() + ( IsMultiSelect ? "s" : string.Empty ) ),
                                      24,
                                      true ),
                       GUI.Align( skin.label, TextAnchor.MiddleCenter ) );

      GUI.Separator();

      // Render AttachmentPair GUI.
      ConstraintAttachmentFrameTool.OnPreTargetMembersGUI();

      Undo.RecordObjects( constraints, "ConstraintTool" );

      UnityEngine.GUI.changed = false;

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.CollisionsState != constraint.CollisionsState );
      var collisionsState = ConstraintCollisionsStateGUI( refConstraint.CollisionsState, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.CollisionsState = collisionsState;
        UnityEngine.GUI.changed = false;
      }

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.SolveType != constraint.SolveType );
      var solveType = ConstraintSolveTypeGUI( refConstraint.SolveType, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.SolveType = solveType;
        UnityEngine.GUI.changed = false;
      }

      GUI.Separator();

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.ConnectedFrameNativeSyncEnabled != constraint.ConnectedFrameNativeSyncEnabled );
      var frameNativeSync = ConstraintConnectedFrameSyncGUI( refConstraint.ConnectedFrameNativeSyncEnabled, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.ConnectedFrameNativeSyncEnabled = frameNativeSync;
        UnityEngine.GUI.changed = false;
      }

      if ( differentTypes ) {
        GUI.WarningLabel( "Constraints are of different types.\nRow data editing not supported.", skin );
        return;
      }

      Func<string, EditorDataEntry> selected = ( id ) =>
      {
        return EditorData.Instance.GetData( refConstraint, id, entry => entry.Bool = false );
      };

      var constraintsParser = ( from constraint
                                in constraints
                                select ConstraintUtils.ConstraintRowParser.Create( constraint ) ).ToArray();
      var allElementaryConstraints = constraints.SelectMany( constraint => constraint.GetOrdinaryElementaryConstraints() ).ToArray();
      Undo.RecordObjects( allElementaryConstraints, "ConstraintTool" );

      var ecRowDataWrappers = InvokeWrapper.FindFieldsAndProperties<ElementaryConstraintRowData>();
      GUI.Separator();
      foreach ( ConstraintUtils.ConstraintRowParser.RowType rowType in Enum.GetValues( typeof( ConstraintUtils.ConstraintRowParser.RowType ) ) ) {
        if ( !GUI.Foldout( selected( "ec_" + rowType.ToString() ),
                           GUI.MakeLabel( rowType.ToString() + " properties", true ),
                           skin ) ) {
          GUI.Separator();
          continue;
        }

        using ( new GUI.Indent( 12 ) ) {
          var refTransOrRotRowData = constraintsParser[ 0 ][ rowType ];
          foreach ( var wrapper in ecRowDataWrappers ) {
            if ( !InspectorEditor.ShouldBeShownInInspector( wrapper.Member ) )
              continue;
            using ( new GUILayout.HorizontalScope() ) {
              GUILayout.Label( InspectorGUI.MakeLabel( wrapper.Member ), skin.label );
              GUILayout.FlexibleSpace();
              using ( new GUILayout.VerticalScope() ) {
                for ( int i = 0; i < 3; ++i ) {
                  var rowDataInstances = ( from constraintParser
                                           in constraintsParser
                                           where constraintParser[ rowType ][ i ] != null
                                           select constraintParser[ rowType ][ i ].RowData ).ToArray();
                  // TODO: This could probably be replaced by using InspectorEditor.HandleType
                  //       with a tweak. We use wrapper.Get<type>( foo.RowData ) while our
                  //       drawers uses wrapper.Get<type>().
                  // UPDATE: Probably not worth it because we have to override all labels
                  //         written by our default drawers.
                  // ****************************************************************************
                  //var objects = ( from constraintParser
                  //                in constraintsParser
                  //                where constraintParser[ rowType ][ i ] != null
                  //                select constraintParser[ rowType ][ i ].RowData ).ToArray();
                  //using ( new GUILayout.HorizontalScope() )
                  //using ( new GUI.EnabledBlock( refTransOrRotRowData[ i ] != null ) ) {
                  //  RowLabel( i, skin );
                  //  InspectorEditor.HandleType( wrapper, objects );
                  //}
                  // ****************************************************************************

                  using ( new GUILayout.HorizontalScope() )
                  using ( new GUI.EnabledBlock( refTransOrRotRowData[ i ] != null ) ) {
                    RowLabel( i, skin );

                    // Handling type float, e.g., compliance and damping.
                    if ( wrapper.IsType<float>() ) {
                      EditorGUI.showMixedValue = !wrapper.AreValuesEqual( rowDataInstances );
                      var value = EditorGUILayout.FloatField( wrapper.Get<float>( refTransOrRotRowData[ i ]?.RowData ) );
                      if ( UnityEngine.GUI.changed ) {
                        foreach ( var constraintParser in constraintsParser )
                          wrapper.ConditionalSet( constraintParser[ rowType ][ i ].RowData, value );
                        UnityEngine.GUI.changed = false;
                      }
                      EditorGUI.showMixedValue = false;
                    }
                    // Handling type RangeReal, e.g., force range.
                    // Note: During multi-selection we don't want to write, e.g., Min from
                    //       reference row data when value for Max is changed.
                    else if ( wrapper.IsType<RangeReal>() ) {
                      EditorGUI.showMixedValue = rowDataInstances.Any( rowData => !Equals( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Min,
                                                                                           wrapper.Get<RangeReal>( rowData ).Min ) );
                      var forceRangeMin = EditorGUILayout.FloatField( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Min,
                                                                      GUILayout.MaxWidth( 128 ) );
                      var forceRangeMinChanged = UnityEngine.GUI.changed;
                      EditorGUI.showMixedValue = false;
                      UnityEngine.GUI.changed  = false;
                      EditorGUI.showMixedValue = rowDataInstances.Any( rowData => !Equals( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Max,
                                                                                           wrapper.Get<RangeReal>( rowData ).Max ) );
                      var forceRangeMax = EditorGUILayout.FloatField( wrapper.Get<RangeReal>( refTransOrRotRowData[ i ]?.RowData ).Max,
                                                                      GUILayout.MaxWidth( 128 ) );
                      if ( forceRangeMinChanged || UnityEngine.GUI.changed ) {
                        foreach ( var constraintParser in constraintsParser ) {
                          var range = wrapper.Get<RangeReal>( constraintParser[ rowType ][ i ].RowData );
                          if ( forceRangeMinChanged )
                            range.Min = forceRangeMin;
                          if ( UnityEngine.GUI.changed )
                            range.Max = forceRangeMax;

                          // Validation of Min > Max has to go somewhere else because if e.g.,
                          // Min = 50 and the user wants to type Max = 200 we're receiving
                          // Max = 2 as the user types.

                          wrapper.ConditionalSet( constraintParser[ rowType ][ i ].RowData, range );
                        }
                        UnityEngine.GUI.changed = false;
                        EditorGUI.showMixedValue = false;
                      }
                    } // IsType RangeReal.
                  } // Horizontal and GUI Enabled blocks.
                } // For U, V, N.
              } // Right align vertical scope.
            } // Horizontal with flexible space for alignment.
            GUI.Separator();
          } // For type wrappers.
        } // Indentation.
        GUI.Separator();
      } // For Translational, Rotational.

      if ( GUI.Foldout( selected( "controllers" ),
                        GUI.MakeLabel( "Controllers", true ),
                        skin ) ) {
        GUI.Separator();
        using ( new GUI.Indent( 12 ) ) {
          foreach ( var refController in refConstraint.GetElementaryConstraintControllers() ) {
            var controllerType    = refController.GetControllerType();
            var controllerTypeTag = controllerType.ToString()[ 0 ].ToString();
            var controllerName    = ConstraintUtils.FindName( refController );
            string dimString      = "[" + GUI.AddColorTag( controllerTypeTag,
                                                           controllerType == Constraint.ControllerType.Rotational ?
                                                             Color.Lerp( UnityEngine.GUI.color, Color.red, 0.75f ) :
                                                             Color.Lerp( UnityEngine.GUI.color, Color.green, 0.75f ) ) + "] ";
            if ( !GUI.Foldout( selected( controllerTypeTag + controllerName ),
                              GUI.MakeLabel( dimString + controllerName, true ),
                              skin ) ) {
              GUI.Separator();
              continue;
            }

            var controllers = ( from constraint
                                in constraints
                                from controller
                                in constraint.GetElementaryConstraintControllers()
                                where controller.GetType() == refController.GetType()
                                select controller ).ToArray();
            using ( new GUI.Indent( 12 ) ) {
              InspectorEditor.DrawMembersGUI( controllers );
              GUI.Separator();
              InspectorEditor.DrawMembersGUI( controllers, controller => (controller as ElementaryConstraint).RowData[ 0 ] );
            }

            GUI.Separator();
          }
        }
      }
    }

    public static Constraint.ECollisionsState ConstraintCollisionsStateGUI( Constraint.ECollisionsState state,
                                                                            GUISkin skin )
    {
      bool guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new GUI.Indent( 12 ) ) {
        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( GUI.MakeLabel( "Disable collisions: ",
                                          true ),
                           GUI.Align( skin.label,
                                      TextAnchor.MiddleLeft ),
                           new GUILayoutOption[] { GUILayout.Width( 140 ),
                                                   GUILayout.Height( 25 ) } );

          UnityEngine.GUI.enabled = !EditorApplication.isPlaying;
          if ( GUILayout.Button( GUI.MakeLabel( "Rb " + GUI.Symbols.Synchronized.ToString() + " Rb",
                                                false,
                                                "Disable all shapes in rigid body 1 against all shapes in rigid body 2." ),
                                 GUI.ConditionalCreateSelectedStyle( !EditorGUI.showMixedValue &&
                                                                       state == Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2,
                                                                     skin.button ),
                                 new GUILayoutOption[] { GUILayout.Width( 76 ), GUILayout.Height( 25 ) } ) )
            state = state == Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2 ?
                      Constraint.ECollisionsState.KeepExternalState :
                      Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;

          if ( GUILayout.Button( GUI.MakeLabel( "Ref " + GUI.Symbols.Synchronized.ToString() + " Con",
                                                false,
                                                "Disable Reference object vs. Connected object." ),
                                 GUI.ConditionalCreateSelectedStyle( !EditorGUI.showMixedValue &&
                                                                       state == Constraint.ECollisionsState.DisableReferenceVsConnected,
                                                                     skin.button ),
                                 new GUILayoutOption[] { GUILayout.Width( 76 ),
                                                         GUILayout.Height( 25 ) } ) )
            state = state == Constraint.ECollisionsState.DisableReferenceVsConnected ?
                      Constraint.ECollisionsState.KeepExternalState :
                      Constraint.ECollisionsState.DisableReferenceVsConnected;
          UnityEngine.GUI.enabled = guiWasEnabled;
        }
        GUILayout.EndHorizontal();
      }

      return state;
    }

    public static Constraint.ESolveType ConstraintSolveTypeGUI( Constraint.ESolveType solveType, GUISkin skin )
    {
      GUILayout.BeginHorizontal();
      {
        GUILayout.Space( 12 );
        GUILayout.Label( GUI.MakeLabel( "Solve Type", true ), skin.label, GUILayout.Width( 140 ) );
        solveType = (Constraint.ESolveType)EditorGUILayout.EnumPopup( solveType, skin.button, GUILayout.ExpandWidth( true ), GUILayout.Height( 18 ), GUILayout.Width( 2 * 76 + 4 ) );
      }
      GUILayout.EndHorizontal();

      return solveType;
    }

    public static bool ConstraintConnectedFrameSyncGUI( bool enabled, GUISkin skin )
    {
      using ( new GUI.Indent( 12 ) ) {
        enabled = GUI.Toggle( GUI.MakeLabel( "Connected frame animated", true ),
                              !EditorGUI.showMixedValue && enabled,
                              skin.button,
                              skin.label );
      }

      return enabled;
    }

    private static string[] RowLabels = new string[] { "U", "V", "N" };
    private static Color[] RowColors = new Color[]
    {
      Color.Lerp( Color.red, Color.white, 0.55f ),
      Color.Lerp( Color.green, Color.white, 0.55f ),
      Color.Lerp( Color.blue, Color.white, 0.55f )
    };

    private static void RowLabel( int i, GUISkin skin )
    {
      GUILayout.Label( GUI.MakeLabel( RowLabels[ i ], RowColors[ i ] ),
                       skin.label,
                       GUILayout.Width( 12 ) );
    }
  }
}
