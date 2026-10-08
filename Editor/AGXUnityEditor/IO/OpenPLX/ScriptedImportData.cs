using UnityEngine;

namespace AGXUnityEditor.IO.OpenPLX
{
  public class ScriptedImportData : ScriptableObject
  {
    [SerializeField]
    public float ImportTime;
    [SerializeField]
    public string[] Depenencies;
  }
}
