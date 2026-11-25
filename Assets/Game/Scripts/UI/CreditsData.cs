using System.Collections.Generic;
using TMPro;
using UnityEngine;

[CreateAssetMenu(menuName = "BURN/Credits")]
public sealed class CreditsData : ScriptableObject
{
    [System.Serializable]
    public class Preview
    {
        public string caption;
        public GameObject animation;
        public Sprite[] sprites = new Sprite[0];
        [HideInInspector] public AnimationClip[] clips = new AnimationClip[0];
        [HideInInspector] public string sourcePath;
    }

    [System.Serializable]
    public class Section
    {
        public string title;
        [TextArea(3, 8)] public string text;
        public Preview left;
        public Preview right;
    }

    public string creatorName = "synchrolord";
    public string professorName = "Dr. blank";
    public TMP_FontAsset font;
    public Font arabicFont;
    public TextAsset poem;
    [TextArea(2, 5)] public string closingText = "This game will be finished during next semester.\nThanks for playing!";
    public Section[] sections = new Section[0];

    [System.NonSerialized] private TMP_FontAsset displayFont;

    public TMP_FontAsset DisplayFont
    {
        get
        {
            if (displayFont != null) return displayFont;
            displayFont = Instantiate(font != null ? font : TMP_Settings.defaultFontAsset);
            displayFont.name = "Credits display font";
            displayFont.fallbackFontAssetTable = displayFont.fallbackFontAssetTable != null
                ? new List<TMP_FontAsset>(displayFont.fallbackFontAssetTable) : new List<TMP_FontAsset>();
            //include the font in the project so the Arabic name also works in builds
            if (arabicFont != null)
            {
                TMP_FontAsset arabic = TMP_FontAsset.CreateFontAsset(arabicFont, 64, 6,
                    UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 256, 256);
                if (arabic != null)
                {
                    arabic.TryAddCharacters("\uFEDE\uFEB4\uFECB");
                    displayFont.fallbackFontAssetTable.Add(arabic);
                }
            }
            return displayFont;
        }
    }

    public string Format(string text)
    {
        //presentation forms keep this short Arabic name joined inside English credits
        return (text ?? "").Replace("{creator}", creatorName).Replace("{professor}", professorName)
            .Replace("عسل", "\uFEDE\uFEB4\uFECB");
    }
}
