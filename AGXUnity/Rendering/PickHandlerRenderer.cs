using UnityEngine;

namespace AGXUnity.Rendering
{
  [AddComponentMenu( "" )]
  public class PickHandlerRenderer : ScriptComponent
  {
    public GameObject ReferenceSphere { get; private set; }
    public GameObject ConnectedSphere { get; private set; }
    public GameObject ConnectingCylinder { get; private set; }

    public void ThisMethodIsntAllowedToBeNamedUpdateByUnity( Constraint constraint, Camera camera )
    {
      if ( constraint == null || State != States.INITIALIZED )
        return;

      if ( constraint.Type == ConstraintType.AngularLockJoint )
        return;

      var sphereRadius           = 0.05f;
      var cylinderRadius         = 0.5f * sphereRadius;
      var distReferenceConnected = Vector3.Distance( constraint.AttachmentPair.ReferenceFrame.Position,
                                                     constraint.AttachmentPair.ConnectedFrame.Position );
      var cameraScale            = Spawner.Utils.FindConstantScreenSizeScale( constraint.AttachmentPair.ReferenceFrame.Position,
                                                                              camera );
      ReferenceSphere.SetActive( true );
      ConnectedSphere.SetActive( true );
      ConnectingCylinder.SetActive( distReferenceConnected > 1.0E-4f );

      Spawner.Utils.SetSphereTransform( ReferenceSphere,
                                        constraint.AttachmentPair.ReferenceFrame.Position,
                                        Quaternion.identity,
                                        cameraScale * sphereRadius );

      Spawner.Utils.SetSphereTransform( ConnectedSphere,
                                        constraint.AttachmentPair.ConnectedFrame.Position,
                                        Quaternion.identity,
                                        cameraScale * sphereRadius );

      Spawner.Utils.SetCylinderTransform( ConnectingCylinder,
                                          constraint.AttachmentPair.ReferenceFrame.Position,
                                          constraint.AttachmentPair.ConnectedFrame.Position,
                                          cameraScale * cylinderRadius );
    }

    protected override bool Initialize()
    {
      const string shader = "Standard";

      if ( GetComponent<Constraint>().Type == ConstraintType.AngularLockJoint )
        return true;

      ReferenceSphere    = Spawner.Create( Spawner.Primitive.Sphere, "PHR_ReferenceSphere", HideFlags.HideAndDontSave, shader );
      ConnectedSphere    = Spawner.Create( Spawner.Primitive.Sphere, "PHR_ConnectedSphere", HideFlags.HideAndDontSave, shader );
      ConnectingCylinder = Spawner.Create( Spawner.Primitive.Cylinder, "PHR_ConnectingCylinder", HideFlags.HideAndDontSave, shader );

      ReferenceSphere.transform.SetParent( gameObject.transform );
      ConnectedSphere.transform.SetParent( gameObject.transform );
      ConnectingCylinder.transform.SetParent( gameObject.transform );

      Spawner.Utils.SetColor( ReferenceSphere, PickHandler.ReferenceSphereColor );
      Spawner.Utils.SetColor( ConnectedSphere, PickHandler.ConnectedSphereColor );
      Spawner.Utils.SetColor( ConnectingCylinder, PickHandler.ConnectingCylinderColor );

      // We'll update this active state in the Update method.
      ReferenceSphere.SetActive( false );
      ConnectedSphere.SetActive( false );
      ConnectingCylinder.SetActive( false );

      return true;
    }

    protected override void OnDestroy()
    {
      Spawner.Destroy( ReferenceSphere );
      Spawner.Destroy( ConnectedSphere );
      Spawner.Destroy( ConnectingCylinder );

      base.OnDestroy();
    }
  }
}
