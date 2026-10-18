using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class MouseCursorUiSetup
{
    [MenuItem("Tools/Dialogue/Setup Mouse Cursor UI")]
    private static void SetupMouseCursorUi()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(scene.path))
        {
            Debug.LogError("当前场景尚未保存为文件，请先保存场景，再运行 Setup Mouse Cursor UI。");
            return;
        }

        MouseCursorController controller = null;
        foreach (MouseCursorController candidate in Resources.FindObjectsOfTypeAll<MouseCursorController>())
        {
            if (candidate.gameObject.scene == scene)
            {
                controller = candidate;
                break;
            }
        }

        if (controller == null)
        {
            Debug.LogError("当前场景没有 MouseCursorController，未修改场景。");
            return;
        }

        Canvas existingCanvas = controller.GetComponentInParent<Canvas>();
        Image existingImage = controller.GetComponent<Image>();
        if (existingCanvas != null && existingCanvas.gameObject.name == "MouseCursorCanvas" && existingImage != null &&
            controller.transform is RectTransform existingRect)
        {
            Configure(existingCanvas, existingImage, existingRect);
            Save(scene);
            return;
        }

        Camera cursorCamera = ReadCamera(controller);
        Sprite fallbackSprite = controller.GetComponent<SpriteRenderer>() != null
            ? controller.GetComponent<SpriteRenderer>().sprite : null;

        GameObject canvasObject = new GameObject("MouseCursorCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        Undo.RegisterCreatedObjectUndo(canvasObject, "Create mouse cursor UI");
        SceneManager.MoveGameObjectToScene(canvasObject, scene);
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.scaleFactor = 1f;
        scaler.referencePixelsPerUnit = 100f;

        GameObject cursorObject = new GameObject("光标",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        Undo.RegisterCreatedObjectUndo(cursorObject, "Create mouse cursor image");
        SceneManager.MoveGameObjectToScene(cursorObject, scene);
        cursorObject.layer = LayerMask.NameToLayer("UI");
        cursorObject.transform.SetParent(canvasObject.transform, false);
        Image image = cursorObject.GetComponent<Image>();
        image.sprite = LoadCursorSprite("猫", "正常") ?? fallbackSprite;
        image.preserveAspect = false;
        image.raycastTarget = false;
        RectTransform rect = cursorObject.GetComponent<RectTransform>();
        Configure(canvas, image, rect);

        MouseCursorController newController = cursorObject.AddComponent<MouseCursorController>();
        SerializedObject serialized = new SerializedObject(newController);
        serialized.FindProperty("worldCamera").objectReferenceValue = cursorCamera;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        Undo.DestroyObjectImmediate(controller.gameObject);
        Save(scene);
        Debug.Log("光标已迁移为 MouseCursorCanvas/光标（Image），场景已保存。", canvasObject);
    }

    private static void Configure(Canvas canvas, Image image, RectTransform rect)
    {
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32767;
        image.raycastTarget = false;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        if (image.sprite != null)
        {
            Rect spriteRect = image.sprite.rect;
            rect.pivot = new Vector2(
                image.sprite.pivot.x / spriteRect.width,
                image.sprite.pivot.y / spriteRect.height);
        }
        rect.anchoredPosition = Vector2.zero;
    }

    private static Sprite LoadCursorSprite(string animal, string state)
    {
        Sprite[] sprites = Resources.LoadAll<Sprite>($"Art/鼠标/鼠标-{animal}/{state}-{animal}");
        return sprites.Length > 0 ? sprites[0] : null;
    }

    private static Camera ReadCamera(MouseCursorController controller)
    {
        SerializedProperty property = new SerializedObject(controller).FindProperty("worldCamera");
        return property != null ? property.objectReferenceValue as Camera : Camera.main;
    }

    private static void Save(Scene scene)
    {
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }
}
