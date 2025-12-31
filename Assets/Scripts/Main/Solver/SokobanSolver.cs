using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class SokobanSolver {
  const int MAX_ITERATIONS = 10_000_000; // Limit total states explored
  const long MAX_MS = 60_000;

  private struct PathNode {
    public SokobanState? ParentState;
    public SokobanMove? Move;
  }

  private DeadSquareMap DeadSquareMap;

  public bool IsSolvable(SokobanState state, int maxIterations = MAX_ITERATIONS) {
    var solution = FindSolutionPath(state, maxIterations);
    return solution != null;
  }

  /// <summary>Generate all legal moves from current state</summary>
  private List<SokobanMove> GenerateValidMoves(SokobanState state) {
    var playerPos = state.PlayerPos;
    var moves = new List<SokobanMove>();
    int width = state.TerrainGrid.GetLength(0);
    int height = state.TerrainGrid.GetLength(1);

    // 4 directions
    foreach (Vector2Int direction in new[] {
               Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
             }) {
      Vector2Int targetPos = playerPos + direction;

      if (!IsInBounds(targetPos, width, height)) continue;

      // Player move to empty cell
      if (state.CanPlayerWalk(targetPos.x, targetPos.y)) {
        moves.Add(SokobanMove.PlayerMove(playerPos, targetPos));
      }
      // Crate push
      else if (state.IsCrateAt(targetPos.x, targetPos.y)) {
        Vector2Int crateTargetPos = targetPos + direction;
        if (IsValidCratePush(state, crateTargetPos, width, height)
            && !IsDeadlock(state, crateTargetPos, width, height)
            && !IsCrateInDeadSquare(crateTargetPos)) {
          moves.Add(SokobanMove.CratePush(
            playerPos,
            targetPos,
            targetPos,
            crateTargetPos
          ));
        }
      }
    }

    return moves;
  }

  private bool IsValidCratePush(SokobanState state, Vector2Int crateTargetPos, int width,
    int height) {
    if (!IsInBounds(crateTargetPos, width, height)) return false;
    return state.CanReceiveCrate(crateTargetPos.x, crateTargetPos.y);
  }

  private bool IsDeadlock(SokobanState state, Vector2Int pos, int width, int height) {
    // If it's on a target, it's not a deadlock (usually)
    if (state.TerrainGrid[pos.x, pos.y].IsTarget()) return false;

    // Check axes (Horizontal and Vertical neighbors)
    // blocked if Wall or existing Crate (that isn't moving)
    bool blockedLeft = IsBlocking(state.TerrainGrid, pos.x - 1, pos.y, width, height);
    bool blockedRight = IsBlocking(state.TerrainGrid, pos.x + 1, pos.y, width, height);
    bool blockedUp = IsBlocking(state.TerrainGrid, pos.x, pos.y + 1, width, height);
    bool blockedDown = IsBlocking(state.TerrainGrid, pos.x, pos.y - 1, width, height);

    // Corner Deadlock: Blocked vertically AND horizontally
    // Top-Left, Top-Right, Bottom-Left, Bottom-Right
    if ((blockedLeft || blockedRight) && (blockedUp || blockedDown)) {
      return true;
    }

    return false;
  }

  private bool IsCrateInDeadSquare(Vector2Int crateTargetPos) {
    return DeadSquareMap.IsDeadSquare(crateTargetPos.x, crateTargetPos.y);
  }

  private bool IsBlocking(TerrainType[,] grid, int x, int y, int width, int height) {
    // Check bounds
    if (x < 0 || x >= width || y < 0 || y >= height) return true; // Edge is a wall

    return grid[x, y] == TerrainType.Wall;
    // Note: Simple deadlocks focus on Walls.
    // Crates can be moved, so they aren't permanent deadlocks unless frozen.
  }

  // ========== UTILITIES ==========

  private bool IsInBounds(Vector2Int pos, int width, int height) {
    return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
  }

  /// <summary>Find shortest solution path (optional extension)</summary>
  public List<SokobanMove> FindSolutionPath(
    SokobanState initialState, int maxIterations = MAX_ITERATIONS) {
    var parentMap = new Dictionary<SokobanState, PathNode>();
    var queue = new Queue<SokobanState>();
    var visited = new HashSet<SokobanState>();

    queue.Enqueue(initialState);
    visited.Add(initialState);

    parentMap[initialState] = new PathNode { ParentState = null, Move = null };

    int iterations = 0;
    Stopwatch timer = Stopwatch.StartNew();

    DeadSquareMap = new DeadSquareMap(initialState);

    while (queue.Count > 0) {
      if (++iterations > maxIterations || timer.ElapsedMilliseconds > MAX_MS) {
        UnityEngine.Debug.LogError(
          $"Solver Timeout! Checked {iterations} states in {timer.ElapsedMilliseconds}ms.");
        return null; // Give up
      }

      var state = queue.Dequeue();

      if (state.IsWin()) {
        return ReconstructPath(parentMap, state);
      }

      foreach (var move in GenerateValidMoves(state)) {
        var newState = MoveRules.ApplyMove(state, move);

        if (!visited.Contains(newState)) {
          visited.Add(newState);
          parentMap[newState] = new PathNode { ParentState = state, Move = move };
          queue.Enqueue(newState);
        }
      }
    }

    return null; // Unsolvable
  }

  private List<SokobanMove>
    ReconstructPath(Dictionary<SokobanState, PathNode> parentMap, SokobanState goalState) {
    var path = new List<SokobanMove>();
    SokobanState current = goalState;

    while (parentMap.ContainsKey(current)) {
      var node = parentMap[current];
      if (node.ParentState == null || node.Move == null) break;

      path.Add((SokobanMove)node.Move);
      current = (SokobanState)node.ParentState;
    }

    path.Reverse();
    return path;
  }
}
