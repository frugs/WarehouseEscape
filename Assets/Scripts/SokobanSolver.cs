using System.Collections.Generic;
using UnityEngine;

public class SokobanSolver {
  private struct PathNode {
    public string ParentHash;
    public SokobanMove Move;
  }

  /// <summary>Check if current board state is solvable</summary>
  public bool IsSolvable(Cell[,] grid, Vector2Int playerPos) {
    var visited = new HashSet<string>();
    var queue = new Queue<SokobanState>();

    var initialState = new SokobanState(grid, playerPos);
    queue.Enqueue(initialState);
    visited.Add(initialState.StateHash());

    while (queue.Count > 0) {
      var state = queue.Dequeue();

      if (state.IsWin) {
        // Computed from grid!
        return true;
      }

      // Generate valid moves → new states
      foreach (var move in GenerateValidMoves(state.grid, state.playerPos)) {
        var newState = MoveManager.ApplyMove(state, move);

        if (!visited.Contains(newState.StateHash())) {
          visited.Add(newState.StateHash());
          queue.Enqueue(newState);
        }
      }
    }

    return false;
  }

  /// <summary>Generate all legal moves from current state</summary>
  private List<SokobanMove> GenerateValidMoves(Cell[,] grid, Vector2Int playerPos) {
    var moves = new List<SokobanMove>();
    int width = grid.GetLength(0);
    int height = grid.GetLength(1);

    // 4 directions
    foreach (Vector2Int direction in new[] {
               Vector2Int.up, Vector2Int.down,
               Vector2Int.left, Vector2Int.right
             }) {
      Vector2Int targetPos = playerPos + direction;

      if (!IsInBounds(targetPos, width, height)) continue;
      Cell targetCell = grid[targetPos.x, targetPos.y];

      if (!targetCell.PlayerCanWalk) continue;

      // Player move to empty cell
      if (targetCell.occupant == Occupant.Empty) {
        moves.Add(SokobanMove.PlayerMove(playerPos, targetPos));
      }
      // Crate push
      else if (targetCell.occupant == Occupant.Crate) {
        Vector2Int crateTargetPos = targetPos + direction;
        if (IsValidCratePush(grid, crateTargetPos, width, height)) {
          moves.Add(SokobanMove.CratePush(
            playerPos, targetPos,
            targetPos, crateTargetPos
          ));
        }
      }
    }

    return moves;
  }

  private bool IsValidCratePush(Cell[,] grid, Vector2Int crateTargetPos, int width, int height) {
    if (!IsInBounds(crateTargetPos, width, height)) return false;
    return grid[crateTargetPos.x, crateTargetPos.y].CanReceiveCrate;
  }

  // ========== UTILITIES ==========

  private bool IsInBounds(Vector2Int pos, int width, int height) {
    return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
  }

  /// <summary>Find shortest solution path (optional extension)</summary>
  public List<SokobanMove> FindSolutionPath(Cell[,] grid, Vector2Int playerPos) {
    var parentMap = new Dictionary<string, PathNode>();
    var queue = new Queue<SokobanState>();
    var visited = new HashSet<string>();

    var initialState = new SokobanState(grid, playerPos);
    string startHash = initialState.StateHash();

    queue.Enqueue(initialState);
    visited.Add(initialState.StateHash());

    parentMap[startHash] = new PathNode { ParentHash = null, Move = null };

    while (queue.Count > 0) {
      var state = queue.Dequeue();

      if (state.IsWin) {
        return ReconstructPath(parentMap, state.StateHash());
      }

      foreach (var move in GenerateValidMoves(state.grid, state.playerPos)) {
        var newState = MoveManager.ApplyMove(state, move);
        string newHash = newState.StateHash();

        if (!visited.Contains(newHash)) {
          visited.Add(newHash);
          parentMap[newHash] = new PathNode {
            ParentHash = state.StateHash(),
            Move = move
          };
          queue.Enqueue(newState);
        }
      }
    }

    return null; // Unsolvable
  }

  private List<SokobanMove>
    ReconstructPath(Dictionary<string, PathNode> parentMap, string goalHash) {
    var path = new List<SokobanMove>();
    string current = goalHash;

    while (parentMap.ContainsKey(current)) {
      var node = parentMap[current];
      if (node.ParentHash == null) break;

      path.Add(node.Move);
      current = node.ParentHash;
    }

    path.Reverse();
    return path;
  }
}
