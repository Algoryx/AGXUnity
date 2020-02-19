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
      public const char ArrowRight              = '\u21D2';
      public const char ArrowLeftRight          = '\u2194';

      public const char ListInsertElementBefore = '\u21B0';
      public const char ListInsertElementAfter  = '\u21B2';
      public const char ListEraseElement        = 'x';

      public const char CircleArrowAcw          = '\u21ba';
    }
  }
}
