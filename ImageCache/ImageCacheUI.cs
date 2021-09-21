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
        return image.sprite;
    }
    Sprite CurrentSprite => GetCurrentSprite();

    private void Update()
    {
        if (ImageCacheSettings.CacheEnabled == false) return;
        if (CurrentSprite == cachedSprite || CurrentSprite == sourceSprite) return;

        this.sourceSprite = CurrentSprite;
        if (this.sourceSprite == null)
        {
            image.sprite = null;
        }
        else
        {
            this.cachedSprite = ImageCacheSettings.Instance.CreateSpriteFromCache(this.sourceSprite);
            if (this.cachedSprite != null)
            {
                this.sourceSprite = this.cachedSprite;
                image.sprite = this.cachedSprite;
            }
        }
    }
}