using AGXUnity.Sensor;
using UnityEngine;

namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( AmbientMaterial ) )]
  class AmbientMaterialTool : CustomTargetTool
  {
    public AmbientMaterial AmbientMaterial
    {
      get
      {
        return Targets[ 0 ] as AmbientMaterial;
      }
    }

    public AmbientMaterialTool( Object[] targets )
      : base( targets )
    {
    }

    public override void OnPreTargetMembersGUI()
    {
    }
  }
}
