using System;
using System.Reflection;
using System.Linq;
using UnityEngine;
using UnityEditor;
using AGXUnity;
using AGXUnity.Utils;

namespace AGXUnityEditor.Utils
{
  // TODO GUI: Remove this file.
  public partial class GUI : AGXUnity.Utils.GUI
  {
    public static class Symbols
    {
      public const char ToggleEnabled           = '\u2714';
      public const char ToggleDisabled          = ' ';

      public const char ArrowRight              = '\u21D2';
      public const char ArrowLeftRight          = '\u2194';

      public const char ShapeResizeTool         = '\u21C4';
      public const char ShapeCreateTool         = '\u210C';
      public const char ShapeVisualCreateTool   = '\u274D';

      public const char SelectInSceneViewTool   = 'p';
      public const char SelectPointTool         = '\u22A1';
      public const char SelectEdgeTool          = '\u2196';
      public const char PositionHandleTool      = 'L';

      public const char ConstraintCreateTool    = '\u2102';

      public const char DisableCollisionsTool   = '\u2229';

      public const char ListInsertElementBefore = '\u21B0';
      public const char ListInsertElementAfter  = '\u21B2';
      public const char ListEraseElement        = 'x';

      public const char Synchronized            = '\u2194';

      public const char CircleArrowAcw          = '\u21ba';
    }
  }
}
