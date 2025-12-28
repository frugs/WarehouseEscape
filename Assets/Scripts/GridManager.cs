using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class GridManager : MonoBehaviour {
  [Header("Level Settings")] [SerializeField]
  private int levelNumber = 1;

  [SerializeField] private string levelsDirectoryName = "Levels";

  [Header("References")] [SerializeField]
  private TerrainMeshBuilder terrainBuilder;

  [SerializeField] private MenuManager menuManager;
  [SerializeField] private PlayerController playerController;
  [SerializeField] private Transform cameraTransform;

  [Header("Prefabs")] [SerializeField] private GameObject playerTile;
  [SerializeField] private GameObject crateTile;
  [SerializeField] private GameObject targetTile;

  [Header("Animation Timing")] [SerializeField]
  private float moveAnimationDuration = 0.2f; // Snappy movement

  [SerializeField] private float fallAnimationDuration = 0.15f;

  // ================= STATE =================
  public Cell[,] grid;
  private GameObject[,] visualGrid; // Visual representation only
  private int gridWidth;
  private int gridHeight;
  private int crateCount;

  // Movement State
  private bool isMovingPlayer = false; // For mouse pathfinding
  private List<Cell> playerMovementPath;
  private float moveDelay = 0.25f;
  private float currentDelay = 0f;
  private bool isPaused = false;

  private string LevelsDirectory => Path.Combine(Application.dataPath, levelsDirectoryName);

  private void Awake() {
    if (terrainBuilder == null) terrainBuilder = GetComponent<TerrainMeshBuilder>();
    if (menuManager == null) menuManager = GetComponent<MenuManager>();
    if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
  }

  private void Start() {
    LoadLevel(levelNumber);
  }

  private void Update() {
    if (isPaused) return;
    HandleMouseInput();
    if (isMovingPlayer) ProcessPlayerPath();
  }

  // ================= LEVEL LOADING =================
  public void LoadLevel(int level) {
    levelNumber = level;
    string filePath = Path.Combine(LevelsDirectory, $"Level{levelNumber}.txt");
    if (File.Exists(filePath)) GenerateGridFromFile(filePath);
    else Debug.LogError($"Level file not found at: {filePath}");
  }

  public void GenerateGridFromFile(string filePath) {
    LevelData data = LevelParser.ParseLevelFile(filePath);
    if (data == null) return;

    CleanupLevel();
    this.grid = data.grid;
    this.gridWidth = data.width;
    this.gridHeight = data.height;
    this.crateCount = data.crateCount;
    this.visualGrid = new GameObject[gridWidth, gridHeight];

    if (terrainBuilder != null) terrainBuilder.BuildTerrain(grid, gridWidth, gridHeight);
    SpawnDynamicObjects();
    SetupCamera();

    if (menuManager != null) menuManager.resumeGame();
    isPaused = false;
  }

  private void CleanupLevel() {
    if (visualGrid != null) {
      foreach (var obj in visualGrid)
        if (obj != null)
          Destroy(obj);
    }

    foreach (var t in GameObject.FindObjectsOfType<GameObject>()) {
      // Cleanup any stray objects including completed crates
      if (t.name.StartsWith("Target_") || t.name.StartsWith("Crate_") ||
          t.name.StartsWith("FilledHoleCrate_") || t.name == "Player")
        Destroy(t);
    }

    visualGrid = null;
    // Re-assign controller if it was destroyed
    if (playerController == null) playerController = FindObjectOfType<PlayerController>();
  }

  private void SpawnDynamicObjects() {
    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        Cell cell = grid[x, y];
        Vector3 pos = GridToWorld(x, y);

        if (cell.isTarget) {
          GameObject t = Instantiate(targetTile, pos, Quaternion.identity);
          t.name = $"Target_{x}_{y}";
        }

        if (cell.occupant == Occupant.Player) {
          GameObject p = Instantiate(playerTile, pos, Quaternion.identity);
          p.name = "Player";
          if (playerController != null) {
            // Reposition existing controller or attach new logic
            // Ideally we assume the controller script is on the prefab
          }

          // Update our reference if the prefab has the script
          playerController = p.GetComponent<PlayerController>();
          visualGrid[x, y] = p;
        } else if (cell.occupant == Occupant.Crate) {
          GameObject c = Instantiate(crateTile, pos, Quaternion.identity);
          c.name = $"Crate_{x}_{y}";
          visualGrid[x, y] = c;
        }
      }
    }
  }

  private void SetupCamera() {
    if (cameraTransform == null) return;
    // Simple centering logic
    cameraTransform.position = new Vector3(gridWidth / 2.0f,
      Mathf.Max(gridWidth, gridHeight) * 0.8f + 5f,
      gridHeight / 2.0f - 2f);
    cameraTransform.LookAt(new Vector3(gridWidth / 2.0f, 0, gridHeight / 2.0f));
  }

  // ================= CORE UPDATE LOGIC =================

  /// <summary>
  /// Updates BOTH the Logic Data (Cells) and the Visual Array (GameObject references).
  /// Returns the GameObjects that need to be animated so the caller doesn't need to look them up.
  /// </summary>
  public void RegisterMoveUpdates(SokobanMove move, out GameObject playerObj,
    out GameObject crateObj) {
    playerObj = null;
    crateObj = null;

    // 1. Capture Objects (before we clear the grid cells)
    if (IsValidPos(move.playerFrom))
      playerObj = visualGrid[move.playerFrom.x, move.playerFrom.y];

    if (move.type == MoveType.CratePush && IsValidPos(move.crateFrom))
      crateObj = visualGrid[move.crateFrom.x, move.crateFrom.y];

    // 2. Update Data Model (The Truth)
    grid[move.playerFrom.x, move.playerFrom.y].occupant = Occupant.Empty;
    if (move.type == MoveType.CratePush)
      grid[move.crateFrom.x, move.crateFrom.y].occupant = Occupant.Empty;

    grid[move.playerTo.x, move.playerTo.y].occupant = Occupant.Player;

    if (move.type == MoveType.CratePush) {
      Cell target = grid[move.crateTo.x, move.crateTo.y];
      if (target.terrain == TerrainType.Hole) {
        target.FillHole();
        target.occupant = Occupant.Empty; // Crate becomes part of floor logic
      } else {
        target.occupant = Occupant.Crate;
      }
    }

    // 3. Update Visual Grid Pointers (The References)
    visualGrid[move.playerFrom.x, move.playerFrom.y] = null;
    if (move.type == MoveType.CratePush)
      visualGrid[move.crateFrom.x, move.crateFrom.y] = null;

    if (playerObj != null)
      visualGrid[move.playerTo.x, move.playerTo.y] = playerObj;

    if (crateObj != null) {
      // We keep tracking the visual object even if it "falls in a hole"
      // so we don't lose the reference until we destroy/change it.
      visualGrid[move.crateTo.x, move.crateTo.y] = crateObj;
    }
  }

  // ================= PURE ANIMATION =================

  public IEnumerator AnimateTransform(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridToWorld(targetGridPos.x, targetGridPos.y);
    float elapsed = 0f;

    while (elapsed < moveAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / moveAnimationDuration;
      // Quadratic ease-out for smoother feel
      t = t * (2 - t);
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;
  }

  public IEnumerator AnimateCrateFall(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    // 1. Slide to the hole position
    yield return AnimateTransform(obj, targetGridPos);

    // 2. Sink down
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = startPos + Vector3.down * 1.0f; // Sink depth
    float elapsed = 0f;

    while (elapsed < fallAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / fallAnimationDuration;
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;

    // Optional: Rename for debugging
    obj.name = $"FilledHole_{targetGridPos.x}_{targetGridPos.y}";
  }

  // ================= UTILS & INPUT =================
  public Vector3 GridToWorld(int x, int y) => new Vector3(x + 0.5f, 0.5f, y + 0.5f);

  public Cell GetCellAtWorldPos(Vector3 worldPos) {
    int x = Mathf.FloorToInt(worldPos.x);
    int y = Mathf.FloorToInt(worldPos.z);
    return GetCell(x, y);
  }

  public Cell GetCell(int x, int y) {
    return IsValidPos(new Vector2Int(x, y)) ? grid[x, y] : null;
  }

  public bool IsValidPos(Vector2Int pos) =>
    pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;

  private void HandleMouseInput() {
    if (Input.GetMouseButtonDown(0) && playerController != null && !playerController.IsBusy) {
      Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
      if (Physics.Raycast(ray, out RaycastHit hit)) {
        Cell clickedCell = GetCellAtWorldPos(hit.point);
        Cell startCell = GetCellAtWorldPos(playerController.transform.position);
        FindPathAndMove(startCell, clickedCell);
      }
    }
  }

  private void FindPathAndMove(Cell start, Cell target) {
    if (start == null || target == null || !target.IsPathableForPlayer) return;

    // BFS Pathfinding
    Queue<Cell> queue = new Queue<Cell>();
    Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();
    queue.Enqueue(start);
    cameFrom[start] = null;

    while (queue.Count > 0) {
      Cell current = queue.Dequeue();
      if (current == target) {
        ReconstructPath(cameFrom, start, target);
        return;
      }

      foreach (var neighbor in GetNeighbors(current)) {
        if (!cameFrom.ContainsKey(neighbor) && neighbor.IsPathableForPlayer) {
          cameFrom[neighbor] = current;
          queue.Enqueue(neighbor);
        }
      }
    }
  }

  private void ReconstructPath(Dictionary<Cell, Cell> cameFrom, Cell start, Cell current) {
    playerMovementPath = new List<Cell>();
    while (current != start) {
      playerMovementPath.Add(current);
      current = cameFrom[current];
    }

    playerMovementPath.Reverse();
    isMovingPlayer = true;
    playerController.IsAutoMoving = true; // Tell controller to ignore inputs
  }

  private void ProcessPlayerPath() {
    if (playerMovementPath == null || playerMovementPath.Count == 0) {
      isMovingPlayer = false;
      if (playerController) playerController.IsAutoMoving = false;
      return;
    }

    // Simple throttle for path movement
    currentDelay += Time.deltaTime;
    if (currentDelay >= moveDelay) {
      Cell nextCell = playerMovementPath[0];
      Cell currentCell = GetCellAtWorldPos(playerController.transform.position);

      // Calculate direction for the controller to execute
      Vector2Int dir = new Vector2Int(nextCell.x - currentCell.x, nextCell.y - currentCell.y);

      // Delegate actual logic/animation to the controller so it's consistent
      playerController.ExecuteMove(dir);

      playerMovementPath.RemoveAt(0);
      currentDelay = 0f;
    }
  }

  private List<Cell> GetNeighbors(Cell cell) {
    List<Cell> list = new List<Cell>();
    int[] dx = { 0, 0, 1, -1 };
    int[] dy = { 1, -1, 0, 0 };
    for (int i = 0; i < 4; i++) {
      Cell n = GetCell(cell.x + dx[i], cell.y + dy[i]);
      if (n != null) {
        list.Add(n);
      }
    }

    return list;
  }

  public void CheckWinCondition() {
    int cratesOnTargets = 0;
    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y].occupant == Occupant.Crate && grid[x, y].isTarget) {
          cratesOnTargets++;
        }
      }
    }

    if (cratesOnTargets >= crateCount) {
      Debug.Log("Level Complete!");
      isPaused = true;
      if (menuManager) {
        menuManager.winGame();
      }
    }
  }
}
