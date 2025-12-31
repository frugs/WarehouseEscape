using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using UnityEngine;

public class SokobanGenerator {
  private const int AttemptsPerLevel = 100;
  private const int GeneratorSolverLimit = 100_000;

  /// <summary>
  /// Generates a valid, solvable Sokoban level.
  /// </summary>
  /// <param name="boxCount">Number of crates/goals to place.</param>
  /// <returns>A fully constructed SokobanState, or null if generation failed.</returns>
  public SokobanState? Generate(int minSize = 6, int maxSize = 12, int boxCount = 5) {
    const int TimeoutMs = 60_000;
    Stopwatch timer = Stopwatch.StartNew();

    for (int i = 0; i < AttemptsPerLevel; i++) {
      if (timer.ElapsedMilliseconds > TimeoutMs) {
        UnityEngine.Debug.LogError(
          $"Timeout! {i} attempts in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      // 1. Create Room Structure
      int maxWidth = Random.Range(minSize, maxSize);
      int maxHeight = Random.Range(minSize, maxSize);
      var roomShape = GenerateRoomShape(maxWidth, maxHeight);

      // 2. Populate (Player, crates, Goals)
      var w = roomShape.GetLength(0);
      var h = roomShape.GetLength(1);
      var maybeState = PopulateLevel(w, h, boxCount, roomShape);

      if (maybeState == null) continue; // Population failed (no space)
      var state = (SokobanState)maybeState;

      // 3. Verify Solvability
      var solver = new SokobanSolver();
      if (solver.IsSolvable(state, GeneratorSolverLimit)) {
        UnityEngine.Debug.Log($"Generated solvable level in {i + 1} attempts.");
        return state;
      }
    }

    UnityEngine.Debug.LogError("Failed to generate a solvable level within attempt limit.");
    return null;
  }

  private int[,] GenerateRoomShape(int w, int h) {
    int[,] map = new int[w, h]; // 0 by default (Wall)

    // Try to place templates until full or timeout
    int failureCount = 0;
    int placedCount = 0;

    // Stop if density > 40% (example) or failures > 100
    while (failureCount < 100 && placedCount < (w * h) / 3) {
      var templates = GeneratorTemplates.Templates;
      var template = templates[Random.Range(0, templates.Count)];
      template = Rotate(template, Random.Range(0, 4));

      int tW = template.GetLength(0);
      int tH = template.GetLength(1);
      int x = Random.Range(0, w - tW);
      int y = Random.Range(0, h - tH);

      if (CanPlace(map, template, x, y)) {
        Place(map, template, x, y);
        placedCount++;
        failureCount = 0; // Reset failure count on success to encourage clustering
      } else {
        failureCount++;
      }
    }

    // Ensure center is valid if map is somehow empty
    if (map[w / 2, h / 2] == 0 && placedCount == 0) {
      map[w / 2, h / 2] = 1;
    }

    // Remove disconnected islands
    int[,] connectedMap = PruneDisconnectedParts(map);

    // Trim excess walls (shrink to fit)
    return TrimMap(connectedMap);
  }

  private bool CanPlace(int[,] map, int[,] temp, int startX, int startY) {
    int tW = temp.GetLength(0);
    int tH = temp.GetLength(1);

    for (int i = 0; i < tW; i++) {
      for (int j = 0; j < tH; j++) {
        int mapVal = map[startX + i, startY + j];
        int tempVal = temp[i, j];

        // Rule: If Map has Floor (1), Template CANNOT be Wall (0).
        // Existing floors must be preserved or matched.
        if (mapVal == 1 && tempVal == 0) return false;
      }
    }
    return true;
  }

  private void Place(int[,] map, int[,] template, int startX, int startY) {
    int tW = template.GetLength(0);
    int tH = template.GetLength(1);
    for (int i = 0; i < tW; i++) {
      for (int j = 0; j < tH; j++) {
        if (template[i, j] == 1) {
          map[startX + i, startY + j] = 1;
        }
      }
    }
  }

  // BFS to find largest connected component and remove the rest
  private int[,] PruneDisconnectedParts(int[,] map) {
    int w = map.GetLength(0);
    int h = map.GetLength(1);
    int[,] visited = new int[w, h]; // 0=Unvisited, 1=Visited

    List<Vector2Int> largestRegion = new List<Vector2Int>();

    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (map[x, y] == 1 && visited[x, y] == 0) {
          var region = GetRegion(map, visited, x, y);
          if (region.Count > largestRegion.Count) {
            largestRegion = region;
          }
        }
      }
    }

    // Rebuild map with ONLY the largest region
    int[,] newMap = new int[w, h];
    foreach (var pos in largestRegion) {
      newMap[pos.x, pos.y] = 1;
    }
    return newMap;
  }

