using System.Collections.Generic;
using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour {
  [Header("Materials")] [SerializeField] private Material FloorMaterial = null;
  [SerializeField] private Material WallMaterial = null;
  [SerializeField] private Material HoleMaterial = null;

  [Header("Settings")] [SerializeField] private readonly float ShortWallHeight = 0.3f;
  [SerializeField] private readonly float TallWallHeight = 1.5f;
  [SerializeField] private readonly float HoleDepth = 1.0f;
  [SerializeField] private readonly float BevelSize = 0.05f;

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

  // Centralized wall height logic
  private float GetWallHeightAt(int x, int y, int gridWidth, int gridHeight) {
    // North (y == max) or East (x == max) walls are tall
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

        float currentHeight = GetWallHeightAt(x, y, gridWidth, gridHeight);
        float baseWallHeight = currentHeight - BevelSize;

        Vector3 basePos = GridUtils.GridToWorld(x, y);

        // Top cap
        AddHorizontalQuad(
            basePos + Vector3.up * currentHeight,
            vertices,
            triangles,
            uvs,
            colors,
            new Color(0.9f, 0.9f, 0.9f),
            0.5f - BevelSize);

        // Bevel ring between body and cap
        AddBevelFaces(
            basePos,
            baseWallHeight,
            currentHeight,
            BevelSize,
            vertices,
            triangles,
            uvs,
            colors);

        // Vertical sides up to baseWallHeight
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
        WallsParent);
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
      float myBaseHeight,
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
        // Neighbor is non-wall (floor/hole)
        shouldDrawFace = true;
      } else {
        // Neighbor is a wall; only draw if neighbor is shorter
        float neighborTotalHeight = GetWallHeightAt(nx, ny, width, height);
        float neighborBaseHeight = neighborTotalHeight - BevelSize;

        if (neighborBaseHeight < myBaseHeight - 0.001f) {
          shouldDrawFace = true;
        }
      }
    }

    if (shouldDrawFace) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.up * (myBaseHeight / 2));
      var wallBodyColor = new Color(0.6f, 0.6f, 0.6f);

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
        HolesParent);
  }


  private void AddBevelFaces(
      Vector3 center,
      float bottomY,
      float topY,
      float inset,
      List<Vector3> verts,
      List<int> tris,
      List<Vector2> uvs,
      List<Color> colors) {
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

    Color bevelColor = new Color(0.8f, 0.8f, 0.8f);

    // Approximate bevel slant for vertical UV scale
    float slantHeight = Mathf.Sqrt(inset * inset + inset * inset);

    AddQuadFromPoints(outSW, outSE, inSE, inSW, verts, tris, uvs, colors, bevelColor, slantHeight);
    AddQuadFromPoints(outNE, outNW, inNW, inNE, verts, tris, uvs, colors, bevelColor, slantHeight);
    AddQuadFromPoints(outSE, outNE, inNE, inSE, verts, tris, uvs, colors, bevelColor, slantHeight);
    AddQuadFromPoints(outNW, outSW, inSW, inNW, verts, tris, uvs, colors, bevelColor, slantHeight);
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
    if (isEdge || !grid[nx, ny].IsHole()) {
      Vector3 center = GridUtils.GridToWorld(x, y);
      Vector3 faceCenter = center + (dirNormal * 0.5f) + (Vector3.down * (HoleDepth / 2));
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

    // Scale UVs by quad width (size * 2)
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

    verts.Add(center - halfRight - halfUp);
    verts.Add(center + halfRight - halfUp);
    verts.Add(center + halfRight + halfUp);
    verts.Add(center - halfRight + halfUp);

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
    LevelParent = new GameObject("LevelTerrain").transform;
    WallsParent = new GameObject("Walls").transform;
    WallsParent.parent = LevelParent;
    HolesParent = new GameObject("Holes").transform;
    HolesParent.parent = LevelParent;
  }
}
