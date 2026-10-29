using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace AGXUnityEditor.Utils
{
  /// <summary>
  /// Discovery and preflight are read-only. Execution creates all replacements before
  /// removing sources. Unsupported dependencies block the entire plan.
  /// </summary>
  public static partial class PhysXConversion
  {
    public sealed class Candidate
    {
      public Component Source { get; internal set; }
      public Collider[] Colliders { get; internal set; } = new Collider[ 0 ];
      public List<string> Errors { get; } = new List<string>();
      public List<string> Warnings { get; } = new List<string>();
      public bool Supported => Errors.Count == 0;
      public int ComponentCount => 1 + Colliders.Length;
      public string Description => Source == null ? "Missing source" :
        Source.GetType().Name + ( Source is Rigidbody ? $" ({Colliders.Length} colliders)" : "" );
    }

    public sealed class Plan
    {
      public List<Candidate> Candidates { get; } = new List<Candidate>();
      public List<string> Errors { get; } = new List<string>();
      public List<string> Warnings { get; } = new List<string>();
      public int ComponentCount => Candidates.Sum( c => c.ComponentCount );
      public bool CanExecute => Candidates.Count > 0 && Errors.Count == 0;
    }

    public sealed class Result
    {
      public int Converted;
      public int Skipped;
      public List<string> Errors { get; } = new List<string>();
      public override string ToString() => $"Converted {Converted} components; skipped {Skipped}; errors {Errors.Count}." +
        ( Errors.Count == 0 ? "" : "\n" + string.Join( "\n", Errors ) );
    }

    public sealed class PrefabInfo
    {
      public string Path;
      public string Name;
      public int SupportedCount;
      public int UnsupportedCount;
      public string Error;
    }

  }
}
