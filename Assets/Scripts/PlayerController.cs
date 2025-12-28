using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour {
  private GridManager gridManager;

  // State flags
  public bool IsBusy { get; private set; } // Processing a move/animation
  public bool IsAutoMoving { get; set; } // Following a mouse path

  private Vector2Int? moveBuffer; // Nullable to track if we have a buffered move

  [UsedImplicitly]
  private void Awake() {
    gridManager = FindAnyObjectByType<GridManager>();
  }

  [UsedImplicitly]
  private void Update() {
    // Block input if busy (animating) or if auto-moving (let the path logic drive)
    if (IsAutoMoving) return;

    // Input Handling
    int x = 0;
    int y = 0;

    if (Input.GetButtonDown("Horizontal")) {
      x = (int)Mathf.Sign(Input.GetAxisRaw("Horizontal"));
    } else if (Input.GetButtonDown("Vertical")) {
      y = (int)Mathf.Sign(Input.GetAxisRaw("Vertical"));
    }

    Vector2Int input = new Vector2Int(x, y);

    // Buffer Logic: If busy, store valid input (overwriting previous)
    if (IsBusy) {
      if (input != Vector2Int.zero) {
        moveBuffer = input;
      }

      return; // Stop here, we can't move yet
    }

    // Execution Logic: If not busy, check buffer first, then current input
    Vector2Int moveDir = Vector2Int.zero;

    if (moveBuffer.HasValue) {
      moveDir = moveBuffer.Value;
      moveBuffer = null; // Consume the buffer
    } else if (input != Vector2Int.zero) {
      moveDir = input;
    }

    // Execute Move
    if (moveDir != Vector2Int.zero) {
      StartCoroutine(AttemptMove(moveDir));
    }
  }

  // Public method for GridManager's auto-pathing to call
  public void ExecuteMove(Vector2Int direction) {
    if (!IsBusy) StartCoroutine(AttemptMove(direction));
  }

  private IEnumerator AttemptMove(Vector2Int direction) {
    IsBusy = true; // Lock input immediately

    Vector2Int currentPos = GetPlayerGridPos();
    Vector2Int targetPos = currentPos + direction;
    Cell targetCell = gridManager.GetCell(targetPos.x, targetPos.y);

    // --- 1. VALIDATION ---
    // If invalid, unlock and exit
    if (targetCell == null || !targetCell.PlayerCanWalk) {
      IsBusy = false;
      yield break;
    }

    SokobanMove move = null;

    // Case A: Player Move
    if (targetCell.occupant == Occupant.Empty) {
      move = SokobanMove.PlayerMove(currentPos, targetPos);
    }
    // Case B: Crate Push
    else if (targetCell.occupant == Occupant.Crate) {
      Vector2Int crateTarget = targetPos + direction;
      Cell cTargetCell = gridManager.GetCell(crateTarget.x, crateTarget.y);

      // Check if crate destination is valid
      if (cTargetCell != null && cTargetCell.CanReceiveCrate) {
        move = SokobanMove.CratePush(currentPos, targetPos, targetPos, crateTarget);
      }
    }

    // If move isn't valid (e.g. pushing crate into wall), exit
    if (move == null) {
      IsBusy = false;
      yield break;
    }

    // --- 2. ATOMIC UPDATE ---
    // This updates the Grid Logic AND the Visual Array pointers instantly.
    // It returns the GameObjects we need to animate.
    gridManager.RegisterMoveUpdates(move, out GameObject playerObj, out GameObject crateObj);

    // --- 3. CONCURRENT ANIMATION ---
    List<Coroutine> activeAnimations = new List<Coroutine>();

    // Queue Player Animation
    if (playerObj != null) {
      activeAnimations.Add(StartCoroutine(gridManager.AnimateTransform(playerObj, move.playerTo)));
    }

    // Queue Crate Animation
    if (move.type == MoveType.CratePush && crateObj != null) {
      // Check if we pushed into a hole (Note: Grid logic is already updated to FilledHole!)
      bool isHole = gridManager.GetCell(move.crateTo.x, move.crateTo.y).terrain ==
                    TerrainType.FilledHole;

      if (isHole)
        activeAnimations.Add(StartCoroutine(gridManager.AnimateCrateFall(crateObj, move.crateTo)));
      else
        activeAnimations.Add(StartCoroutine(gridManager.AnimateTransform(crateObj, move.crateTo)));
    }

    // Wait for ALL animations to finish
    foreach (var anim in activeAnimations) {
      yield return anim;
    }

    // --- 4. FINALIZE ---
    gridManager.CheckWinCondition();
    IsBusy = false; // Unlock input
  }

  private Vector2Int GetPlayerGridPos() {
    // Simple scan is robust enough for small grids.
    // Optimized way: GridManager tracks playerPos vector.
    for (int x = 0; x < gridManager.grid.GetLength(0); x++) {
      for (int y = 0; y < gridManager.grid.GetLength(1); y++) {
        if (gridManager.grid[x, y].occupant == Occupant.Player)
          return new Vector2Int(x, y);
      }
    }

    return Vector2Int.zero;
  }
}
