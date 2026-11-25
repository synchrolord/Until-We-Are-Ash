using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public sealed class StartScreen2D : MonoBehaviour
{
    [SerializeField] private string gameSceneName = "Floor_01";
    [SerializeField] private Button startButton;
    private Button continueButton, settingsButton;
    private bool showSettings;
    private GUIStyle button, label, toggle;

    private void Start()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
        if (startButton == null) startButton = FindAnyObjectByType<Button>();
        if (startButton == null) return;

        //all three choices reuse the existing button's artwork
        continueButton = AddButton("Continue", PlayerProgress2D.ContinueGame);
        continueButton.interactable = PlayerProgress2D.CanContinue;
        settingsButton = AddButton("Settings", () => ShowSettings(true));
        startButton.GetComponentInChildren<TMP_Text>().text = "New Game";
        startButton.GetComponent<RectTransform>().anchoredPosition += Vector2.up * 110f;
        settingsButton.GetComponent<RectTransform>().anchoredPosition -= Vector2.up * 110f;
    }

    private Button AddButton(string title, UnityAction action)
    {
        Button copy = Instantiate(startButton, startButton.transform.parent);
        copy.name = title;
        copy.GetComponentInChildren<TMP_Text>().text = title;
        copy.onClick = new Button.ButtonClickedEvent();
        copy.onClick.AddListener(action);
        return copy;
    }

    private void ShowSettings(bool value)
    {
        showSettings = value;
        startButton.gameObject.SetActive(!value);
        continueButton.gameObject.SetActive(!value);
        settingsButton.gameObject.SetActive(!value);
        continueButton.interactable = PlayerProgress2D.CanContinue;
        if (!value) GameOptions.Save();
    }

    private void Update()
    {
        if (showSettings && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            ShowSettings(false);
    }

    private void OnGUI()
    {
        if (!showSettings) return;
        if (button == null)
        {
            button = new GUIStyle(GUI.skin.button) { fontSize = 24, fixedHeight = 44f };
            label = new GUIStyle(GUI.skin.label) { fontSize = 28, alignment = TextAnchor.MiddleCenter };
            toggle = new GUIStyle(GUI.skin.toggle) { fontSize = 22, fixedHeight = 38f };
        }
        GUI.depth = -1000;
        Matrix4x4 oldMatrix = GUI.matrix;
        float scale = Screen.height / 720f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float width = Screen.width / scale;
        Color oldColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.88f);
        GUI.DrawTexture(new Rect(0, 0, width, 720), Texture2D.whiteTexture);
        GUI.color = oldColor;
        GUILayout.BeginArea(new Rect((width - 440f) * 0.5f, 180f, 440f, 360f));
        GUILayout.Label("Settings", label);
        GUILayout.Space(15f);
        SettingsUI.Draw(label, toggle);
        GUILayout.Space(15f);
        if (GUILayout.Button("Clear Continue", button))
        {
            PlayerProgress2D.ClearSave();
            continueButton.interactable = false;
        }
        if (GUILayout.Button("Back", button)) ShowSettings(false);
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
    }

    //the original button already calls this method
    public void StartGame() => PlayerProgress2D.NewGame(gameSceneName);
    private void OnDisable() => GameOptions.Save();
}
