using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(SpriteRenderer))]
public class ImageCacheSprite : MonoBehaviour
{
    Sprite sourceSprite = null;
    Sprite cachedSprite = null;
    SpriteRenderer spriteRenderer;

    private void Start()
    {
        if (ImageCacheSettings.Instance == null) this.enabled = false;
    }

    Sprite GetCurrentSprite()
    {
        if (this.spriteRenderer == null) this.spriteRenderer = GetComponent<SpriteRenderer>();
        var sprite = this.spriteRenderer.sprite;
        return sprite;
    }
    Sprite CurrentSprite => GetCurrentSprite();

    private void Update()
    {
        if (CurrentSprite == cachedSprite || CurrentSprite == sourceSprite) return;

        this.sourceSprite = CurrentSprite;
        if (this.sourceSprite == null) spriteRenderer.sprite = null;
        else
        {
            this.cachedSprite = ImageCacheSettings.Instance.CreateSpriteFromCache(this.sourceSprite);
            spriteRenderer.sprite = this.cachedSprite;
        }
    }
}