using System.Collections.Generic;
using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour {
  [Header("Materials")] [SerializeField] private Material FloorMaterial = null;
  [SerializeField] private Material WallMaterial = null;
  [SerializeField] private Material HoleMaterial = null;

  [Header("Settings")] [SerializeField] private readonly float WallHeight = 0.5f;

  [SerializeField] private readonly float HoleDepth = 1.0f;

  [SerializeField] private readonly float BevelSize = 0.05f; // Width/Height of the chamfer

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
        if (grid[x, y].PlayerCanWalk()) {
          AddHorizontalQuad(
              GridUtils.GridToWorld(x, y),
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

    // Height where the vertical wall stops and the bevel begins
    float baseWallHeight = WallHeight - BevelSize;

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y] != TerrainType.Wall) continue;

        Vector3 basePos = GridUtils.GridToWorld(x, y);

        // 1. TOP CAP FACE (Inset by BevelSize)
        // We create a smaller quad on top
        AddHorizontalQuad(
            basePos + Vector3.up * WallHeight,
            vertices,
            triangles,
            uvs,
            colors,
            new Color(0.9f, 0.9f, 0.9f),
            0.5f - BevelSize); // Shrink the quad by the bevel amount);

        // 2. BEVEL FACES (4 angled sides connecting top to sides)
        AddBevelFaces(
            basePos,
            baseWallHeight,
            WallHeight,
            BevelSize,
            vertices,
            triangles,
            uvs,
            colors);

        // 3. VERTICAL SIDE FACES
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
            baseWallHeight,
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
            baseWallHeight,
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
            baseWallHeight,
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
            baseWallHeight,
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

        Vector3 center = GridUtils.GridToWorld(x, y);

        // 1. BOTTOM FACE (Y = -holeDepth)
        AddHorizontalQuad(
            center + Vector3.down * HoleDepth,
            vertices,
            triangles,
            uvs,
            colors,
            Color.black);

        // 2. INNER WALLS
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

  private void AddBevelFaces(
      Vector3 center,
      float bottomY,
      float topY,
      float inset,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    float outerS = 0.5f; // Outer edge (connects to vertical wall)
    float innerS = 0.5f - inset; // Inner edge (connects to top cap)

    // Corners
    // SW ( -1, -1 )
    Vector3 outSW = center + new Vector3(-outerS, bottomY, -outerS);
    Vector3 inSW = center + new Vector3(-innerS, topY, -innerS);

    // SE ( +1, -1 )
    Vector3 outSE = center + new Vector3(outerS, bottomY, -outerS);
    Vector3 inSE = center + new Vector3(innerS, topY, -innerS);

    // NE ( +1, +1 )
    Vector3 outNE = center + new Vector3(outerS, bottomY, outerS);
    Vector3 inNE = center + new Vector3(innerS, topY, innerS);

    // NW ( -1, +1 )
    Vector3 outNW = center + new Vector3(-outerS, bottomY, outerS);
    Vector3 inNW = center + new Vector3(-innerS, topY, innerS);

    Color bevelColor = new Color(0.8f, 0.8f, 0.8f);

    // South Bevel
    AddQuadFromPoints(outSW, outSE, inSE, inSW, verts, tris, uvs, colors, bevelColor);
    // North Bevel
    AddQuadFromPoints(outNE, outNW, inNW, inNE, verts, tris, uvs, colors, bevelColor);
    // East Bevel
    AddQuadFromPoints(outSE, outNE, inNE, inSE, verts, tris, uvs, colors, bevelColor);
    // West Bevel
    AddQuadFromPoints(outNW, outSW, inSW, inNW, verts, tris, uvs, colors, bevelColor);
  }

  private void AddQuadFromPoints(
      Vector3 p0,
      Vector3 p1,
      Vector3 p2,
      Vector3 p3,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors,
      Color c) {
    int i = verts.Count;
    verts.Add(p0);
    verts.Add(p1);
    verts.Add(p2);
    verts.Add(p3);

    // Tris 0-2-1, 0-3-2 (Standard Quad winding)
    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 1);
    tris.Add(i);
    tris.Add(i + 3);
    tris.Add(i + 2);

    // Simple UVs (stretched)
    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, 1));
    uvs.Add(new Vector2(0, 1));

    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
  }

  private void CheckAndAddWallSide(
      int x,
      int y,
      int dx,
      int dy,
      Vector3 dirNormal,
      TerrainType[,] grid,
      int width,
      int height,
      float sideHeight,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    var wallBodyColor = new Color(0.6f, 0.6f, 0.6f);

    int nx = x + dx;
    int ny = y + dy;

    // If neighbor is out of bounds OR not a wall, we need a face here
    bool isEdge = nx < 0 || nx >= width || ny < 0 || ny >= height;
    if (isEdge || grid[nx, ny] != TerrainType.Wall) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      // Move center up to half the height of this specific side segment
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.up * (sideHeight / 2));

      AddVerticalQuad(
          faceCenter,
          dirNormal,
          sideHeight,
          verts,
          tris,
          uvs,
          colors,
          wallBodyColor,
          wallBodyColor);
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
    if (isEdge || !grid[nx, ny].IsHole()) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.down * (HoleDepth / 2));

      AddVerticalQuad(
          faceCenter,
          -dirNormal, // Invert normal because we are looking INTO the hole
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
      Color c,
      float size = 0.5f) {
    int i = verts.Count;
    float s = size;

    verts.Add(center + new Vector3(-s, 0, -s)); // SW
    verts.Add(center + new Vector3(s, 0, -s)); // SE
    verts.Add(center + new Vector3(s, 0, s)); // NE
    verts.Add(center + new Vector3(-s, 0, s)); // NW

    // Correct Winding for Top Face (Up Normal)
    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 1); // 0-2-1
    tris.Add(i);
    tris.Add(i + 3);
    tris.Add(i + 2); // 0-3-2

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
    Vector3 right = Vector3.Cross(Vector3.up, normal).normalized;
    Vector3 halfRight = right * 0.5f;
    Vector3 halfUp = Vector3.up * (height * 0.5f);

    int i = verts.Count;

    verts.Add(center - halfRight - halfUp); // 0: Bottom-Left
    verts.Add(center + halfRight - halfUp); // 1: Bottom-Right
    verts.Add(center + halfRight + halfUp); // 2: Top-Right
    verts.Add(center - halfRight + halfUp); // 3: Top-Left

    // Tris
    tris.Add(i);
    tris.Add(i + 1);
    tris.Add(i + 2);
    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 3);

    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, 1));
    uvs.Add(new Vector2(0, 1));

    colors.Add(bottomColor);
    colors.Add(bottomColor);
    colors.Add(topColor);
    colors.Add(topColor);
  }

  // ========== UTILS ==========

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
