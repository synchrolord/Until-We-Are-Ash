using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public sealed class Checkpoint2D : MonoBehaviour
{
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform bossSpawnPoint;
    [SerializeField] private Vector2 bossOffset = new Vector2(-8f, 2f);

    public Transform PlayerSpawn => spawnPoint != null ? spawnPoint : transform;
    public Transform BossSpawn => bossSpawnPoint;

    private void Awake()
    {
        if (bossSpawnPoint != null) return;

        //keep the usual checkpoint and give MABEL her own nearby position
        bossSpawnPoint = new GameObject("MABEL Spawn").transform;
        bossSpawnPoint.SetParent(PlayerSpawn, false);
        bossSpawnPoint.position = PlayerSpawn.position + (Vector3)bossOffset;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Vector3 point = bossSpawnPoint != null
            ? bossSpawnPoint.position : PlayerSpawn.position + (Vector3)bossOffset;
        Gizmos.DrawWireSphere(point, 0.5f);
        Gizmos.DrawLine(PlayerSpawn.position, point);
    }

    public void Configure(Transform playerPoint, Transform creaturePoint)
    {
        spawnPoint = playerPoint;
        bossSpawnPoint = creaturePoint;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerRespawn2D player =
            other.GetComponentInParent<PlayerRespawn2D>();

        if (player == null) return;

        //remember the last checkpoint touched
        player.SetCheckpoint(
            spawnPoint != null ? spawnPoint : transform,
            bossSpawnPoint);
    }
}
