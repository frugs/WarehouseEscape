using System.Collections.Generic;
using UnityEngine;

public static class Pather {
  /// <summary>
  /// Calculates a path between two grid coordinates using BFS.
  /// Returns a list of coordinates to visit (excluding the start).
  /// </summary>
  public static List<Vector2Int> FindPath(SokobanState state, Vector2Int start, Vector2Int goal) {
    if (!state.IsValidPos(start) ||
        !state.IsValidPos(goal) ||
        !state.CanPlayerWalk(goal.x, goal.y)) {
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
      if (current == goal) {
        found = true;
        break;
      }

      foreach (var neighbor in GetNeighbors(current)) {
        if (!cameFrom.ContainsKey(neighbor) &&
            state.CanPlayerWalk(neighbor.x, neighbor.y)) {
          cameFrom[neighbor] = current;
          queue.Enqueue(neighbor);
        }
      }
    }

    if (!found) return null;

    // Reconstruct
    List<Vector2Int> path = new List<Vector2Int>();
    var curr = goal;
    while (curr != start) {
      path.Add(new Vector2Int(curr.x, curr.y));
      curr = cameFrom[curr];
    }

    path.Reverse();
    return path;
  }

  private static List<Vector2Int> GetNeighbors(Vector2Int cell) {
    List<Vector2Int> list = new List<Vector2Int>();
    int[] dx = { 0, 0, 1, -1 };
    int[] dy = { 1, -1, 0, 0 };
    for (int i = 0; i < 4; i++) {
      var n = new Vector2Int(cell.x + dx[i], cell.y + dy[i]);
      list.Add(n);
    }

    return list;
  }
}
