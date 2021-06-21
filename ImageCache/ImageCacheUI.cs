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
        var sprite = image.sprite;
        return sprite;
    }
    Sprite CurrentSprite => GetCurrentSprite();

    private void Update()
    {
        if (CurrentSprite == cachedSprite || CurrentSprite == sourceSprite) return;

        this.sourceSprite = CurrentSprite;
        if (this.sourceSprite == null) image.sprite = null;
        else
        {
            this.cachedSprite = ImageCacheSettings.Instance.CreateSpriteFromCache(this.sourceSprite);
            image.sprite = this.cachedSprite;
        }
    }
}