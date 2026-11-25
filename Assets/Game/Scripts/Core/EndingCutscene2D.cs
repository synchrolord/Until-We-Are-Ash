using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

//show the burning rooms, using placed screens for the first floor
public sealed class EndingCutscene2D : MonoBehaviour
{
    [System.Serializable]
    private class SceneShot
    {
        public string scene;
        public string firstRoom;
    }

    [System.Serializable]
    private class SceneCount
    {
        public string scene = "";
        public int shots = 0;
    }
    [SerializeField, HideInInspector] private SceneCount[] sceneCounts = new SceneCount[0];

    [Header("Camera")]
    [SerializeField, Min(0.1f)] private float zoomSeconds = 3f;
    [SerializeField, Min(0.1f)] private float roomSeconds = 3f;
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.6f;
    [Tooltip("Leave empty to show the earlier numbered floors automatically.")]
    [SerializeField] private SceneShot[] otherScenes = new SceneShot[0];
    [SerializeField] private string[] excludedScenes = new string[0];
    [SerializeField, Range(0, 200)] private int settleSteps = 100;

    [Header("Poem and credits")]
    [SerializeField] private CreditsData credits;
    [SerializeField, Min(0.5f)] private float finalLineSeconds = 4f;
    [SerializeField] private string creditsScene = "Credits";

    [Header("Ending music")]
    [SerializeField] private AudioClip endingMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 1f;
    [SerializeField, Min(0.1f)] private float musicFadeSeconds = 1.5f;

    [Header("Burning rooms")]
    [SerializeField, Range(1, 64)] private int firePoints = 24;
    [SerializeField, Range(1, 8)] private int fireSize = 3;
    [SerializeField, Min(0.1f)] private float fireInterval = 0.25f;
    [SerializeField] private Material sceneryMaterial;
    [SerializeField, Range(0f, 1f)] private float charStrength = 0.35f;
    [SerializeField] private Color charTint = new Color(0.22f, 0.17f, 0.14f, 1f);

    [Header("Smoke")]
    [SerializeField] private Texture2D mist;
    [SerializeField] private Shader smokeShader;
    [SerializeField, Range(0f, 1f)] private float smokeOpacity = 0.7f;
    [SerializeField, Range(0f, 1f)] private float vignette = 0.65f;
    [SerializeField, Min(0.1f)] private float smokeTiling = 2f;
    [SerializeField] private Vector2 smokeSpeed = new Vector2(0.01f, 0.025f);
    [SerializeField] private UnityEvent onFinished = new UnityEvent();

    private BossEncounter2D encounter;
    private Camera cameraView;
    private CameraFollow2D follow;
    private CellWorldRoomManager manager;
    private readonly List<GameObject> hiddenPlayers = new List<GameObject>();
    private Canvas overlay;
    private Image black;
    private Material smokeMaterial;
    private EndingRoom2D activeRoom;
    private readonly List<EndingRoom2D> effects = new List<EndingRoom2D>();
    private readonly List<Behaviour> paused = new List<Behaviour>();
    private readonly List<Rigidbody2D> bodies = new List<Rigidbody2D>();
    private bool running;
    public bool IsRunning => running;
    private bool quitting;
    private float oldTimeScale;
    private float startedAt;
    private CanvasGroup poemPanel;
    private TextMeshProUGUI poemLabel;
    private string[] verses = new string[0];
    private float poemTime;
    private float poemDuration;
    private int shownVerse = -1;
    private bool poemClock;
    private RawImage smokeImage;
    private AudioSource music;
    private float musicGain;
    private readonly List<AudioSource> mutedMusic = new List<AudioSource>();

    private void Awake() => encounter = GetComponent<BossEncounter2D>();
    private void OnEnable()
    {
        if (encounter != null) encounter.Finished += Play;
    }

