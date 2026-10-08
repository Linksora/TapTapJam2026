using UnityEngine;

/// <summary>
/// 让世界空间物体（角色、对话框等前景）相对相机保持固定的屏幕位置：
/// 相机左右滚动调查场景时，本物体按相同位移反向跟随，只有背景和货架物品在移动。
/// </summary>
public sealed class CameraScreenAnchor : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;

    private float offsetX;
    private bool initialized;

    private void LateUpdate()
    {
        if (!initialized)
        {
            if (targetCamera == null)
            {
                targetCamera = Camera.main;
            }

            if (targetCamera == null)
            {
                return;
            }

            offsetX = transform.position.x - targetCamera.transform.position.x;
            initialized = true;
        }

        Vector3 position = transform.position;
        position.x = targetCamera.transform.position.x + offsetX;
        transform.position = position;
    }
}
