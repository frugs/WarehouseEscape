using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class LevelLoader : MonoBehaviour {
  private Transform _cameraTransform;

  [Header("References")]
  [field: SerializeField]
  [UsedImplicitly]
  private TerrainMeshBuilder TerrainBuilder { get; set; }


  [Header("Prefabs")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameObject PlayerPrefab { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private GameObject CratePrefab { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private GameObject TargetPrefab { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private GameObject EntrancePrefab { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private GameObject ExitPrefab { get; set; }

  [Header("Settings")]
  [field: SerializeField]
  [UsedImplicitly]
  private string LevelsDirectoryName { get; set; } = "Levels";

  private string LevelsDirectory => Path.Combine(Application.dataPath, LevelsDirectoryName);

  [UsedImplicitly]
  private void Awake() {
    if (TerrainBuilder == null) TerrainBuilder = GetComponent<TerrainMeshBuilder>();
    if (_cameraTransform == null && Camera.main != null) _cameraTransform = Camera.main.transform;
  }

  // ================= LEVEL LOADING =================
  public bool LoadLevel(
      int level,
      out SokobanState state,
      out GameObject[,] visualGrid,
      out GameObject entrance,
      out GameObject exit,
      out string levelName) {
    state = new SokobanState();
    visualGrid = null;
    entrance = null;
    exit = null;
    levelName = $"Level{level}";

    string filePath = Path.Combine(LevelsDirectory, $"{levelName}.txt");
    if (File.Exists(filePath)) {
      return LoadLevelFromFile(filePath, out state, out visualGrid, out entrance, out exit);
    }

    Debug.LogError($"Level file not found at: {filePath}");

    return false;
  }

  public bool LoadLevelFromFile(
      string filePath,
      out SokobanState state,
      out GameObject[,] visualGrid,
      out GameObject entrance,
      out GameObject exit) {
    LevelData data = LevelParser.ParseLevelFile(filePath);
    if (data == null) {
      state = new SokobanState();
      visualGrid = null;
      entrance = null;
      exit = null;
      return false;
    }

    state = SokobanState.Create(data.grid, data.playerPos, data.crates);
    LoadLevelFromState(state, out visualGrid, out entrance, out exit);
    return true;
  }

  public void LoadLevelFromState(
      SokobanState state,
      out GameObject[,] visualGrid,
      out GameObject entrance,
      out GameObject exit) {
    var width = state.GridWidth;
    var height = state.GridHeight;
    visualGrid = new GameObject[width, height];

    if (TerrainBuilder != null) {
      TerrainBuilder.BuildTerrain(state.TerrainGrid);
    }

    SpawnDynamicObjects(state, visualGrid, out entrance, out exit);
    SetupCamera(width, height);
  }


  public void CleanupLevel(GameObject[,] visualGrid, GameObject entrance, GameObject exit) {
    if (entrance != null) {
      Destroy(entrance);
    }

    if (exit != null) {
      Destroy(exit);
    }

    if (visualGrid != null) {
      foreach (var obj in visualGrid) {
        if (obj != null) {
          Destroy(obj);
        }
      }
    }

    foreach (var t in GameObject.FindObjectsByType<GameObject>(FindObjectsSortMode.None)) {
      // Cleanup any stray objects including completed crates
      if (t.name.StartsWith("Target_") || t.name.StartsWith("Crate_") ||
          t.name.StartsWith("FilledHole_") || t.name == "Player")
        Destroy(t);
    }
  }

  private void SpawnDynamicObjects(
      SokobanState initialState,
      GameObject[,] visualGrid,
      out GameObject entrance,
      out GameObject exit) {
    entrance = null;
    exit = null;

    var terrainGrid = initialState.TerrainGrid;
    var width = terrainGrid.GetLength(0);
    var height = terrainGrid.GetLength(1);

    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        var terrain = terrainGrid[x, y];
        Vector3 pos = GridUtils.GridToWorld(x, y, 0.5f);

        if (terrain.IsTarget()) {
          GameObject t = Instantiate(TargetPrefab, pos, Quaternion.identity);
          t.name = $"Target_{x}_{y}";
        }

        if (terrain.IsEntrance() && EntrancePrefab != null) {
          GameObject go = Instantiate(EntrancePrefab, pos, Quaternion.identity);
          go.name = "Entrance";
          entrance = go;
        }

        if (terrain.IsExit() && ExitPrefab != null) {
          GameObject go = Instantiate(
              ExitPrefab,
              pos,
              x == 0
                  ? Quaternion.Euler(0f, 180, 0f)
                  : Quaternion.Euler(0f, -90f, 0f));
          go.name = "Exit";
          exit = go;
        }

        if (initialState.IsPlayerAt(x, y)) {
          GameObject p = Instantiate(PlayerPrefab, pos, Quaternion.identity);
          p.name = "Player";
          visualGrid[x, y] = p;
        } else if (initialState.IsCrateAt(x, y)) {
          GameObject c = Instantiate(CratePrefab, pos, Quaternion.identity);
          c.name = $"Crate_{x}_{y}";
          visualGrid[x, y] = c;
        }
      }
    }

    foreach (var pos in initialState.FilledHoles) {
      GameObject c = Instantiate(CratePrefab, pos.GridToWorld(-0.5f), Quaternion.identity);
      c.name = $"FilledHole_{pos.x}_{pos.y}";
    }
  }

  private void SetupCamera(int gridWidth, int gridHeight) {
    if (_cameraTransform == null) return;

    // Isometric camera angles
    // Yaw: 45° (looking diagonally across the grid)
    // Pitch: 50° (steeper than a classic isometric angle to prioritise grid visibility)
    const float yaw = 45.0f;
    const float pitch = 50f;

    // Calculate grid center in world space
    float centerX = gridWidth / 2.0f;
    float centerZ = gridHeight / 2.0f;

    // Calculate camera distance from center
    // This ensures the entire level is visible
    float diagonal = Mathf.Sqrt(gridWidth * gridWidth + gridHeight * gridHeight);
    float distance = diagonal * 1.2f; // 1.2 = padding factor

    // Position camera at isometric angle
    // Convert spherical coordinates to Cartesian
    float pitchRad = pitch * Mathf.Deg2Rad;
    float yawRad = yaw * Mathf.Deg2Rad;

    _cameraTransform.position = new Vector3(
        centerX - distance * Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
        distance * Mathf.Sin(pitchRad),
        centerZ - distance * Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
    );

    // Rotate camera to look at grid center
    _cameraTransform.rotation = Quaternion.Euler(pitch, yaw, 0f);

    Camera cam = _cameraTransform.GetComponent<Camera>();

    if (cam != null) {
      cam.orthographic = true;

      // Calculate orthographic size for isometric view
      // We need to fit the diagonal of the grid
      float padding = 2f;
      float isometricWidth = (gridWidth + gridHeight) / 2.0f;
      float sizeForLevel = (isometricWidth + padding) * 0.5f;

      // Account for aspect ratio
      float currentAspect = cam.aspect;
      float sizeForAspect = sizeForLevel / currentAspect;

      cam.orthographicSize = Mathf.Max(sizeForLevel, sizeForAspect);
    }
  }
}
