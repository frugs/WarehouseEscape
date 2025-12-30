using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class GridManager : MonoBehaviour {
  [Header("Level Settings")] [SerializeField]
  private int levelNumber = 1;

  [SerializeField] private readonly string LevelsDirectoryName = "Levels";

  [Header("References")] [SerializeField]
  private TerrainMeshBuilder terrainBuilder;

  [SerializeField] private MenuManager menuManager;
  [SerializeField] private Transform cameraTransform;

  [Header("Prefabs")] [SerializeField] private GameObject PlayerTile = null;
  [SerializeField] private GameObject CrateTile = null;
  [SerializeField] private GameObject TargetTile = null;

  [Header("Animation Timing")] [SerializeField]
  private readonly float MoveAnimationDuration = 0.2f; // Snappy movement

  [SerializeField] private readonly float FallAnimationDuration = 0.15f;

  // ================= STATE =================
  public SokobanState currentState;
  private GameObject[,] visualGrid; // Visual representation only
  private int gridWidth;
  private int gridHeight;

  private string LevelsDirectory => Path.Combine(Application.dataPath, LevelsDirectoryName);

  [UsedImplicitly]
  private void Awake() {
    if (terrainBuilder == null) terrainBuilder = GetComponent<TerrainMeshBuilder>();
    if (menuManager == null) menuManager = GetComponent<MenuManager>();
    if (cameraTransform == null && Camera.main != null) cameraTransform = Camera.main.transform;
  }

  [UsedImplicitly]
  private void Start() {
    LoadLevel(levelNumber);
  }

  // Update loop removed: GridManager no longer handles input or movement updates.

  // ================= LEVEL LOADING =================
  public void LoadLevel(int level) {
    levelNumber = level;
    string filePath = Path.Combine(LevelsDirectory, $"Level{levelNumber}.txt");
    if (File.Exists(filePath)) {GenerateGridFromFile(filePath);}
    else {Debug.LogError($"Level file not found at: {filePath}");}
  }

  public void GenerateGridFromFile(string filePath) {
    LevelData data = LevelParser.ParseLevelFile(filePath);
    if (data == null) return;

    CleanupLevel();
    this.currentState = new SokobanState(data.grid, data.playerPos);
    this.gridWidth = data.width;
    this.gridHeight = data.height;
    this.visualGrid = new GameObject[gridWidth, gridHeight];

    if (terrainBuilder != null) {terrainBuilder.BuildTerrain(data.grid, gridWidth, gridHeight);}
    SpawnDynamicObjects();
    SetupCamera();

    if (menuManager != null) {menuManager.ResumeGame();}
  }

  private void CleanupLevel() {
    if (visualGrid != null) {
      foreach (var obj in visualGrid)
        if (obj != null)
          Destroy(obj);
    }

    foreach (var t in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
      // Cleanup any stray objects including completed crates
      if (t.name.StartsWith("Target_") || t.name.StartsWith("Crate_") ||
          t.name.StartsWith("FilledHoleCrate_") || t.name == "Player")
        Destroy(t);
    }

    visualGrid = null;
  }

  private void SpawnDynamicObjects() {
    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        Cell cell = currentState.grid[x, y];
        Vector3 pos = GridToWorld(x, y);

        if (cell.isTarget) {
          GameObject t = Instantiate(TargetTile, pos, Quaternion.identity);
          t.name = $"Target_{x}_{y}";
        }

        if (cell.occupant == Occupant.Player) {
          GameObject p = Instantiate(PlayerTile, pos, Quaternion.identity);
          p.name = "Player";
          visualGrid[x, y] = p;
        } else if (cell.occupant == Occupant.Crate) {
          GameObject c = Instantiate(CrateTile, pos, Quaternion.identity);
          c.name = $"Crate_{x}_{y}";
          visualGrid[x, y] = c;
        }
      }
    }
  }

  private void SetupCamera() {
    const float cameraAngle = 70.0f;
    if (cameraTransform == null) return;

    // Position camera to look at centre of grid
    cameraTransform.position = new Vector3(
      gridWidth / 2.0f,
      Mathf.Tan(Mathf.Deg2Rad * cameraAngle) * gridHeight / 2.0f,
      0.0f);
    cameraTransform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);

    Camera cam = cameraTransform.GetComponent<Camera>();

    // Set Orthographic Size based on Grid Size & Aspect Ratio
    if (cam != null)
    {
      cam.orthographic = true;

      float padding = 2f;
      float sizeForHeight = (gridHeight + padding) * 0.5f;

      float currentAspect = cam.aspect;
      float sizeForWidth = ((gridWidth + padding) * 0.5f) / currentAspect;

      cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }
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
    currentState = MoveManager.ApplyMove(currentState, move);

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

    while (elapsed < MoveAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / MoveAnimationDuration;
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

    while (elapsed < FallAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / FallAnimationDuration;
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;

    // Optional: Rename for debugging
    obj.name = $"FilledHole_{targetGridPos.x}_{targetGridPos.y}";
  }

  // ================= UTILS & PATHFINDING SERVICE =================

  /// <summary>
  /// Calculates a path between two grid coordinates using BFS.
  /// Returns a list of Cell coordinates to visit (excluding the start).
  /// </summary>
  public List<Vector2Int> GetPath(Vector2Int startPos, Vector2Int targetPos) {
    Cell start = GetCell(startPos.x, startPos.y);
    Cell target = GetCell(targetPos.x, targetPos.y);

    if (start == null || target == null || !target.IsPathableForPlayer) return null;

    // BFS Pathfinding
    Queue<Cell> queue = new Queue<Cell>();
    Dictionary<Cell, Cell> cameFrom = new Dictionary<Cell, Cell>();

    queue.Enqueue(start);
    cameFrom[start] = null;

    bool found = false;

    while (queue.Count > 0) {
      Cell current = queue.Dequeue();
      if (current == target) {
        found = true;
        break;
      }

      foreach (var neighbor in GetNeighbors(current)) {
        if (!cameFrom.ContainsKey(neighbor) && neighbor.IsPathableForPlayer) {
          cameFrom[neighbor] = current;
          queue.Enqueue(neighbor);
        }
      }
    }

    if (!found) return null;

    // Reconstruct
    List<Vector2Int> path = new List<Vector2Int>();
    Cell curr = target;
    while (curr != start) {
      path.Add(new Vector2Int(curr.x, curr.y));
      curr = cameFrom[curr];
    }

    path.Reverse();
    return path;
  }

  public Vector3 GridToWorld(int x, int y) => new Vector3(x + 0.5f, 0.5f, y + 0.5f);

  public Cell GetCellAtWorldPos(Vector3 worldPos) {
    int x = Mathf.FloorToInt(worldPos.x);
    int y = Mathf.FloorToInt(worldPos.z);
    return GetCell(x, y);
  }

  public Cell GetCell(int x, int y) {
    return IsValidPos(new Vector2Int(x, y)) ? currentState.grid[x, y] : null;
  }

  public bool IsValidPos(Vector2Int pos) =>
    pos.x >= 0 && pos.x < gridWidth && pos.y >= 0 && pos.y < gridHeight;

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
    if (currentState.IsWin) {
        Debug.Log("Level Complete!");
        if (menuManager) {
            menuManager.WinGame();
        }
    }
  }

  [UsedImplicitly]
  public void ResetLevel() => LoadLevel(levelNumber);

  [UsedImplicitly]
  public void NextLevel() {
    levelNumber++;
    LoadLevel(levelNumber);
  }
}
