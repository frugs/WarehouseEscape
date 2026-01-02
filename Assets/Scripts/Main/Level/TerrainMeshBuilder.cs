using System.Collections.Generic;
using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour {
  [Header("Materials")] [SerializeField] private Material FloorMaterial = null;
  [SerializeField] private Material WallMaterial = null;
  [SerializeField] private Material HoleMaterial = null;

  [Header("Settings")] [SerializeField] private readonly float WallHeight = 1.2f;
  [SerializeField] private readonly float HoleDepth = 1.0f;

  private Transform LevelParent, WallsParent, HolesParent;

  public void BuildTerrain(TerrainType[,] grid) {
    var w = grid.GetLength(0);
    var h = grid.GetLength(1);

    SetupHierarchy();

    CreateFloor(grid, w, h);
    CreateWalls(grid, w, h);
    CreateHoles(grid, w, h);
  }

  public void ClearPreviousLevel() {
    if (LevelParent) DestroyImmediate(LevelParent.gameObject);
  }

  // ========== CORE MESH GENERATION ==========

  private void CreateFloor(TerrainType[,] grid, int gridWidth, int gridHeight) {
    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();
    List<Vector2> uvs = new List<Vector2>();
    List<Color> colors = new List<Color>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y] == TerrainType.Floor) {
          AddHorizontalQuad(
              GridToWorld(x, y, 0f),
              verts,
              tris,
              uvs,
              colors,
              Color.white);
        }
      }
    }

    CreateGameObjectFromMeshData("Floor", verts, tris, uvs, colors, FloorMaterial, LevelParent);
  }

  private void CreateWalls(TerrainType[,] grid, int gridWidth, int gridHeight) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var uvs = new List<Vector2>();
    var colors = new List<Color>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y] != TerrainType.Wall) continue;

        Vector3 basePos = GridToWorld(x, y, 0);

        // 1. TOP FACE (Y = wallHeight)
        AddHorizontalQuad(
            basePos + Vector3.up * WallHeight,
            vertices,
            triangles,
            uvs,
            colors,
            Color.white);

        // 2. SIDE FACES (Check 4 neighbors)
        // North (Z+1)
        CheckAndAddWallSide(
            x,
            y,
            0,
            1,
            Vector3.forward,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        // South (Z-1)
        CheckAndAddWallSide(
            x,
            y,
            0,
            -1,
            Vector3.back,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        // East (X+1)
        CheckAndAddWallSide(
            x,
            y,
            1,
            0,
            Vector3.right,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        // West (X-1)
        CheckAndAddWallSide(
            x,
            y,
            -1,
            0,
            Vector3.left,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
      }
    }

    CreateGameObjectFromMeshData(
        "Walls",
        vertices,
        triangles,
        uvs,
        colors,
        WallMaterial,
        WallsParent);
  }

  private void CreateHoles(TerrainType[,] grid, int gridWidth, int gridHeight) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var uvs = new List<Vector2>();
    var colors = new List<Color>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (!grid[x, y].IsHole()) continue;

        Vector3 center = GridToWorld(x, y, 0);

        // 1. BOTTOM FACE (Y = -holeDepth)
        AddHorizontalQuad(
            center + Vector3.down * HoleDepth,
            vertices,
            triangles,
            uvs,
            colors,
            Color.black);

        // 2. INNER WALLS (Check 4 neighbors)
        CheckAndAddHoleSide(
            x,
            y,
            0,
            1,
            Vector3.forward,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        CheckAndAddHoleSide(
            x,
            y,
            0,
            -1,
            Vector3.back,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        CheckAndAddHoleSide(
            x,
            y,
            1,
            0,
            Vector3.right,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
        CheckAndAddHoleSide(
            x,
            y,
            -1,
            0,
            Vector3.left,
            grid,
            gridWidth,
            gridHeight,
            vertices,
            triangles,
            uvs,
            colors);
      }
    }

    CreateGameObjectFromMeshData(
        "Holes",
        vertices,
        triangles,
        uvs,
        colors,
        HoleMaterial,
        HolesParent);
  }

  // ========== FACE GENERATION HELPERS ==========

  private void CheckAndAddWallSide(
      int x,
      int y,
      int dx,
      int dy,
      Vector3 dirNormal,
      TerrainType[,] grid,
      int width,
      int height,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    int nx = x + dx;
    int ny = y + dy;

    // If neighbor is out of bounds OR not a wall, we need a face here
    bool isEdge = (nx < 0 || nx >= width || ny < 0 || ny >= height);
    if (isEdge || grid[nx, ny] != TerrainType.Wall) {
      Vector3 center = GridToWorld(x, y, 0);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.up * (WallHeight / 2));
      AddVerticalQuad(
          faceCenter,
          dirNormal,
          WallHeight,
          verts,
          tris,
          uvs,
          colors,
          Color.white,
          Color.white);
    }
  }

  private void CheckAndAddHoleSide(
      int x,
      int y,
      int dx,
      int dy,
      Vector3 dirNormal,
      TerrainType[,] grid,
      int width,
      int height,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    int nx = x + dx;
    int ny = y + dy;

    // For holes, if neighbor is NOT a hole, we see the side
    bool isEdge = nx < 0 || nx >= width || ny < 0 || ny >= height;
    if (isEdge || grid[nx, ny] != TerrainType.Hole) {
      Vector3 center = GridToWorld(x, y, 0);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.down * (HoleDepth / 2));
      // Invert normal because we are looking INTO the hole
      AddVerticalQuad(
          faceCenter,
          -dirNormal,
          HoleDepth,
          verts,
          tris,
          uvs,
          colors,
          Color.black,
          Color.white);
    }
  }

  private void AddHorizontalQuad(
      Vector3 center,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors,
      Color c) {
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

    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
  }

  private void AddVerticalQuad(
      Vector3 center,
      Vector3 normal,
      float height,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors,
      Color bottomColor,
      Color topColor) {
    // Calculate tangent vectors
    Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
    Vector3 halfRight = right * 0.5f;
    Vector3 halfUp = Vector3.up * (height * 0.5f);

    int i = verts.Count;

    verts.Add(center - halfRight - halfUp); // 0: Bottom-Left
    verts.Add(center + halfRight - halfUp); // 1: Bottom-Right
    verts.Add(center + halfRight + halfUp); // 2: Top-Right
    verts.Add(center - halfRight + halfUp); // 3: Top-Left

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

    colors.Add(bottomColor); // 0
    colors.Add(bottomColor); // 1
    colors.Add(topColor); // 2
    colors.Add(topColor); // 3
  }

  // ========== UTILS ==========

  private Vector3 GridToWorld(int x, int y, float yPos) {
    return new Vector3(x + 0.5f, yPos, y + 0.5f);
  }

  private void CreateGameObjectFromMeshData(
      string meshName,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors,
      Material mat,
      Transform parent) {
    if (verts.Count == 0) return;

    GameObject go = new GameObject(meshName);
    if (parent) {
      go.transform.parent = parent;
    }

    Mesh mesh = new Mesh();
    mesh.vertices = verts.ToArray();
    mesh.triangles = tris.ToArray();
    mesh.uv = uvs.ToArray();

    if (colors != null && colors.Count == verts.Count) {
      mesh.colors = colors.ToArray();
    }

    mesh.RecalculateNormals();

    go.AddComponent<MeshFilter>().mesh = mesh;
    go.AddComponent<MeshRenderer>().material = mat ?? FloorMaterial;
    go.AddComponent<MeshCollider>();
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
