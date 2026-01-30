using System;
using UnityEngine;

namespace AGXUnity
{
  [AddComponentMenu( "" )]
  [HelpURL( "https://us.download.algoryx.se/AGXUnity/documentation/current/editor_interface.html#openplx-import" )]
  public class Generic1DOFControlledConstraint : Constraint
  {
    public enum SecondaryType
    {
      Lock,
      TargetSpeed,
      Range
    }

    [SerializeField]
    private SecondaryType m_controller = SecondaryType.Lock;

    [InspectorPriority( -1 )]
    public SecondaryType Controller
    {
      get => m_controller;
      set
      {
        if ( value != m_controller ) {
          if ( Native != null ) {
            Debug.LogWarning( "Cannot set controller type at runtime" );
            return;
          }
          m_controller = value;

          RecreateElementary();
        }
      }
    }

    private void Reset()
    {
      Type = ConstraintType.GenericConstraint1DOF;
      RecreateElementary();
    }

    public Generic1DOFControlledConstraint()
    {
      Type = ConstraintType.GenericConstraint1DOF;
      RecreateElementary();
    }

    private void RecreateElementary()
    {
      var old = ConstraintController;
      ConstraintController = System.Activator.CreateInstance( System.Type.GetType( "AGXUnity." + NativeControllerType.Name ) ) as ElementaryConstraintController;
      ConstraintController.Construct( NativeControllerName, true, 1 );
      if ( old != null ) {
        ConstraintController.Enable = old.Enable;
        ConstraintController.Compliance = old.Compliance;
        ConstraintController.Damping = old.Damping;
        ConstraintController.ForceRange = old.ForceRange;
      }

      m_elementaryConstraintsNew.Clear();
      m_elementaryConstraintsNew.Add( ConstraintController );
    }

    private agx.Angle ConstraintAngle => DOFType == agx.Angle.Type.ROTATIONAL ? new agx.RotationalAngle( DOFAxis ) : new agx.SeparationAngle( DOFAxis );
    private System.Type NativeControllerType => System.Type.GetType( "agx." + Enum.GetName( typeof( SecondaryType ), Controller ) + "Controller, agxDotNet" );
    private string NativeControllerName
    {
      get
      {
        var prefix = DOFType switch
        {
          agx.Angle.Type.ROTATIONAL => "R",
          agx.Angle.Type.TRANSLATIONAL => "T",
          _ => "Unknown"
        };
        var postfix = Controller switch
        {
          SecondaryType.TargetSpeed => "M",
          SecondaryType.Lock => "L",
          SecondaryType.Range => "R",
          _ => "Unknown"
        };
        return prefix + postfix;
      }
    }

    public agx.Angle.Axis DOFAxis = agx.Angle.Axis.N;

    [SerializeField]
    private agx.Angle.Type m_DOFType = agx.Angle.Type.ROTATIONAL;
    public agx.Angle.Type DOFType
    {
      get => m_DOFType;
      set
      {
        if ( value != m_DOFType ) {
          if ( Native != null ) {
            Debug.LogWarning( "Cannot set DOF type at runtime" );
            return;
          }
          m_DOFType = value;

          if ( ConstraintController == null )
            RecreateElementary();
          else
            ConstraintController.NativeName = NativeControllerName;
        }
      }
    }

    [SerializeReference]
    public ElementaryConstraintController ConstraintController;

    protected override agx.Constraint CreateNative( RigidBody rb1, agx.Frame f1, RigidBody rb2, agx.Frame f2 )
    {
      agx.Angle angle = ConstraintAngle;
      agx.ConstraintAngleBasedData cabd = new agx.ConstraintAngleBasedData(null, angle);

      var nativeController = (agx.BasicControllerConstraint)Activator.CreateInstance( NativeControllerType, new object[] { cabd } );
      nativeController.setName( NativeControllerName );

      var native = (agx.Constraint)new agx.SingleControllerConstraint1DOF( rb1.Native, f1, ( rb2 != null ? rb2.Native : null ), f2, nativeController);

      return native;
    }
  }
}
