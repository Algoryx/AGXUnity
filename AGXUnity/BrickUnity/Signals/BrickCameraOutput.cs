using System;
using UnityEngine;

public class BrickCameraOutput : MonoBehaviour
{
  public Brick.Signal.CameraOutput b_cameraOutput;
  private Material mat;
  private RenderTexture renderTexture;
  private Texture2D texture2D;
  private Rect rect;
  private Camera m_camera;

  private void Start()
  {
    m_camera = GetComponent<Camera>();

    var b_image = b_cameraOutput.Image;
    int width = (int)b_image.Width;
    int height = (int)b_image.Height;

    renderTexture = new RenderTexture(width, height, 24);
    m_camera.targetTexture = renderTexture;
    texture2D = new Texture2D(width, height, TextureFormat.RGB24, false);
    rect = new Rect(0, 0, width, height);

    b_cameraOutput.ImageRequested += OnImageRequest;

    // To control the rendering, the camera needs to be disabled.
    // It renders automatically otherwise
    m_camera.enabled = false;
  }

  private void OnImageRequest(object sender, EventArgs e)
  {
    m_camera.Render();
  }

  private void OnPostRender()
  {
    RenderTexture.active = renderTexture;
    texture2D.ReadPixels(rect, 0, 0);
    var numBytes = b_cameraOutput.Image.Data.Length;
    Array.Copy(texture2D.GetRawTextureData(), b_cameraOutput.Image.Data, numBytes);
  }

  private void OnDestroy()
  {
    if (b_cameraOutput != null)
      b_cameraOutput.ImageRequested -= OnImageRequest;
  }
}

