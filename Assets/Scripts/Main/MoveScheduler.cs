using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class MoveScheduler : MonoBehaviour {
  [SerializeField] private GridManager GridManager;

  // The unified queue
  private readonly Queue<SokobanMove> MoveQueue = new Queue<SokobanMove>();

  // Configurable delay for solution playback
  public float StepDelay { get; set; } = 0f;

  // Unified Busy Check: Busy if animating OR if moves are waiting
  public bool IsBusy => CurrentProcess != null || MoveQueue.Count > 0;

  private Coroutine CurrentProcess;

  [UsedImplicitly]
  private void Awake() {
    GridManager = GetComponent<GridManager>();
  }

  public void Enqueue(SokobanMove move) {
    MoveQueue.Enqueue(move);
    TryProcessQueue();
  }

  public void Enqueue(IEnumerable<SokobanMove> moves) {
    foreach (var move in moves) {
      MoveQueue.Enqueue(move);
    }
    TryProcessQueue();
  }

  public void Clear() {
    MoveQueue.Clear();
    // Note: We deliberately do NOT stop the currently playing animation
    // to avoid visual snapping/glitches. We just clear future steps.
  }

  private void TryProcessQueue() {
    if (CurrentProcess == null && MoveQueue.Count > 0) {
      CurrentProcess = StartCoroutine(ProcessQueueRoutine());
    }
  }

  private IEnumerator ProcessQueueRoutine() {
    while (MoveQueue.Count > 0) {
      var move = MoveQueue.Dequeue();

      // 1. Logic & Visual Pointers (The "Instant" part)
      GridManager.RegisterMoveUpdates(move, out GameObject playerObj, out GameObject crateObj);

      // 2. Animations (The "Over Time" part)
      var anims = new List<Coroutine>();

      if (playerObj != null)
        anims.Add(StartCoroutine(GridManager.AnimateTransform(playerObj, move.playerTo)));

      if (move.type == MoveType.CratePush && crateObj != null) {
        // Handle the "fell in hole" logic here centrally
        bool fellInHole = GridManager.GridState.IsFilledHoleAt(move.crateTo.x, move.crateTo.y)
                          && !GridManager.GridState.IsCrateAt(move.crateTo.x, move.crateTo.y);

        anims.Add(fellInHole
            ? StartCoroutine(GridManager.AnimateCrateFall(crateObj, move.crateTo))
            : StartCoroutine(GridManager.AnimateTransform(crateObj, move.crateTo)));
      }

      // Wait for animations
      foreach (var c in anims) yield return c;

      // Optional delay (useful for solution playback)
      if (StepDelay > 0) yield return new WaitForSeconds(StepDelay);

      // Check win after every move
      GridManager.CheckWinCondition();
    }

    CurrentProcess = null;
  }
}
