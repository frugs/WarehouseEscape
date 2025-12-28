using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UndoRedoManager : MonoBehaviour
{
    private Stack<MoveAction> undoStack;
    private Stack<MoveAction> redoStack;
    public GridManager gridManager;

    public void Awake()
    {
        undoStack = new Stack<MoveAction>();
        redoStack = new Stack<MoveAction>();
        gridManager = FindObjectOfType<GridManager>();
    }

    public void AddAction(GameObject player, Vector3 playerOriginalPosition, Vector3 playerTargetPosition, GameObject crate = null, Vector3? crateOriginalPosition = null, Vector3? crateTargetPosition = null)
    {
        MoveAction moveAction = new MoveAction(player, playerOriginalPosition, playerTargetPosition, crate, crateOriginalPosition, crateTargetPosition);
        undoStack.Push(moveAction);

        // Clear redo stack when a new move action is added
        redoStack.Clear();
    }

    public void UndoMove()
    {
        if (undoStack.Count > 0)
        {
            MoveAction moveAction = undoStack.Pop();
            moveAction.Undo(gridManager);
            redoStack.Push(moveAction);
        }
        else
        {
            Debug.Log("No Undo Action Available");
        }
    }

    public void RedoMove()
    {
        if (redoStack.Count > 0)
        {
            MoveAction moveAction = redoStack.Pop();
            moveAction.Redo(gridManager);
            undoStack.Push(moveAction);
        }
        else
        {
            Debug.Log("No Redo Action Available");
        }
    }
}

// Inner class or separate file for MoveAction
public class MoveAction
{
    public GameObject player;
    public Vector3 playerOriginalPosition;
    public Vector3 playerTargetPosition;
    public GameObject crate;
    public Vector3? crateOriginalPosition;
    public Vector3? crateTargetPosition;

    public MoveAction(GameObject player, Vector3 playerOriginalPosition, Vector3 playerTargetPosition, GameObject crate, Vector3? crateOriginalPosition, Vector3? crateTargetPosition)
    {
        this.player = player;
        this.playerOriginalPosition = playerOriginalPosition;
        this.playerTargetPosition = playerTargetPosition;
        this.crate = crate;
        this.crateOriginalPosition = crateOriginalPosition;
        this.crateTargetPosition = crateTargetPosition;
    }

    // ================= UNDO LOGIC =================

    public void Undo(GridManager gridManager)
    {
        // 1. Undo Crate Move (if any)
        if (crate != null && crateTargetPosition.HasValue && crateOriginalPosition.HasValue)
        {
            // Reverse: Move from Target -> Original
            MoveEntityByWorldPos(gridManager, crateTargetPosition.Value, crateOriginalPosition.Value);
        }

        // 2. Undo Player Move
        // Reverse: Move from Target -> Original
        MoveEntityByWorldPos(gridManager, playerTargetPosition, playerOriginalPosition);
    }

    // ================= REDO LOGIC =================

    public void Redo(GridManager gridManager)
    {
        // 1. Redo Player Move
        // Forward: Move from Original -> Target
        MoveEntityByWorldPos(gridManager, playerOriginalPosition, playerTargetPosition);

        // 2. Redo Crate Move (if any)
        if (crate != null && crateTargetPosition.HasValue && crateOriginalPosition.HasValue)
        {
            // Forward: Move from Original -> Target
            MoveEntityByWorldPos(gridManager, crateOriginalPosition.Value, crateTargetPosition.Value);
        }
    }

    // Helper to bridge World Positions -> GridManager.MoveEntity
    private void MoveEntityByWorldPos(GridManager gridManager, Vector3 fromPos, Vector3 toPos)
    {
        Cell fromCell = gridManager.GetCellAtWorldPos(fromPos);
        Cell toCell = gridManager.GetCellAtWorldPos(toPos);

        if (fromCell != null && toCell != null)
        {
            gridManager.MoveEntity(new Vector2Int(fromCell.x, fromCell.y), new Vector2Int(toCell.x, toCell.y));
        }
        else
        {
            Debug.LogError($"Undo/Redo Error: Could not resolve cells for positions {fromPos} -> {toPos}");
        }
    }
}
