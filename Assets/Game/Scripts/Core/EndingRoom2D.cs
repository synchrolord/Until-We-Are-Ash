using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

//a few maintained fires and a scenery tint, only while this room is on camera
[DefaultExecutionOrder(100)]
public sealed class EndingRoom2D : MonoBehaviour
{
    private CellWorld2D world;
    private readonly List<Vector2> fires = new List<Vector2>();
    private Tilemap[] maps;
    private Color[] mapColors;
    private SpriteRenderer[] sprites;
    private Color[] spriteColors;
    private TilemapRenderer[] mapRenderers;
    private Material[] mapMaterials;
    private Material[] spriteMaterials;
    private Color charTint;
    private int fireSize;
    private float interval;
    private float nextFire;
    private float charAmount;
    private float lastCharAmount = -1f;

    public void Setup(CellWorld2D source, Transform room, int count, int radius, float seconds, Color tint, float strength, Material material)
    {
        world = source;
        fireSize = radius;
        interval = seconds;
        //keep the texture detail instead of multiplying already-dark scenery almost to black
        tint.a = 1f;
        charTint = Color.Lerp(Color.white, tint, strength);
        maps = room.GetComponentsInChildren<Tilemap>(true);
        mapColors = new Color[maps.Length];
        mapRenderers = new TilemapRenderer[maps.Length];
        mapMaterials = new Material[maps.Length];
        for (int i = 0; i < maps.Length; i++)
        {
            mapColors[i] = maps[i].color;
            mapRenderers[i] = maps[i].GetComponent<TilemapRenderer>();
            if (mapRenderers[i] == null) continue;
            mapMaterials[i] = mapRenderers[i].sharedMaterial;
            if (material != null) mapRenderers[i].sharedMaterial = material;
        }
        List<SpriteRenderer> scenery = new List<SpriteRenderer>();
        foreach (SpriteRenderer sprite in room.GetComponentsInChildren<SpriteRenderer>(true))
            if (sprite.GetComponentInParent<CellWorld2D>() == null &&
                sprite.GetComponentInParent<vineController>() == null) scenery.Add(sprite);
        sprites = scenery.ToArray();
        spriteColors = new Color[sprites.Length];
        spriteMaterials = new Material[sprites.Length];
        for (int i = 0; i < sprites.Length; i++)
        {
            spriteColors[i] = sprites[i].color;
            spriteMaterials[i] = sprites[i].sharedMaterial;
            if (material != null) sprites[i].sharedMaterial = material;
        }

        //sample once per room, not every frame; favor platforms over floating fire
        System.Random random = new System.Random(room.name.GetHashCode());
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        int ground = LayerMask.GetMask("Ground");
        float height = world.Height * world.CellSize;
        for (int attempt = 0; attempt < count * 16 && fires.Count < count; attempt++)
        {
            int x = random.Next(1, world.Width - 1);
            int y = random.Next(1, world.Height - 1);
            if (Mathf.PerlinNoise(x * 0.03f, y * 0.03f) < 0.4f) continue;
            Vector2 point = world.CellToWorld(x, y);
            RaycastHit2D hit = Physics2D.Raycast(point, Vector2.down, height, ground);
            if (hit.collider != null)
            {
                if (hit.distance < 0.01f || hit.collider.isTrigger) continue;
                point = hit.point + hit.normal * (world.CellSize * 2f);
            }
            if (!world.TryWorldToCell(point, out _)) continue;
            Vector2Int key = Vector2Int.RoundToInt(point * 2f);
            if (used.Add(key)) fires.Add(point);
        }
    }

    private void Update()
    {
        if (world == null || !world.IsInitialized || !world.IsRoomActive) return;
        charAmount = Mathf.MoveTowards(charAmount, 1f, Time.unscaledDeltaTime / 3f);
        if (Time.unscaledTime < nextFire) return;
        nextFire = Time.unscaledTime + interval;
        foreach (Vector2 point in fires) world.Ignite(point, fireSize, 3);
    }

    private void LateUpdate()
    {
        if (maps == null || charAmount == lastCharAmount) return;
        lastCharAmount = charAmount;
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null)
                maps[i].color = Color.Lerp(mapColors[i], mapColors[i] * charTint, charAmount);
        for (int i = 0; i < sprites.Length; i++)
        {
            SpriteRenderer sprite = sprites[i];
            if (sprite == null) continue;
            sprite.color = Color.Lerp(spriteColors[i], spriteColors[i] * charTint, charAmount);
        }
    }

    private void OnDestroy()
    {
        if (maps == null) return;
        for (int i = 0; i < maps.Length; i++)
            if (maps[i] != null)
            {
                maps[i].color = mapColors[i];
                if (mapRenderers[i] != null) mapRenderers[i].sharedMaterial = mapMaterials[i];
            }
        for (int i = 0; i < sprites.Length; i++)
            if (sprites[i] != null)
            {
                sprites[i].color = spriteColors[i];
                sprites[i].sharedMaterial = spriteMaterials[i];
            }
    }
}
