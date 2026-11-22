using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class DeathZone2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float edgeMargin = 0.08f;
    private Collider2D zone;
    private readonly List<Collider2D> hits = new List<Collider2D>();

    private void Awake()
    {
        zone = GetComponent<Collider2D>();
        zone.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other) => CheckPlayer(other);
    private void OnTriggerStay2D(Collider2D other) => CheckPlayer(other);

    private void CheckPlayer(Collider2D other)
    {
        PlayerVitals2D player = other.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;

        if (edgeMargin > 0f)
        {
            //ignore a small strip around the player so grazing a spike corner is safe
            Bounds bounds = other.bounds;
            Vector2 size = new Vector2(
                Mathf.Max(0.01f, bounds.size.x - edgeMargin * 2f),
                Mathf.Max(0.01f, bounds.size.y - edgeMargin * 2f));
            ContactFilter2D filter = new ContactFilter2D();
            filter.SetLayerMask(1 << gameObject.layer);
            filter.useTriggers = true;
            Physics2D.OverlapBox(bounds.center, size, 0f, filter, hits);
            if (!hits.Contains(zone)) return;
        }

        player.Kill();
    }
}