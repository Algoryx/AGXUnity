using UnityEngine.UIElements;

namespace AGXUnityEditor.UIElements
{
  public class DependentToggle : Toggle
  {
    private bool selfValue;
    public DependentToggle( Toggle dependent )
    {
      selfValue = this.value;
      dependent.RegisterValueChangedCallback( ce => {
        value = ce.newValue ? selfValue : false;
        this.SetEnabled( ce.newValue );
      } );

      this.RegisterValueChangedCallback( ce => {
        if ( dependent.value )
          selfValue = ce.newValue;
      } );
    }
  }
}
