
using System;
using AGXUnity;
using AGXUnity.Collide;
using UnityEditor;

namespace AGXUnityEditor.Editors
{
  [CustomEditor( typeof( AGXUnity.IO.RestoredAGXFile ) )]
  [CanEditMultipleObjects]
  public class AGXUnityIORestoredAGXFileEditor : InspectorEditor
  { }
}