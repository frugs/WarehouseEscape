using System.Collections.Generic;
using UnityEngine;

public class WalkableAreaScanner {
  public readonly struct FloodFillScannerAdapter : IGridGraph {
    private readonly SokobanState _state;

    public int Width => _state.GridWidth;
    public int Height => _state.GridHeight;

    public FloodFillScannerAdapter(SokobanState state) {
      _state = state;
    }

    public bool IsValid(int x, int y) {
      var terrain = _state.TerrainGrid[x, y];
      return terrain.PlayerCanWalk() ||
             (terrain.IsHole() && _state.IsFilledHoleAt(x, y));
    }
  }

  private FloodFillScanner _floodFillScanner = new();

  public List<Vector2Int> GetWalkableArea(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    return new List<Vector2Int>(GetWalkableAreaNoCopy(state, out canonicalPlayerPos));
  }

  /// <summary>
  /// Performs a flood fill to find all reachable squares from the player's current position.
  /// Returns the list of reachable squares AND the "Canonical" position (Min X, then Min Y).
  /// </summary>
  public IReadOnlyList<Vector2Int> GetWalkableAreaNoCopy(
      SokobanState state,
      out Vector2Int canonicalPlayerPos) {
    _floodFillScanner.Scan(
        new FloodFillScannerAdapter(state),
        state.PlayerPos,
        state.CratePositions);

    Vector2Int minPos = state.PlayerPos;
    foreach (var pos in _floodFillScanner.Reached) {
      if (pos.x < minPos.x || (pos.x == minPos.x && pos.y < minPos.y)) {
        minPos = pos;
      }
    }

    canonicalPlayerPos = minPos;
    return _floodFillScanner.Reached;
  }
}
