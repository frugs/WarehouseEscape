using System.Collections.Generic;
using NUnit.Framework;

public class SokobanTestCases {
  public static IEnumerable<TestCaseData> GetSolverCases() {
    // CASE 1: Simple 3x1 win (Player pushes Crate to Target)
    // P = Player, B = Box, T = Target, . = Floor
    string level1 =
        "5 1\n" + // Width Height
        "P B T . ."; // Map
    yield return new TestCaseData(level1, true).SetName("Simple_Push_Win");

    // CASE 2: Blocked by Wall (Player, Box, Wall, Target)
    // X = Wall
    string level2 =
        "5 1\n" +
        "P B X T .";
    yield return new TestCaseData(level2, false).SetName("Blocked_By_Wall");

    // CASE 3: Box in Corner (Unsolvable)
    // X P .
    // X B .
    // X X .
    // Target is far away
    {
      string level =
          "5 5\n" +
          "X X X X X\n" +
          "X P . . X\n" +
          "X B . . X\n" + // Box at 1,2 against wall X=0
          "X X . T X\n" + // Corner at 0,3 and 1,3
          "X X X X X";
      yield return new TestCaseData(level, false).SetName("Box_In_Corner_Deadlock");
    }

    // CASE 4: Two Boxes, Two Targets (Solvable)
    {
      string level =
          "6 3\n" +
          "X X . B T X\n" +
          "X P B . . T\n" +
          "X X X X X X";
      yield return new TestCaseData(level, true).SetName("Two_Boxes_Two_Targets");
    }

    // P -> B -> H -> T
    // Box fills hole (consumes box), then player walks over filled hole to... wait.
    // If box fills hole, box is GONE. Win condition requires box on target.
    // So this level is UNSOLVABLE unless there is a SPARE box.
    {
      string level =
          "5 1\n" +
          "P B H T .";
      yield return new TestCaseData(level, false).SetName("Hole_Consumes_Box_No_Win");
    }

    // CASE 6: Hole Handling with Spare Box
    // P B B H T
    // 1st box fills hole. 2nd box crosses filled hole to target.
    {
      string level =
          "6 1\n" +
          "P B H B T .";
      yield return new TestCaseData(level, true).SetName("Hole_Fill_Bridge_Win");
    }

    // Original level 1
    {
      string level =
          "8 9\n" +
          "E E H H H H H E\n" +
          "X X X E E E H E\n" +
          "X T P B E E H E\n" +
          "X X X E B T H E\n" +
          "X T X X B E H E\n" +
          "X E X E T E X X\n" +
          "X B E b B B T X\n" +
          "X E E E T E E X\n" +
          "X X X X X X X X";
      yield return new TestCaseData(level, true).SetName("Original_Level_1");
    }

    // CASE: Exit unreachable (unsolvable)
    {
      // 5x3:
      // Row 2: X X X X X
      // Row 1: > B T X X
      // Row 0: . . . X <  (exit enclosed by walls above/left)
      //
      // Player can solve crates, but cannot reach the exit.
      string level = @"
5 3
X X X X X
> B T X X
. . . X <
";
      yield return new TestCaseData(level, false)
          .SetName("Exit_Unreachable_Unsolvable");
    }

    // CASE: Two targets, one exit, solvable
    {
      // Room is open; both crates can reach targets and player can walk to exit.
      string level = @"
7 4
> X X X X X X
. B T B T . .
. . . . . . .
X X X X X X <
";
      yield return new TestCaseData(level, true)
          .SetName("TwoTargets_OneExit_Solvable");
    }

    // CASE: One targets, one exit, unsolvable
    {
      string level = @"
7 3
X X X X X X X
> B T . . . <
X X X X X X X
";
      yield return new TestCaseData(level, false)
          .SetName("OneTarget_OneExit_BoxBlocksExit_Unsolvable");
    }

    {
      string level = @"
9 5
X > X . . X X X X
X . . B . . . . X
X . X X H X X B X
X . B T . B T H X
X X X X X X X < X
";
      yield return new TestCaseData(level, true)
          .SetName("EntranceExit_HoleBridge_4Boxes2Targets_Solvable");
    }
  }
}

public class SokobanParamTests {
  private const bool LogSolutions = true;


  [Test, TestCaseSource(typeof(SokobanTestCases), nameof(SokobanTestCases.GetSolverCases))]
  public void Solver_Verifies_Level_Solvability(string levelDataString, bool expectedSolvable) {
    // 1. ARRANGE
    // Parse the ASCII string into a LevelData object
    LevelData data = LevelParser.ParseLevelFromText(levelDataString);
    var initialState = SokobanState.Create(data.grid, data.playerPos, data.crates);

    Assert.IsNotNull(data, "Level parsing failed. Check input string format.");

    // Setup State
    SokobanSolver solver = new SokobanSolver();

    // 2. ACT
    var solution = solver.FindSolutionPath(initialState);
    if (LogSolutions && solution != null) {
      foreach (var move in solution) {
        UnityEngine.Debug.Log(move);
      }
    }


    // 3. ASSERT
    var solvable = solution != null;
    Assert.AreEqual(
        expectedSolvable,
        solvable,
        $"Expected level to be {(expectedSolvable ? "Solvable" : "Unsolvable")}, " +
        $"but Solver returned {solution}.");
  }
}
