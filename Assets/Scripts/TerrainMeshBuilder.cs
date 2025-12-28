using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class TerrainMeshBuilder : MonoBehaviour
{
    [Header("Materials")]
    [SerializeField] private Material floorMaterial, wallMaterial, holeMaterial;

    private Transform levelParent, wallsParent, holesParent;
    private int gridHeight;

    public void BuildTerrain(Cell[,] grid, int gridWidth, int gridHeight)
    {
        this.gridHeight = gridHeight;
        SetupHierarchy();

        CreateFloor(grid, gridWidth, gridHeight);           // Y = 0
        CreateWalls(grid, gridWidth, gridHeight);           // Y = 1+
        CreateHoles(grid, gridWidth, gridHeight);           // Y = -0.5
    }

    public void ClearPreviousLevel()
    {
        if (levelParent) DestroyImmediate(levelParent.gameObject);
    }

    // ========== CORE MESH GENERATION ==========

    private void CreateFloor(Cell[,] grid, int gridWidth, int gridHeight)
    {
        var floorPositions = new List<Vector2Int>();
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                // Floor and filled holes should both render as floor
                if (grid[x, y].terrain == TerrainType.Floor ||
                    grid[x, y].terrain == TerrainType.FilledHole)
                {
                    floorPositions.Add(new Vector2Int(x, y));
                }
            }
        }
        if (floorPositions.Count > 0)
        {
            var floorMesh = GenerateQuadMesh(floorPositions, -0.5f);
            floorMesh.transform.parent = levelParent;
            floorMesh.name = "Floor";
        }
    }

    private void CreateWalls(Cell[,] grid, int gridWidth, int gridHeight)
    {
        bool[,] visited = new bool[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y].terrain == TerrainType.Wall && !visited[x, y])
                {
                    var wallCluster = new List<Vector2Int>();
                    FloodFillWalls(x, y, grid, visited, wallCluster);
                    var wallMesh = GenerateQuadMesh(wallCluster, 1f, wallMaterial);
                    wallMesh.transform.parent = wallsParent;
                    wallMesh.name = "WallsCluster";
                }
            }
        }
    }

    private void CreateHoles(Cell[,] grid, int gridWidth, int gridHeight)
    {
        bool[,] visited = new bool[gridWidth, gridHeight];
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                if (grid[x, y].terrain == TerrainType.Hole && !visited[x, y])
                {
                    var holeCluster = new List<Vector2Int>();
                    FloodFillHoles(x, y, grid, visited, holeCluster);
                    var holeMesh = GenerateQuadMesh(holeCluster, -1.5f, holeMaterial);
                    holeMesh.transform.parent = holesParent;
                    holeMesh.name = "HolesCluster";
                }
            }
        }
    }

    // ========== HELPERS ==========

    private Vector3 GridToWorld(int gridX, int gridY, float yHeight)
    {
        // Simple mapping: Grid Y = World Z
        return new Vector3(gridX + 0.5f, yHeight, gridY + 0.5f);
    }

    private GameObject GenerateQuadMesh(List<Vector2Int> positions, float yHeight, Material mat = null)
    {
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        var uvs = new List<Vector2>();

        foreach (var pos in positions)
        {
            var center = GridToWorld(pos.x, pos.y, yHeight);
            int vertIndex = vertices.Count;

            // Quad on XZ plane
            vertices.AddRange(new[]
            {
                center + new Vector3(-0.5f, 0, -0.5f),
                center + new Vector3( 0.5f, 0, -0.5f),
                center + new Vector3( 0.5f, 0,  0.5f),
                center + new Vector3(-0.5f, 0,  0.5f)
            });

            triangles.AddRange(new[]
            {
                vertIndex, vertIndex + 2, vertIndex + 1,
                vertIndex, vertIndex + 3, vertIndex + 2
            });

            uvs.AddRange(new[]
            {
                Vector2.zero,
                Vector2.right,
                new Vector2(1, 1),
                Vector2.up
            });
        }

        var mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uv = uvs.ToArray()
        };
        mesh.RecalculateNormals();

        var go = new GameObject($"Mesh_{positions.Count}");
        go.AddComponent<MeshFilter>().mesh = mesh;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.material = mat ?? floorMaterial;
        go.AddComponent<MeshCollider>();

        return go;
    }

    private void FloodFillWalls(int x, int y, Cell[,] grid, bool[,] visited, List<Vector2Int> cluster)
    {
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(x, y));
        visited[x, y] = true;

        while (stack.Count > 0)
        {
            var pos = stack.Pop();
            cluster.Add(pos);

            foreach (var dir in new[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            })
            {
                int nx = pos.x + dir.x;
                int ny = pos.y + dir.y;

                if (nx >= 0 && nx < grid.GetLength(0) &&
                    ny >= 0 && ny < gridHeight &&
                    !visited[nx, ny] &&
                    grid[nx, ny].terrain == TerrainType.Wall)
                {
                    visited[nx, ny] = true;
                    stack.Push(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void FloodFillHoles(int x, int y, Cell[,] grid, bool[,] visited, List<Vector2Int> cluster)
    {
        var stack = new Stack<Vector2Int>();
        stack.Push(new Vector2Int(x, y));
        visited[x, y] = true;

        while (stack.Count > 0)
        {
            var pos = stack.Pop();
            cluster.Add(pos);

            foreach (var dir in new[]
            {
                new Vector2Int(0, 1),
                new Vector2Int(0, -1),
                new Vector2Int(1, 0),
                new Vector2Int(-1, 0)
            })
            {
                int nx = pos.x + dir.x;
                int ny = pos.y + dir.y;

                if (nx >= 0 && nx < grid.GetLength(0) &&
                    ny >= 0 && ny < gridHeight &&
                    !visited[nx, ny] &&
                    grid[nx, ny].terrain == TerrainType.Hole)
                {
                    visited[nx, ny] = true;
                    stack.Push(new Vector2Int(nx, ny));
                }
            }
        }
    }

    private void SetupHierarchy()
    {
        ClearPreviousLevel();
        levelParent = (new GameObject("LevelTerrain")).transform;
        wallsParent = new GameObject("Walls").transform;
        wallsParent.parent = levelParent;
        holesParent = new GameObject("Holes").transform;
        holesParent.parent = levelParent;
    }
}
