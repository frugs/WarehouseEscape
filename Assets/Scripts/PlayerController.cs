using System;
using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
    private GridManager gridManager;
    private UndoRedoManager undoRedoManager;

    // Track movement state to prevent inputs while auto-moving
    public bool isMoving = false;

    private bool isProcessingInput = false;

    void Awake()
    {
        gridManager = FindObjectOfType<GridManager>();
        undoRedoManager = FindObjectOfType<UndoRedoManager>();
    }

    // Helper for legacy access if needed
    public Vector3 PlayerPos()
    {
        return transform.position;
    }

    void Update()
    {
        // Don't accept input if player is auto-moving (e.g. via mouse path)
        if (isMoving) return;

        // ================= INPUT HANDLING =================

        if (!isProcessingInput)
        {
            // Horizontal: D/RightArrow (+1) -> Grid X+1
            //             A/LeftArrow (-1)  -> Grid X-1
            if (Input.GetButtonDown("Horizontal"))
            {
                int dir = (int)Mathf.Sign(Input.GetAxisRaw("Horizontal"));
                StartInputActionCoroutine(AttemptMove(new Vector2Int(dir, 0)));
            }
            // Vertical: W/UpArrow (+1)   -> Grid Y+1 (North)
            //           S/DownArrow (-1) -> Grid Y-1 (South)
            else if (Input.GetButtonDown("Vertical"))
            {
                int dir = (int)Mathf.Sign(Input.GetAxisRaw("Vertical"));
                StartInputActionCoroutine(AttemptMove(new Vector2Int(0, dir)));
            }
        }

        // ================= UTILITY KEYS =================

        // Reset
        if (Input.GetKeyDown(KeyCode.R))
        {
            StopAllCoroutines();
            gridManager.StopAllCoroutines();
            gridManager.ResetLevel();
            isProcessingInput = false;
        }

        // Undo
        if (Input.GetKeyDown(KeyCode.Z))
        {
            if (undoRedoManager) undoRedoManager.UndoMove();
        }
        // Redo (X or Y key depending on preference/keyboard layout)
        else if (Input.GetKeyDown(KeyCode.X) || Input.GetKeyDown(KeyCode.Y))
        {
            if (undoRedoManager) undoRedoManager.RedoMove();
        }
    }

    private void StartInputActionCoroutine(IEnumerator coroutine)
    {
        isProcessingInput = true;

        StartCoroutine(InnerCoroutine());
        return;

        IEnumerator InnerCoroutine()
        {
            yield return coroutine;
            isProcessingInput = false;
        }
    }

    private IEnumerator AttemptMove(Vector2Int direction)
    {
        // 1. Identify where we are right now
        Cell currentCell = gridManager.GetCellAtWorldPos(transform.position);
        if (currentCell == null) yield break;

        Vector2Int playerPos = new Vector2Int(currentCell.x, currentCell.y);
        Vector2Int targetPos = playerPos + direction;

        // 2. Identify target cell
        Cell targetCell = gridManager.GetCell(targetPos.x, targetPos.y);

        // Bounds/Wall check
        if (targetCell == null || targetCell.terrain == TerrainType.Wall) yield break;

        // 3. LOGIC: Move to Empty Space
        if (targetCell.occupant == Occupant.Empty && targetCell.PlayerCanWalk)
        {
            // Perform Move
            yield return gridManager.AnimateMoveEntity(playerPos, targetPos);

            // Record Undo (Player Move)
            if (undoRedoManager)
            {
                undoRedoManager.AddAction(
                    gameObject,
                    gridManager.GridToWorld(playerPos.x, playerPos.y),
                    gridManager.GridToWorld(targetPos.x, targetPos.y)
                );
            }
        }
        // 4. LOGIC: Push Crate
        else if (targetCell.occupant == Occupant.Crate)
        {
            Vector2Int crateTargetPos = targetPos + direction;
            Cell crateTargetCell = gridManager.GetCell(crateTargetPos.x, crateTargetPos.y);

            // Check if crate can be pushed:
            // 1. Target cell exists
            // 2. Target cell is Empty (no other crate/player)
            // 3. Target cell is Valid Floor/Hole
            if (crateTargetCell != null &&
                crateTargetCell.occupant == Occupant.Empty &&
                crateTargetCell.CanReceiveCrate)
            {
                // Calculate World Positions for Undo/Deadlock Logic
                Vector3 playerStartPos = gridManager.GridToWorld(playerPos.x, playerPos.y);
                Vector3 playerEndPos = gridManager.GridToWorld(targetPos.x, targetPos.y);
                Vector3 crateStartPos = playerEndPos;
                Vector3 crateEndPos = gridManager.GridToWorld(crateTargetPos.x, crateTargetPos.y);

                // A. Move Crate First
                yield return gridManager.AnimateMoveEntity(targetPos, crateTargetPos);

                // B. Move Player Second
                yield return gridManager.AnimateMoveEntity(playerPos, targetPos);

                // Check Win Condition
                gridManager.CheckWinCondition();

                // Deadlock Checks (Optional - Purely for player feedback)
                if (!crateTargetCell.isTarget)
                {
                    if (CheckTwobyTwo(crateEndPos)) Debug.Log("Tip: 2x2 Deadlock detected");
                    else if (InvalidBox(crateEndPos, new Vector3(direction.x, 0, direction.y)))
                        Debug.Log("Tip: Deadlock detected");
                }

                // Record Undo (Push Move)
                if (undoRedoManager)
                {
                    // Note: We pass 'null' for the crate object reference here because
                    // GridManager tracks instances internally now. 
                    // Ideally UndoRedoManager should look up objects by position or ID.
                    undoRedoManager.AddAction(
                        gameObject,
                        playerStartPos,
                        playerEndPos,
                        null,
                        crateStartPos,
                        crateEndPos
                    );
                }
            }
        }
    }

    // ================== DEADLOCK DETECTION ==================

    public bool InvalidBox(Vector3 targetPos, Vector3 direction)
    {
        if (IsInCorner(targetPos)) return true;
        if (IsInDeadEnd(targetPos, direction)) return true;
        if (CheckFourCrates(targetPos)) return true;
        if (CheckTwobyTwo(targetPos)) return true;
        return false;
    }

    private bool IsInCorner(Vector3 targetPos)
    {
        // Check surrounding walls (Forward/Back/Left/Right)
        bool up = CheckforWall(targetPos + Vector3.forward);
        bool down = CheckforWall(targetPos + Vector3.back);
        bool right = CheckforWall(targetPos + Vector3.right);
        bool left = CheckforWall(targetPos + Vector3.left);

        // A corner is formed by two adjacent walls
        if (up && right) return true;
        if (up && left) return true;
        if (down && right) return true;
        if (down && left) return true;

        return false;
    }

    private bool IsInDeadEnd(Vector3 targetPos, Vector3 direction)
    {
        // If moving horizontally (X-axis), check Vertical walls (Z-axis)
        if (direction.x != 0) return CheckWallDeadEnd(targetPos, targetPos + direction, Vector3.forward);

        // If moving vertically (Z-axis), check Horizontal walls (X-axis)
        if (direction.z != 0) return CheckWallDeadEnd(targetPos, targetPos + direction, Vector3.right);

        return false;
    }

    private bool CheckWallDeadEnd(Vector3 blockPos, Vector3 wallPos, Vector3 axis)
    {
        // Check both directions along the perpendicular axis
        for (int i = -1; i <= 1; i += 2)
        {
            Vector3 checkPos = blockPos;
            Vector3 checkWallPos = wallPos;

            while (true)
            {
                checkPos += axis * i;
                checkWallPos += axis * i;

                // 1. If the wall stops, it's not a dead end (we can escape)
                if (!CheckforWall(checkWallPos)) return false;

                // 2. If we find a target, it's a valid placement (not a dead end)
                if (CheckforTarget(checkPos)) return false;

                // 3. If we hit a corner (another wall), stop checking this side
                if (IsInCorner(checkPos)) break;
            }
        }

        // If both sides are blocked by corners and continuous walls, it's a dead end
        return true;
    }

    private bool CheckFourCrates(Vector3 cratePos)
    {
        // Check 3x3 area for crate clusters
        int[] xOffsets = { -1, -1, 0, 1, 1, 1, 0, -1 };
        int[] zOffsets = { 0, 1, 1, 1, 0, -1, -1, -1 };

        int crateCount = 0;

        for (int i = 0; i < xOffsets.Length; i++)
        {
            Vector3 checkPos = cratePos + new Vector3(xOffsets[i], 0, zOffsets[i]);
            if (CheckforCrate(checkPos)) crateCount++;
            else crateCount = 0;

            if (crateCount >= 3) return true;
        }

        return false;
    }

    private bool CheckTwobyTwo(Vector3 cratePos)
    {
        // Check for 2x2 formations of immovable objects (Walls/Crates on non-targets)

        // Check Left + Forward + ForwardLeft
        if (CheckBlocker(cratePos + Vector3.left) &&
            CheckBlocker(cratePos + Vector3.forward) &&
            CheckBlocker(cratePos + Vector3.left + Vector3.forward)) return true;

        // Check Forward + Right + ForwardRight
        if (CheckBlocker(cratePos + Vector3.forward) &&
            CheckBlocker(cratePos + Vector3.right) &&
            CheckBlocker(cratePos + Vector3.forward + Vector3.right)) return true;

        // Check Right + Back + BackRight
        if (CheckBlocker(cratePos + Vector3.right) &&
            CheckBlocker(cratePos + Vector3.back) &&
            CheckBlocker(cratePos + Vector3.right + Vector3.back)) return true;

        // Check Back + Left + BackLeft
        if (CheckBlocker(cratePos + Vector3.back) &&
            CheckBlocker(cratePos + Vector3.left) &&
            CheckBlocker(cratePos + Vector3.back + Vector3.left)) return true;

        return false;
    }

    private bool CheckBlocker(Vector3 pos)
    {
        return CheckforWall(pos) || (CheckforCrate(pos) && !CheckforTarget(pos));
    }

    // ================== DATA HELPERS ==================

    private bool CheckforWall(Vector3 targetPos)
    {
        var cell = gridManager.GetCellAtWorldPos(targetPos);
        return cell != null && cell.terrain == TerrainType.Wall;
    }

    private bool CheckforCrate(Vector3 targetPos)
    {
        var cell = gridManager.GetCellAtWorldPos(targetPos);
        return cell != null && cell.occupant == Occupant.Crate;
    }

    private bool CheckforTarget(Vector3 targetPos)
    {
        var cell = gridManager.GetCellAtWorldPos(targetPos);
        return cell != null && cell.isTarget;
    }
}
