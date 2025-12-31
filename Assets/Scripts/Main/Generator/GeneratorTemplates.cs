using System.Collections.Generic;

public static class GeneratorTemplates {
  // Templates from sokoban_template.txt
  // 0 = Wall/Empty, 1 = Floor, 2 = Floor
  public static readonly IList<int[,]> Templates = new List<int[,]> {
        // @formatter:off
        // Template 0
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 1
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 2
        new[,] {
            { 0, 0, 0, 1, 1 },
            { 0, 1, 1, 1, 1 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 3
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 4
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 5
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 6
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 7
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 0, 0, 1, 0, 0 },
        },
        // Template 8
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 1, 0, 0 },
        },
        // Template 9
        new[,] {
            { 0, 0, 1, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 10
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 11
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 1 },
            { 0, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 0, 0, 0, 0 },
        },
        // Template 12
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 0, 0, 0, 0 },
        },
        // Template 13
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2 -> 1
            { 1, 1, 1, 1, 0 },
            { 1, 1, 0, 0, 0 },
        },
        // Template 14
        new[,] {
            { 0, 1, 0, 1, 0 },
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 0, 1, 0 },
        },
        // Template 15
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 1, 0 },
        },
        // Template 16
        new[,] {
            { 0, 0, 0, 0, 0 },
            { 0, 1, 1, 1, 0 }, // Converted 2s -> 1
            { 1, 1, 1, 1, 1 }, // Converted 2 -> 1
            { 0, 1, 1, 1, 0 },
            { 0, 1, 1, 0, 0 },
        }
    // @formatter:on
  };
}
