using System.Runtime.InteropServices;
using UnityEngine;

namespace AGXUnity.Utils
{
  // Create a union type for matrices to allow for more efficient conversion between AffineMatrix4x4f and Matrix4x4
  [StructLayout( LayoutKind.Explicit )]
  public class MatrixUnion
  {
    [FieldOffset(0)]
    public Matrix4x4[] unityMatrices;
    [FieldOffset(0)]
    public agx.AffineMatrix4x4f[] agxMatrices;
  }
}