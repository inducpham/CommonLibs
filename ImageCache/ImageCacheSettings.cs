using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu]
public class ImageCacheSettings : ScriptableObject
{
    public static ImageCacheSettings Instance => FindInstance();
    static ImageCacheSettings instance;
    static ImageCacheSettings FindInstance()
    {
        if (instance != null) return instance;

        var objs = Resources.FindObjectsOfTypeAll<ImageCacheSettings>();
        if (objs.Length <= 0) return null;

        instance = objs[0];
        return instance;
    }

    [System.Serializable]
    public class MapItem
    {
        public Sprite sprite;
        public TextAsset binarySprite;
    }

    public List<string> matchingPatterns = new List<string>();
    public List<MapItem> mapItems = new List<MapItem>();
    Dictionary<Sprite, TextAsset> map = null;

    public Sprite CreateSpriteFromCache(Sprite referenceSprite)
    {
        if (map == null) Remap();
        if (map.ContainsKey(referenceSprite) == false) return referenceSprite;

        var texture = new Texture2D(1, 1);
        texture.LoadImage(map[referenceSprite].bytes);
        return Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one / 2);
    }

    void Remap()
    {
        map = new Dictionary<Sprite, TextAsset>();
        foreach (var item in mapItems) map[item.sprite] = item.binarySprite;
    }

}
