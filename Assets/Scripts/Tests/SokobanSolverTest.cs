using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class SokobanSolverTests {
  // Helper to create a basic 5x5 empty room with walls around edges
  private Cell[,] CreateEmptyRoom(int width, int height) {
    Cell[,] grid = new Cell[width, height];
    for (int x = 0; x < width; x++) {
      for (int y = 0; y < height; y++) {
        grid[x, y] = new Cell {
          x = x,
          y = y,
          terrain = TerrainType.Floor,
          occupant = Occupant.Empty,
          isTarget = false
        };

        // Add walls to edges
        if (x == 0 || x == width - 1 || y == 0 || y == height - 1) {
          grid[x, y].terrain = TerrainType.Wall;
        }
      }
    }
    return grid;
  }

  [Test]
  public void Solver_Detects_Simple_Win() {
    // ARRANGE: Create a simple 4x1 corridor
    // [Player] [Crate] [Target]
    int width = 5;
    int height = 3;
    Cell[,] grid = CreateEmptyRoom(width, height);

    // Player at (1,1)
    grid[1, 1].occupant = Occupant.Player;
    Vector2Int playerPos = new Vector2Int(1, 1);

    // Crate at (2,1)
    grid[2, 1].occupant = Occupant.Crate;

    // Target at (3,1)
    grid[3, 1].isTarget = true;

    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(grid, playerPos);

    // ASSERT
    Assert.IsTrue(isSolvable, "Solver should find a solution for a simple push.");
  }

  [Test]
  public void Solver_Detects_Unsolvable_Corner() {
    // ARRANGE: Crate pushed into a corner (1,1) with walls at x=0 and y=0
    // Target is far away at (3,3)
    int width = 5;
    int height = 5;
    Cell[,] grid = CreateEmptyRoom(width, height);

    // Player at (2,1)
    grid[2, 1].occupant = Occupant.Player;
    Vector2Int playerPos = new Vector2Int(2, 1);

    // Crate at (1,1) - This is the corner because (0,y) and (x,0) are walls
    grid[1, 1].occupant = Occupant.Crate;

    // Target at (3,3)
    grid[3, 3].isTarget = true;

    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(grid, playerPos);

    // ASSERT
    Assert.IsFalse(isSolvable, "Solver should return false when crate is stuck in a corner.");
  }

  [Test]
  public void Solver_Finds_Correct_Path_Sequence() {
    // ARRANGE
    // [Player(1,1)] [Crate(2,1)] [Target(3,1)]
    // Requires 1 push to the right.
    int width = 5;
    int height = 3;
    Cell[,] grid = CreateEmptyRoom(width, height);

    Vector2Int playerPos = new Vector2Int(1, 1);
    grid[1, 1].occupant = Occupant.Player;
    grid[2, 1].occupant = Occupant.Crate;
    grid[3, 1].isTarget = true;

    SokobanSolver solver = new SokobanSolver();

    // ACT
    List<SokobanMove> solution = solver.FindSolutionPath(grid, playerPos);

    // ASSERT
    Assert.IsNotNull(solution, "Solution should not be null.");
    Assert.AreEqual(1, solution.Count, "Should require exactly 1 move.");

    // Verify the specific move details
    SokobanMove move = solution[0];
    Assert.AreEqual(MoveType.CratePush, move.type);
    Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
    Assert.AreEqual(new Vector2Int(2, 1), move.playerTo); // Player moves into crate's old spot
    Assert.AreEqual(new Vector2Int(2, 1), move.crateFrom);
    Assert.AreEqual(new Vector2Int(3, 1), move.crateTo); // Crate ends up on target
  }

  [Test]
  public void Solver_Can_Fill_Hole() {
    // ARRANGE: Player pushes crate into a hole to cross it?
    // Or simply checking if logic handles Hole filling correctly.
    // Setup: [Player] [Crate] [Hole] [Target]
    // Note: In your logic, a crate fills a hole and disappears (Occupant.Empty)
    // but creates a filled floor (FilledHole).

    int width = 6;
    int height = 3;
    Cell[,] grid = CreateEmptyRoom(width, height);

    Vector2Int playerPos = new Vector2Int(1, 1);
    grid[1, 1].occupant = Occupant.Player;

    grid[2, 1].occupant = Occupant.Crate;

    // The hole the crate must fall into
    grid[3, 1].terrain = TerrainType.Hole;

    // The target is BEYOND the hole, so we might need another crate?
    // Actually, let's just test if the solver accepts "Crate in Hole" as a valid state transition
    // Your win condition requires crates on targets.
    // If a crate falls in a hole, it's gone.
    // So this test checks if the solver can navigate *around* or *use* the hole logic without crashing.

    // Let's change the test: A simple valid push into a hole.
    // If the goal requires all crates on targets, losing a crate in a hole makes it UNSOLVABLE
    // (unless you have spare crates, which your parser allows).

    // Let's just verify the MoveManager logic applied by the solver for holes:
    SokobanState state = new SokobanState(grid, playerPos);
    SokobanMove move = SokobanMove.CratePush(new Vector2Int(1, 1), new Vector2Int(2, 1), new Vector2Int(2, 1), new Vector2Int(3, 1));

    // ACT
    SokobanState newState = MoveManager.ApplyMove(state, move);
    Cell holeCell = newState.grid[3, 1];

    // ASSERT
    Assert.AreEqual(TerrainType.FilledHole, holeCell.terrain, "Hole should become FilledHole.");
    Assert.AreEqual(Occupant.Empty, holeCell.occupant, "Filled hole should be empty (crate consumed).");
  }

  [Test]
  public void State_Detects_Win_Correctly() {
    // ARRANGE: 2 Targets, 2 Crates
    int width = 3;
    int height = 3;
    Cell[,] grid = CreateEmptyRoom(width, height);

    // Target 1 & Crate 1
    grid[1, 1].isTarget = true;
    grid[1, 1].occupant = Occupant.Crate;

    // Target 2 & Crate 2
    grid[2, 2].isTarget = true;
    grid[2, 2].occupant = Occupant.Crate;

    SokobanState state = new SokobanState(grid, new Vector2Int(0, 0));

    // ACT & ASSERT
    Assert.IsTrue(state.IsWin, "Should be a win when all targets have crates.");

    // Remove one crate
    state.grid[2, 2].occupant = Occupant.Empty;
    Assert.IsFalse(state.IsWin, "Should NOT be a win if a target is empty.");
  }
}
