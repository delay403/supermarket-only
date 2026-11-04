using System.Collections;
using System.Collections.Generic;
using Game.UI.Preview;
using UnityEngine;
using UnityEngine.UI;

public class RawImageCamera: MonoBehaviour
{
    public Camera camera;
    public RawImage rawImage;

    // 摄像机专用渲染纹理
    private RenderTexture renderTexture;

    void OnEnable()
    {
        var cam = camera != null ? camera : CosmeticPreviewController.Instance?.PreviewCamera;
        if (cam == null || rawImage == null)
            return;

        // 创建一张和 RawImage 一样大的渲染纹理
        var w = Mathf.Max(256, (int)rawImage.rectTransform.rect.width);
        var h = Mathf.Max(256, (int)rawImage.rectTransform.rect.height);
        renderTexture = new RenderTexture(w, h, 24);
        
        // 让摄像机把画面渲染到这张纹理上
        cam.targetTexture = renderTexture;
        
        // 赋值给你的 texture 变量
        rawImage.texture = renderTexture;
        rawImage.uvRect = new Rect(0f, 0f, 1f, 1f);
    }

    void OnDisable()
    {
        var cam = camera != null ? camera : CosmeticPreviewController.Instance?.PreviewCamera;
        if (cam != null && cam.targetTexture == renderTexture)
            cam.targetTexture = null;

        if (renderTexture != null)
        {
            renderTexture.Release();
            Destroy(renderTexture);
            renderTexture = null;
        }
    }
}