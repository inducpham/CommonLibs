using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;

public static class SpriteAssetHelpers
{

    public static void SplitSelectingSprite(List<string> names, int columns)
    {
        var rows = (names.Count - 1) / columns + 1;

        var path = AssetDatabase.GetAssetPath(Selection.activeObject);
        var importer = (TextureImporter)TextureImporter.GetAtPath(path);
        if (importer == null) return;
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        var emote_width = texture.width / columns;
        var emote_height = texture.height / rows;

        importer.spriteImportMode = SpriteImportMode.Multiple;
        importer.textureType = TextureImporterType.Sprite;
        List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        int index = 0;
        int max_row = Mathf.CeilToInt(names.Count * 1.0f / columns) - 1;
        foreach (var name in names)
        {
            var col = index % columns;
            var row = index / columns;
            row = max_row - row;

            sprites.Add(new SpriteMetaData()
            {
                alignment = (int)SpriteAlignment.Center,
                border = new Vector4(0, 0, 0, 0),
                pivot = new Vector2(0.5f, 0.5f),
                name = name,
                rect = new Rect(emote_width * col, emote_height * row, emote_width, emote_height)
            });
            index++;
        }
        importer.spritesheet = sprites.ToArray();
        importer.SaveAndReimport();

    }
}
#endif