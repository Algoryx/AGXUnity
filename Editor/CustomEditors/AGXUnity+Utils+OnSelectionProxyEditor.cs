
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.Utils.OnSelectionProxy ) )]
  [CanEditMultipleObjects]
  public class AGXUnityUtilsOnSelectionProxyEditor : InspectorEditor
  { }
}