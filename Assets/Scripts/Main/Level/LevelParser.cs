using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class LevelParser {
  public static LevelData ParseLevelFile(string filePath, bool validate = true) {
    if (!File.Exists(filePath)) {
      Debug.LogError($"Level file not found: {filePath}");
      return null;
    }

    string[] lines = File.ReadAllLines(filePath);
    return ParseLevelLines(lines, validate);
  }

  public static LevelData ParseLevelFromText(string levelText, bool validate = true) {
    // Split by newlines (handling varied line endings)
    string[] lines = levelText.Split(
        new[] { "\r\n", "\r", "\n" },
        System.StringSplitOptions.None);
    return ParseLevelLines(lines, validate);
  }

  private static LevelData ParseLevelLines(string[] lines, bool validate) {
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
        grid = new TerrainType[width, height],
        playerDetected = false,
        targetCount = 0,
        crateCount = 0,
        playerPos = new Vector2Int(-1, -1),
        crates = new List<Vector2Int>(),
    };

    for (int y = 0; y < height; y++) {
      string[] line = lines[y + 1].Split(' ');
      if (line.Length != width) {
        Debug.LogError($"Line {y} length mismatch");
        return null;
      }

      for (int x = 0; x < width; x++) {
        char symbol = line[x][0];
        levelData.grid[x, y] = ParseTerrain(symbol, x, y, levelData);
      }
    }

    if (validate) {
      ValidateLevelData(levelData);
    }

    return levelData;
  }

  private static TerrainType ParseTerrain(char symbol, int x, int y, LevelData data) {
    TerrainType terrain;

    switch (symbol) {
      case 'E':
      case '.':
        terrain = TerrainType.Floor;
        break;

      case 'X':
      case '#':
        terrain = TerrainType.Wall;
        break;

      case 'H':
        terrain = TerrainType.Hole;
        break;

      case 'T':
        terrain = TerrainType.Target;
        data.targetCount++;
        break;

      case 'P':
        terrain = TerrainType.Floor;
        if (data.playerDetected) {
          Debug.LogError("More than one player detected");
        }

        data.playerDetected = true;
        data.playerPos = new Vector2Int(x, y);
        break;

      case 'B':
        terrain = TerrainType.Floor;
        data.crates.Add(new Vector2Int(x, y));
        data.crateCount++;
        break;

      case 'p': // player on target
        terrain = TerrainType.Target;
        data.targetCount++;
        if (data.playerDetected) {
          Debug.LogError("More than one player detected");
        }

        data.playerDetected = true;
        data.playerPos = new Vector2Int(x, y);
        break;

      case 'b': // crate on target
        terrain = TerrainType.Target;
        data.crates.Add(new Vector2Int(x, y));
        data.targetCount++;
        data.crateCount++;
        break;
      
      case '>':
        terrain = TerrainType.Entrance;
        data.playerDetected = true;
        data.playerPos = new Vector2Int(x, y);
        break;
      
      case '<':
        terrain = TerrainType.Exit;
        break;

      default:
        Debug.LogWarning($"Unknown symbol '{symbol}' at {x},{y}");
        terrain = TerrainType.Floor;
        break;
    }

    return terrain;
  }

  private static void ValidateLevelData(LevelData data) {
    if (!data.playerDetected || data.crateCount < data.targetCount ||
        data.targetCount <= 0 || data.crateCount <= 0) {
      Debug.LogError("Level validation failed - check ImportantInfo.txt");
    }
  }
}

[System.Serializable]
public class LevelData {
  public TerrainType[,] grid;
  public int width, height;

  public bool playerDetected;
  public Vector2Int playerPos;

  public List<Vector2Int> crates;

  public int targetCount, crateCount;
}
