using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SokobanSolution {
    public SokobanState InitialState;      // Full starting state (grid, crates, etc.)
    public List<SokobanMove> Moves;        // Solution path
    public long SolveTimeMs;
    public int NodesVisited;
    
    // Computed metrics
    public int SolutionLength => Moves?.Count ?? 0;
    public int NumPushes => Moves?.Count(m => m.type == SokobanMove.MoveType.CratePush) ?? 0;
    public int TrueHoles => CountTerrain(InitialState, t => t.IsTrueHole());
    public int Targets => CountTerrain(InitialState, t => t.IsTarget);
    public float CrateDispersion => CalculateDispersion(InitialState);
    
    // 0-10 difficulty score
    public float Difficulty => CalculateDifficulty();
    
    private static int CountTerrain(SokobanState state, System.Func<TerrainType, bool> pred) {
        int count = 0;
        for (int x = 0; x < state.GridWidth; x++)
            for (int y = 0; y < state.GridHeight; y++)
                if (pred(state.TerrainGrid[x, y])) count++;
        return count;
    }
    
    private static float CalculateDispersion(SokobanState state) {
        var targets = new List<Vector2Int>();
        for (int x = 0; x < state.GridWidth; x++)
            for (int y = 0; y < state.GridHeight; y++)
                if (state.TerrainGrid[x, y].IsTarget) targets.Add(new Vector2Int(x, y));
        
        float totalDist = 0f;
        foreach (var crate in state.CratePositions) {
            float minDist = float.MaxValue;
            foreach (var tgt in targets) {
                float dist = Mathf.Abs(crate.x - tgt.x) + Mathf.Abs(crate.y - tgt.y);
                if (dist < minDist) minDist = dist;
            }
            totalDist += minDist;
        }
        return totalDist / state.CratePositions.Length;
    }
    
    private float CalculateDifficulty() {
        float normLen = Mathf.Log(SolutionLength + 1f, 2f);
        float pushes = NumPushes * 1.5f;
        float risk = TrueHoles * (Targets * 0.8f);
        float search = Mathf.Sqrt(NodesVisited / 1000f);
        float disp = CrateDispersion / (InitialState.GridWidth + InitialState.GridHeight);
        float raw = normLen * pushes * (1f + risk) * (1f + search) * (1f + disp);
        return Mathf.Clamp(raw * 0.6f, 0.5f, 10f);
    }
}