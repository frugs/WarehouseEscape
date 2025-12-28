using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GridManager : MonoBehaviour
{
    [Header("Level Settings")]
    [SerializeField] private int levelNumber = 1;
    [SerializeField] private string levelsDirectoryName = "Levels";

    [Header("References")]
    [SerializeField] private TerrainMeshBuilder terrainBuilder;
    [SerializeField] private MenuManager menuManager;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private Transform cameraTransform;

    [Header("Prefabs")]
    [SerializeField] private GameObject playerTile;
    [SerializeField] private GameObject crateTile;
    [SerializeField] private GameObject targetTile;

    // ================= STATE =================

    // The Logical Grid (Data)
    public Cell[,] grid;

    // The Visual Grid (Tracked GameObjects for Movers)
    private GameObject[,] visualGrid;

    private int gridWidth;
    private int gridHeight;
    private int crateCount;
    private int targetCount;

    // Movement / Pathfinding State (for Mouse Click)
    private bool isMovingPlayer = false;
    private List<Cell> playerMovementPath;
    private float moveDelay = 0.3f; // Faster default
    private float currentDelay = 0f;
    private bool isPaused = false;

    private string LevelsDirectory => Path.Combine(Application.dataPath, levelsDirectoryName);

    private void Awake()
    {
        if (terrainBuilder == null) terrainBuilder = GetComponent<TerrainMeshBuilder>();
        if (menuManager == null) menuManager = GetComponent<MenuManager>();
        if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
    }

    private void Start()
    {
        LoadLevel(levelNumber);
    }

    private void Update()
    {
        if (isPaused) return;

        // Mouse Input (Click to Move)
        HandleMouseInput();

        // Path Movement Execution
        if (isMovingPlayer)
        {
            ProcessPlayerPath();
        }
    }

    // ================= LEVEL LOADING =================

    public void LoadLevel(int level)
    {
        levelNumber = level;
        string filePath = Path.Combine(LevelsDirectory, $"Level{levelNumber}.txt");

        if (File.Exists(filePath))
        {
            GenerateGridFromFile(filePath);
        }
        else
        {
            Debug.LogError($"Level file not found at: {filePath}");
        }
    }

    public void GenerateGridFromFile(string filePath)
    {
        // 1. Parse Data
        LevelData data = LevelParser.ParseLevelFile(filePath);
        if (data == null) return;

        CleanupLevel();

        // 2. Setup Logical Grid
        this.grid = data.grid;
        this.gridWidth = data.width;
        this.gridHeight = data.height;
        this.crateCount = data.crateCount;
        this.targetCount = data.targetCount;

        // 3. Initialize Visual Tracker
        this.visualGrid = new GameObject[gridWidth, gridHeight];

        // 4. Build Static Terrain (Floor, Walls, Holes)
        if (terrainBuilder != null)
        {
            terrainBuilder.BuildTerrain(grid, gridWidth, gridHeight);
        }

        // 5. Spawn Dynamic Objects (Player, Crates, Targets)
        SpawnDynamicObjects();

        // 6. Camera Setup
        SetupCamera();

        // 7. Game State
        if (menuManager != null) menuManager.resumeGame();
        isPaused = false;
    }

    private void CleanupLevel()
    {
        // TerrainMeshBuilder handles clearing the static mesh (levelParent).
        // We strictly clear dynamic objects we spawned.
        if (visualGrid != null)
        {
            foreach (var obj in visualGrid)
            {
                if (obj != null) Destroy(obj);
            }
        }

        // Remove old targets (which aren't in visualGrid because they don't move)
        GameObject[] oldTargets = GameObject.FindGameObjectsWithTag("Target"); // Assuming Tag or track in list
        // Better: Track in a list if you don't use tags
        foreach (var t in GameObject.FindObjectsOfType<GameObject>())
        {
            // Simple name check cleanup for safety if tags aren't set
            if (t.name.StartsWith("Target_") || t.name.StartsWith("Crate_") || t.name == "Player")
                Destroy(t);
        }

        visualGrid = null;
        playerController = null;
    }

    private void SpawnDynamicObjects()
    {
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Cell cell = grid[x, y];
                Vector3 pos = GridToWorld(x, y);

                // 1. Targets (Visual Overlays)
                if (cell.isTarget)
                {
                    GameObject t = Instantiate(targetTile, pos, Quaternion.identity);
                    t.name = $"Target_{x}_{y}";
                    // We don't track targets in visualGrid because they don't move
                }

                // 2. Dynamic Occupants
                if (cell.occupant == Occupant.Player)
                {
                    GameObject p = Instantiate(playerTile, pos, Quaternion.identity);
                    p.name = "Player";
                    playerController = p.GetComponent<PlayerController>();
                    visualGrid[x, y] = p; // Track in Visual Grid
                }
                else if (cell.occupant == Occupant.Crate)
                {
                    GameObject c = Instantiate(crateTile, pos, Quaternion.identity);
                    c.name = $"Crate_{x}_{y}";
                    visualGrid[x, y] = c; // Track in Visual Grid
                }
            }
        }
    }

    // ================= CORE MOVEMENT LOGIC =================

    /// <summary>
    /// Moves an entity (Player or Crate) from A to B.
    /// Updates Logic AND Visuals.
    /// </summary>
    public void MoveEntity(Vector2Int from, Vector2Int to)
    {
        if (!IsValidPos(from) || !IsValidPos(to)) return;

        Cell fromCell = grid[from.x, from.y];
        Cell toCell = grid[to.x, to.y];
        GameObject obj = visualGrid[from.x, from.y];

        // 1. Update Logic
        toCell.occupant = fromCell.occupant;
        fromCell.occupant = Occupant.Empty;

        // 2. Handle Hole Logic (Crate falls in)
        if (toCell.terrain == TerrainType.Hole && toCell.occupant == Occupant.Crate)
        {
            toCell.FillHole();
            // Optional: Destroy crate visual here if you want it to disappear
            // Destroy(obj); 
            // obj = null; 
            // But usually we keep it to show the hole is plugged.
        }

        // 3. Update Visual Tracker
        visualGrid[to.x, to.y] = obj;
        visualGrid[from.x, from.y] = null;

        // 4. Update Transform
        if (obj != null)
        {
            obj.transform.position = GridToWorld(to.x, to.y);
        }
    }

    // ================= COORDINATE MAPPING =================

    /// <summary>
    /// Convert Grid (X, Y) to World (X, 0, Z).
    /// Grid Y+ maps to World Z+ (North).
    /// </summary>
    public Vector3 GridToWorld(int x, int y)
    {
        // Tiles are 1x1, centered at 0.5
        return new Vector3(x + 0.5f, 0f, y + 0.5f);
    }

    /// <summary>
    /// Convert World (X, 0, Z) to Grid (X, Y).
    /// </summary>
    public Cell GetCellAtWorldPos(Vector3 worldPos)
    {
        int x = Mathf.FloorToInt(worldPos.x);
        int y = Mathf.FloorToInt(worldPos.z);
        return GetCell(x, y);
    }

    public Cell GetCell(int x, int y)
    {
        if (IsValidPos(new Vector2Int(x, y)))
        {
            return grid[x, y];
        }
        return null;
    }

    public bool IsValidPos(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;
    }

    // ================= INPUT & PATHFINDING (Legacy) =================

    private void HandleMouseInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                Cell clickedCell = GetCellAtWorldPos(hit.point);
                if (clickedCell != null && playerController != null)
                {
                    Cell startCell = GetCellAtWorldPos(playerController.transform.position);
                    FindPathAndMove(startCell, clickedCell);
                }
            }
        }
    }

    private void FindPathAndMove(Cell start, Cell target)
    {
        if (start == null || target == null) return;

        // BFS for shortest path
        Queue<Cell> queue = new Queue<Cell>();
        Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

        queue.Enqueue(start);
        cameFrom[start] = null;

        while (queue.Count > 0)
        {
            Cell current = queue.Dequeue();
            if (current == target)
            {
                // Reconstruct Path
                playerMovementPath = new List<Cell>();
                while (current != start)
                {
                    playerMovementPath.Add(current);
                    current = cameFrom[current];
                }
                playerMovementPath.Reverse();
                isMovingPlayer = true;
                if (playerController) playerController.isMoving = true;
                return;
            }

            foreach (var neighbor in GetNeighbors(current))
            {
                if (!cameFrom.ContainsKey(neighbor) && neighbor.IsPathableForPlayer)
                {
                    cameFrom[neighbor] = current;
                    queue.Enqueue(neighbor);
                }
            }
        }
    }

    private void ProcessPlayerPath()
    {
        if (playerMovementPath == null || playerMovementPath.Count == 0)
        {
            isMovingPlayer = false;
            if (playerController) playerController.isMoving = false;
            return;
        }

        currentDelay += Time.deltaTime;
        if (currentDelay >= moveDelay)
        {
            Cell nextCell = playerMovementPath[0];
            Cell currentCell = GetCellAtWorldPos(playerController.transform.position);

            // Execute Move using core logic
            MoveEntity(new Vector2Int(currentCell.x, currentCell.y), new Vector2Int(nextCell.x, nextCell.y));

            playerMovementPath.RemoveAt(0);
            currentDelay = 0f;
        }
    }

    private List<Cell> GetNeighbors(Cell cell)
    {
        List<Cell> list = new List<Cell>();
        int[] dx = { 0, 0, 1, -1 };
        int[] dy = { 1, -1, 0, 0 }; // Up, Down, Right, Left

        for (int i = 0; i < 4; i++)
        {
            Cell n = GetCell(cell.x + dx[i], cell.y + dy[i]);
            if (n != null) list.Add(n);
        }
        return list;
    }

    // ================= GAME LOGIC =================

    public void CheckWinCondition()
    {
        int cratesOnTargets = 0;

        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                Cell c = grid[x, y];
                if (c.occupant == Occupant.Crate && c.isTarget)
                {
                    cratesOnTargets++;
                }
            }
        }

        if (cratesOnTargets >= crateCount)
        {
            Debug.Log("Level Complete!");
            isPaused = true;
            if (menuManager) menuManager.winGame();
        }
    }

    public void ResetLevel()
    {
        LoadLevel(levelNumber);
    }

    public void NextLevel()
    {
        levelNumber++;
        LoadLevel(levelNumber);
    }

    private void SetupCamera()
    {
        if (cameraTransform != null)
        {
            // Center camera over the grid
            float centerX = gridWidth / 2.0f;
            float centerZ = gridHeight / 2.0f;

            // Position camera up high, looking down
            cameraTransform.position = new Vector3(centerX, Mathf.Max(gridWidth, gridHeight) * 0.8f + 5f, centerZ - 2f);
            cameraTransform.rotation = Quaternion.Euler(70f, 0f, 0f); // Slight angle is often better than 90
        }
    }
}
