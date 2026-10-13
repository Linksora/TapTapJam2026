using System.Collections.Generic;
using System.Text.RegularExpressions;
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

            AddToIndex(sprite, sprite.name);

            // Multiple 模式下 Unity 给自动切出的子精灵追加 _0/_1 后缀（如 小毛_0），
            // CSV 里只写文件名（小毛），这里登记一个去掉后缀的别名，两种写法都能命中。
            string alias = StripSliceSuffix(sprite.name);
            if (alias != sprite.name)
            {
                AddToIndex(sprite, alias);
            }
        }
    }

    private static void AddToIndex(Sprite sprite, string key)
    {
        if (artIndex.TryGetValue(key, out Sprite existing) && existing != sprite)
        {
            Debug.LogWarning($"[PortraitLoader] Resources/Art 下存在同名图片：{key}，" +
                             "按文件名加载只会取到其中一张，请给不同图片起不同文件名。");
            return;
        }

        artIndex[key] = sprite;
    }

    private static string StripSliceSuffix(string spriteName)
    {
        // 仅去掉结尾的 _数字（Unity 切片自动命名），普通文件名里的下划线不受影响。
        return Regex.Replace(spriteName, @"_\d+$", string.Empty);
    }
}
