using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

//show the real imported animation, or cycle a small list of artwork sprites
public sealed class CreditsPreview2D : MonoBehaviour
{
    private GameObject model;
    private GameObject sampleRoot;
    private Transform fit;
    private Transform offset;
    private AnimationClip[] clips;
    private Sprite[] frames;
    private SpriteRenderer image;
    private SpriteRenderer[] images;
    private int index;
    private float time;
    private bool liveVines;
    private Material artMaterial;

    public void Setup(CreditsData.Preview preview, Material material)
    {
        artMaterial = material;
        fit = new GameObject("Fit").transform;
        fit.SetParent(transform, false);
        offset = new GameObject("Center").transform;
        offset.SetParent(fit, false);
        clips = preview.clips;
        frames = preview.sprites;
        if (preview.animation != null)
        {
            model = Instantiate(preview.animation, offset, false);
            sampleRoot = model;
            Animator[] animators = model.GetComponentsInChildren<Animator>(true);
            List<AnimationClip> found = new List<AnimationClip>();
            foreach (Animator animator in animators)
            {
                sampleRoot = animator.gameObject;
                if (animator.runtimeAnimatorController != null)
                    foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        if (clip != null && !found.Contains(clip)) found.Add(clip);
                animator.enabled = false;
            }
            if (clips == null || clips.Length == 0) clips = found.ToArray();
            liveVines = model.GetComponent<vineController>() != null;
        }
        else
        {
            model = new GameObject("Sprite");
            model.transform.SetParent(offset, false);
            image = model.AddComponent<SpriteRenderer>();
            if (frames != null && frames.Length > 0) image.sprite = frames[0];
        }
        if (clips == null) clips = new AnimationClip[0];
        if (frames == null) frames = new Sprite[0];
        if (clips.Length > 0 && clips[0] != null) clips[0].SampleAnimation(sampleRoot, 0f);
        images = model.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (SpriteRenderer sprite in images)
        {
            sprite.sortingOrder = 3;
            sprite.sharedMaterial = material;
        }
        foreach (SortingGroup group in model.GetComponentsInChildren<SortingGroup>(true))
            group.sortingOrder = 3;
        if (!liveVines) FitArtwork();
    }

    private IEnumerator Start()
    {
        if (!liveVines) yield break;
        yield return null;
        yield return null;
        foreach (VineRope2D rope in model.GetComponentsInChildren<VineRope2D>()) rope.Settle(100);
        foreach (SpriteRenderer sprite in model.GetComponentsInChildren<SpriteRenderer>(true))
            sprite.sharedMaterial = artMaterial;
        //these anchors were authored to fit the square; scaling afterward would drag the rope points
    }

    private void FitArtwork()
    {
        bool found = false;
        Bounds bounds = new Bounds();
        foreach (SpriteRenderer sprite in images)
        {
            if (sprite.sprite == null) continue;
            if (!found) { bounds = sprite.bounds; found = true; }
            else bounds.Encapsulate(sprite.bounds);
        }
        if (!found) return;
        offset.localPosition -= fit.InverseTransformPoint(bounds.center);
        float size = Mathf.Max(bounds.size.x, bounds.size.y);
        fit.localScale = Vector3.one * (4.4f / Mathf.Max(0.01f, size));
    }

    private void Update()
    {
        if (liveVines || images == null) return;
        bool visible = false;
        foreach (SpriteRenderer sprite in images)
            if (sprite != null && sprite.isVisible) { visible = true; break; }
        if (!visible) return;
        time += Time.unscaledDeltaTime;
        if (clips.Length > 0)
        {
            AnimationClip clip = clips[index];
            if (clip == null) { index = (index + 1) % clips.Length; time = 0f; return; }
            clip.SampleAnimation(sampleRoot, Mathf.Min(time, clip.length));
            if (time >= Mathf.Max(0.15f, clip.length) + 0.2f)
            { index = (index + 1) % clips.Length; time = 0f; }
        }
        else if (image != null && frames.Length > 0 && time >= 0.6f)
        {
            time = 0f;
            index = (index + 1) % frames.Length;
            image.sprite = frames[index];
        }
    }
}
