using UnityEngine;

//wake the placed boss when the player reaches this part of the level
[RequireComponent(typeof(BoxCollider2D))]
public sealed class BossWake2D : MonoBehaviour
{
    [SerializeField] private BossGrapple2D boss;
    [SerializeField] private BossEncounter2D encounter;
    private bool triggered;

    private void Awake()
    {
        GetComponent<BoxCollider2D>().isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered || (boss == null && encounter == null)) return;

        PlayerVitals2D player = other.GetComponentInParent<PlayerVitals2D>();
        if (player == null || player.IsDead) return;

        triggered = true;
        if (encounter != null)
            encounter.Wake();
        else
            boss.SetAggro(true);
    }
}
