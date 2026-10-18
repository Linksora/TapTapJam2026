using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

/// <summary>播放短暂的剧情推演画面；角色立绘统一由 DialogueBox 的 Character 显示。</summary>
public static class StorySceneCast
{
    public static void Run(string action, GameObject owner)
    {
        if (action == "Footage" || action == "ReplayDrink" || action == "StreetReplay")
        {
            var player = owner.GetComponent<InvestigationCutscene>();
            if (player == null) player = owner.AddComponent<InvestigationCutscene>();
            player.Show(action == "Footage", action == "StreetReplay");
        }
    }
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
        Image drink=null;
        if(!footage && !street)drink=AddImage("Drink",Resources.LoadAll<Sprite>("Art/证据Level1/第一章证据/ST01豆奶").FirstOrDefault(),new Vector2(.6f,.4f),new Vector2(60,60));
        var label=new GameObject("TimeLabel",typeof(RectTransform),typeof(Text));label.transform.SetParent(panel.transform,false);
        var labelRect=label.GetComponent<RectTransform>();labelRect.anchorMin=new Vector2(0,.82f);labelRect.anchorMax=Vector2.one;labelRect.offsetMin=labelRect.offsetMax=Vector2.zero;
        var text=label.GetComponent<Text>();text.text=footage?"监控片段 · 昨天（静帧）":street?"小毛的推演 · 昨天":"小毛的推演 · 前几天";text.font=GameFont.Primary;text.fontSize=18;text.alignment=TextAnchor.MiddleCenter;text.color=Color.black;text.raycastTarget=false;
        yield return new WaitForSecondsRealtime(1);
        if(drink!=null)drink.gameObject.SetActive(false);
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
