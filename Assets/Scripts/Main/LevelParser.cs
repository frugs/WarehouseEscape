using System.IO;
using UnityEngine;

public class LevelParser {
  public static LevelData ParseLevelFile(string filePath) {
    if (!File.Exists(filePath)) {
      Debug.LogError($"Level file not found: {filePath}");
      return null;
    }

    string[] lines = File.ReadAllLines(filePath);
    if (lines.Length < 2) return null;

    // Parse dimensions
    string[] sizeValues = lines[0].Split(' ');
    if (sizeValues.Length != 2 ||
        !int.TryParse(sizeValues[0], out int width) ||
        !int.TryParse(sizeValues[1], out int height)) {
      Debug.LogError("Invalid level dimensions");
      return null;
    }

    if (lines.Length - 1 != height) {
      Debug.LogError("Line count doesn't match height");
      return null;
    }

    var levelData = new LevelData {
      width = width,
      height = height,
      grid = new Cell[width, height],
      playerDetected = false,
      targetCount = 0,
      crateCount = 0,
      playerPos = new Vector2Int(-1, -1)
    };

    for (int y = 0; y < height; y++) {
      string[] line = lines[y + 1].Split(' ');
      if (line.Length != width) {
        Debug.LogError($"Line {y} length mismatch");
        return null;
      }

      for (int x = 0; x < width; x++) {
        char symbol = line[x][0];
        levelData.grid[x, y] = ParseCell(symbol, x, y, levelData);
      }
    }

    ValidateLevelData(levelData);
    return levelData;
  }

  private static Cell ParseCell(char symbol, int x, int y, LevelData data) {
    var cell = new Cell { x = x, y = y };

    switch (symbol) {
      case 'E':
        cell.terrain = TerrainType.Floor;
        cell.occupant = Occupant.Empty;
        break;

      case 'X':
        cell.terrain = TerrainType.Wall;
        cell.occupant = Occupant.Empty;
        break;

      case 'H':
        cell.terrain = TerrainType.Hole;
        cell.occupant = Occupant.Empty;
        break;

      case 'T':
        cell.terrain = TerrainType.Floor;
        cell.isTarget = true;
        cell.occupant = Occupant.Empty;
        data.targetCount++;
        break;

      case 'P':
        cell.terrain = TerrainType.Floor;
        cell.occupant = Occupant.Player;
        if (data.playerDetected) Debug.LogError("More than one player detected");
        data.playerDetected = true;
        data.playerPos = new Vector2Int(x, y);
        break;

      case 'B':
        cell.terrain = TerrainType.Floor;
        cell.occupant = Occupant.Crate;
        data.crateCount++;
        break;

      case 'p': // player on target
        cell.terrain = TerrainType.Floor;
        cell.isTarget = true;
        cell.occupant = Occupant.Player;
        data.targetCount++;
        if (data.playerDetected) Debug.LogError("More than one player detected");
        data.playerDetected = true;
        data.playerPos = new Vector2Int(x, y);
        break;

      case 'b': // crate on target
        cell.terrain = TerrainType.Floor;
        cell.isTarget = true;
        cell.occupant = Occupant.Crate;
        data.targetCount++;
        data.crateCount++;
        break;

      default:
        Debug.LogWarning($"Unknown symbol '{symbol}' at {x},{y}");
        cell.terrain = TerrainType.Floor;
        cell.occupant = Occupant.Empty;
        break;
    }

    return cell;
  }

  private static void ValidateLevelData(LevelData data) {
    if (!data.playerDetected || data.targetCount < data.crateCount ||
        data.targetCount <= 0 || data.crateCount <= 0) {
      Debug.LogError("Level validation failed - check ImportantInfo.txt");
    }
  }
}

[System.Serializable]
public class LevelData {
  public Cell[,] grid;
  public int width, height;

  public bool playerDetected;
  public Vector2Int playerPos;

  public int targetCount, crateCount;
}
