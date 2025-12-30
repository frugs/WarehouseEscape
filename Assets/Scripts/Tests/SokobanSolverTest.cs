using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SokobanSolverTests {
  // Helper to create a basic empty room with walls around edges
  private TerrainType[,] CreateEmptyRoom(int width, int height) {
    TerrainType[,] grid = new TerrainType[width, height];
    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        // Add walls to edges
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
          grid[x, y] = TerrainType.Wall;
        } else {
          grid[x, y] = TerrainType.Floor;
        }
      }
    }
    return grid;
  }

  [Test]
  public void Solver_Detects_Simple_Win() {
    // ARRANGE: Simple 5x3 room
    // [Player] [Crate] [Target]
    int width = 5;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    // Setup Player
    Vector2Int playerPos = new Vector2Int(1, 1);

    // Setup Crates
    List<Vector2Int> crates = new List<Vector2Int> { new Vector2Int(2, 1) };

    // Setup Target
    grid[3, 1] = TerrainType.Target;

    // Construct State
    SokobanState state = new SokobanState(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(state);

    // ASSERT
    Assert.IsTrue(isSolvable, "Solver should find a solution for a simple push.");
  }

  [Test]
  public void Solver_Detects_Unsolvable_Corner() {
    // ARRANGE: Crate pushed into a corner (1,1)
    int width = 5;
    int height = 5;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    Vector2Int playerPos = new Vector2Int(2, 1);
    List<Vector2Int> crates = new List<Vector2Int> { new Vector2Int(1, 1) }; // Stuck in corner

    // Target is elsewhere
    grid[3, 3] = TerrainType.Target;

    SokobanState state = new SokobanState(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(state);

    // ASSERT
    Assert.IsFalse(isSolvable, "Solver should return false when crate is stuck in a corner.");
  }

  [Test]
  public void Solver_Finds_Correct_Path_Sequence() {
    // ARRANGE
    // [Player(1,1)] [Crate(2,1)] [Target(3,1)]
    int width = 5;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    Vector2Int playerPos = new Vector2Int(1, 1);
    List<Vector2Int> crates = new List<Vector2Int> { new Vector2Int(2, 1) };

    grid[3, 1] = TerrainType.Target;

    SokobanState state = new SokobanState(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    List<SokobanMove> solution = solver.FindSolutionPath(state);

    // ASSERT
    Assert.IsNotNull(solution, "Solution should not be null.");
    Assert.AreEqual(1, solution.Count, "Should require exactly 1 move.");

    // Verify move
    SokobanMove move = solution[0];
    Assert.AreEqual(MoveType.CratePush, move.type);
    Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
    Assert.AreEqual(new Vector2Int(2, 1), move.playerTo);
    Assert.AreEqual(new Vector2Int(2, 1), move.crateFrom);
    Assert.AreEqual(new Vector2Int(3, 1), move.crateTo);
  }

  [Test]
  public void Solver_Can_Fill_Hole() {
    // ARRANGE: Player pushes crate into a hole
    // [Player(1,1)] [Crate(2,1)] [Hole(3,1)]
    int width = 6;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    Vector2Int playerPos = new Vector2Int(1, 1);
    List<Vector2Int> crates = new List<Vector2Int> { new Vector2Int(2, 1) };

    // Set Hole
    grid[3, 1] = TerrainType.Hole;

    SokobanState state = new SokobanState(grid, playerPos, crates);

    // 1. Verify we can PUSH into the hole
    Assert.IsTrue(state.CanReceiveCrate(3, 1), "Hole should receive crate.");

    // 2. Simulate the outcome manually (since we are testing State Logic, not MoveManager here)
    // If we push crate at (2,1) -> (3,1), it fills the hole.

    var nextFilledHoles = new List<Vector2Int> { new Vector2Int(3, 1) };
    var nextCrates = new List<Vector2Int>(); // Crate consumed

    // New player pos would be (2,1)
    SokobanState nextState = new SokobanState(grid, new Vector2Int(2, 1), nextCrates, nextFilledHoles);

    // 3. Verify the hole is now walkable
    Assert.IsTrue(nextState.CanPlayerWalk(3, 1), "Player SHOULD walk on filled hole.");
  }
}
