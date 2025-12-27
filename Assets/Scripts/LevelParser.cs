using System.IO;
using UnityEngine;

public class LevelParser : MonoBehaviour {
    public static LevelData ParseLevelFile(string filePath) {
        if(!File.Exists(filePath)) {
            Debug.LogError($"Level file not found: {filePath}");
            return null;
        }
        
        string[] lines = File.ReadAllLines(filePath);
        if(lines.Length < 2) return null;
        
        // Parse dimensions
        string[] sizeValues = lines[0].Split(' ');
        if(sizeValues.Length != 2 || 
           !int.TryParse(sizeValues[0], out int width) || 
           !int.TryParse(sizeValues[1], out int height)) {
            Debug.LogError("Invalid level dimensions");
            return null;
        }
        
        if(lines.Length - 1 != height) {
            Debug.LogError("Line count doesn't match height");
            return null;
        }
        
        var grid = new Cell[width, height];
        var levelData = new LevelData {
            width = width, height = height, 
            grid = grid, playerDetected = false,
            targetCount = 0, crateCount = 0
        };
        
        for(int y = 0; y < height; y++) {
            string[] line = lines[y + 1].Split(' ');
            if(line.Length != width) {
                Debug.LogError($"Line {y} length mismatch");
                return null;
            }
            
            for(int x = 0; x < width; x++) {
                char symbol = line[x].ToCharArray()[0];
                levelData.grid[x, y] = ParseCell(symbol, x, y);
            }
        }
        
        // Final validation
        ValidateLevelData(levelData);
        return levelData;
    }
    
    private static Cell ParseCell(char symbol, int x, int y) {
        Cell cell = new() { x = x, y = y };
        
        switch(symbol) {
            case 'E': cell.tile = null; break;
            case 'X': 
                cell.tile = new GameObject().AddDummyTag("Wall"); 
                cell.isPassable = false; 
                break;
            case 'P': 
                cell.tile = new GameObject().AddDummyTag("Player"); 
                cell.playerDetected = true; 
                break;
            case 'B': 
                cell.tile = new GameObject().AddDummyTag("Crate"); 
                cell.crateCount++; 
                break;
            case 'T': 
                cell.isTarget = true; 
                cell.tile = null; 
                cell.targetCount++; 
                break;
            case 'b': 
                cell.tile = new GameObject().AddDummyTag("Crate"); 
                cell.isTarget = true; 
                cell.crateCount++; 
                cell.targetCount++; 
                break;
            case 'p': 
                cell.tile = new GameObject().AddDummyTag("Player"); 
                cell.isTarget = true; 
                cell.playerDetected = true; 
                cell.targetCount++; 
                break;
            case 'H': 
                cell.isHole = true; 
                cell.isPassable = false; 
                break;
            default: 
                Debug.LogWarning($"Unknown symbol '{symbol}' at {x},{y}");
                break;
        }
        return cell;
    }
    
    private static void ValidateLevelData(LevelData data) {
        if(!data.playerDetected || data.targetCount < data.crateCount || 
           data.targetCount <= 0 || data.crateCount <= 0) {
            Debug.LogError("Level validation failed - check ImportantInfo.txt");
        }
    }
}

// Data container
[System.Serializable]
public class LevelData {
    public Cell[,] grid;
    public int width, height;
    public bool playerDetected;
    public int targetCount, crateCount;
}
