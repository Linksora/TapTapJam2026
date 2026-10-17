using UnityEngine;

/// <summary>小狗专属调查点的像素鼻子标记；仅表示能力，不是可搜集的临时调查点。</summary>
public sealed class InvestigationNoseMarker : MonoBehaviour
{
    private static Sprite icon;
    public static void Show(Transform target)
    {
        Transform existing = target.Find("DogNoseMarker");
        if (existing != null) { existing.gameObject.SetActive(true); return; }
        SpriteRenderer source = target.GetComponent<SpriteRenderer>();
        if (source == null || source.sprite == null) return;
        if (icon == null)
        {
            string[] pixels = {"000111111000","001222222100","012223322210","122233332221","122233332221","012223322210","001222222100","000122221000","000012210000","000001100000"};
            var texture = new Texture2D(12,10,TextureFormat.RGBA32,false) { filterMode = FilterMode.Point };
            for (int y=0;y<10;y++) for(int x=0;x<12;x++) texture.SetPixel(x,9-y,pixels[y][x]=='0'?Color.clear:pixels[y][x]=='1'?new Color(.03f,.3f,.18f):pixels[y][x]=='2'?new Color(1,.93f,.72f):new Color(.8f,.45f,.5f));
            texture.Apply(); icon = Sprite.Create(texture,new Rect(0,0,12,10),new Vector2(.5f,.5f),100);
        }
        var go = new GameObject("DogNoseMarker",typeof(SpriteRenderer),typeof(InvestigationNoseMarker)); go.transform.SetParent(target,false);
        Bounds bounds = source.sprite.bounds;
        go.transform.localPosition = new Vector3(bounds.max.x,bounds.max.y,-.1f);
        go.transform.localScale = Vector3.one * Mathf.Max(.8f,bounds.size.y*.15f/icon.bounds.size.y);
        var renderer=go.GetComponent<SpriteRenderer>(); renderer.sprite=icon; renderer.sortingLayerID=source.sortingLayerID; renderer.sortingOrder=source.sortingOrder+10;
    }
}
