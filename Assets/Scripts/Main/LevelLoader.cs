using System.IO;
using JetBrains.Annotations;
using UnityEngine;

public class LevelLoader : MonoBehaviour {
  [Header("References")]
  [SerializeField] private TerrainMeshBuilder TerrainBuilder;
  [SerializeField] private Transform CameraTransform;

  [Header("Prefabs")]
  [SerializeField] private GameObject PlayerPrefab = null;
  [SerializeField] private GameObject CratePrefab = null;
  [SerializeField] private GameObject TargetPrefab = null;

  [Header("Settings")]
  [SerializeField] private readonly string LevelsDirectoryName = "Levels";

  private string LevelsDirectory => Path.Combine(Application.dataPath, LevelsDirectoryName);

  [UsedImplicitly]
  private void Awake() {
    if (TerrainBuilder == null) TerrainBuilder = GetComponent<TerrainMeshBuilder>();
    if (CameraTransform == null && Camera.main != null) CameraTransform = Camera.main.transform;
  }

  // ================= LEVEL LOADING =================
  public bool LoadLevel(
    int level,
    out SokobanState state,
    out GameObject[,] visualGrid) {
    string filePath = Path.Combine(LevelsDirectory, $"Level{level}.txt");
    if (File.Exists(filePath)) {
      return LoadLevelFromFile(filePath, out state, out visualGrid);
    } else {
      Debug.LogError($"Level file not found at: {filePath}");
      state = new SokobanState();
      visualGrid = null;
      return false;
    }
  }

  public bool LoadLevelFromFile(
      string filePath,
      out SokobanState state,
      out GameObject[,] visualGrid) {
    LevelData data = LevelParser.ParseLevelFile(filePath);
    if (data == null) {
      state = new SokobanState();
      visualGrid = null;
      return false;
    }

    state = new SokobanState(data.grid, data.playerPos, data.crates.ToArray());
    visualGrid = new GameObject[data.width, data.height];

    if (TerrainBuilder != null) {
      TerrainBuilder.BuildTerrain(data.grid);
    }
    SpawnDynamicObjects(state, visualGrid);
    SetupCamera(data.width, data.height);

    return true;
  }

  public void CleanupLevel(GameObject[,] visualGrid) {
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

  private void SpawnDynamicObjects(SokobanState initialState, GameObject[,] visualGrid) {
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
  }

  private void SetupCamera(int gridWidth, int gridHeight) {
    const float cameraAngle = 70.0f;
    if (CameraTransform == null) return;

    // Position camera to look at centre of grid
    CameraTransform.position = new Vector3(
      gridWidth / 2.0f,
      Mathf.Tan(Mathf.Deg2Rad * cameraAngle) * gridHeight / 2.0f,
      0.0f);
    CameraTransform.rotation = Quaternion.Euler(cameraAngle, 0f, 0f);

    Camera cam = CameraTransform.GetComponent<Camera>();

    // Set Orthographic Size based on Grid Size & Aspect Ratio
    if (cam != null) {
      cam.orthographic = true;

      float padding = 2f;
      float sizeForHeight = (gridHeight + padding) * 0.5f;

      float currentAspect = cam.aspect;
      float sizeForWidth = ((gridWidth + padding) * 0.5f) / currentAspect;

      cam.orthographicSize = Mathf.Max(sizeForHeight, sizeForWidth);
    }
  }
}