  private List<Vector2Int> GetRegion(int[,] map, int[,] visited, int startX, int startY) {
    var region = new List<Vector2Int>();
    var queue = new Queue<Vector2Int>();
    queue.Enqueue(new Vector2Int(startX, startY));
    visited[startX, startY] = 1;

    int w = map.GetLength(0);
    int h = map.GetLength(1);

    while (queue.Count > 0) {
      var p = queue.Dequeue();
      region.Add(p);

      foreach (
        var dir in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right }) {
        Vector2Int n = p + dir;
        if (n.x >= 0 && n.y >= 0 && n.x < w && n.y < h) {
          if (map[n.x, n.y] == 1 && visited[n.x, n.y] == 0) {
            visited[n.x, n.y] = 1;
            queue.Enqueue(n);
          }
        }
      }
    }
    return region;
  }

  private int[,] TrimMap(int[,] map) {
    int w = map.GetLength(0);
    int h = map.GetLength(1);
    int minX = w, maxX = 0, minY = h, maxY = 0;
    bool foundFloor = false;

    // 1. Calculate Bounds
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (map[x, y] == 1) // If Floor
        {
          if (x < minX) minX = x;
          if (x > maxX) maxX = x;
          if (y < minY) minY = y;
          if (y > maxY) maxY = y;
          foundFloor = true;
        }
      }
    }

    if (!foundFloor) return map; // Empty map, return as is

    // 2. Create new grid with 1-tile padding
    int padding = 1;
    int newWidth = (maxX - minX + 1) + (padding * 2);
    int newHeight = (maxY - minY + 1) + (padding * 2);

    int[,] newMap = new int[newWidth, newHeight];

    // 3. Copy content
    for (int x = minX; x <= maxX; x++) {
      for (int y = minY; y <= maxY; y++) {
        if (map[x, y] == 1) {
          // Shift to new coordinates (0,0 becomes padding,padding)
          newMap[x - minX + padding, y - minY + padding] = 1;
        }
      }
    }

    return newMap;
  }

  private int[,] Rotate(int[,] matrix, int n) {
    int[,] ret = (int[,])matrix.Clone();
    for (int i = 0; i < n; i++) {
      ret = Rotate90(ret);
    }
    return ret;
  }

  private int[,] Rotate90(int[,] matrix) {
    int w = matrix.GetLength(0);
    int h = matrix.GetLength(1);
    int[,] ret = new int[h, w];
    for (int i = 0; i < w; i++) {
      for (int j = 0; j < h; j++) {
        ret[j, w - 1 - i] = matrix[i, j];
      }
    }
    return ret;
  }

  private SokobanState? PopulateLevel(
    int w, int h, int count, int[,] roomShape) {
    TerrainType[,] grid = new TerrainType[w, h];
    List<Vector2Int> floors = new List<Vector2Int>();

    // Convert int map to TerrainType and collect floor positions
    for (int x = 0; x < w; x++) {
      for (int y = 0; y < h; y++) {
        if (roomShape[x, y] == 1) {
          grid[x, y] = TerrainType.Floor;
          floors.Add(new Vector2Int(x, y));
        } else {
          grid[x, y] = TerrainType.Wall;
        }
      }
    }

    if (floors.Count < count * 2 + 1) return null;

    // Shuffle floors for random placement
    floors = floors.OrderBy(a => Random.value).ToList();
    int idx = 0;

    // 1. Player
    Vector2Int playerPos = floors[idx++];

    // 2. Goals (Targets)
    for (int i = 0; i < count; i++) {
      Vector2Int pos = floors[idx++];
      grid[pos.x, pos.y] = TerrainType.Target;
    }

    // 3. crates
    List<Vector2Int> crates = new List<Vector2Int>();
    for (int i = 0; i < count; i++) {
      crates.Add(floors[idx++]);
    }

    return new SokobanState(grid, playerPos, crates);
  }

  private int[,] RotateTemplate(int[,] original, int times) {
    if (times == 0) return original;
    int w = original.GetLength(0);
    int h = original.GetLength(1);
    int[,] newArr = new int[h, w];

    for (int i = 0; i < w; i++) {
      for (int j = 0; j < h; j++) {
        newArr[j, w - 1 - i] = original[i, j];
      }
    }
    return RotateTemplate(newArr, times - 1);
  }
}
