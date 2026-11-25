using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//the camera scrolls past the art, so real ropes can stay in world space
public sealed class CreditsRoll2D : MonoBehaviour
{
    [SerializeField] private CreditsData credits;
    [SerializeField, Min(0.1f)] private float scrollSpeed = 1.5f;
    [SerializeField, Min(5f)] private float rowHeight = 8f;
    [SerializeField] private string menuScene = "StartScreen";
    private Camera view;
    private Sprite square;
    [SerializeField] private Material artMaterial;
    private RectTransform textCanvas;
    private readonly List<RectTransform> labels = new List<RectTransform>();
    private readonly List<Vector2> labelPositions = new List<Vector2>();
    private readonly List<Vector2> labelSizes = new List<Vector2>();
    private float finishY;
    private float wait = 2f;

    private void Start()
    {
        if (credits == null) { enabled = false; return; }
        Time.timeScale = 1f;
        view = Camera.main;
        if (view == null) { enabled = false; return; }
        view.clearFlags = CameraClearFlags.SolidColor;
        view.backgroundColor = Color.black;
        view.orthographic = true;
        view.orthographicSize = Mathf.Max(10f, 15f / view.aspect);
        view.transform.position = new Vector3(0f, 0f, -10f);
        square = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        Canvas canvas = new GameObject("Credits text", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        canvas.transform.SetParent(transform, false);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        scaler.matchWidthOrHeight = 1f;
        textCanvas = canvas.GetComponent<RectTransform>();
        Label("UNTIL WE ARE ASH", new Vector2(0, 1), new Vector2(28, 3), 10f);
        Label("A game by " + credits.creatorName, new Vector2(0, -2), new Vector2(20, 2), 4f);
        float y = -rowHeight;
        foreach (CreditsData.Section section in credits.sections)
        {
            Label(section.title, new Vector2(0, y + 1.7f), new Vector2(13, 2), 5.5f);
            Label(section.text, new Vector2(0, y - 0.4f), new Vector2(13, 4.5f), 4f);
            Preview(section.left, new Vector2(-10.2f, y));
            Preview(section.right, new Vector2(10.2f, y));
            y -= rowHeight;
        }
        finishY = y;
        Label(credits.closingText, new Vector2(0, y), new Vector2(27, 5), 5.2f);
        Label("Enter / Esc: return to title    |    Hold Space: pause credits",
            new Vector2(0, y - 4), new Vector2(28, 2), 2.5f);
    }

    private void Label(string text, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject go = new GameObject("Credits text", typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(textCanvas, false);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.rectTransform.anchorMin = label.rectTransform.anchorMax = Vector2.one * 0.5f;
        labels.Add(label.rectTransform);
        labelPositions.Add(position);
        labelSizes.Add(size);
        label.font = credits.DisplayFont;
        label.text = credits.Format(text);
        label.fontSize = fontSize * 7f;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.wordSpacing = 3f;
        label.raycastTarget = false;
    }

    private void Preview(CreditsData.Preview preview, Vector2 position)
    {
        if (preview == null || (preview.animation == null && (preview.sprites == null || preview.sprites.Length == 0) &&
            string.IsNullOrWhiteSpace(preview.caption))) return;
        Transform frame = new GameObject("Showcase - " + preview.caption).transform;
        frame.SetParent(transform, false);
        frame.localPosition = position;
        Box(frame, 5.2f, new Color(0.16f, 0.17f, 0.18f), 0);
        Box(frame, 5f, Color.black, 1);
        frame.gameObject.AddComponent<CreditsPreview2D>().Setup(preview, artMaterial);
        Label(preview.caption, position + Vector2.down * 3.2f, new Vector2(5.5f, 1.5f), 2.6f);
    }

    private void Box(Transform parent, float size, Color color, int order)
    {
        SpriteRenderer image = new GameObject("Frame").AddComponent<SpriteRenderer>();
        image.transform.SetParent(parent, false);
        image.transform.localScale = new Vector3(size, size, 1f);
        image.sprite = square;
        image.color = color;
        image.sortingOrder = order;
        image.sharedMaterial = artMaterial;
    }

    private void Update()
    {
        if (view == null) return;
        Keyboard keys = Keyboard.current;
        if (keys != null && (keys.escapeKey.wasPressedThisFrame || keys.enterKey.wasPressedThisFrame))
        { ReturnToMenu(); return; }
        if (wait > 0f) { wait -= Time.unscaledDeltaTime; return; }
        if (keys != null && keys.spaceKey.isPressed) return;
        Vector3 position = view.transform.position;
        position.y = Mathf.MoveTowards(position.y, finishY, scrollSpeed * Time.unscaledDeltaTime);
        view.transform.position = position;
    }

    public void ReturnToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScene)) return;
        EndingCutscene2D ending = FindAnyObjectByType<EndingCutscene2D>();
        if (ending != null) ending.CloseCredits();
        SceneManager.LoadScene(menuScene);
    }
    private void LateUpdate()
    {
        if (view == null || textCanvas == null) return;
        float scale = textCanvas.rect.height / (view.orthographicSize * 2f);
        for (int i = 0; i < labels.Count; i++)
        {
            Vector3 point = view.WorldToViewportPoint(labelPositions[i]);
            labels[i].anchoredPosition = new Vector2(
                Mathf.Round((point.x - 0.5f) * textCanvas.rect.width),
                Mathf.Round((point.y - 0.5f) * textCanvas.rect.height));
            labels[i].sizeDelta = labelSizes[i] * scale;
        }
    }

    private void OnDestroy()
    {
        if (square != null) Destroy(square);
    }
}
