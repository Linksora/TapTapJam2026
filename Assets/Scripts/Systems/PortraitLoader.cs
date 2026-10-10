using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按文件名加载立绘，带缓存。
/// 美术 PNG 放在 Assets/美术合集 下，执行菜单 Tools/同步美术到 Resources 后，
/// 会镜像到 Assets/Resources/Art（含子目录），这里按文件名（不带扩展名）查找，
/// CSV 里只需写文件名，不用关心它在哪个子文件夹。
/// </summary>
public static class PortraitLoader
{
    private const string ArtFolder = "Art";
    private const string LegacyFolder = "Portraits/";

    private static readonly Dictionary<string, Sprite> Cache = new();
    private static Dictionary<string, Sprite> artIndex;
    private static bool indexBuilt;

    public static Sprite Load(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return null;
        }

        string key = fileName.Trim();
        if (Cache.TryGetValue(key, out Sprite cached))
        {
            return cached;
        }

        Sprite sprite = FindInArtTree(key) ?? Resources.Load<Sprite>(LegacyFolder + key);
        if (sprite == null)
        {
            Debug.LogWarning($"[PortraitLoader] 找不到立绘：{key}（先执行 Tools/同步美术到 Resources）。");
        }

        Cache[key] = sprite;
        return sprite;
    }

    private static Sprite FindInArtTree(string key)
    {
        if (!indexBuilt)
        {
            BuildArtIndex();
        }

        return artIndex != null && artIndex.TryGetValue(key, out Sprite sprite) ? sprite : null;
    }

    private static void BuildArtIndex()
    {
        indexBuilt = true;
        artIndex = new Dictionary<string, Sprite>();

        Sprite[] all = Resources.LoadAll<Sprite>(ArtFolder);
        foreach (Sprite sprite in all)
        {
            if (sprite == null)
            {
                continue;
            }

            if (artIndex.TryGetValue(sprite.name, out Sprite existing) && existing != sprite)
            {
                Debug.LogWarning($"[PortraitLoader] Resources/Art 下存在同名图片：{sprite.name}，" +
                                 "按文件名加载只会取到其中一张，请给不同图片起不同文件名。");
                continue;
            }

            artIndex[sprite.name] = sprite;
        }
    }
}
