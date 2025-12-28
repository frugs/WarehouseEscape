using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class PlayerController : MonoBehaviour {
  private GridManager gridManager;

  // State flags
  public bool IsBusy { get; private set; } // Processing a move/animation

  // Input & Movement Queues
  private Vector2Int? moveBuffer; // Single buffered keyboard input
  private List<Vector2Int> pathQueue; // List of steps for mouse pathing

  [UsedImplicitly]
  private void Awake() {
    gridManager = FindAnyObjectByType<GridManager>();
    pathQueue = new List<Vector2Int>();
  }

  [UsedImplicitly]
  private void Update() {
    HandleInput();
    ProcessMovement();
  }

  private void HandleInput() {
    // --- 1. KEYBOARD INPUT (Priority: High) ---
    // Pressing a key cancels any active pathfinding immediately
    int x = 0;
    int y = 0;

    // Prioritize Horizontal to avoid diagonal confusion
    if (Input.GetButtonDown("Horizontal")) {
      x = (int)Mathf.Sign(Input.GetAxisRaw("Horizontal"));
    } else if (Input.GetButtonDown("Vertical")) {
      y = (int)Mathf.Sign(Input.GetAxisRaw("Vertical"));
    }

    if (x != 0 || y != 0) {
      // User took control: Clear the path
      pathQueue.Clear();

      // Buffer the input:
      // If busy: we store it for the next available frame.
      // If not busy: we store it and consume it immediately in ProcessMovement.
      moveBuffer = new Vector2Int(x, y);
      return;
    }

    // --- 2. MOUSE INPUT (Priority: Low) ---
    // Only process mouse if no keyboard input this frame and user isn't holding a key
    if (Input.GetMouseButtonDown(0)) {
      if (Camera.main == null) return;

      Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
      if (Physics.Raycast(ray, out RaycastHit hit)) {
        // Clear existing buffer/path
        moveBuffer = null;
        pathQueue.Clear();

        // Calculate new path
        Cell startCell = gridManager.GetCellAtWorldPos(transform.position);
        Cell targetCell = gridManager.GetCellAtWorldPos(hit.point);

        if (startCell != null && targetCell != null) {
          // Note: Ensure GridManager.GetPath is public and implemented as discussed
          var newPath = gridManager.GetPath(new Vector2Int(startCell.x, startCell.y),
            new Vector2Int(targetCell.x, targetCell.y));
          if (newPath != null) {
            pathQueue = newPath;
          }
        }
      }
    }
  }

  private void ProcessMovement() {
    // If busy, we can't start a new move yet.
    // We just wait for the coroutine to finish and flip IsBusy back to false.
    if (IsBusy) return;

    Vector2Int moveDir = Vector2Int.zero;

    // 1. Check Keyboard Buffer first (Manual Control)
    if (moveBuffer.HasValue) {
      moveDir = moveBuffer.Value;
      moveBuffer = null;
      pathQueue.Clear(); // Manual move cancels remaining path
    }
    // 2. Check Auto-Path Queue (Mouse Control)
    else if (pathQueue.Count > 0) {
      Vector2Int nextPos = pathQueue[0];
      Vector2Int currentPos = GetPlayerGridPos();

      // Convert target position to direction
      moveDir = nextPos - currentPos;

      // Remove the step we are about to take
      pathQueue.RemoveAt(0);

      // Safety check: if path implies a jump or diagonal (invalid), abort
      if (moveDir.sqrMagnitude != 1) {
        pathQueue.Clear();
        return;
      }
    }

    // 3. Execute
    if (moveDir != Vector2Int.zero) {
      StartCoroutine(AttemptMove(moveDir));
    }
  }

  private IEnumerator AttemptMove(Vector2Int direction) {
    IsBusy = true; // Lock input immediately

    Vector2Int currentPos = GetPlayerGridPos();
    Vector2Int targetPos = currentPos + direction;
    Cell targetCell = gridManager.GetCell(targetPos.x, targetPos.y);

    // --- 1. VALIDATION ---\n        // If invalid, unlock and exit
    if (targetCell == null || !targetCell.PlayerCanWalk) {
      IsBusy = false;
      pathQueue.Clear(); // Abort path if we hit a wall unexpectedly
      yield break;
    }

    SokobanMove move = null;

    // Case A: Player Move
    if (targetCell.occupant == Occupant.Empty) {
      move = SokobanMove.PlayerMove(currentPos, targetPos);
    }
    // Case B: Crate Push
    else if (targetCell.occupant == Occupant.Crate) {
      // Pushing a crate cancels the mouse path (auto-pathing shouldn't push crates implicitly)
      pathQueue.Clear();

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
      Cell finalCell = gridManager.GetCell(move.crateTo.x, move.crateTo.y);

      // FIX: Only play fall animation if the crate "disappeared" (became the floor).
      // If occupant is Crate, it means it's sitting on TOP of a filled hole/floor.
      bool fellInHole = finalCell.terrain == TerrainType.FilledHole &&
                        finalCell.occupant == Occupant.Empty;

      activeAnimations.Add(fellInHole
        ? StartCoroutine(gridManager.AnimateCrateFall(crateObj, move.crateTo))
        : StartCoroutine(gridManager.AnimateTransform(crateObj, move.crateTo)));
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
    for (int x = 0; x < gridManager.grid.GetLength(0); x++) {
      for (int y = 0; y < gridManager.grid.GetLength(1); y++) {
        if (gridManager.grid[x, y].occupant == Occupant.Player)
          return new Vector2Int(x, y);
      }
    }

    return Vector2Int.zero;
  }
}
