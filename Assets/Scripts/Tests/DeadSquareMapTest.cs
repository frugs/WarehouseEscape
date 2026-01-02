using NUnit.Framework;

public class DeadSquareMapTest {
  [Test]
  public void Test_SimpleCorridor() {
    // 5x3 Level
    // Line 1: Dimensions (width height)
    // Line 2+: Map
    string levelText = @"5 3
# # # # #
# T . . #
# # # # #";

    var levelData = LevelParser.ParseLevelFromText(levelText, validate: false);
    Assert.IsNotNull(levelData, "Parser failed");

    // Construct State from Data
    var state = new SokobanState(
        levelData.grid,
        levelData.playerPos,
        levelData.crates
    );

    var map = new DeadSquareMap(state);

    // Validations
    Assert.IsFalse(map.IsDeadSquare(1, 1), "Target (1,1) should be Safe");
    Assert.IsFalse(map.IsDeadSquare(2, 1), "Middle (2,1) should be Safe");

    // Corner (3,1) is floor, but you can't push a crate OUT of it because right wall blocks player stand position
    Assert.IsTrue(map.IsDeadSquare(3, 1), "Dead End (3,1) should be Dead");
  }

  [Test]
  public void Test_CornerDeadlock() {
    // 4x4 Level
    string levelText = @"4 4
# # # #
# T . #
# . . #
# # # #";

    var levelData = LevelParser.ParseLevelFromText(levelText, validate: false);
    var state = new SokobanState(levelData.grid, levelData.playerPos, levelData.crates);
    var map = new DeadSquareMap(state);

    // (2,1) Top-Right floor
    // Push Left? Dest(1,1) OK. Player Stand(3,1) -> Wall. FAIL.
    // Push Down? Dest(2,0) OK. Player Stand(2,2) -> Wall. FAIL.
    Assert.IsTrue(map.IsDeadSquare(2, 1), "Corner (2,1) is Dead");
    Assert.IsTrue(map.IsDeadSquare(1, 2), "Corner (1,2) is Dead");
    Assert.IsTrue(map.IsDeadSquare(2, 2), "Corner (2,2) is Dead");
  }

  [Test]
  public void Test_T_Intersection() {
    // A "T" shape where the center is safe
    // 5x4
    // #####
    // #. .#  <-- Two targets
    // #   #  <-- Corridor connecting them
    // ## ##

    string levelText = @"5 4
# # # # #
# T . T #
# . . . #
# # # # #";

    var levelData = LevelParser.ParseLevelFromText(levelText, validate: false);
    var state = new SokobanState(levelData.grid, levelData.playerPos, levelData.crates);
    var map = new DeadSquareMap(state);

    // (2,2) is the center bottom of the T.
    // It can push Up-Left to (1,1) target?
    //  - Push (2,2)->(1,2)? No wall there. Then (1,2)->(1,1).
    // It should be reachable.
    Assert.IsFalse(map.IsDeadSquare(2, 1), "Center of T-junction should be Safe");
    Assert.IsTrue(map.IsDeadSquare(1, 2), "Bottom edge (1, 3) is Dead");
    Assert.IsTrue(map.IsDeadSquare(2, 2), "Bottom edge (2, 3) is Dead");
    Assert.IsTrue(map.IsDeadSquare(3, 2), "Bottom edge (3, 3) is Dead");
  }

  [Test]
  public void Test_ZigZag_Reachability() {
    // A map that requires 2 steps to prove safety.
    // Target (1,1).
    // We verify (2,2) is safe because it can push Down->(2,1) then Left->(1,1).
    string levelText = @"5 5
# # # # #
# T . . #
# . . # #
# . . # #
# # # # #";

    var levelData = LevelParser.ParseLevelFromText(levelText, validate: false);
    var state = new SokobanState(levelData.grid, levelData.playerPos, levelData.crates);
    var map = new DeadSquareMap(state);

    // 1. Target (1,1) is inherently Safe.
    Assert.IsFalse(map.IsDeadSquare(1, 1), "Target (1,1)");

    // 2. (2,1) can push Left to (1,1). Player stands at (3,1) (Floor). OK.
    Assert.IsFalse(map.IsDeadSquare(2, 1), "Next to Target (2,1)");

    // 3. (2,2) can push Down to (2,1). Player stands at (2,3) (Floor). OK.
    Assert.IsFalse(map.IsDeadSquare(2, 2), "Around corner (2,2)");

    Assert.IsTrue(map.IsDeadSquare(1, 3), "Dead");
    Assert.IsTrue(map.IsDeadSquare(2, 3), "Dead");
    Assert.IsTrue(map.IsDeadSquare(3, 1), "Dead");
  }

  [Test]
  public void Test_SolvableSnakeCorridor() {
    // 6x5 Level with a pocket at (4,4) allowing the Down push.
    // S = (1,3)
    // T = (1,1)
    string levelText = @"6 5
# # # # . #
# . . . . #
# # # # . #
# T . . . .
# # # # # #";

    var levelData = LevelParser.ParseLevelFromText(levelText, validate: false);
    var state = new SokobanState(levelData.grid, levelData.playerPos, levelData.crates);
    var map = new DeadSquareMap(state);

    Assert.IsFalse(map.IsDeadSquare(1, 3), "Target (1, 3)");
    Assert.IsFalse(map.IsDeadSquare(2, 3));
    Assert.IsFalse(map.IsDeadSquare(3, 3));
    Assert.IsFalse(map.IsDeadSquare(4, 3));

    Assert.IsTrue(map.IsDeadSquare(5, 3), "Edge");

    Assert.IsFalse(map.IsDeadSquare(4, 2), "Corner");
    Assert.IsFalse(map.IsDeadSquare(4, 1));

    Assert.IsTrue(map.IsDeadSquare(4, 0), "Edge");

    Assert.IsFalse(map.IsDeadSquare(3, 1), "Corner");
    Assert.IsFalse(map.IsDeadSquare(2, 1));

    Assert.IsTrue(map.IsDeadSquare(1, 1));
  }
}
