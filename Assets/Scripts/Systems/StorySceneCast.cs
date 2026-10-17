using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>推演与侧院陪同角色的最小舞台表现，复用现有立绘与证据素材。</summary>
public static class StorySceneCast
{
    public static void Run(string action, BackgroundController background, GameObject owner)
    {
        if (action == "MeetGirl") ShowYard(background.CurrentRoot, false);
        if (action == "CastYard") ShowYard(background.CurrentRoot, true);
        if (action == "Footage" || action == "ReplayDrink" || action == "StreetReplay")
        {
            var player = owner.GetComponent<InvestigationCutscene>();
            if (player == null) player = owner.AddComponent<InvestigationCutscene>();
            player.Show(action == "Footage", action == "StreetReplay");
        }
    }

    public static void ShowYard(Transform root, bool companions)
    {
        if (root == null) return;
        SpriteRenderer background = root.Find("背景")?.GetComponent<SpriteRenderer>();
        if (background == null) return;
        Spawn(root, background.bounds, "守宫妹妹", .72f);
        if (companions) { Spawn(root, background.bounds, "松鼠店员", .53f); Spawn(root, background.bounds, "仓鼠", .17f); }
    }
    private static void Spawn(Transform root, Bounds bounds, string id, float x)
    {
        Transform actor = root.Find("SceneCast_" + id);
        if (actor != null) { actor.gameObject.SetActive(true); return; }
        Sprite sprite = PortraitLoader.Load(id); if (sprite == null) return;
        var go = new GameObject("SceneCast_" + id,typeof(SpriteRenderer)); go.transform.SetParent(root,false);
        var renderer = go.GetComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sortingOrder = 5;
        float scale = bounds.size.y * .32f / sprite.bounds.size.y;
        go.transform.localScale = Vector3.one * scale / Mathf.Max(.0001f,root.lossyScale.x);
        go.transform.position = new Vector3(Mathf.Lerp(bounds.min.x,bounds.max.x,x),bounds.min.y+bounds.size.y*.3f,backgroundZ(root)-.2f);
        if (id == "守宫妹妹" && root.Find("石头") != null)
            go.transform.position = root.Find("石头").position + new Vector3(0, bounds.size.y*.18f, -.2f);
    }
    private static float backgroundZ(Transform root) => root.Find("背景") != null ? root.Find("背景").position.z : 0;
}

public sealed class InvestigationCutscene : MonoBehaviour
{
    private GameObject panel;
    private Coroutine playing;
    public void Show(bool footage, bool street = false)
    {
        if (playing != null) StopCoroutine(playing);
        if (panel != null) Destroy(panel);
        playing = StartCoroutine(Play(footage, street));
    }
    private IEnumerator Play(bool footage, bool street)
    {
        Canvas canvas = null;
        foreach(Canvas item in FindObjectsByType<Canvas>()) if(item.name=="Canvas" && item.gameObject.scene==gameObject.scene) canvas=item;
        if(canvas==null)yield break;
        panel=new GameObject("Reconstruction",typeof(RectTransform),typeof(Image));panel.transform.SetParent(canvas.transform,false);
        var rect=panel.GetComponent<RectTransform>();rect.anchorMin=new Vector2(.28f,.49f);rect.anchorMax=new Vector2(.75f,.84f);rect.offsetMin=rect.offsetMax=Vector2.zero;
        panel.GetComponent<Image>().color=new Color(.93f,.9f,.78f,.95f);panel.GetComponent<Image>().raycastTarget=false;
        Image girl=AddImage("PastGirl",PortraitLoader.Load("守宫妹妹"),new Vector2(.32f,.48f),new Vector2(120,145));girl.color=new Color(.7f,.75f,.65f,.6f);
        Image drink=null;
        if(!footage && !street)drink=AddImage("Drink",Resources.LoadAll<Sprite>("Art/证据Level1/第一章证据/ST01豆奶").FirstOrDefault(),new Vector2(.6f,.4f),new Vector2(60,60));
        if(street)AddImage("PastHamster",PortraitLoader.Load("仓鼠"),new Vector2(.65f,.48f),new Vector2(120,145));
        var label=new GameObject("TimeLabel",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
        var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=new Vector2(0,.82f);labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        var text=label.GetComponent<Text>();text.text=footage?"监控片段 · 昨天（静帧）":street?"小毛的推演 · 昨天":"小毛的推演 · 前几天";text.font=GameFont.Primary;text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;text.color=Color.black;text.raycastTarget=false;
        yield return new WaitForSecondsRealtime(1);
        if(footage) girl.rectTransform.anchoredPosition=new Vector2(-80,0); else if(drink!=null)drink.gameObject.SetActive(false);
        yield return new WaitForSecondsRealtime(1);
        Destroy(panel);panel=null;playing=null;
    }
    private Image AddImage(string name,Sprite sprite,Vector2 anchor,Vector2 size)
    {
        var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(panel.transform,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=anchor;rect.sizeDelta=size;
        var image=go.GetComponent<Image>();image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;return image;
    }
    private void OnDisable(){if(panel!=null)Destroy(panel);}
}
