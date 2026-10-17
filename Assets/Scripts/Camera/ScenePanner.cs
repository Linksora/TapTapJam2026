using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// 水平移动相机：键盘 ←→/A D 或按住界面上的左右按钮，视野被夹在背景图边界内。
/// 挂在 Main Camera 上，背景精灵用于计算可移动范围。
/// 键盘按下时，对应按钮同步显示 Button 的 Pressed 视觉状态。
/// </summary>
public sealed class ScenePanner : MonoBehaviour
{
    [SerializeField] private SpriteRenderer background;
    [SerializeField] private float moveSpeed = 300f;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float edgePadding = 0.3f;
    // 按钮一律按物体名查找，不做 Inspector 序列化接线：
    // 场景复制/按钮删改后引用不会丢，新建场景只要保留这两个名字即可。
    private const string LeftButtonName = "LeftButton";
    private const string RightButtonName = "RightButton";

    private HoldButton leftHoldButton;
    private HoldButton rightHoldButton;

    private Camera targetCamera;
    private float minX;
    private float maxX;
    private float dampVelocity;
    private Bounds lastBackgroundBounds;
    private float lastOrthographicSize;
    private float lastAspect;
    private float lastEdgePadding;

    private PointerEventData syntheticPointer;
    private bool keyLeftVisual;
    private bool keyRightVisual;
    public bool NavigationMode { get; set; }

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

        RefreshBounds();
    }

    private void Update()
    {
        if (background == null || targetCamera == null || !targetCamera.orthographic) return;
        // Sprite 更换、背景移动/缩放、窗口比例和相机缩放都可能改变可移动范围。
        if (background != null && (background.bounds != lastBackgroundBounds
            || targetCamera.orthographicSize != lastOrthographicSize
            || targetCamera.aspect != lastAspect || edgePadding != lastEdgePadding))
            RefreshBounds();

        if (NavigationMode) return;

        ResolveButtons();

        Keyboard keyboard = Keyboard.current;
        bool keyLeft = keyboard != null && (keyboard.leftArrowKey.isPressed || keyboard.aKey.isPressed);
        bool keyRight = keyboard != null && (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed);
        bool touchLeft = leftHoldButton != null && leftHoldButton.IsPressed;
        bool touchRight = rightHoldButton != null && rightHoldButton.IsPressed;

        // 键盘按下时让按钮呈现 Pressed 状态；松开时恢复。
        ReflectKeyVisual(leftHoldButton, keyLeft, ref keyLeftVisual);
        ReflectKeyVisual(rightHoldButton, keyRight, ref keyRightVisual);

        float direction = 0f;
        if (keyLeft || touchLeft)
        {
            direction -= 1f;
        }

        if (keyRight || touchRight)
        {
            direction += 1f;
        }

        Vector3 position = transform.position;
        float targetX = Clamp(position.x + direction * moveSpeed * Time.deltaTime);
        position.x = Mathf.SmoothDamp(position.x, targetX, ref dampVelocity, smoothTime);
        position.x = Clamp(position.x);
        transform.position = position;
    }

    private void OnDisable()
    {
        // 组件被停用时恢复按钮视觉，避免按钮卡在 Pressed 状态。
        if (keyLeftVisual)
        {
            ReflectKeyVisual(leftHoldButton, false, ref keyLeftVisual);
        }

        if (keyRightVisual)
        {
            ReflectKeyVisual(rightHoldButton, false, ref keyRightVisual);
        }
    }

    /// <summary>
    /// 直接调用 Button 的指针回调，复用其 Pressed 颜色/贴图过渡（比 ExecuteEvents 精确，
    /// 不会误触发同一物体上 HoldButton 的按下逻辑）。
    /// </summary>
    private void ReflectKeyVisual(HoldButton holdButton, bool pressed, ref bool current)
    {
        if (holdButton == null || pressed == current)
        {
            return;
        }

        Button button = holdButton.Button;
        if (button == null)
        {
            return;
        }

        syntheticPointer ??= new PointerEventData(EventSystem.current)
        {
            button = PointerEventData.InputButton.Left
        };

        if (pressed)
        {
            button.OnPointerDown(syntheticPointer);
        }
        else
        {
            button.OnPointerUp(syntheticPointer);
        }

        current = pressed;
    }

    // 始终按物体名查找 LeftButton/RightButton；按钮被删除重建后下一帧自动恢复。
    private void ResolveButtons()
    {
        if (leftHoldButton == null || rightHoldButton == null)
        {
            HoldButton[] all = FindObjectsByType<HoldButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (HoldButton hold in all)
            {
                if (leftHoldButton == null && hold.gameObject.name == LeftButtonName)
                {
                    leftHoldButton = hold;
                }
                else if (rightHoldButton == null && hold.gameObject.name == RightButtonName)
                {
                    rightHoldButton = hold;
                }
            }
        }
    }

    /// <summary>背景分组切换后，绑定新底图并立即更新可移动范围。</summary>
    public void SetBackground(SpriteRenderer renderer, bool center = true)
    {
        background = renderer;
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        enabled = background != null && targetCamera != null && targetCamera.orthographic;
        if (!enabled) return;
        if (center)
        {
            Vector3 position = transform.position;
            position.x = background.bounds.center.x;
            transform.position = position;
        }
        RefreshBounds();
    }

    public void FocusAt(float normalizedX)
    {
        if (background == null) return;
        RefreshBounds();
        Vector3 position = transform.position;
        position.x = Clamp(Mathf.Lerp(background.bounds.min.x, background.bounds.max.x, Mathf.Clamp01(normalizedX)));
        transform.position = position;
        dampVelocity = 0;
    }

    public void PanInBackground(float direction)
    {
        if (background == null) return;
        Vector3 position = transform.position;
        position.x = Clamp(position.x + direction * moveSpeed * Time.unscaledDeltaTime);
        transform.position = position;
    }

    public void RefreshBounds()
    {
        if (targetCamera == null) targetCamera = GetComponent<Camera>();
        if (background == null || targetCamera == null || !targetCamera.orthographic) return;
        Bounds bounds = background.bounds;
        float halfViewWidth = targetCamera.orthographicSize * targetCamera.aspect;
        float scrollRange = Mathf.Max(0f, bounds.extents.x - halfViewWidth - Mathf.Max(0f, edgePadding));
        minX = bounds.center.x - scrollRange;
        maxX = bounds.center.x + scrollRange;
        lastBackgroundBounds = bounds;
        lastOrthographicSize = targetCamera.orthographicSize;
        lastAspect = targetCamera.aspect;
        lastEdgePadding = edgePadding;
        dampVelocity = 0f;
        Vector3 position = transform.position;
        position.x = Clamp(position.x);
        transform.position = position;
    }

    private float Clamp(float x)
    {
        return Mathf.Clamp(x, minX, maxX);
    }
}
