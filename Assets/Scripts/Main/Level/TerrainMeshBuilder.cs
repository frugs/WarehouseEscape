using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour {
  [Header("Materials")]
  [field: SerializeField]
  [UsedImplicitly]
  private Material FloorMaterial { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private Material WallMaterial { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private Material HoleMaterial { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private Material SkirtMaterial { get; set; }

  [Header("Settings")]
  [field: SerializeField]
  [UsedImplicitly]
  private float ShortWallHeight { get; set; } = 0.3f;

  [field: SerializeField]
  [UsedImplicitly]
  private float TallWallHeight { get; set; } = 2.0f;

  [field: SerializeField]
  [UsedImplicitly]
  private float BevelSize { get; set; } = 0.05f;

  [field: SerializeField]
  [UsedImplicitly]
  private float HoleDepth { get; set; } = 1.0f;

  [field: SerializeField]
  [UsedImplicitly]
  private float SkirtDepth { get; set; } = 6f;

  private Transform _levelParent, _wallsParent, _holesParent;

  public void BuildTerrain(TerrainType[,] grid) {
    var w = grid.GetLength(0);
    var h = grid.GetLength(1);

    SetupHierarchy();

    CreateFloor(grid, w, h);
    CreateWalls(grid, w, h);
    CreateHoles(grid, w, h);
    CreatePlatformSides(grid, w, h);
  }

  public void ClearPreviousLevel() {
    if (_levelParent) DestroyImmediate(_levelParent.gameObject);
  }

  // ========== HELPER LOGIC ==========

  // Determines if a wall should be short or tall based on position
  private float GetWallHeightAt(int x, int y, int gridWidth, int gridHeight) {
    // North (y == max) or East (x == max) walls are tall to create a background effect
    bool isBackWall = (x == gridWidth - 1) || (y == gridHeight - 1);
    return isBackWall ? TallWallHeight : ShortWallHeight;
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

    CreateGameObjectFromMeshData("Floor", verts, tris, uvs, colors, FloorMaterial, _levelParent);
  }

  private void CreateWalls(TerrainType[,] grid, int gridWidth, int gridHeight) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var uvs = new List<Vector2>();
    var colors = new List<Color>();

    for (int x = 0; x < gridWidth; x++) {
      for (int y = 0; y < gridHeight; y++) {
        if (grid[x, y] != TerrainType.Wall) continue;

        float currentHeight = GetWallHeightAt(x, y, gridWidth, gridHeight);
        float baseWallHeight = currentHeight - BevelSize;
        Vector3 basePos = GridUtils.GridToWorld(x, y);

        // 1. TOP CAP
        AddHorizontalQuad(
            basePos + Vector3.up * currentHeight,
            vertices,
            triangles,
            uvs,
            colors,
            new Color(0.9f, 0.9f, 0.9f),
            size: 0.5f - BevelSize);

        // 2. BEVEL FACES
        AddBevelFaces(
            basePos,
            baseWallHeight,
            currentHeight,
            BevelSize,
            vertices,
            triangles,
            uvs,
            colors,
            new Color(0.9f, 0.9f, 0.9f));

        // 3. VERTICAL SIDES
        // Check all 4 directions because even internal walls might need faces
        // if they are next to a SHORTER wall.
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
        _wallsParent);
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
      float myBaseHeight, // The vertical height of the CURRENT wall
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    int nx = x + dx;
    int ny = y + dy;
    bool isEdge = nx < 0 || nx >= width || ny < 0 || ny >= height;

    bool shouldDrawFace = false;

    if (isEdge) {
      // Faces on world edge are always visible
      shouldDrawFace = true;
    } else {
      var neighborType = grid[nx, ny];
      if (neighborType != TerrainType.Wall) {
        // Neighbor is non-wall (floor/hole) -> Draw face
        shouldDrawFace = true;
      } else {
        // Neighbor IS a wall. BUT is it shorter?
        float neighborTotalHeight = GetWallHeightAt(nx, ny, width, height);
        float neighborBaseHeight = neighborTotalHeight - BevelSize;

        // If neighbor's vertical body is shorter than mine, I have exposed skin.
        // E.g. A Tall wall next to a Short wall needs to draw the top part of its side.
        if (neighborBaseHeight < myBaseHeight - 0.001f) {
          shouldDrawFace = true;
        }
      }
    }

    if (shouldDrawFace) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      // Center is calculated so the quad covers from Y=0 to Y=myBaseHeight
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.up * (myBaseHeight / 2));
      var wallBodyColor = new Color(0.8f, 0.8f, 0.8f);

      AddVerticalQuad(
          faceCenter,
          dirNormal,
          myBaseHeight,
          verts,
          tris,
          uvs,
          colors,
          wallBodyColor,
          wallBodyColor);
    }
  }

  private void CreatePlatformSides(TerrainType[,] grid, int w, int h) {
    var vertices = new List<Vector3>();
    var triangles = new List<int>();
    var uvs = new List<Vector2>();
    var colors = new List<Color>();

    // 1. SOUTH EDGE (y=0)
    for (int x = 0; x < w; x++) {
      if (h > 0) {
        DrawSkirtFace(x, 0, Vector3.back, grid, vertices, triangles, uvs, colors);
      }
    }

    // 2. WEST EDGE (x=0)
    for (int y = 0; y < h; y++) {
      if (w > 0) {
        DrawSkirtFace(0, y, Vector3.left, grid, vertices, triangles, uvs, colors);
      }
    }

    CreateGameObjectFromMeshData(
        "PlatformSkirt",
        vertices,
        triangles,
        uvs,
        colors,
        SkirtMaterial,
        _levelParent);
  }

  private void DrawSkirtFace(
      int x,
      int y,
      Vector3 dir,
      TerrainType[,] grid,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
    var terrain = grid[x, y];
    // A hole at the edge starts drawing the skirt from the bottom of the hole
    var offset = terrain.IsHole() ? HoleDepth : 0f;
    var offsetSkirtHeight = SkirtDepth - offset;

    Vector3 center = GridUtils.GridToWorld(x, y);
    Vector3 faceCenter = center +
                         (dir * 0.5f) +
                         (Vector3.down * (offset + offsetSkirtHeight / 2));

    AddVerticalQuad(
        faceCenter,
        dir,
        offsetSkirtHeight,
        verts,
        tris,
        uvs,
        colors,
        Color.white,
        Color.white
    );
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
        AddHorizontalQuad(
            center + Vector3.down * HoleDepth,
            vertices,
            triangles,
            uvs,
            colors,
            Color.black);

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
        _holesParent);
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
    bool isEdge = nx < 0 || nx >= width || ny < 0 || ny >= height;

    // If it's an edge, OR the neighbor is NOT a hole (meaning it's a wall or floor), we see the side of the hole
    if (isEdge || !grid[nx, ny].IsHole()) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.down * (HoleDepth / 2));
      AddVerticalQuad(
          faceCenter,
          -dirNormal, // Note: Hole faces point INWARDS
          HoleDepth,
          verts,
          tris,
          uvs,
          colors,
          Color.black,
          Color.white);
    }
  }

  // ========== PRIMITIVE HELPERS ==========

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

    verts.Add(center + new Vector3(-s, 0, -s));
    verts.Add(center + new Vector3(s, 0, -s));
    verts.Add(center + new Vector3(s, 0, s));
    verts.Add(center + new Vector3(-s, 0, s));

    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 1);
    tris.Add(i);
    tris.Add(i + 3);
    tris.Add(i + 2);

    float uvScale = size * 2.0f;
    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(uvScale, 0));
    uvs.Add(new Vector2(uvScale, uvScale));
    uvs.Add(new Vector2(0, uvScale));

    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
    colors.Add(c);
  }

  private void AddBevelFaces(
      Vector3 center,
      float bottomY,
      float topY,
      float inset,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors,
      Color color) {
    float outerS = 0.5f;
    float innerS = 0.5f - inset;

    Vector3 outSW = center + new Vector3(-outerS, bottomY, -outerS);
    Vector3 inSW = center + new Vector3(-innerS, topY, -innerS);
    Vector3 outSE = center + new Vector3(outerS, bottomY, -outerS);
    Vector3 inSE = center + new Vector3(innerS, topY, -innerS);
    Vector3 outNE = center + new Vector3(outerS, bottomY, outerS);
    Vector3 inNE = center + new Vector3(innerS, topY, innerS);
    Vector3 outNW = center + new Vector3(-outerS, bottomY, outerS);
    Vector3 inNW = center + new Vector3(-innerS, topY, innerS);

    // Approximate bevel slant for vertical UV scale
    float slantHeight = Mathf.Sqrt(inset * inset + inset * inset);

    AddQuadFromPoints(outSW, outSE, inSE, inSW, verts, tris, uvs, colors, color, slantHeight);
    AddQuadFromPoints(outNE, outNW, inNW, inNE, verts, tris, uvs, colors, color, slantHeight);
    AddQuadFromPoints(outSE, outNE, inNE, inSE, verts, tris, uvs, colors, color, slantHeight);
    AddQuadFromPoints(outNW, outSW, inSW, inNW, verts, tris, uvs, colors, color, slantHeight);
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
      Color c,
      float vScale = 1.0f) {
    int i = verts.Count;
    verts.Add(p0);
    verts.Add(p1);
    verts.Add(p2);
    verts.Add(p3);

    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 1);
    tris.Add(i);
    tris.Add(i + 3);
    tris.Add(i + 2);

    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, vScale));
    uvs.Add(new Vector2(0, vScale));

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

    verts.Add(center - halfRight - halfUp); // Bottom-Left
    verts.Add(center + halfRight - halfUp); // Bottom-Right
    verts.Add(center + halfRight + halfUp); // Top-Right
    verts.Add(center - halfRight + halfUp); // Top-Left

    tris.Add(i);
    tris.Add(i + 1);
    tris.Add(i + 2);
    tris.Add(i);
    tris.Add(i + 2);
    tris.Add(i + 3);

    // V coordinate encodes world-space height
    uvs.Add(new Vector2(0, 0));
    uvs.Add(new Vector2(1, 0));
    uvs.Add(new Vector2(1, height));
    uvs.Add(new Vector2(0, height));

    colors.Add(bottomColor);
    colors.Add(bottomColor);
    colors.Add(topColor);
    colors.Add(topColor);
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
    if (parent) go.transform.parent = parent;

    Mesh mesh = new Mesh();
    mesh.vertices = verts.ToArray();
    mesh.triangles = tris.ToArray();
    mesh.uv = uvs.ToArray();
    if (colors != null && colors.Count == verts.Count) mesh.colors = colors.ToArray();

    mesh.RecalculateNormals();
    go.AddComponent<MeshFilter>().mesh = mesh;
    go.AddComponent<MeshRenderer>().material = mat ?? FloorMaterial;
    go.AddComponent<MeshCollider>();
  }

  private void SetupHierarchy() {
    ClearPreviousLevel();
    _levelParent = new GameObject("LevelTerrain").transform;

    _wallsParent = new GameObject("Walls").transform;
    _wallsParent.parent = _levelParent;

    _holesParent = new GameObject("Holes").transform;
    _holesParent.parent = _levelParent;
  }
}
