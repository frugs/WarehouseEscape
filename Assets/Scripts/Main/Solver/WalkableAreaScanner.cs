using System.Collections.Generic;
using UnityEngine;

public class WalkableAreaScanner {
  private readonly HashSet<Vector2Int> _visited = new HashSet<Vector2Int>();
  private readonly Queue<Vector2Int> _queue = new Queue<Vector2Int>();
  private readonly List<Vector2Int> _reachable = new List<Vector2Int>(100);

  // ReSharper disable once UnusedMember.Global
  public List<Vector2Int> GetWalkableArea(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    return new List<Vector2Int>(GetWalkableAreaNoCopy(state, out canonicalPlayerPos));
  }


  /// <summary>
  /// Performs a flood fill to find all reachable squares from the player's current position.
  /// Returns the list of reachable squares AND the "Canonical" position (Min X, then Min Y).
  /// </summary>
  public List<Vector2Int> GetWalkableAreaNoCopy(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    canonicalPlayerPos = state.PlayerPos;

    _visited.Clear();
    _queue.Clear();
    _reachable.Clear();

    var start = state.PlayerPos;
    _queue.Enqueue(start);
    _visited.Add(start);

    Vector2Int minPos = start;

    while (_queue.Count > 0) {
      var current = _queue.Dequeue();
      _reachable.Add(current);

      // Canonical check: "min x > min y"
      // We prioritize Smallest X. If X is equal, prioritize Smallest Y.
      if (current.x < minPos.x || (current.x == minPos.x && current.y < minPos.y)) {
        minPos = current;
      }

      foreach (var dir in Vector2IntExtensions.Cardinals) {
        var neighbor = current + dir;

        // We can only walk to neighbors that are valid (Floor/Target/FilledHole) and NO CRATE.
        // CanPlayerWalk handles these checks.
        if (!_visited.Contains(neighbor) && state.CanPlayerWalk(neighbor.x, neighbor.y)) {
          _visited.Add(neighbor);
          _queue.Enqueue(neighbor);
        }
      }
    }

    canonicalPlayerPos = minPos;
    return _reachable;
  }
}
