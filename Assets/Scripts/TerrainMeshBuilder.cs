using System.Collections.Generic;

using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour {
  [Header("Materials")] [SerializeField] private readonly Material FloorMaterial = null;
  [SerializeField] private readonly Material WallMaterial = null;
  [SerializeField] private readonly Material HoleMaterial = null;

  [Header("Settings")] [SerializeField] private readonly float WallHeight = 1.0f;
  [SerializeField] private readonly float HoleDepth = 1.0f;

  private Transform LevelParent, WallsParent, HolesParent;

  public void BuildTerrain(Cell[,] grid, int gridWidth, int gridHeight) {
    SetupHierarchy();

    CreateFloor(grid, gridWidth, gridHeight);
    CreateWalls(grid, gridWidth, gridHeight);
    CreateHoles(grid, gridWidth, gridHeight);
  }

  public void ClearPreviousLevel() {
    if (LevelParent) DestroyImmediate(LevelParent.gameObject);
  }

  // ========== CORE MESH GENERATION ==========

  private void CreateFloor(Cell[,] grid, int gridWidth, int gridHeight) {
    var floorPositions = new List<Vector2Int>();
    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y].terrain == TerrainType.Floor ||
            grid[x, y].terrain == TerrainType.FilledHole) {
          floorPositions.Add(new Vector2Int(x, y));
        }
      }
    }

    if (floorPositions.Count > 0) {
      // Floor at Y=0
      GameObject go = GenerateMesh("Floor", floorPositions, 0f, FloorMaterial);
      go.transform.parent = LevelParent;
    }
  }

  private void CreateWalls(Cell[,] grid, int gridWidth, int gridHeight) {
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y].terrain != TerrainType.Wall) continue;

        Vector3 basePos = GridToWorld(x, y, 0);

        // 1. TOP FACE (Y = wallHeight)
        AddHorizontalQuad(basePos + Vector3.up * WallHeight, vertices, triangles, uvs);

        // 2. SIDE FACES (Check 4 neighbors)
        // North (Z+1)
        CheckAndAddWallSide(x, y, 0, 1, Vector3.forward, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        // South (Z-1)
        CheckAndAddWallSide(x, y, 0, -1, Vector3.back, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        // East (X+1)
        CheckAndAddWallSide(x, y, 1, 0, Vector3.right, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        // West (X-1)
        CheckAndAddWallSide(x, y, -1, 0, Vector3.left, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
      }
    }

    CreateGameObjectFromMeshData("Walls", vertices, triangles, uvs, WallMaterial, WallsParent);
  }

  private void CreateHoles(Cell[,] grid, int gridWidth, int gridHeight) {
    List<Vector3> vertices = new List<Vector3>();
    List<int> triangles = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y].terrain != TerrainType.Hole) continue;

        Vector3 center = GridToWorld(x, y, 0);

        // 1. BOTTOM FACE (Y = -holeDepth)
        AddHorizontalQuad(center + Vector3.down * HoleDepth, vertices, triangles, uvs);

        // 2. INNER WALLS (Check 4 neighbors)
        CheckAndAddHoleSide(x, y, 0, 1, Vector3.forward, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        CheckAndAddHoleSide(x, y, 0, -1, Vector3.back, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        CheckAndAddHoleSide(x, y, 1, 0, Vector3.right, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
        CheckAndAddHoleSide(x, y, -1, 0, Vector3.left, grid, gridWidth, gridHeight, vertices,
          triangles, uvs);
      }
    }

    CreateGameObjectFromMeshData("Holes", vertices, triangles, uvs, HoleMaterial, HolesParent);
  }

  // ========== FACE GENERATION HELPERS ==========

  private void CheckAndAddWallSide(int x, int y, int dx, int dy, Vector3 dirNormal, Cell[,] grid,
    int width, int height, List<Vector3> verts, List<int> tris, List<Vector2> uvs) {
    int nx = x + dx;
    int ny = y + dy;

    // If neighbor is out of bounds OR not a wall, we need a face here
    bool isEdge = (nx < 0 || nx >= width || ny < 0 || ny >= height);
    if (isEdge || grid[nx, ny].terrain != TerrainType.Wall) {
      Vector3 center = GridToWorld(x, y, 0);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.up * (WallHeight / 2));
      AddVerticalQuad(faceCenter, dirNormal, WallHeight, verts, tris, uvs);
    }
  }

  private void CheckAndAddHoleSide(int x, int y, int dx, int dy, Vector3 dirNormal, Cell[,] grid,
    int width, int height, List<Vector3> verts, List<int> tris, List<Vector2> uvs) {
    int nx = x + dx;
    int ny = y + dy;

    // For holes, if neighbor is NOT a hole, we see the side
    bool isEdge = (nx < 0 || nx >= width || ny < 0 || ny >= height);
    if (isEdge || grid[nx, ny].terrain != TerrainType.Hole) {
      Vector3 center = GridToWorld(x, y, 0);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.down * (HoleDepth / 2));
      // Invert normal because we are looking INTO the hole
      AddVerticalQuad(faceCenter, -dirNormal, HoleDepth, verts, tris, uvs);
    }
  }

  private void AddHorizontalQuad(Vector3 center, List<Vector3> verts, List<int> tris,
    List<Vector2> uvs) {
    int i = verts.Count;
    float s = 0.5f;

    // Vertices
    // 0: Bottom-Left (SW)
    // 1: Bottom-Right (SE)
    // 2: Top-Right (NE)
    // 3: Top-Left (NW)
    verts.Add(center + new Vector3(-s, 0, -s));
    verts.Add(center + new Vector3(s, 0, -s));
    verts.Add(center + new Vector3(s, 0, s));
    verts.Add(center + new Vector3(-s, 0, s));

    // Triangles (Clockwise Winding: 0->2->1, 0->3->2)
    // This ensures the face points UP

    // Triangle 1 (Bottom-Left -> Top-Right -> Bottom-Right)
    tris.Add(i);
    tris.Add(i + 3); // Changed from 1
    tris.Add(i + 2); // Changed from 2

    // Triangle 2 (Bottom-Left -> Top-Left -> Top-Right)
    tris.Add(i);
    tris.Add(i + 2); // Changed from 2
    tris.Add(i + 1); // Changed from 3

    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, 1));
    uvs.Add(new Vector2(0, 1));
  }

  private void AddVerticalQuad(Vector3 center, Vector3 normal, float height, List<Vector3> verts,
    List<int> tris, List<Vector2> uvs) {
    // Calculate tangent vectors
    Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;

    Vector3 halfRight = right * 0.5f;
    Vector3 halfUp = Vector3.up * (height * 0.5f);

    int i = verts.Count;

    // 0: Bottom-Left
    verts.Add(center - halfRight - halfUp);
    // 1: Bottom-Right
    verts.Add(center + halfRight - halfUp);
    // 2: Top-Right
    verts.Add(center + halfRight + halfUp);
    // 3: Top-Left
    verts.Add(center - halfRight + halfUp);

    // Triangles (Clockwise Winding)
    // 0 -> 1 -> 2
    tris.Add(i);
    tris.Add(i + 1);
    tris.Add(i + 2);

    // 0 -> 2 -> 3
    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 3);

    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, 1));
    uvs.Add(new Vector2(0, 1));
  }

  // ========== UTILS ==========

  private Vector3 GridToWorld(int x, int y, float yPos) {
    return new Vector3(x + 0.5f, yPos, y + 0.5f);
  }

  private GameObject GenerateMesh(string meshName, List<Vector2Int> positions, float yHeight,
    Material mat) {
    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    foreach (var pos in positions) {
      AddHorizontalQuad(GridToWorld(pos.x, pos.y, yHeight), verts, tris, uvs);
    }

    return CreateGameObjectFromMeshData(meshName, verts, tris, uvs, mat, null);
  }

  private GameObject CreateGameObjectFromMeshData(string meshName, List<Vector3> verts, List<int> tris,
    List<Vector2> uvs, Material mat, Transform parent) {
    if (verts.Count == 0) return null;

    GameObject go = new GameObject(meshName);
    if (parent) go.transform.parent = parent;

    Mesh mesh = new Mesh();
    mesh.vertices = verts.ToArray();
    mesh.triangles = tris.ToArray();
    mesh.uv = uvs.ToArray();
    mesh.RecalculateNormals();

    go.AddComponent<MeshFilter>().mesh = mesh;
    go.AddComponent<MeshRenderer>().material = mat ?? FloorMaterial;
    go.AddComponent<MeshCollider>();

    return go;
  }

  private void SetupHierarchy() {
    ClearPreviousLevel();
    LevelParent = new GameObject("LevelTerrain").transform;

    WallsParent = new GameObject("Walls").transform;
    WallsParent.parent = LevelParent;

    HolesParent = new GameObject("Holes").transform;
    HolesParent.parent = LevelParent;
  }
}
