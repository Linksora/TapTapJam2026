using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

[RequireComponent(typeof(SpriteRenderer))]
public sealed class MouseCursorController : MonoBehaviour
{
    [SerializeField] private Camera worldCamera;
    [SerializeField] private float interactiveScale = 1.18f;
    private Vector3 baseScale;

    private void Awake()
    {
        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        baseScale = transform.localScale;
    }

    private void OnEnable()
    {
        Cursor.visible = false;
    }

    private void LateUpdate()
    {
        if (worldCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 screenPosition = Mouse.current.position.ReadValue();
        Vector3 worldPosition = worldCamera.ScreenToWorldPoint(
            new Vector3(screenPosition.x, screenPosition.y, -worldCamera.transform.position.z));
        worldPosition.z = transform.position.z;
        transform.position = worldPosition;

        bool overInteractive = IsOverInteractiveWorldObject(screenPosition);
        transform.localScale = baseScale * (overInteractive ? interactiveScale : 1f);
    }

    private bool IsOverInteractiveWorldObject(Vector2 screenPosition)
    {
        // 指针在 UI 上时不把下方世界物体误判为可交互目标。
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return false;
        }

        Vector3 worldPoint = worldCamera.ScreenToWorldPoint(screenPosition);
        Collider2D hit = Physics2D.OverlapPoint(worldPoint);
        return hit != null &&
               (hit.GetComponentInParent<WorldItem>() != null || hit.GetComponentInParent<ItemReceiver>() != null);
    }

    private void OnDisable()
    {
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        Cursor.visible = !hasFocus;
    }
}
