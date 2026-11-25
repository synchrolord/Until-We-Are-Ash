using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

//count shots in the editor so the game never has to preload all levels together
public sealed class EndingTimingSetup : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;
    public void OnPreprocessBuild(BuildReport report) => Refresh();

    [InitializeOnLoadMethod]
    private static void Setup()
    {
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.ExitingEditMode) Refresh();
        };
    }

    [MenuItem("Tools/Until We Are Ash/Refresh Ending Timing")]
    public static void Refresh()
    {
        if (Application.isPlaying) return;
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Prefabs/Levels/MABEL Fight.prefab");
        if (prefab == null) return;
        EndingCutscene2D ending = prefab.GetComponent<EndingCutscene2D>();
        if (ending == null) return;
        SerializedObject data = new SerializedObject(ending);
        List<string> names = new List<string>();
        List<int> counts = new List<int>();
        foreach (EditorBuildSettingsScene entry in EditorBuildSettings.scenes)
        {
            string name = Path.GetFileNameWithoutExtension(entry.path);
            if (!entry.enabled || !name.StartsWith("Floor_") || !int.TryParse(name.Substring(6), out _)) continue;
            Scene scene = SceneManager.GetSceneByPath(entry.path);
            bool preview = !scene.isLoaded;
            if (preview) scene = EditorSceneManager.OpenPreviewScene(entry.path);
            try
            {
                List<CellWorld2D> worlds = new List<CellWorld2D>();
                List<CameraZone2D> screens = new List<CameraZone2D>();
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (CellWorld2D world in root.GetComponentsInChildren<CellWorld2D>(true))
                        if (world.enabled && world.gameObject.activeInHierarchy) worlds.Add(world);
                    foreach (CameraZone2D screen in root.GetComponentsInChildren<CameraZone2D>(true))
                        if (screen.enabled && screen.gameObject.activeInHierarchy && screen.IsStatic) screens.Add(screen);
                }
                int screenCount = 0;
                if (name == "Floor_01")
                    foreach (CameraZone2D screen in screens)
                        foreach (CellWorld2D world in worlds)
                        {
                            Vector3 point = world.transform.InverseTransformPoint(screen.transform.position);
                            if (point.x < 0f || point.y < 0f || point.x >= world.Width * world.CellSize || point.y >= world.Height * world.CellSize) continue;
                            screenCount++;
                            break;
                        }
                names.Add(name);
                counts.Add(screenCount > 0 ? screenCount : worlds.Count);
            }
            finally { if (preview) EditorSceneManager.ClosePreviewScene(scene); }
        }
        SerializedProperty saved = data.FindProperty("sceneCounts");
        saved.arraySize = names.Count;
        for (int i = 0; i < names.Count; i++)
        {
            SerializedProperty row = saved.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("scene").stringValue = names[i];
            row.FindPropertyRelative("shots").intValue = counts[i];
        }
        if (data.ApplyModifiedPropertiesWithoutUndo()) PrefabUtility.SavePrefabAsset(prefab);
    }
}