    [ContextMenu("Play Ending")]
    public void Play()
    {
        if (!Application.isPlaying || running) return;
        if (mist == null || smokeShader == null)
        {
            Debug.LogWarning("Ending needs its mist texture and smoke shader.", this);
            return;
        }
        running = true;
        startedAt = Time.unscaledTime;
        oldTimeScale = Time.timeScale;
        Time.timeScale = 1f;
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        BuildOverlay();
        StartMusic(endingMusic);
        if (credits != null && credits.poem != null)
            verses = credits.poem.text.Replace("\r\n", "\n").Trim()
                .Split(new[] { "\n\n" }, System.StringSplitOptions.RemoveEmptyEntries);
        StartCoroutine(FadeMusic(1f));
        SceneManager.sceneLoaded += SceneLoaded;
        PrepareScene();
        StartCoroutine(Sequence());
    }

    private void PrepareScene()
    {
        cameraView = Camera.main;
        if (cameraView == null) cameraView = FindAnyObjectByType<Camera>();
        manager = FindAnyObjectByType<CellWorldRoomManager>();
        follow = cameraView != null ? cameraView.GetComponent<CameraFollow2D>() : null;
        if (follow != null) follow.Cutscene = true;
        if (cameraView != null) Pause(cameraView.GetComponent<PixelPerfectCamera>());

        foreach (PlayerVitals2D player in FindObjectsByType<PlayerVitals2D>(FindObjectsSortMode.None))
        {
            foreach (Behaviour control in player.GetComponents<Behaviour>())
                if (control is PlayerInput || control is PlayerMotor2D || control is PlayerVitals2D ||
                    control is PressureTool2D || control is GrenadeThrower2D ||
                    control is PlayerAim2D || control is PlayerLiquidInteraction2D)
                    Pause(control);
            Freeze(player.GetComponent<Rigidbody2D>());
            //hide the whole player, including the torch and attached effects
            hiddenPlayers.Add(player.gameObject);
            player.gameObject.SetActive(false);
        }
        //stop other encounters from starting during the montage
        foreach (BossEncounter2D other in FindObjectsByType<BossEncounter2D>(FindObjectsSortMode.None))
            if (other != encounter) Pause(other);
        foreach (Enemy2D enemy in FindObjectsByType<Enemy2D>(FindObjectsSortMode.None))
        {
            Pause(enemy);
            Pause(enemy.GetComponent<SurfaceWalker2D>());
            Freeze(enemy.GetComponent<Rigidbody2D>());
        }
        foreach (BossGrapple2D boss in FindObjectsByType<BossGrapple2D>(FindObjectsSortMode.None))
        {
            if (boss.CurrentState == BossGrapple2D.State.Burning) continue;
            Pause(boss);
            Pause(boss.GetComponent<BossSpit2D>());
            Freeze(boss.GetComponent<Rigidbody2D>());
        }
        HideInterface();
        MuteSceneMusic();
    }

