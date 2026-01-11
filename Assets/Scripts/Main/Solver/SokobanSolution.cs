using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[System.Serializable]
public class SokobanSolution {
  public List<SokobanMove> Moves { get; } // Solution path
  public SokobanState InitialState { get; } // Full starting state (grid, crates, etc.)
  public int StatesExplored { get; }

  public SokobanSolution(List<SokobanMove> moves, SokobanState state, int statesExplored) {
    Moves = moves;
    InitialState = state;
    StatesExplored = statesExplored;
  }

  // Computed metrics
  public int SolutionLength => Moves?.Count ?? 0;
  public int NumPushes => Moves?.Count(m => m.type == MoveType.CratePush) ?? 0;
  public int TrueHoles => CountTerrain(InitialState, t => t.IsTrueHole());
  public int Targets => CountTerrain(InitialState, t => t.IsTarget());
  public float CrateDispersion => CalculateDispersion(InitialState);

  // 0-10 difficulty score
  public float Difficulty => CalculateDifficulty();

  private static int CountTerrain(SokobanState state, System.Func<TerrainType, bool> pred) {
    int count = 0;
    for (int x = 0; x < state.GridWidth; x++)
    for (int y = 0; y < state.GridHeight; y++)
      if (pred(state.TerrainGrid[x, y]))
        count++;
    return count;
  }

  private static float CalculateDispersion(SokobanState state) {
    var targets = new List<Vector2Int>();
    for (int x = 0; x < state.GridWidth; x++) {
      for (int y = 0; y < state.GridHeight; y++) {
        if (state.TerrainGrid[x, y].IsTarget()) {
          targets.Add(new Vector2Int(x, y));
        }
      }
    }

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
    float ratio = StatesExplored / Mathf.Max(SolutionLength, 1f);
    float complexity = Mathf.Log(ratio + 1f, 2f);
    float push = Mathf.Log(NumPushes + 1f, 5f) * 0.3f;
    float risk = Mathf.Log(TrueHoles + Targets + 1f, 2f) * 0.3f;
    float disp = CrateDispersion / 100f;
    float raw = complexity + push + risk + disp;
    float final = Mathf.Clamp(raw, 0.5f, 10f);

    Debug.Log(
        $"Difficulty breakdown: complexity={complexity:F2}, push={push:F2}, " +
        $"risk={risk:F2}, disp={disp:F2}, raw={raw:F2}, final={final:F2}");

    return final;
  }
}
