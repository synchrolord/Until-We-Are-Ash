using UnityEngine;

//one shared simulation for all the screens and tilemaps in this room
[DefaultExecutionOrder(-200)]
public sealed class Room2D : MonoBehaviour
{
    [Tooltip("Room width and height in world units. The room's position is its bottom-left corner.")]
    [SerializeField] private Vector2 size = new Vector2(80f, 22.5f);
    [Tooltip("Smaller cells give more detail and cost more. Resize the room here instead of scaling its Transform.")]
    [SerializeField, Min(0.0625f)] private float cellSize = 0.25f;
    [SerializeField] private CellWorld2D world;
    public Vector2 Size => size;
    public CellWorld2D World => world;

    public void Configure(CellWorld2D source) { world = source; ApplySize(); }

    public bool ApplySize()
    {
        if (world == null) return false;
        int columns = Mathf.CeilToInt(size.x / cellSize);
        int rows = Mathf.CeilToInt(size.y / cellSize);
        if (columns < 16 || rows < 16 || columns > 2048 || rows > 2048 || (long)columns * rows > 1048576)
        {
            Debug.LogError("Room size needs 16–2048 cells per side and at most 1,048,576 cells. Increase Cell Size for a larger room.", this);
            return false;
        }
        //resizing should not move a world the level designer has already positioned
        world.ConfigureGrid(columns, rows, cellSize);
        return true;
    }

    //the saved CellWorld bounds are already configured in the editor; keep them at runtime

    private void Start()
    {
        CellWorldRoomManager manager = FindAnyObjectByType<CellWorldRoomManager>();
        if (manager != null) manager.RegisterWorld(world);
        CameraFollow2D camera = FindAnyObjectByType<CameraFollow2D>();
        if (camera != null) camera.UseRooms(true);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.matrix = world != null ? world.transform.localToWorldMatrix : transform.localToWorldMatrix;
        Vector2 actualSize = world != null ? new Vector2(world.Width, world.Height) * world.CellSize : size;
        Gizmos.DrawWireCube(actualSize * 0.5f, actualSize);
    }
}
