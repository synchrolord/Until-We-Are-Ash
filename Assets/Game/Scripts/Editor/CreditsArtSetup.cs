using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

//resolve imported Aseprite subassets without manually dragging every animation clip
public static class CreditsArtSetup
{
    private const string DataPath = "Assets/Game/Data/Ending Credits.asset";
    [InitializeOnLoadMethod]
    private static void Queue()
    {
        EditorApplication.delayCall += () =>
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode) Refresh();
        };
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += Refresh;
        };
    }

    [MenuItem("Tools/Until We Are Ash/Refresh Credits Art")]
    public static void Refresh()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        CreditsData data = AssetDatabase.LoadAssetAtPath<CreditsData>(DataPath);
        if (data == null) return;
        bool changed = false;
        int missing = 0;
        foreach (CreditsData.Section section in data.sections)
            foreach (CreditsData.Preview preview in new[] { section.left, section.right })
            {
                if (preview == null) continue;
                string path = preview.animation != null ? AssetDatabase.GetAssetPath(preview.animation) : preview.sourcePath;
                if (string.IsNullOrEmpty(path)) continue;
                Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path) ?? assets.OfType<GameObject>().FirstOrDefault();
                AnimationClip[] clips = assets.OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__"))
                    .OrderBy(c => c.name).ToArray();
                if (prefab == null)
                {
                    //sprite-sheet imports have no prefab, so show their individual sprites
                    Sprite[] sprites = assets.OfType<Sprite>().OrderBy(sprite => sprite.name).ToArray();
                    if (sprites.Length == 0) { missing++; continue; }
                    if (preview.animation != null || preview.sprites == null || !preview.sprites.SequenceEqual(sprites))
                    {
                        preview.animation = null;
                        preview.sprites = sprites;
                        preview.clips = new AnimationClip[0];
                        changed = true;
                    }
                    continue;
                }
                if (preview.animation != prefab || preview.clips == null || !preview.clips.SequenceEqual(clips))
                {
                    preview.animation = prefab;
                    preview.clips = clips;
                    changed = true;
                }
            }
        if (changed) { EditorUtility.SetDirty(data); AssetDatabase.SaveAssetIfDirty(data); }
        File.WriteAllText("Library/CreditsArtCheck.txt", "Missing previews: " + missing);
    }
}
