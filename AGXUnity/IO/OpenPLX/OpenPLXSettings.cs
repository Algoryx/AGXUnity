using System.Collections.Generic;

namespace AGXUnity.IO.OpenPLX
{
  [RuntimeSettings]
  public class OpenPLXSettings : AGXUnitySettings<OpenPLXSettings>
  {
    public List<string> AdditionalBundleDirs = new List<string>();
  }
}
