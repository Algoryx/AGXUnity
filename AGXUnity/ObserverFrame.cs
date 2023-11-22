using AGXUnity.Utils;
using UnityEngine;

namespace AGXUnity
{
  /// <summary>
  /// Observer frame object mainly created in other applications
  /// to communicate reference transforms. This object is often
  /// only created when reading .agx files.
  /// </summary>
  [AddComponentMenu("AGXUnity/Observer Frame")]
  public class ObserverFrame : ScriptComponent
  {
    /// <summary>
    /// Native instance of the observer frame - created in Start/Initialize if valid.
    /// </summary>
    public agx.ObserverFrame Native { get; private set; } = null;

    public void RestoreLocalDataFrom(agx.ObserverFrame native, GameObject parent)
    {
      transform.SetParent(parent != null ? parent.transform : null);
      transform.position = native.getPosition().ToHandedVector3();
      transform.rotation = native.getRotation().ToHandedQuaternion();
    }

    protected override bool Initialize()
    {
      Native = new agx.ObserverFrame();

      Native.setName(name);

      var rb = gameObject.GetInitializedComponentInParent<RigidBody>();
      Native.attachWithWorldTransform(rb != null ? rb.Native : null,
                                       new agx.AffineMatrix4x4(transform.rotation.ToHandedQuat(),
                                                                transform.position.ToHandedVec3()));

      GetSimulation().add(Native);

      return true;
    }

    protected override void OnDestroy()
    {
      if (Simulation.HasInstance)
        GetSimulation().remove(Native);
      
      Native = null;
      base.OnDestroy();
    }

    protected void OnDrawGizmos()
    {
      bool RightHanded = false;
      DrawLine( transform.up, Color.green );
      DrawLine( transform.right * ( RightHanded ? -1 : 1 ), Color.red );
      DrawLine( transform.forward, Color.blue );
    }

    private void DrawLine( Vector3 direction, Color color )
    {
      float Alpha = 1.0f;
      float Size = .25f;
      int LineDivisions = 7;
      float width = 1.5f;

      int count = 1 + Mathf.CeilToInt(width);
      int segments = LineDivisions * 2 - 1;

      Gizmos.color = new Color( color.r, color.g, color.b, Alpha );

      Camera c = Camera.current;
      Vector3 start = transform.position;
      Vector3 end = transform.position + Size * direction;
      var startSS = c.WorldToScreenPoint(start);
      var endSS = c.WorldToScreenPoint(end);
      Vector3 v1 = (endSS - startSS).normalized;      // line direction
      Vector3 n = Vector3.Cross(v1, Vector3.forward); // normal vector

      // Draw parallel lines to increase thickness
      for ( int w = 0; w < count; w++ ) {
        Vector3 o = ((float)w / (count - 1) - 0.5f) * 0.99f * width * n;

        Vector3 s = c.ScreenToWorldPoint(startSS + o);
        Vector3 e = c.ScreenToWorldPoint(endSS + o);

        // Draw segments
        for ( int i = 0; i < segments; i += 2 ) {
          Vector3 p1 = s + ((float)i / segments) * (e - s);
          Vector3 p2 = s + ((i + 1.0f) / segments) * (e - s);
          Gizmos.DrawLine( p1, p2 );
        }
      }
    }
  }
}
