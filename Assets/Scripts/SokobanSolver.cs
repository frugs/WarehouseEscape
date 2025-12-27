using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SokobanSolver {
    public enum SolveResult { Solvable, Unsolvable }
    
    /// <summary>Check if current board state is solvable</summary>
    public SolveResult IsSolvable(Cell[,] grid, Vector2Int playerPos) {
        var visited = new HashSet<string>();
        var queue = new Queue<SokobanState>();
        
        var initialState = new SokobanState(grid, playerPos);
        queue.Enqueue(initialState);
        visited.Add(initialState.StateHash());
        
        while(queue.Count > 0) {
            var state = queue.Dequeue();
            
            if(state.IsWin) {  // Computed from grid!
                return SolveResult.Solvable;
            }
            
            // Generate valid moves → new states
            foreach(var move in GenerateValidMoves(state.grid, state.playerPos)) {
                var newState = MoveManager.ApplyMove(state, move);
                
                if(!visited.Contains(newState.StateHash())) {
                    visited.Add(newState.StateHash());
                    queue.Enqueue(newState);
                }
            }
        }
        
        return SolveResult.Unsolvable;
    }
    
    /// <summary>Generate all legal moves from current state</summary>
    private List<SokobanMove> GenerateValidMoves(Cell[,] grid, Vector2Int playerPos) {
        var moves = new List<SokobanMove>();
        int width = grid.GetLength(0);
        int height = grid.GetLength(1);
        
        // 4 directions
        foreach(Vector2Int direction in new[] {
            Vector2Int.up, Vector2Int.down, 
            Vector2Int.left, Vector2Int.right 
        }) {
            Vector2Int targetPos = playerPos + direction;
            
            if(!IsInBounds(targetPos, width, height)) continue;
            Cell targetCell = grid[targetPos.x, targetPos.y];
            
            if(!targetCell.PlayerCanWalk) continue;
            
            // Player move to empty cell
            if(targetCell.occupant == Occupant.Empty) {
                moves.Add(SokobanMove.PlayerMove(playerPos, targetPos));
            }
            // Crate push
            else if(targetCell.occupant == Occupant.Crate) {
                Vector2Int crateTargetPos = targetPos + direction;
                if(IsValidCratePush(grid, crateTargetPos, width, height)) {
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
        if(!IsInBounds(crateTargetPos, width, height)) return false;
        return grid[crateTargetPos.x, crateTargetPos.y].CanReceiveCrate;
    }
    
    // ========== UTILITIES ==========
    
    private bool IsInBounds(Vector2Int pos, int width, int height) {
        return pos.x >= 0 && pos.x < width && pos.y >= 0 && pos.y < height;
    }
    
    /// <summary>Find shortest solution path (optional extension)</summary>
    public List<SokobanMove> FindSolutionPath(Cell[,] grid, Vector2Int playerPos) {
        var parentMap = new Dictionary<string, SokobanMove>();
        var queue = new Queue<SokobanState>();
        var visited = new HashSet<string>();
        
        var initialState = new SokobanState(grid, playerPos);
        queue.Enqueue(initialState);
        visited.Add(initialState.StateHash());
        
        while(queue.Count > 0) {
            var state = queue.Dequeue();
            
            if(state.IsWin) {
                return ReconstructPath(parentMap, state.StateHash());
            }
            
            foreach(var move in GenerateValidMoves(state.grid, state.playerPos)) {
                var newState = MoveManager.ApplyMove(state, move);
                string newHash = newState.StateHash();
                
                if(!visited.Contains(newHash)) {
                    visited.Add(newHash);
                    parentMap[newHash] = move;
                    queue.Enqueue(newState);
                }
            }
        }
        
        return null;  // Unsolvable
    }
    
    private List<SokobanMove> ReconstructPath(Dictionary<string, SokobanMove> parentMap, string goalHash) {
        var path = new List<SokobanMove>();
        string current = goalHash;
        
        while(parentMap.ContainsKey(current)) {
            path.Add(parentMap[current]);
            current = MoveManager.ApplyMove(/* previous state */, parentMap[current]).StateHash();
        }
        
        path.Reverse();
        return path;
    }
}
