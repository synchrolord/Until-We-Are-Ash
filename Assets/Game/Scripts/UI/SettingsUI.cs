using UnityEngine;

//both menus draw the same controls and use the same saved preferences
public static class SettingsUI
{
    public static void Draw(GUIStyle label, GUIStyle toggle)
    {
        GUILayout.Label("Volume: " + Mathf.RoundToInt(GameOptions.MasterVolume * 100f) + "%", label);
        float volume = GUILayout.HorizontalSlider(GameOptions.MasterVolume, 0f, 1f);
        if (!Mathf.Approximately(volume, GameOptions.MasterVolume)) GameOptions.SetMasterVolume(volume);
        bool flicker = GUILayout.Toggle(GameOptions.LightFlicker, "Light Flicker", toggle);
        if (flicker != GameOptions.LightFlicker) GameOptions.SetLightFlicker(flicker);
    }
}
