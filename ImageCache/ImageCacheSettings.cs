using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;

[CreateAssetMenu]
public class ImageCacheSettings : ScriptableObject
{
    public static bool CacheEnabled = true;
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
        public AssetReference binarySpriteReference;
    }

    public string addressableGroupName;
    public List<string> matchingPatterns = new List<string>();
    public List<MapItem> mapItems = new List<MapItem>();
    Dictionary<Sprite, AssetReference> refMap = null;
    Dictionary<Sprite, Sprite> refMapInstance = null;

    public Sprite CreateSpriteFromCache(Sprite referenceSprite)
    {
        if (refMap == null) Remap();
        if (refMap.ContainsKey(referenceSprite) == false) return referenceSprite;
        if (refMapInstance.ContainsKey(referenceSprite)) return refMapInstance[referenceSprite];

        var ref_asset = refMap[referenceSprite];
        var textAsset = (TextAsset)ref_asset.Asset;

        if (textAsset == null) {
            var handle = ref_asset.LoadAssetAsync<TextAsset>();
            textAsset = handle.WaitForCompletion();
        }

        var texture = new Texture2D(1, 1);
        texture.LoadImage(textAsset.bytes);
        refMapInstance[referenceSprite] = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), Vector2.one / 2);
        return refMapInstance[referenceSprite];
    }
        
    void Remap()
    {
        refMap = new Dictionary<Sprite, AssetReference>();
        foreach (var item in mapItems) refMap[item.sprite] = item.binarySpriteReference;
        refMapInstance = new Dictionary<Sprite, Sprite>();
        TryMapReleaseAssets();
    }

    bool release_asset_mapped = false;
    void TryMapReleaseAssets()
    {
        if (release_asset_mapped) return;
        SceneManager.sceneUnloaded += (scene) => ReleaseAllAssets();
        release_asset_mapped = true;
    }    

    public void ReleaseAllAssets()
    {
        if (refMap == null) return;
        foreach (var ref_asset in refMap.Values) ref_asset.ReleaseAsset();
        refMapInstance = new Dictionary<Sprite, Sprite>();
    }

}
