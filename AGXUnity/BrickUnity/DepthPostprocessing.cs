using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DepthPostprocessing : MonoBehaviour
{
  private Material m_material;
  // Start is called before the first frame update
  private void Start()
  {
    Camera camera = GetComponent<Camera>();
    var depthShader = Shader.Find("Custom/DepthGrayscale");
    m_material = new Material(depthShader);
    camera.depthTextureMode = camera.depthTextureMode | DepthTextureMode.Depth;
  }

  void OnRenderImage(RenderTexture source, RenderTexture destination)
  {
    //draws the pixels from the source texture to the destination texture
    Graphics.Blit(source, destination, m_material);
  }
}
