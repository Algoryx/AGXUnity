using UnityEngine;


namespace AGXUnityEditor.Tools
{
  [CustomTool( typeof( AGXUnity.Utils.Plot ) )]
  public class PlotTool : CustomTargetTool
  {
    public PlotTool( Object[] targets ) : base( targets )
    {

    }

    public AGXUnity.Utils.Plot Plot { get => Targets[ 0 ] as AGXUnity.Utils.Plot; }

    public override void OnPostTargetMembersGUI()
    {
      using ( new AGXUnity.Utils.GUI.EnabledBlock( Plot.Native != null ) ) {
        if ( GUILayout.Button( "Open Plot Window" ) ) {
          Plot.OpenPlotWindow();
        }
      }
    }
  }
}
