using System.Collections.Generic;

[System.Serializable]
public class SolutionData {
  public string LevelName;
  public int StepCount;
  public long SolveTimeMs;
  public List<SokobanMove> Moves;
}
