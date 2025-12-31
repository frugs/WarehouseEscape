using System.Collections.Generic;

public static class GeneratorTemplates {
  // Templates from sokoban_template.txt
  // 0 = Wall/Empty, 1 = Floor, 2 = Floor
  public static readonly IList<int[,]> Templates = new List<int[,]>
  {
        // Template 0
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 1
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 2
        new int[,] {
            { 0, 0, 0, 1, 1 },
            { 0, 1, 1, 1, 1 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 3
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 4
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 5
        new int[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 6
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 7
        new int[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 1, 0, 0 },
        },
        // Template 8
        new int[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 1, 0, 0 },
        },
        // Template 9
        new int[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 10
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 11
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 12
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 13
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 1, 1, 0, 0, 0 },
        },
        // Template 14
        new int[,] {
            { 0, 1, 0, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 0, 1, 0 },
        },
        // Template 15
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
        },
        // Template 16
        new int[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 0, 0 },
        }
    };

}
