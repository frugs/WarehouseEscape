using System.Collections;
using System.Collections.Generic;
using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class GridManager : MonoBehaviour {
  [Header("Level Settings")]
  [SerializeField]
  private int levelNumber = 1;

  [SerializeField] private readonly string LevelsDirectoryName = "Levels";

  [Header("References")]
  [SerializeField]
  private TerrainMeshBuilder terrainBuilder;

  [SerializeField] private MenuManager menuManager;
  [SerializeField] private Transform cameraTransform;

  [Header("Prefabs")][SerializeField] private GameObject PlayerTile = null;
  [SerializeField] private GameObject CrateTile = null;
  [SerializeField] private GameObject TargetTile = null;

  [Header("Animation Timing")]
  [SerializeField]
  private readonly float MoveAnimationDuration = 0.2f; // Snappy movement

  [SerializeField] private readonly float FallAnimationDuration = 0.15f;

  // ================= STATE =================
  private SokobanState CurrentState;
  private GameObject[,] VisualGrid; // Visual representation only
  private int GridWidth;
  private int GridHeight;

  private string LevelsDirectory => Path.Combine(Application.dataPath, LevelsDirectoryName);

  public SokobanState GridState => CurrentState;

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
    if (File.Exists(filePath)) { GenerateGridFromFile(filePath); } else { Debug.LogError($"Level file not found at: {filePath}"); }
  }

  public void GenerateGridFromFile(string filePath) {
    LevelData data = LevelParser.ParseLevelFile(filePath);
    if (data == null) return;

    CleanupLevel();
    this.CurrentState = new SokobanState(data.grid, data.playerPos, data.crates.ToArray());
    this.GridWidth = data.width;
    this.GridHeight = data.height;
    this.VisualGrid = new GameObject[GridWidth, GridHeight];

    if (terrainBuilder != null) {
      terrainBuilder.BuildTerrain(data.grid, GridWidth, GridHeight);
    }
    SpawnDynamicObjects();
    SetupCamera();

    if (menuManager != null) { menuManager.ResumeGame(); }
  }

  private void CleanupLevel() {
    if (VisualGrid != null) {
      foreach (var obj in VisualGrid)
        if (obj != null)
          Destroy(obj);
    }

    foreach (var t in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
      // Cleanup any stray objects including completed crates
      if (t.name.StartsWith("Target_") || t.name.StartsWith("Crate_") ||
          t.name.StartsWith("FilledHoleCrate_") || t.name == "Player")
        Destroy(t);
    }

    VisualGrid = null;
  }

  private void SpawnDynamicObjects() {
    for (int x = 0; x < GridWidth; x++) {
      for (int y = 0; y < GridHeight; y++) {
        var terrain = CurrentState.TerrainGrid[x, y];
        Vector3 pos = GridToWorld(x, y);

        if (terrain.IsTarget()) {
          GameObject t = Instantiate(TargetTile, pos, Quaternion.identity);
          t.name = $"Target_{x}_{y}";
        }

        if (CurrentState.IsPlayerAt(x, y)) {
          GameObject p = Instantiate(PlayerTile, pos, Quaternion.identity);
          p.name = "Player";
          VisualGrid[x, y] = p;
        } else if (CurrentState.IsCrateAt(x, y)) {
          GameObject c = Instantiate(CrateTile, pos, Quaternion.identity);
          c.name = $"Crate_{x}_{y}";
          VisualGrid[x, y] = c;
        }
      }
    }
  }

  private void SetupCamera() {
    const float cameraAngle = 70.0f;
    if (cameraTransform == null) return;

    // Position camera to look at centre of grid
    cameraTransform.position = new Vector3(
      GridWidth / 2.0f,
      Mathf.Tan(Mathf.Deg2Rad * cameraAngle) * GridHeight / 2.0f,
      0.0f);
    cameraTransform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);

    Camera cam = cameraTransform.GetComponent<Camera>();

    // Set Orthographic Size based on Grid Size & Aspect Ratio
    if (cam != null) {
      cam.orthographic = true;

      float padding = 2f;
      float sizeForHeight = (GridHeight + padding) * 0.5f;

      float currentAspect = cam.aspect;
      float sizeForWidth = ((GridWidth + padding) * 0.5f) / currentAspect;

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
      playerObj = VisualGrid[move.playerFrom.x, move.playerFrom.y];

    if (move.type == MoveType.CratePush && IsValidPos(move.crateFrom))
      crateObj = VisualGrid[move.crateFrom.x, move.crateFrom.y];

    // 2. Update Data Model (The Truth)
    CurrentState = MoveManager.ApplyMove(CurrentState, move );

    // 3. Update Visual Grid Pointers (The References)
    VisualGrid[move.playerFrom.x, move.playerFrom.y] = null;
    if (move.type == MoveType.CratePush)
      VisualGrid[move.crateFrom.x, move.crateFrom.y] = null;

    if (playerObj != null)
      VisualGrid[move.playerTo.x, move.playerTo.y] = playerObj;

    if (crateObj != null) {
      // We keep tracking the visual object even if it "falls in a hole"
      // so we don't lose the reference until we destroy/change it.
      VisualGrid[move.crateTo.x, move.crateTo.y] = crateObj;
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
  public List<Vector2Int> GetPath(Vector2Int start, Vector2Int target) {
    if (!IsValidPos(start) ||
        !IsValidPos(target) ||
        !CurrentState.CanPlayerWalk(target.x, target.y)) {
      return null;
    }

    // BFS Pathfinding
    Queue<Vector2Int> queue = new Queue<Vector2Int>();
    Dictionary<Vector2Int, Vector2Int> cameFrom = new Dictionary<Vector2Int, Vector2Int>();

    queue.Enqueue(start);
    cameFrom[start] = start;

    bool found = false;

    while (queue.Count > 0) {
      var current = queue.Dequeue();
      if (current == target) {
        found = true;
        break;
      }

      foreach (var neighbor in GetNeighbors(current)) {
        if (!cameFrom.ContainsKey(neighbor) &&
            CurrentState.CanPlayerWalk(neighbor.x, neighbor.y)) {
          cameFrom[neighbor] = current;
          queue.Enqueue(neighbor);
        }
      }
    }

    if (!found) return null;

    // Reconstruct
    List<Vector2Int> path = new List<Vector2Int>();
    var curr = target;
    while (curr != start) {
      path.Add(new Vector2Int(curr.x, curr.y));
      curr = cameFrom[curr];
    }

    path.Reverse();
    return path;
  }

  public Vector3 GridToWorld(int x, int y) => new Vector3(x + 0.5f, 0.5f, y + 0.5f);

  public Vector2Int WorldToGrid(Vector3 worldPos) {
    int x = Mathf.FloorToInt(worldPos.x);
    int y = Mathf.FloorToInt(worldPos.z);
    return new Vector2Int(x, y);
  }

  public bool IsValidPos(Vector2Int pos) =>
    pos.x >= 0 && pos.x < GridWidth && pos.y >= 0 && pos.y < GridHeight;

  private List<Vector2Int> GetNeighbors(Vector2Int cell) {
    List<Vector2Int> list = new List<Vector2Int>();
    int[] dx = { 0, 0, 1, -1 };
    int[] dy = { 1, -1, 0, 0 };
    for (int i = 0; i < 4; i++) {
      var n = new Vector2Int(cell.x + dx[i], cell.y + dy[i]);
      if (n != null) {
        list.Add(n);
      }
    }

    return list;
  }

  public void CheckWinCondition() {
    if (CurrentState.IsWin()) {
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
