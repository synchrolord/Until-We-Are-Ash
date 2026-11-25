using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

//the small debug menu uses built-in GUI so it needs no extra canvas or prefabs
public sealed class PauseMenu2D : MonoBehaviour
{
    [SerializeField] private string menuScene = "StartScreen";
    [SerializeField] private bool enableCheats = true;
    private readonly List<Behaviour> controls = new List<Behaviour>();
    private PlayerVitals2D vitals;
    private PressureTool2D tool;
    private PlayerCheats2D cheats;
    private PlayerProgress2D progress;
    private bool paused, showCheats, showUnlocks;
    private float oldTimeScale;
    private bool oldAudioPause;
    private Vector2 scroll;
    private GUIStyle button, label, toggle;

    private void Awake()
    {
        vitals = GetComponent<PlayerVitals2D>();
        tool = GetComponent<PressureTool2D>();
        cheats = GetComponent<PlayerCheats2D>();
        progress = GetComponent<PlayerProgress2D>();
    }

    private void Update()
    {
        if (Keyboard.current == null || !Keyboard.current.escapeKey.wasPressedThisFrame) return;
        if (paused) Resume();
        else if (Time.timeScale > 0f && vitals != null && !vitals.IsDead && !PlayerProgress2D.EndingActive)
            Pause();
    }

    private void Pause()
    {
        paused = true;
        oldTimeScale = Time.timeScale;
        oldAudioPause = AudioListener.pause;
        //stop input as well as time, so clicking the menu cannot fire the tool
        foreach (Behaviour control in GetComponentsInChildren<Behaviour>(true))
            if (control.enabled && (control is PlayerInput ||
                control is GrenadeThrower2D || control is PlayerAim2D || control is PlayerLiquidInteraction2D))
            { controls.Add(control); control.enabled = false; }
        if (tool != null) tool.SetSpraying(false);
        Time.timeScale = 0f;
        AudioListener.pause = true;
        if (progress != null) progress.Save();
    }

    public void Resume()
    {
        if (!paused) return;
        paused = false;
        Time.timeScale = oldTimeScale;
        AudioListener.pause = oldAudioPause;
        foreach (Behaviour control in controls) if (control != null) control.enabled = true;
        controls.Clear();
        GameOptions.Save();
    }

    private void MainMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(menuScene))
        { Debug.LogError("Menu scene is missing from the build: " + menuScene, this); return; }
        if (progress != null) progress.Save();
        Resume();
        SceneManager.LoadScene(menuScene);
        GUIUtility.ExitGUI();
    }

    private void OnGUI()
    {
        if (!paused) return;
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
        GUILayout.BeginArea(new Rect((width - 440f) * 0.5f, 35f, 440f, 650f));
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("Paused", label);
        GUILayout.Space(15f);
        if (GUILayout.Button("Resume", button)) Resume();
        if (GUILayout.Button("Main Menu", button)) MainMenu();
        GUILayout.Space(15f);
        SettingsUI.Draw(label, toggle);
        if (enableCheats)
        {
            GUILayout.Space(15f);
            showCheats = GUILayout.Toggle(showCheats, "Cheat menu", toggle);
            if (showCheats) DrawCheats();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
        GUI.matrix = oldMatrix;
    }

    private void DrawCheats()
    {
        if (cheats != null && cheats.enabled)
            cheats.SetFlying(GUILayout.Toggle(cheats.IsFlying, "Fly / pass through walls", toggle));
        if (tool != null)
        {
            tool.UnlimitedResources = GUILayout.Toggle(tool.UnlimitedResources, "Infinite fuel and water", toggle);
            if (GUILayout.Button("Refill supplies", button)) tool.RefillAllResources();
        }
        if (GUILayout.Button("Infection: " + vitals.InfectionStage + " (next)", button))
            vitals.SetInfectionStage((vitals.InfectionStage + 1) % InfectionProfile.InfectionCount);
        if (GUILayout.Button("Unlock item...", button)) showUnlocks = !showUnlocks;
        if (showUnlocks)
            foreach (PlayerUnlocks2D.Item item in System.Enum.GetValues(typeof(PlayerUnlocks2D.Item)))
                if (GUILayout.Button(item + (PlayerUnlocks2D.Has(item) ? " (unlocked)" : ""), button))
                { PlayerUnlocks2D.Add(item); showUnlocks = false; }
        if (GUILayout.Button("Kill / respawn", button))
        {
            if (cheats != null) cheats.SetFlying(false);
            Resume();
            vitals.Kill();
            GUIUtility.ExitGUI();
        }
    }

    private void OnDisable() => Resume();
}
