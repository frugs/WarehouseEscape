using UnityEngine;

public static class GridUtils {
  public static Vector2Int WorldToGrid(this Vector3 worldPos) {
    int x = Mathf.FloorToInt(worldPos.x);
    int y = Mathf.FloorToInt(worldPos.z);
    return new Vector2Int(x, y);
  }

  public static Vector3 GridToWorld(this Vector2Int gridPos, float worldY = 0.0f) {
    return GridToWorld(gridPos.x, gridPos.y, worldY);
  }

  public static Vector3 GridToWorld(int gridX, int gridY, float worldY = 0.0f) {
    return new Vector3(gridX + 0.5f, worldY, gridY + 0.5f);
  }

  public static bool IsInBounds(int x, int y, TerrainType[,] grid) {
    var w = grid.GetLength(0);
    var h = grid.GetLength(1);
    return x >= 0 && x < w && y >= 0 && y < h;
  }

  public static bool IsInBounds(Vector2Int pos, TerrainType[,] grid) {
    return IsInBounds(pos.x, pos.y, grid);
  }
}
