using AGXUnity;
using AGXUnity.Model;
using AGXUnityEditor.UIElements;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace AGXUnityEditor.Windows
{
  public class EditSimWindows : EditorWindow
  {
    class DisableSteppingScope : IDisposable
    {
      Simulation.AutoSteppingModes mode;
      public DisableSteppingScope()
      {
        mode = Simulation.Instance.AutoSteppingMode;
        Simulation.Instance.AutoSteppingMode = Simulation.AutoSteppingModes.Disabled;
      }

      public void Dispose()
      {
        Simulation.Instance.AutoSteppingMode = mode;
      }
    }

    [MenuItem( "AGXUnity/Edit-Time Simulation", priority = 101 )]
    public static EditSimWindows Open()
    {
      // Get existing open window or if none, make a new one:
      var window = GetWindow<EditSimWindows>( false,
                                              "Edit-time Simulation",
                                              true );
      return window;
    }

    [SerializeField]
    GameObject m_reconfigureTarget = null;

    Dictionary<Constraint,float> m_reconfigureValues;

    private void OnEnable()
    {
    }

    private void CreateGUI()
    {
      var ve = new VisualElement();

      ve.SetPadding( 5 );

      var stepButton = new Button(EditTimeStep) { text = "Step" };
      stepButton.SetMargin( 0, 0, 10, 0 );
      ve.Add( stepButton );

      var stepForBlock = new VisualElement();
      stepForBlock.style.flexDirection = FlexDirection.Row;


      var stepForTime = new FloatField() { value = 1, isDelayed = true, label = "Step For" };
      stepForTime.RegisterValueChangedCallback( ce =>
      {
        if ( ce.newValue < Simulation.Instance.Native.getTimeStep() ) {
          stepForTime.value = (float)Simulation.Instance.Native.getTimeStep();
          ce.PreventDefault();
        }
      } );
      stepForTime.style.width = 200;

      var stepForButton = new Button(() => EditTimeStepFor(stepForTime.value)) { text = "Step For" };

      stepForBlock.Add( stepForTime );
      stepForBlock.Add( stepForButton );
      stepForBlock.SetMargin( 0, 0, 10, 0 );
      ve.Add( stepForBlock );

      var reconfigureBlock = new VisualElement();

      var so = new SerializedObject( this );
      var reconfiguredObject = new ObjectField() {label = "Reconfigure Target", value = null };
      reconfiguredObject.BindProperty( so.FindProperty( "m_reconfigureTarget" ) );
      reconfiguredObject.RegisterValueChangedCallback( ce =>
      {
        if ( ce.newValue != ce.previousValue ) {
          if ( reconfigureBlock.childCount > 1 )
            reconfigureBlock.RemoveAt( 1 );

          reconfigureBlock.Add( BuildReconfigureGUI( (GameObject)ce.newValue ) );
        }
      } );

      reconfigureBlock.Add( reconfiguredObject );

      ve.Add( reconfigureBlock );

      rootVisualElement.Add( ve );
    }

    private VisualElement BuildReconfigureGUI( GameObject go )
    {
      m_reconfigureValues = new Dictionary<Constraint, float>();

      var reconfigureGUI = new VisualElement();
      if ( go != null ) {
        var scroll = new ScrollView();
        scroll.SetMargin( 5, 0, 0, 0 );
        scroll.contentContainer.SetPadding( 5 );
        scroll.style.backgroundColor = new Color( 0.15f, 0.15f, 0.15f, 1.0f );
        scroll.SetBorderRadius( 4 );
        scroll.style.maxHeight = 400;
        var bodies = new Foldout() { text = "Bodies", value = false };
        foreach ( var body in go.GetComponentsInChildren<RigidBody>() ) {
          bodies.Add( new Label( body.name ) );
        }
        scroll.Add( bodies );

        var constraints = new Foldout() { text = "Constraints" };
        foreach ( var constraint in go.GetComponentsInChildren<Constraint>() ) {
          var constraintRow = new VisualElement();
          constraintRow.style.flexDirection = FlexDirection.Row;
          constraintRow.style.justifyContent = Justify.SpaceBetween;
          constraintRow.Add( new Label( constraint.name ) );
          var reconfigureData = new VisualElement();
          reconfigureData.style.flexDirection = FlexDirection.Row;
          var value = new FloatField() { value = constraint.GetCurrentAngle() };
          value.SetEnabled( false );
          value.style.width = 100;

          var enable = new Toggle() { value = false };
          enable.RegisterValueChangedCallback( ce =>
          {
            value.SetEnabled( ce.newValue );
            if ( ce.newValue )
              m_reconfigureValues[ constraint ] = value.value;
            else
              m_reconfigureValues.Remove( constraint );
          } );

          value.RegisterValueChangedCallback( ce =>
          {
            if ( enable.value )
              m_reconfigureValues[ constraint ] = ce.newValue;
          } );

          reconfigureData.Add( enable );
          reconfigureData.Add( value );
          constraintRow.Add( reconfigureData );

          constraints.Add( constraintRow );

        }
        scroll.Add( constraints );
        reconfigureGUI.Add( scroll );
        var reconfigureButton = new Button( Reconfigure ) { text = "Reconfigure" };
        reconfigureButton.SetMargin( 5, 0, 5, 0 );
        reconfigureGUI.Add( reconfigureButton );
      }
      return reconfigureGUI;
    }

    private void RecordUndo( ScriptComponent[] comps )
    {
      var undoObjects = comps.Cast<UnityEngine.Object>().Union( comps.Select( c => c.gameObject ) ).Union( comps.Select( c => c.transform ) );
      Undo.RecordObjects( undoObjects.ToArray(), "Edit-time step" );
    }

    private void EditTimeStep()
    {
      using ( var tmpSim = new Simulation.TemporarySimulation() ) {
        RecordUndo( tmpSim.Components );
        using ( new DisableSteppingScope() )
          Simulation.Instance.DoStep();
        foreach ( var constraint in tmpSim.Components.Where( c => c is Constraint && c.enabled ) )
          PatchConstraint( (Constraint)constraint );
      }
    }

    private void EditTimeStepFor( float time )
    {
      using ( var tmpSim = new Simulation.TemporarySimulation() ) {
        RecordUndo( tmpSim.Components );
        var sim = Simulation.Instance;
        var start = sim.Native.getTimeStamp();
        using ( new DisableSteppingScope() ) {
          while ( sim.Native.getTimeStamp() - start < time ) {
            var prog = sim.Native.getTimeStamp() - start;
            EditorUtility.DisplayProgressBar( $"Stepping for {time}s", $"Stepping simulation {prog:f2}s/{time}s", (float)prog / time );
            Simulation.Instance.DoStep();
          }
        }
        //foreach ( var constraint in tmpSim.Components.Where( c => c is Constraint && c.enabled ) )
        //  PatchConstraint( (Constraint)constraint );
      }
      EditorUtility.ClearProgressBar();
    }

    private void PatchConstraint( Constraint constraint )
    {
      float position = (float)constraint.GetCurrentAngle();

      var range = constraint.GetController<RangeController>();
      if ( range != null )
        range.Range = new RangeReal( range.Range.Min - position, range.Range.Max - position );

      var lockc = constraint.GetController<LockController>();
      if ( lockc != null )
        lockc.Position -= position;

      constraint.AttachmentPair.Synchronized = false;
      bool sync = constraint.ConnectedFrameNativeSyncEnabled;
      constraint.ConnectedFrameNativeSyncEnabled = true;
      typeof( Constraint ).InvokeMember( "SynchronizeNativeFramesWithAttachmentPair", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.InvokeMethod, Type.DefaultBinder, constraint, null );
      constraint.ConnectedFrameNativeSyncEnabled = sync;
    }

    private void Reconfigure()
    {
      using ( var tmpSim = new Simulation.TemporarySimulation() ) {
        RecordUndo( tmpSim.Components );
        var rc = new agxUtil.ReconfigureRequest();
        var collection = new agxSDK.Collection();

        Tuple<RigidBody,float> maxMassBody = Tuple.Create<RigidBody,float>(null,float.NegativeInfinity);
        foreach ( var body in m_reconfigureTarget.GetComponentsInChildren<RigidBody>() ) {
          collection.add( body.Native );
          if ( body.Native.getMassProperties().getMass() > maxMassBody.Item2 )
            maxMassBody = Tuple.Create( body, (float)body.Native.getMassProperties().getMass() );
        }

        foreach ( var constraint in m_reconfigureTarget.GetComponentsInChildren<Constraint>() )
          collection.add( constraint.Native );

        foreach ( var twobodytire in m_reconfigureTarget.GetComponentsInChildren<TwoBodyTire>() )
          collection.add( twobodytire.Native );

        var constraintPositions = new agxUtil.ConstraintPositionVector();

        foreach ( var (constraint, value) in m_reconfigureValues ) {
          constraintPositions.Add( new agxUtil.ConstraintPosition( constraint.Native, value ) );
        }

        var bodyTransforms = new agxUtil.BodyTransformVector();
        rc.computeTransforms( collection, maxMassBody.Item1.Native, constraintPositions, bodyTransforms );
        rc.applyTransforms( collection, constraintPositions, bodyTransforms );
        Simulation.Instance.StepCallbacks.PostSynchronizeTransforms.Invoke();
        Simulation.Instance.StepCallbacks.PostStepForward?.Invoke();

        foreach ( var constraint in m_reconfigureTarget.GetComponentsInChildren<Constraint>() )
          PatchConstraint( constraint );
      }
    }
  }
}