    private void SceneLoaded(Scene scene, LoadSceneMode mode) => PrepareScene();
    private void Pause(Behaviour control)
    {
        if (control == null || !control.enabled) return;
        paused.Add(control);
        control.enabled = false;
    }
    private void Freeze(Rigidbody2D body)
    {
        if (body == null || !body.simulated) return;
        bodies.Add(body);
        body.simulated = false;
    }
    private void HideInterface()
    {
        foreach (Canvas canvas in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (canvas != overlay) Pause(canvas);
    }

    private IEnumerator Sequence()
    {
        yield return null;
        CellWorld2D first = null;
        if (manager != null)
        {
            CellWorld2D world = encounter != null && encounter.ActiveBoss != null
                ? manager.GetWorldAt(encounter.ActiveBoss.transform.position) : manager.CurrentWorld;
            if (world == null) world = manager.CurrentWorld;
            first = world;
        }
        List<SceneShot> scenes = GetScenes();
        for (int i = scenes.Count - 1; i >= 0; i--)
            if (scenes[i] == null || string.IsNullOrWhiteSpace(scenes[i].scene) ||
                !Application.CanStreamedLevelBeLoaded(scenes[i].scene) ||
                System.Array.IndexOf(excludedScenes, scenes[i].scene) >= 0) scenes.RemoveAt(i);
        //count the actual shots before choosing each verse's share of the montage
        int shots = CurrentShotCount();
        foreach (SceneShot scene in scenes) shots += SavedShotCount(scene.scene);
        poemDuration = zoomSeconds + Mathf.Max(1, shots) * (roomSeconds + fadeSeconds * 2f);
        poemTime = 0f;
        poemClock = true;
        yield return ShowScene(first, true);
        for (int i = 0; i < scenes.Count; i++)
        {
            SceneShot shot = scenes[i];
            yield return SceneManager.LoadSceneAsync(shot.scene);
            yield return null;
            CellWorld2D selected = null;
            foreach (CellWorld2D world in FindObjectsByType<CellWorld2D>(FindObjectsSortMode.None))
                if (world.name == shot.firstRoom || RoomRoot(world).name == shot.firstRoom) selected = world;
            yield return ShowScene(selected, false);
        }
        poemClock = false;
        if (black.color.a < 1f) yield return Fade(1f);
        //the final sentence belongs to the blackout, not an earlier room
        if (poemLabel != null && verses.Length > 0)
        {
            poemLabel.text = verses[verses.Length - 1];
            poemLabel.fontSize = 44f;
            RectTransform panel = poemPanel.GetComponent<RectTransform>();
            panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.anchoredPosition = Vector2.zero;
            poemPanel.alpha = 1f;
            yield return new WaitForSecondsRealtime(finalLineSeconds);
        }
        onFinished.Invoke();
        yield return ShowCredits();
    }

    private int SavedShotCount(string scene)
    {
        foreach (SceneCount entry in sceneCounts)
            if (entry.scene == scene) return entry.shots;
        Debug.LogWarning("Refresh ending timing before playing: " + scene, this);
        return Mathf.Max(1, CurrentShotCount());
    }

    private int CurrentShotCount()
    {
        if (SceneManager.GetActiveScene().name == "Floor_01" && manager != null)
        {
            int screens = 0;
            foreach (CameraZone2D screen in CameraZone2D.Active)
                if (screen != null && screen.IsStatic && manager.GetWorldAt(screen.transform.position) != null) screens++;
            if (screens > 0) return screens;
        }
        int count = 0;
        foreach (CellWorld2D world in FindObjectsByType<CellWorld2D>())
            if (world.isActiveAndEnabled) count++;
        return count;
    }

    private List<SceneShot> GetScenes()
    {
        if (otherScenes.Length > 0) return new List<SceneShot>(otherScenes);

        List<SceneShot> scenes = new List<SceneShot>();
        string current = SceneManager.GetActiveScene().name;
        int currentNumber = int.MaxValue;
        if (current.StartsWith("Floor_") && int.TryParse(current.Substring(6), out int number))
            currentNumber = number;

        //use the build list so test scenes and disabled old floors are left out
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i));
            if (name.StartsWith("Floor_") && int.TryParse(name.Substring(6), out int floor) &&
                floor < currentNumber)
                scenes.Add(new SceneShot { scene = name });
        }
        scenes.Sort((a, b) => int.Parse(b.scene.Substring(6)).CompareTo(int.Parse(a.scene.Substring(6))));
        return scenes;
    }

    private IEnumerator SettleVines(Transform room)
    {
        if (settleSteps == 0) yield break;
        int prepared = 0;
        foreach (VineRope2D rope in room.GetComponentsInChildren<VineRope2D>())
        {
            if (rope.IsBurning || rope.IsBroken || rope.GetComponentInParent<BossGrapple2D>() != null) continue;
            rope.Settle(settleSteps);
            //spread this work across frames while the screen is black
            if (++prepared % 2 == 0) yield return null;
        }
    }

    private Transform RoomRoot(CellWorld2D world)
    {
        Room2D room = world.GetComponentInParent<Room2D>();
        return room != null ? room.transform :
            world.transform.parent != null ? world.transform.parent : world.transform;
    }

    private IEnumerator ShowScene(CellWorld2D first, bool zoom)
    {
        //the first floor has the older rooms, so find the simulations directly
        CellWorld2D[] worlds = FindObjectsByType<CellWorld2D>(FindObjectsSortMode.None);
        System.Array.Sort(worlds, (a, b) =>
        {
            int row = b.transform.position.y.CompareTo(a.transform.position.y);
            return row != 0 ? row : a.transform.position.x.CompareTo(b.transform.position.x);
        });
        List<CellWorld2D> ordered = new List<CellWorld2D>();
        if (first != null && first.isActiveAndEnabled) ordered.Add(first);
        foreach (CellWorld2D world in worlds)
            if (world != first && world.isActiveAndEnabled) ordered.Add(world);
        if (ordered.Count == 0)
            Debug.LogWarning("No CellWorlds found for the ending in " + SceneManager.GetActiveScene().name, this);
        if (SceneManager.GetActiveScene().name == "Floor_01" && manager != null)
        {
            foreach (CellWorld2D world in ordered)
                while (!world.IsInitialized) yield return null;
            List<CameraZone2D> screens = new List<CameraZone2D>();
            foreach (CameraZone2D screen in CameraZone2D.Active)
                if (screen != null && screen.IsStatic)
                {
                    if (manager.GetWorldAt(screen.transform.position) != null) screens.Add(screen);
                    else Debug.LogWarning("Ending screen is outside a CellWorld: " + screen.name, screen);
                }
            //show the level in reverse: Screen (4), Screen (3)... Screen
            screens.Sort((a, b) => string.CompareOrdinal(b.name, a.name));
            if (screens.Count > 0)
            {
                for (int i = 0; i < screens.Count; i++)
                    yield return ShowRoom(manager.GetWorldAt(screens[i].transform.position), false, screens[i]);
                yield break;
            }
        }
        for (int i = 0; i < ordered.Count; i++)
            yield return ShowRoom(ordered[i], zoom && i == 0);
    }

    private IEnumerator PrepareRoom(CellWorld2D world, bool settle)
    {
        Transform room = RoomRoot(world);
        while (!world.IsInitialized) yield return null;
        HideInterface();
        if (activeRoom != null) activeRoom.enabled = false;
        manager.RegisterWorld(world);
        manager.SetCutsceneWorld(world);
        //let the manager select this room, then let its enabled controllers build their ropes
        yield return null;
        yield return null;
        if (settle) yield return SettleVines(room);
        activeRoom = room.GetComponent<EndingRoom2D>();
        if (activeRoom == null)
        {
            activeRoom = room.gameObject.AddComponent<EndingRoom2D>();
            activeRoom.Setup(world, room, firePoints, fireSize, fireInterval, charTint, charStrength, sceneryMaterial);
            effects.Add(activeRoom);
        }
        activeRoom.enabled = true;
    }

    private IEnumerator ShowRoom(CellWorld2D world, bool zoom, CameraZone2D screen = null)
    {
        if (world == null || cameraView == null || manager == null) yield break;
        if (!zoom && black.color.a < 1f) yield return Fade(1f);
        yield return PrepareRoom(world, !zoom);
        Vector2 low = world.transform.TransformPoint(Vector3.zero);
        Vector2 high = world.transform.TransformPoint(new Vector3(world.Width * world.CellSize, world.Height * world.CellSize));
        Vector2 center = (low + high) * 0.5f;
        Vector2 size = high - low;
        float height = Mathf.Max(Mathf.Abs(size.y), Mathf.Abs(size.x) / cameraView.aspect) * 1.05f;
        if (screen != null)
        {
            //screens only frame the shot; the CellWorld still handles the fire
            height = screen.ViewHeight > 0f ? screen.ViewHeight :
                Mathf.Max(screen.Bounds.size.y, screen.Bounds.size.x / cameraView.aspect);
            center = screen.CameraPosition(screen.Bounds.center, height, cameraView.aspect);
        }
        if (zoom) yield return MoveCamera(center, height, zoomSeconds);
        else
        {
            cameraView.transform.position = new Vector3(center.x, center.y, cameraView.transform.position.z);
            cameraView.orthographicSize = height * 0.5f;
        }
        yield return Fade(0f);
        yield return HoldShot(roomSeconds);
        yield return Fade(1f);
    }

    private IEnumerator MoveCamera(Vector2 point, float height, float seconds)
    {
        Vector3 start = cameraView.transform.position;
        Vector3 end = new Vector3(point.x, point.y, start.z);
        float size = cameraView.orthographicSize;
        for (float time = 0f; time < seconds;)
        {
            float step = Mathf.Min(Time.unscaledDeltaTime, seconds - time);
            float t = Mathf.SmoothStep(0f, 1f, time / seconds);
            cameraView.transform.position = Vector3.Lerp(start, end, t);
            cameraView.orthographicSize = Mathf.Lerp(size, height * 0.5f, t);
            AdvancePoem(step);
            time += step;
            yield return null;
        }
        cameraView.transform.position = end;
        cameraView.orthographicSize = height * 0.5f;
    }

    private IEnumerator Fade(float alpha)
    {
        float start = black.color.a;
        for (float time = 0f; time < fadeSeconds;)
        {
            float step = Mathf.Min(Time.unscaledDeltaTime, fadeSeconds - time);
            black.color = new Color(0f, 0f, 0f, Mathf.Lerp(start, alpha, time / fadeSeconds));
            AdvancePoem(step);
            time += step;
            yield return null;
        }
        black.color = new Color(0f, 0f, 0f, alpha);
    }

    private RectTransform Panel(string label)
    {
        RectTransform rect = new GameObject(label, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(overlay.transform, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return rect;
    }
    private void BuildOverlay()
    {
        overlay = new GameObject("Ending Overlay", typeof(RectTransform), typeof(Canvas)).GetComponent<Canvas>();
        DontDestroyOnLoad(overlay.gameObject);
        overlay.renderMode = RenderMode.ScreenSpaceOverlay;
        overlay.sortingOrder = 2000;
        CanvasScaler scaler = overlay.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        smokeMaterial = new Material(smokeShader);
        RawImage smoke = Panel("Smoke and vignette").gameObject.AddComponent<RawImage>();
        smokeImage = smoke;
        smoke.texture = mist;
        smoke.material = smokeMaterial;
        smoke.raycastTarget = false;
        black = Panel("Fade").gameObject.AddComponent<Image>();
        black.color = Color.clear;
        BuildPoem();
    }
    private void Update()
    {
        if (!running || smokeMaterial == null) return;
        if (music != null)
            music.volume = musicVolume * GameOptions.MusicVolume * musicGain;
        float age = Time.unscaledTime - startedAt;
        smokeMaterial.SetFloat("_SmokeOpacity", smokeOpacity * Mathf.Clamp01(age));
        smokeMaterial.SetFloat("_Vignette", vignette * Mathf.Clamp01(age));
        float aspect = cameraView != null ? cameraView.aspect : 16f / 9f;
        smokeMaterial.SetVector("_Tiling", new Vector4(aspect * smokeTiling, smokeTiling, 0f, 0f));
        smokeMaterial.SetVector("_Scroll", new Vector4(-age * smokeSpeed.x, -age * smokeSpeed.y, 0f, 0f));
    }
    private IEnumerator HoldShot(float seconds)
    {
        for (float time = 0f; time < seconds;)
        {
            float step = Mathf.Min(Time.unscaledDeltaTime, seconds - time);
            AdvancePoem(step);
            time += step;
            yield return null;
        }
    }

    private void AdvancePoem(float seconds)
    {
        if (!poemClock) return;
        poemTime += seconds;
        if (poemLabel == null || verses.Length < 2) return;
        int count = verses.Length - 1;
        int verse = Mathf.Min(count - 1, Mathf.FloorToInt(poemTime / Mathf.Max(0.1f, poemDuration) * count));
        if (verse != shownVerse)
        {
            shownVerse = verse;
            poemLabel.text = verses[verse];
        }
        poemPanel.alpha = 1f;
    }

    private IEnumerator FadeMusic(float target)
    {
        float start = musicGain;
        for (float time = 0f; time < musicFadeSeconds; time += Time.unscaledDeltaTime)
        {
            musicGain = Mathf.Lerp(start, target, time / musicFadeSeconds);
            yield return null;
        }
        musicGain = target;
        if (music != null) music.volume = musicVolume * GameOptions.MusicVolume * musicGain;
    }

    private void BuildPoem()
    {
        if (credits == null || credits.poem == null) return;
        RectTransform panel = Panel("Poem");
        panel.anchorMin = panel.anchorMax = new Vector2(0.5f, 0f);
        panel.sizeDelta = new Vector2(1100f, 170f);
        panel.anchoredPosition = new Vector2(0f, 35f);
        panel.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        poemPanel = panel.gameObject.AddComponent<CanvasGroup>();
        poemPanel.alpha = 0f;
        poemPanel.blocksRaycasts = false;
        RectTransform text = new GameObject("Verse", typeof(RectTransform)).GetComponent<RectTransform>();
        text.SetParent(panel, false);
        text.anchorMin = Vector2.zero;
        text.anchorMax = Vector2.one;
        text.offsetMin = new Vector2(25f, 15f);
        text.offsetMax = new Vector2(-25f, -15f);
        poemLabel = text.gameObject.AddComponent<TextMeshProUGUI>();
        poemLabel.font = credits.DisplayFont;
        poemLabel.fontSize = 30f;
        poemLabel.alignment = TextAlignmentOptions.Center;
        poemLabel.color = Color.white;
        poemLabel.wordSpacing = 3f;
        poemLabel.raycastTarget = false;
    }

    private void StartMusic(AudioClip clip)
    {
        if (clip == null) return;
        if (music == null)
        {
            music = gameObject.AddComponent<AudioSource>();
            music.playOnAwake = false;
            music.spatialBlend = 0f;
            music.loop = true;
        }
        music.clip = clip;
        music.volume = 0f;
        musicGain = 0f;
        music.Play();
        MuteSceneMusic();
    }

    private void MuteSceneMusic()
    {
        if (music == null) return;
        foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
        {
            if (source == music || source.mute) continue;
            bool background = source.name.ToLowerInvariant().Contains("music") ||
                (source.outputAudioMixerGroup != null && source.outputAudioMixerGroup.name.ToLowerInvariant().Contains("music"));
            if (!background) continue;
            mutedMusic.Add(source);
            source.mute = true;
        }
    }

    private IEnumerator ShowCredits()
    {
        if (!Application.CanStreamedLevelBeLoaded(creditsScene))
        {
            Debug.LogError("Add the Credits scene to the build before playing the ending.", this);
            yield break;
        }
        if (poemPanel != null) poemPanel.gameObject.SetActive(false);
        SceneManager.sceneLoaded -= SceneLoaded;
        yield return SceneManager.LoadSceneAsync(creditsScene);
        yield return null;
        cameraView = Camera.main;
        follow = null;
        manager = null;
        activeRoom = null;
        MuteSceneMusic();
        if (smokeImage != null) smokeImage.enabled = false;
        //the same persistent audio source keeps its playback position through credits
        yield return Fade(0f);
        overlay.enabled = false;
    }

    public void CloseCredits()
    {
        if (music != null) music.Stop();
        Destroy(gameObject);
    }

    private void OnApplicationQuit() => quitting = true;

    private void OnDisable()
    {
        if (encounter != null) encounter.Finished -= Play;
        if (!running) return;
        StopAllCoroutines();
        SceneManager.sceneLoaded -= SceneLoaded;
        if (follow != null) follow.Cutscene = false;
        if (manager != null) manager.SetCutsceneWorld(null);
        //do not restart encounters while Unity is closing Play mode
        if (!quitting)
        {
            foreach (Behaviour control in paused) if (control != null) control.enabled = true;
            foreach (Rigidbody2D body in bodies) if (body != null) body.simulated = true;
            foreach (AudioSource source in mutedMusic) if (source != null) source.mute = false;
            foreach (GameObject player in hiddenPlayers) if (player != null) player.SetActive(true);
        }
        foreach (EndingRoom2D effect in effects) if (effect != null) Destroy(effect);
        if (overlay != null) Destroy(overlay.gameObject);
        if (smokeMaterial != null) Destroy(smokeMaterial);
        Time.timeScale = oldTimeScale;
        running = false;
    }
}
