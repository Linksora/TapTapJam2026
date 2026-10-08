using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 按住左右方向键（或 A/D）水平移动相机，视野被夹在背景图边界内。
/// 挂在 Main Camera 上，背景精灵用于计算可移动范围。
/// </summary>
public sealed class ScenePanner : MonoBehaviour
{
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private float moveSpeed = 300f;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float edgePadding = 0.3f;

    private Camera targetCamera;
    private float minX;
    private float maxX;
    private float dampVelocity;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        if (background == null)
        {
            GameObject backgroundObject = GameObject.Find("背景");
            if (backgroundObject != null)
            {
                background = backgroundObject.GetComponent<SpriteRenderer>();
            }
        }

        if (background == null || targetCamera == null || !targetCamera.orthographic)
        {
            enabled = false;
            return;
        }

        RecalculateBounds();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        float direction = 0f;
        if (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed)
        {
            direction -= 1f;
        }

        if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed)
        {
            direction += 1f;
        }

        Vector3 position = transform.position;
        float targetX = Clamp(position.x + direction * moveSpeed * Time.deltaTime);
        position.x = Mathf.SmoothDamp(position.x, targetX, ref dampVelocity, smoothTime);
        position.x = Clamp(position.x);
        transform.position = position;
    }

    private void RecalculateBounds()
    {
        Bounds bounds = background.bounds;
        float halfViewWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float scrollRange = Mathf.Max(0f, bounds.extents.x - halfViewWidth - edgePadding);
        minX = bounds.center.x - scrollRange;
        maxX = bounds.center.x + scrollRange;
    }

    private float Clamp(float x)
    {
        return Mathf.Clamp(x, minX, maxX);
    }
}
