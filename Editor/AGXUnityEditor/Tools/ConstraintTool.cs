using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnityEditor.Utils.GUI;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( Constraint ) )]
  public class ConstraintTool : ConstraintAttachmentFrameTool
  {
    public Constraint Constraint { get; private set; }

    public Action<bool> OnFoldoutStateChange = delegate { };

    public ConstraintTool( Constraint constraint )
      : base( constraint.AttachmentPair, constraint )
    {
      Constraint = constraint;
    }

    public override void OnAdd()
    {
      base.OnAdd();
    }

    public override void OnRemove()
    {
      base.OnRemove();
    }

    public override void OnPreTargetMembersGUI( InspectorEditor editor )
    {
      // Possible undo performed that deleted the constraint. Remove us.
      if ( Constraint == null ) {
        PerformRemoveFromParent();
        return;
      }

      if ( editor.IsMultiSelect ) {
        OnPreTargetMembersGUIMultiSelect( editor );
        return;
      }

      var skin = InspectorEditor.Skin;

      GUILayout.Label( GUI.MakeLabel( Constraint.Type.ToString(), 24, true ), GUI.Align( skin.label, TextAnchor.MiddleCenter ) );
      GUI.Separator();

      // Render AttachmentPair GUI.
      base.OnPreTargetMembersGUI( editor );

      GUI.Separator();

      Constraint.CollisionsState = ConstraintCollisionsStateGUI( Constraint.CollisionsState, skin );
      Constraint.SolveType = ConstraintSolveTypeGUI( Constraint.SolveType, skin );

      GUI.Separator();

      Constraint.ConnectedFrameNativeSyncEnabled = ConstraintConnectedFrameSyncGUI( Constraint.ConnectedFrameNativeSyncEnabled, skin );

      GUI.Separator();

      ConstraintRowsGUI( skin );
    }

    private static void OnPreTargetMembersGUIMultiSelect( InspectorEditor editor )
    {
      var skin           = InspectorEditor.Skin;
      var constraints    = editor.Targets<Constraint>().ToArray();
      var refConstraint  = constraints[ 0 ];
      var differentTypes = false;
      for ( int i = 1; i < constraints.Length; ++i )
        differentTypes = differentTypes || refConstraint.Type != constraints[ i ].Type;

      if ( differentTypes ) {
        GUI.WarningLabel( "Constraints are of different types.\nMulti-selection edit not supported.", skin );
        return;
      }

      Undo.RecordObjects( constraints, "ConstraintTool" );

      GUILayout.Label( GUI.MakeLabel( constraints[ 0 ].Type.ToString() + 's', 24, true ), GUI.Align( skin.label, TextAnchor.MiddleCenter ) );

      // Frames goes here.

      GUI.Separator();

      UnityEngine.GUI.changed = false;

      EditorGUI.showMixedValue = constraints.FirstOrDefault( constraint => refConstraint.CollisionsState != constraint.CollisionsState ) != null;
      var collisionsState = ConstraintCollisionsStateGUI( refConstraint.CollisionsState, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.CollisionsState = collisionsState;
        UnityEngine.GUI.changed = false;
      }

      EditorGUI.showMixedValue = constraints.FirstOrDefault( constraint => refConstraint.SolveType != constraint.SolveType ) != null;
      var solveType = ConstraintSolveTypeGUI( refConstraint.SolveType, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.SolveType = solveType;
        UnityEngine.GUI.changed = false;
      }

      GUI.Separator();

      EditorGUI.showMixedValue = constraints.FirstOrDefault( constraint =>
                                                               refConstraint.ConnectedFrameNativeSyncEnabled != constraint.ConnectedFrameNativeSyncEnabled ) != null;
      var frameNativeSync = ConstraintConnectedFrameSyncGUI( refConstraint.ConnectedFrameNativeSyncEnabled, skin );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.ConnectedFrameNativeSyncEnabled = frameNativeSync;
        UnityEngine.GUI.changed = false;
      }

      var constraintsRowData = ( from constraint
                                 in constraints
                                 select ConstraintUtils.ConstraintRowParser.Create( constraint ) ).ToArray();
      var ecRowDataWrappers = InvokeWrapper.FindFieldsAndProperties( null, typeof( ElementaryConstraintRowData ) );
      foreach ( ConstraintUtils.ConstraintRowParser.RowType rowType in Enum.GetValues( typeof( ConstraintUtils.ConstraintRowParser.RowType ) ) ) {
        // Foldout
        GUILayout.Label( GUI.MakeLabel( rowType.ToString() + " properties", true ), skin.label );

        using ( new GUI.Indent( 12 ) ) {
          var refRowData = constraintsRowData[ 0 ][ rowType ];
          foreach ( var wrapper in ecRowDataWrappers ) {
            if ( !InspectorEditor.ShouldBeShownInInspector( wrapper.Member ) )
              continue;
            using ( new GUILayout.HorizontalScope() ) {
              GUILayout.Label( InspectorGUI.MakeLabel( wrapper.Member ), skin.label );
              GUILayout.FlexibleSpace();
              using ( new GUILayout.VerticalScope() ) {
                for ( int i = 0; i < 3; ++i ) {
                  using ( new GUILayout.HorizontalScope() )
                  using ( new GUI.EnabledBlock( refRowData[ i ] != null ) ) {
                    RowLabel( i, skin );

                    // Handling type float, e.g., compliance and damping.
                    if ( wrapper.IsType<float>() ) {
                      var value = EditorGUILayout.FloatField( wrapper.Get<float>( refRowData[ i ]?.RowData ) );
                      if ( UnityEngine.GUI.changed ) {
                        foreach ( var constraintRowData in constraintsRowData )
                          wrapper.ConditionalSet( constraintRowData[ rowType ][ i ].RowData, value );
                        UnityEngine.GUI.changed = false;
                      }
                    }
                    // Handling type RangeReal, e.g., force range.
                    // Note: During multi-selection we don't want to write, e.g., Min from
                    //       reference row data when value for Max is changed.
                    else if ( wrapper.IsType<RangeReal>() ) {
                      var forceRangeMin = EditorGUILayout.FloatField( wrapper.Get<RangeReal>( refRowData[ i ]?.RowData ).Min,
                                                                      GUILayout.MaxWidth( 128 ) );
                      var forceRangeMinChanged = UnityEngine.GUI.changed;
                      UnityEngine.GUI.changed = false;
                      var forceRangeMax = EditorGUILayout.FloatField( wrapper.Get<RangeReal>( refRowData[ i ]?.RowData ).Max,
                                                                      GUILayout.MaxWidth( 128 ) );
                      if ( forceRangeMinChanged || UnityEngine.GUI.changed ) {
                        foreach ( var constraintRowData in constraintsRowData ) {
                          var range = wrapper.Get<RangeReal>( constraintRowData[ rowType ][ i ].RowData );
                          if ( forceRangeMinChanged )
                            range.Min = forceRangeMin;
                          if ( UnityEngine.GUI.changed )
                            range.Max = forceRangeMax;

                          // Validation of Min > Max has to go somewhere else because if e.g.,
                          // Min = 50 and the user wants to type Max = 200 we're receiving
                          // Max = 2 as the user types.

                          wrapper.ConditionalSet( constraintRowData[ rowType ][ i ].RowData, range );
                        }
                        UnityEngine.GUI.changed = false;
                      }
                    } // IsType RangeReal.
                  } // Horizontal and GUI Enabled blocks.
                } // For U, V, N.
              } // Right align vertical scope.
            } // Horizontal with flexible space for alignment.
            GUI.Separator();
          } // For type wrappers.
        } // Indentation.
      } // For Translational, Rotational.
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

    public void ConstraintRowsGUI( GUISkin skin )
    {
      try {
        ConstraintUtils.ConstraintRowParser constraintRowParser = ConstraintUtils.ConstraintRowParser.Create( Constraint );

        InvokeWrapper[] memberWrappers = InvokeWrapper.FindFieldsAndProperties( null, typeof( ElementaryConstraintRowData ) );
        if ( constraintRowParser.HasTranslationalRows ) {
          if ( GUI.Foldout( Selected( SelectedFoldout.OrdinaryElementaryTranslational ),
                            GUI.MakeLabel( "Translational properties </b>(along constraint axis)<b>", true ),
                            skin,
                            OnFoldoutStateChange ) )
            using ( new GUI.Indent( 12 ) )
              HandleConstraintRowsGUI( constraintRowParser.TranslationalRows, memberWrappers, skin );
        }

        if ( constraintRowParser.HasRotationalRows ) {
          GUI.Separator();

          if ( GUI.Foldout( Selected( SelectedFoldout.OrdinaryElementaryRotational ),
                            GUI.MakeLabel( "Rotational properties </b>(about constraint axis)<b>", true ),
                            skin,
                            OnFoldoutStateChange ) )
            using ( new GUI.Indent( 12 ) )
              HandleConstraintRowsGUI( constraintRowParser.RotationalRows, memberWrappers, skin );
        }

        ElementaryConstraintController[] controllers = Constraint.GetElementaryConstraintControllers();
        if ( controllers.Length > 0 ) {
          if ( !constraintRowParser.Empty )
            GUI.Separator();

          if ( GUI.Foldout( Selected( SelectedFoldout.Controllers ),
                            GUI.MakeLabel( "Controllers", true ),
                            skin,
                            OnFoldoutStateChange ) ) {
            using ( new GUI.Indent( 12 ) ) {
              GUI.Separator();
              foreach ( var controller in controllers ) {
                HandleConstraintControllerGUI( controller, skin );

                GUI.Separator();
              }
            }
          }
        }
      }
      catch ( AGXUnity.Exception e ) {
        GUILayout.Label( GUI.MakeLabel( "Unable to parse constraint rows", true ), skin.label );
        GUILayout.Label( GUI.MakeLabel( "  - " + e.Message, Color.red ), skin.label );
      }
    }

    private enum SelectedFoldout
    {
      OrdinaryElementaryTranslational,
      OrdinaryElementaryRotational,
      Controllers,
      Controller
    }

    private EditorDataEntry Selected( SelectedFoldout sf, string identifier = "", bool defaultSelected = false )
    {
      return EditorData.Instance.GetData( Constraint, sf.ToString() + identifier, newEntry => { newEntry.Bool = defaultSelected; } );
    }

    private class BeginConstraintRowGUI : IDisposable
    {
      private bool m_guiWasEnabled = true;

      public BeginConstraintRowGUI( ConstraintUtils.ConstraintRow row, InvokeWrapper wrapper )
      {
        m_guiWasEnabled = UnityEngine.GUI.enabled;

        UnityEngine.GUI.enabled = m_guiWasEnabled && row != null && row.Valid;
      }

      public void Dispose()
      {
        UnityEngine.GUI.enabled = m_guiWasEnabled;
      }
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

    private void HandleConstraintRowsGUI( ConstraintUtils.ConstraintRow[] rows, InvokeWrapper[] wrappers, GUISkin skin )
    {
      foreach ( InvokeWrapper wrapper in wrappers ) {
        if ( wrapper.HasAttribute<HideInInspector>() )
          continue;

        GUILayout.BeginHorizontal();
        {
          GUILayout.Label( GUI.MakeLabel( wrapper.Member.Name ), skin.label, GUILayout.MinWidth( 74 ) );
          GUILayout.FlexibleSpace();
          GUILayout.BeginVertical();
          {
            for ( int i = 0; i < 3; ++i ) {
              using ( new BeginConstraintRowGUI( rows[ i ], wrapper ) ) {
                GUILayout.BeginHorizontal();
                {
                  HandleConstraintRowType( rows[ i ], i, wrapper, skin );
                }
                GUILayout.EndHorizontal();
              }
            }
          }
          GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();

        GUI.Separator();
      }
    }

    private void HandleConstraintRowType( ConstraintUtils.ConstraintRow row, int rowIndex, InvokeWrapper wrapper, GUISkin skin )
    {
      if ( UnityEngine.GUI.enabled )
        Undo.RecordObject( row.ElementaryConstraint, "Row" );

      RowLabel( rowIndex, skin );

      var rowData = row != null ? row.RowData : null;
      object value = null;
      if ( wrapper.IsType<float>() )
        value = EditorGUILayout.FloatField( wrapper.Get<float>( rowData ) );
      else if ( wrapper.IsType<RangeReal>() ) {
        RangeReal currValue = wrapper.Get<RangeReal>( rowData );

        currValue.Min = EditorGUILayout.FloatField( currValue.Min, GUILayout.MaxWidth( 128 ) );
        currValue.Max = EditorGUILayout.FloatField( currValue.Max, GUILayout.MaxWidth( 128 ) );

        if ( currValue.Min > currValue.Max )
          currValue.Min = currValue.Max;

        value = currValue;
      }
      else {
      }

      if ( wrapper.ConditionalSet( rowData, value ) )
        EditorUtility.SetDirty( Constraint );
    }

    private void HandleConstraintControllerGUI( ElementaryConstraintController controller, GUISkin skin )
    {
      var controllerType    = controller.GetControllerType();
      var controllerTypeTag = controllerType.ToString().Substring( 0, 1 );
      string dimString      = "[" + GUI.AddColorTag( controllerTypeTag,
                                                     controllerType == Constraint.ControllerType.Rotational ?
                                                       Color.Lerp( UnityEngine.GUI.color, Color.red, 0.75f ) :
                                                       Color.Lerp( UnityEngine.GUI.color, Color.green, 0.75f ) ) + "] ";
      if ( GUI.Foldout( Selected( SelectedFoldout.Controller,
                                  controllerTypeTag + ConstraintUtils.FindName( controller ) ),
                        GUI.MakeLabel( dimString + ConstraintUtils.FindName( controller ), true ),
                        skin,
                        OnFoldoutStateChange ) ) {
        using ( new GUI.Indent( 12 ) ) {
          controller.Enable = GUI.Toggle( GUI.MakeLabel( "Enable", controller.Enable ), controller.Enable, skin.button, skin.label );
          InspectorEditor.DrawMembersGUI( new UnityEngine.Object[] { controller } );
        }
      }
    }
  }
}
