using UnityEngine;

//choose the active room before the worlds update
[DefaultExecutionOrder(-100)]

public sealed class CellWorldRoomManager : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private CellWorld2D[] worlds = new CellWorld2D[0];
    [SerializeField] private PressureTool2D pressureTool;
    
    [SerializeField]
    private PlayerLiquidInteraction2D playerLiquidInteraction;
    public CellWorld2D CurrentWorld { get; private set; }
    private readonly System.Collections.Generic.Dictionary<CellWorld2D, float> effectUntil =
        new System.Collections.Generic.Dictionary<CellWorld2D, float>();

    public CellWorld2D GetWorldAt(Vector2 position)
    {
        CellWorld2D selected = null;
        float smallestArea = float.PositiveInfinity;
        foreach (CellWorld2D world in worlds)
        {
            if (world == null || !world.isActiveAndEnabled || !world.IsInitialized ||
                !world.TryWorldToCell(position, out _)) continue;
            float area = world.Width * world.Height * world.CellSize * world.CellSize *
                Mathf.Abs(world.transform.lossyScale.x * world.transform.lossyScale.y);
            //a broad world must not hide a smaller room and its registered plants
            if (area < smallestArea || (area == smallestArea && world == CurrentWorld))
            {
                selected = world;
                smallestArea = area;
            }
        }
        return selected;
    }
    public void KeepWorldActive(CellWorld2D world)
    {
        if (world == null) return;
        //let spray across a doorway affect the next room before the player enters it
        effectUntil[world] = Time.time + 3f;
        world.SetRoomActive(true);
    }

    [Header("Darkness defaults")]
    [SerializeField] private bool usePixelDarkness = true;
    [SerializeField, Range(0f, 1f)] private float darkness = 0.95f;
    [SerializeField, Range(1, 64)] private int fullyLitLevel = 28;
    [SerializeField, Range(1, 64)] private int fireLightLevel = 28;
    [SerializeField, Range(0.02f, 0.5f)] private float lightRefreshSeconds = 0.2f;
    [SerializeField] private int darknessOrder = 100;
    [SerializeField, Range(0f, 1f)] private float unityLightBase = 0.2f;
    [SerializeField, Range(0f, 4f)] private float unityLightEffect = 1.25f;
    [SerializeField, Range(0f, 1f)] private float unityLightMaximum = 0.85f;

    public void RegisterWorld(CellWorld2D world)
    {
        if (world == null || System.Array.IndexOf(worlds, world) >= 0) return;
        int index = worlds.Length;
        System.Array.Resize(ref worlds, index + 1);
        worlds[index] = world;
        world.SetActivityTarget(player);
        world.SetRoomActive(false);
    }

    private void Update()
    {
        CurrentWorld = player != null ? GetWorldAt(player.position) : null;

        //simulate the player room and any neighbour currently receiving spray
        foreach (CellWorld2D world in worlds)
        {
            if (world != null)
                world.SetRoomActive(world == CurrentWorld ||
                    (effectUntil.TryGetValue(world, out float until) && Time.time < until));
        }
        
        
        //change the active cellWorld
        if (pressureTool != null)
            pressureTool.SetWorld(CurrentWorld);

        if (playerLiquidInteraction != null)
        {
            playerLiquidInteraction.SetWorld(CurrentWorld);
        }
    }

    private void OnDisable()
    {
        //release control if this manager is disabled
        foreach (CellWorld2D world in worlds)
        {
            if (world != null)
                world.SetRoomActive(true);
        }

        CurrentWorld = null;
        
        if (pressureTool != null)
            pressureTool.SetWorld(CurrentWorld);

        if (playerLiquidInteraction != null)
        {
            playerLiquidInteraction.SetWorld(null);
        }
    }
    
    
    
    
}
