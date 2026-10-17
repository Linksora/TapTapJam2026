using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public sealed class GameAudioButton : MonoBehaviour
{
    private Button button;

    private void Awake() => Bind();

    // 选项按钮复用时会清空 onClick，需要重新绑定。
    public void Bind()
    {
        if (button == null) button = GetComponent<Button>();
        button.onClick.RemoveListener(Play);
        button.onClick.AddListener(Play);
    }

    private void Play() => GameAudio.Instance.Play(GameSound.Click);

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(Play);
    }
}
