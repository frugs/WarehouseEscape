using System.Collections.Generic;
using System.Linq;
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
    SokobanState state = SokobanState.Create(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(state, out _);

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

    SokobanState state = SokobanState.Create(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    bool isSolvable = solver.IsSolvable(state, out _);

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
    List<Vector2Int> crates = new List<Vector2Int> { new(2, 1) };

    grid[3, 1] = TerrainType.Target;

    SokobanState state = SokobanState.Create(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    solver.IsSolvable(state, out var solution);
    var moves = solution.Moves;

    // ASSERT
    Assert.IsNotNull(moves, "Solution should not be null.");
    Assert.AreEqual(1, moves.Count, "Should require exactly 1 move.");

    // Verify move
    SokobanMove move = moves[0];
    Assert.AreEqual(MoveType.CratePush, move.type);
    Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
    Assert.AreEqual(new Vector2Int(2, 1), move.playerTo);
    Assert.AreEqual(new Vector2Int(2, 1), move.crateFrom);
    Assert.AreEqual(new Vector2Int(3, 1), move.crateTo);
  }

  [Test]
  public void Solver_Simple_Entrance_Exit() {
    // ARRANGE
    int width = 5;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    grid[0, 1] = TerrainType.Entrance;
    grid[4, 1] = TerrainType.Exit;

    Vector2Int playerPos = new Vector2Int(0, 1);

    SokobanState state = SokobanState.Create(grid, playerPos, Enumerable.Empty<Vector2Int>());
    SokobanSolver solver = new SokobanSolver();

    // ACT
    solver.IsSolvable(state, out var solution);
    var moves = solution.Moves;

    // ASSERT
    Assert.IsNotNull(moves, "Solution should not be null.");
    Assert.AreEqual(4, moves.Count, "Should require exactly 4 moves.");

    // Verify move
    var iter = moves.GetEnumerator();
    iter.MoveNext();

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(0, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(3, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(3, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(4, 1), move.playerTo);
      iter.MoveNext();
    }
  }

  [Test]
  public void Solver_Entrance_Exit_Single_Target() {
    // ARRANGE
    int width = 5;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    grid[0, 1] = TerrainType.Entrance;
    grid[3, 0] = TerrainType.Exit;
    grid[4, 1] = TerrainType.Target;

    Vector2Int playerPos = new Vector2Int(0, 1);
    List<Vector2Int> crates = new List<Vector2Int> { new(3, 1) };

    SokobanState state = SokobanState.Create(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    solver.IsSolvable(state, out var solution);
    var moves = solution.Moves;

    // ASSERT
    Assert.IsNotNull(moves, "Solution should not be null.");
    Assert.AreEqual(4, moves.Count, "Should require exactly 4 moves.");

    // Verify moves
    var iter = moves.GetEnumerator();
    iter.MoveNext();

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(0, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.CratePush, move.type);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(3, 1), move.playerTo);
      Assert.AreEqual(new Vector2Int(3, 1), move.crateFrom);
      Assert.AreEqual(new Vector2Int(4, 1), move.crateTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(3, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(3, 0), move.playerTo);
      iter.MoveNext();
    }
  }

  [Test]
  public void Solver_Entrance_Crate_Hole_Exit() {
    // ARRANGE
    int width = 4;
    int height = 3;
    TerrainType[,] grid = CreateEmptyRoom(width, height);

    grid[0, 1] = TerrainType.Entrance;
    grid[2, 1] = TerrainType.Hole;
    grid[3, 1] = TerrainType.Exit;

    Vector2Int playerPos = new Vector2Int(0, 1);
    Vector2Int[] crates = { new(1, 1) };
    SokobanState state = SokobanState.Create(grid, playerPos, crates);
    SokobanSolver solver = new SokobanSolver();

    // ACT
    solver.IsSolvable(state, out var solution);
    var moves = solution.Moves;

    // ASSERT
    Assert.IsNotNull(moves, "Solution should not be null.");
    Assert.AreEqual(3, moves.Count, "Should require exactly 3 moves.");

    // Verify move
    var iter = moves.GetEnumerator();
    iter.MoveNext();

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.CratePush, move.type);
      Assert.AreEqual(new Vector2Int(0, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerTo);
      Assert.AreEqual(new Vector2Int(1, 1), move.crateFrom);
      Assert.AreEqual(new Vector2Int(2, 1), move.crateTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(1, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerTo);
      iter.MoveNext();
    }

    {
      var move = iter.Current;
      Assert.AreEqual(MoveType.PlayerMove, move.type);
      Assert.AreEqual(new Vector2Int(2, 1), move.playerFrom);
      Assert.AreEqual(new Vector2Int(3, 1), move.playerTo);
      iter.MoveNext();
    }
  }
}
