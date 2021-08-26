using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;
using GUI = AGXUnity.Utils.GUI;
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
      var anyUnknownType = constraints.Any( c => c.Type == ConstraintType.Unknown );
      for ( int i = 1; i < constraints.Length; ++i )
        differentTypes = differentTypes || refConstraint.Type != constraints[ i ].Type;

      // Render AttachmentPair GUI.
      ConstraintAttachmentFrameTool.OnPreTargetMembersGUI();

      Undo.RecordObjects( constraints, "ConstraintTool" );

      UnityEngine.GUI.changed = false;

      // The constraint type is Unknown when, e.g., go.AddComponent<Constraint>()
      // or when the constraint has been reset. If any of the selected constraints
      // is of type Unknown, we exit the GUI here.
      if ( !ConstraintTypeGUI( constraints, differentTypes ) )
        return;

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.CollisionsState != constraint.CollisionsState );
      var collisionsState = ConstraintCollisionsStateGUI( refConstraint.CollisionsState );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.CollisionsState = collisionsState;
        UnityEngine.GUI.changed = false;
      }

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.SolveType != constraint.SolveType );
      var solveType = ConstraintSolveTypeGUI( refConstraint.SolveType );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.SolveType = solveType;
        UnityEngine.GUI.changed = false;
      }

      EditorGUI.showMixedValue = constraints.Any( constraint => refConstraint.ConnectedFrameNativeSyncEnabled != constraint.ConnectedFrameNativeSyncEnabled );
      var frameNativeSync = ConstraintConnectedFrameSyncGUI( refConstraint.ConnectedFrameNativeSyncEnabled );
      EditorGUI.showMixedValue = false;

      if ( UnityEngine.GUI.changed ) {
        foreach ( var constraint in constraints )
          constraint.ConnectedFrameNativeSyncEnabled = frameNativeSync;
        UnityEngine.GUI.changed = false;
      }

      if ( differentTypes ) {
        InspectorGUI.WarningLabel( "Constraints are of different types.\nRow data editing not supported." );
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
      foreach ( ConstraintUtils.ConstraintRowParser.RowType rowType in Enum.GetValues( typeof( ConstraintUtils.ConstraintRowParser.RowType ) ) ) {
        if ( !InspectorGUI.Foldout( selected( "ec_" + rowType.ToString() ),
                                    GUI.MakeLabel( rowType.ToString() + " properties", true ) ) ) {
          continue;
        }

        using ( InspectorGUI.IndentScope.Single ) {
          var refTransOrRotRowData = constraintsParser[ 0 ][ rowType ];
          foreach ( var wrapper in ecRowDataWrappers ) {
            if ( !InspectorEditor.ShouldBeShownInInspector( wrapper.Member ) )
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
                  var value = InspectorGUI.CustomFloatField( labelContent,
                                                             fieldContent,
                                                             wrapper.Get<float>( refTransOrRotRowData[ i ]?.RowData ) );
                  if ( UnityEngine.GUI.changed ) {
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

              UnityEngine.GUI.changed = false;
              EditorGUI.showMixedValue = false;
            }
          } // For type wrappers.
        } // Indentation.
      } // For Translational, Rotational.

      var ecControllers = refConstraint.GetElementaryConstraintControllers();
      if ( ecControllers.Length > 0 &&
           InspectorGUI.Foldout( selected( "controllers" ),
                                 GUI.MakeLabel( "Controllers", true ) ) ) {
        using ( InspectorGUI.IndentScope.Single ) {
          foreach ( var refController in ecControllers ) {
            var controllerType    = refController.GetControllerType();
            var controllerTypeTag = controllerType.ToString()[ 0 ].ToString();
            var controllerName    = ConstraintUtils.FindName( refController );
            if ( controllerName.EndsWith( " Controller" ) )
              controllerName = controllerName.Remove( controllerName.LastIndexOf( " Controller" ) );
            var controllerLabel   = GUI.MakeLabel( ( controllerType == Constraint.ControllerType.Rotational ?
                                                       GUI.Symbols.CircleArrowAcw.ToString() + " " :
                                                       GUI.Symbols.ArrowRight.ToString() + " " ) + controllerName, true );
            if ( !InspectorGUI.Foldout( selected( controllerTypeTag + controllerName ),
                                        controllerLabel ) ) {
              continue;
            }

            var controllers = ( from constraint
                                in constraints
                                from controller
                                in constraint.GetElementaryConstraintControllers()
                                where controller.GetType() == refController.GetType() &&
                                      controller.GetControllerType() == refController.GetControllerType()
                                select controller ).ToArray();
            using ( InspectorGUI.IndentScope.Single ) {
              InspectorEditor.DrawMembersGUI( controllers );
              InspectorEditor.DrawMembersGUI( controllers, controller => (controller as ElementaryConstraint).RowData[ 0 ] );
            }
          }
        }
      }
    }

    private bool ConstraintTypeGUI( Constraint[] constraints, bool differentTypes )
    {
      var anyUnknownType = constraints.Any( c => c.Type == ConstraintType.Unknown );
      // Reference type is set to unknown if we have multi-select and
      // the types aren't the same. This is to detect if the user has
      // selected some valid type, with the limitation that it's not
      // possible to change to Unknown (that action will be ignored).
      var refType = differentTypes ?
                      ConstraintType.Unknown :
                      constraints[ 0 ].Type;

      EditorGUI.showMixedValue = differentTypes;

      var newType = refType;
      using ( new GUI.EnabledBlock( !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
        if ( refType == ConstraintType.Unknown )
          newType = (ConstraintType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Choose type" ),
                                                               refType,
                                                               InspectorEditor.Skin.Popup );
        else
          newType = (ConstraintType)EditorGUILayout.EnumPopup( GUI.MakeLabel( "Type" ),
                                                               refType,
                                                               InspectorEditor.Skin.Popup,
                                                               GUILayout.Width( EditorGUIUtility.labelWidth +
                                                                                2.0f * 76.0f ) );
      }

      EditorGUI.showMixedValue = false;

      // Avoiding change to Unknown when it wasn't possible
      // to handle undo/redo for that case.
      if ( newType != refType && newType != ConstraintType.Unknown ) {
        var performChange = constraints.All( c => c.Type == ConstraintType.Unknown ) ||
                            EditorUtility.DisplayDialog( "Change constraint type",
                                                         "Any changes to parameters (compliance, damping etc.) and/or states of " +
                                                         $"any controller will be lost when the type changes to {newType}.\n\n" +
                                                         "Do you want to continue?",
                                                         "Yes",
                                                         "Cancel" );
        if ( performChange ) {
          var undoIndex = Undo.GetCurrentGroup();
          foreach ( var constraint in constraints ) {
            if ( newType == constraint.Type )
              continue;

            constraint.ChangeType( newType,
                                   createdObject =>
                                   {
                                     Undo.RegisterCreatedObjectUndo( createdObject, "ElementaryConstraint created." );
                                   },
                                   destroyObject =>
                                   {
                                     Undo.DestroyObjectImmediate( destroyObject );
                                   } );
          }
          Undo.CollapseUndoOperations( undoIndex );
        }
      }

      return !anyUnknownType;
    }

    public static Constraint.ECollisionsState ConstraintCollisionsStateGUI( Constraint.ECollisionsState state )
    {
      var skin          = InspectorEditor.Skin;
      var guiWasEnabled = UnityEngine.GUI.enabled;

      using ( new EditorGUILayout.HorizontalScope() ) {
        EditorGUILayout.PrefixLabel( GUI.MakeLabel( "Disable Collisions", true ),
                                      InspectorEditor.Skin.LabelMiddleLeft );

        using ( new GUI.EnabledBlock( !EditorApplication.isPlayingOrWillChangePlaymode ) ) {
          var rbVsRbActive = !EditorGUI.showMixedValue &&
                             state == Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;
          var refVsConActive = !EditorGUI.showMixedValue &&
                               state == Constraint.ECollisionsState.DisableReferenceVsConnected;

          if ( GUILayout.Toggle( rbVsRbActive,
                                 GUI.MakeLabel( "Rb " + GUI.Symbols.ArrowLeftRight.ToString() + " Rb",
                                                rbVsRbActive,
                                                "Disable all shapes in rigid body 1 against all shapes in rigid body 2." ),
                                  skin.GetButton( InspectorGUISkin.ButtonType.Left ),
                                  GUILayout.Width( 76 ) ) != rbVsRbActive )
            state = state == Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2 ?
                      Constraint.ECollisionsState.KeepExternalState :
                      Constraint.ECollisionsState.DisableRigidBody1VsRigidBody2;

          if ( GUILayout.Toggle( refVsConActive,
                                 GUI.MakeLabel( "Ref " + GUI.Symbols.ArrowLeftRight.ToString() + " Con",
                                                refVsConActive,
                                                "Disable Reference object vs. Connected object." ),
                                  skin.GetButton( InspectorGUISkin.ButtonType.Right ),
                                  GUILayout.Width( 76 ) ) != refVsConActive )
            state = state == Constraint.ECollisionsState.DisableReferenceVsConnected ?
                      Constraint.ECollisionsState.KeepExternalState :
                      Constraint.ECollisionsState.DisableReferenceVsConnected;
        }
      }

      return state;
    }

    public static Constraint.ESolveType ConstraintSolveTypeGUI( Constraint.ESolveType solveType )
    {
      // Matching with disable buttons above where each button is 76.
      var position = EditorGUILayout.GetControlRect( GUILayout.Width( EditorGUIUtility.labelWidth +
                                                                      2.0f * 76.0f ) );
      solveType = (Constraint.ESolveType)EditorGUI.EnumPopup( position,
                                                              GUI.MakeLabel( "Solve Type", true ),
                                                              solveType,
                                                              InspectorEditor.Skin.Popup );
      return solveType;
    }

    public static bool ConstraintConnectedFrameSyncGUI( bool enabled )
    {
      enabled = InspectorGUI.Toggle( GUI.MakeLabel( "Connected Frame Animated", true ),
                                     !EditorGUI.showMixedValue && enabled );
      return enabled;
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
