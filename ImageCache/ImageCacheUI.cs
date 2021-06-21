using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class ImageCacheUI : MonoBehaviour
{
    Sprite sourceSprite = null;
    Sprite cachedSprite = null;
    Image image;

    private void Start()
    {
        if (ImageCacheSettings.Instance == null) this.enabled = false;
    }

    Sprite GetCurrentSprite()
    {
        if (image == null) image = GetComponent<Image>();
        if (image.overrideSprite != null) return image.overrideSprite;
        return image.sprite;
    }
    Sprite CurrentSprite => GetCurrentSprite();

    private void Update()
    {
        if (CurrentSprite == cachedSprite || CurrentSprite == sourceSprite) return;

        this.sourceSprite = CurrentSprite;
        if (this.sourceSprite == null)
        {
            if (image.overrideSprite != null) image.overrideSprite = this.cachedSprite;
            else image.sprite = null;
        }
        else
        {
            this.cachedSprite = ImageCacheSettings.Instance.CreateSpriteFromCache(this.sourceSprite);
            if (this.cachedSprite != null)
            {
                this.sourceSprite = this.cachedSprite;
                if (image.overrideSprite != null) image.overrideSprite = this.cachedSprite;
                else image.sprite = this.cachedSprite;
            }
        }
    }
}