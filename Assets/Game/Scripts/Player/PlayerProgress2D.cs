using UnityEngine;
using UnityEngine.SceneManagement;

//continue restarts the saved level; burned plants and other room changes are not saved
[RequireComponent(typeof(PlayerVitals2D))]
public sealed class PlayerProgress2D : MonoBehaviour
{
    [System.Serializable]
    private class SaveData
    {
        public string scene;
        public int infection;
        public int items;
    }

    private const string SaveKey = "BURN.Continue";
    private static int restoreStage = -1;
    private PlayerVitals2D vitals;
    private string scene;
    private bool ready;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetPending() => restoreStage = -1;

    public static bool EndingActive
    {
        get
        {
            EndingCutscene2D ending = FindAnyObjectByType<EndingCutscene2D>();
            return ending != null && ending.IsRunning;
        }
    }

    private static SaveData ReadSave()
    {
        if (!PlayerPrefs.HasKey(SaveKey)) return null;
        try { return JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); }
        catch (System.ArgumentException) { return null; }
    }

    public static bool CanContinue
    {
        get
        {
            SaveData data = ReadSave();
            return data != null && !string.IsNullOrEmpty(data.scene) &&
                Application.CanStreamedLevelBeLoaded(data.scene);
        }
    }

    public static void NewGame(string firstScene)
    {
        if (!Application.CanStreamedLevelBeLoaded(firstScene))
        { Debug.LogError("Start scene is missing from the build: " + firstScene); return; }
        ClearSave();
        PlayerUnlocks2D.ResetItems();
        SceneManager.LoadScene(firstScene);
    }

    public static void ClearSave()
    {
        //only remove Continue progress; volume and other settings keep their values
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        restoreStage = -1;
    }

    public static void ContinueGame()
    {
        if (!CanContinue) return;
        SaveData data = ReadSave();
        restoreStage = data.infection;
        PlayerUnlocks2D.Restore(data.items);
        SceneManager.LoadScene(data.scene);
    }

    private void Start()
    {
        if (EndingActive) return;
        vitals = GetComponent<PlayerVitals2D>();
        scene = SceneManager.GetActiveScene().name;
        if (restoreStage >= 0)
        {
            vitals.ResetForSpawn(restoreStage);
            restoreStage = -1;
        }
        ready = true;
        Save();
    }

    public void Save()
    {
        if (!ready || vitals == null || EndingActive) return;
        SaveData data = new SaveData
        { scene = scene, infection = vitals.InfectionStage, items = PlayerUnlocks2D.GetMask() };
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data));
        PlayerPrefs.Save();
    }

    private void OnDisable() => Save();
    private void OnApplicationPause(bool paused) { if (paused) Save(); }
}
