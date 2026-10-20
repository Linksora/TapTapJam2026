using System.Collections;
using UnityEngine;

public sealed partial class StoryManager
{
    private Coroutine delayedTransition;

    private void ScheduleDelayedTransition(DialogueData transition)
    {
        CancelDelayedTransition();
        pendingNextTarget = null;
        delayedTransition = StartCoroutine(PlayAfterDelay(transition));
    }

    private IEnumerator PlayAfterDelay(DialogueData transition)
    {
        // 使用游戏时间，暂停菜单暂停游戏时，不让主动对话在菜单后继续推进。
        yield return new WaitForSeconds(transition.HoldSeconds);
        delayedTransition = null;
        Goto(transition.NextTarget);
    }

    private void CancelDelayedTransition()
    {
        if (delayedTransition == null) return;
        StopCoroutine(delayedTransition);
        delayedTransition = null;
    }

    private void OnDisable() => CancelDelayedTransition();
}
