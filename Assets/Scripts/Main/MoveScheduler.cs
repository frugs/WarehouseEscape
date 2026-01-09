using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

public class MoveScheduler : MonoBehaviour {
  [field: Header("References")]
  [field: SerializeField]
  [UsedImplicitly]
  private GameSession GameSession { get; set; }

  [field: SerializeField]
  [UsedImplicitly]
  private MoveAnimator MoveAnimator { get; set; }


  [field: SerializeField]
  [UsedImplicitly]
  private UndoBehaviour UndoBehaviour { get; set; }

  // The unified queue
  private readonly Queue<SokobanMove> MoveQueue = new Queue<SokobanMove>();

  // Configurable delay for solution playback
  public float StepDelay { get; set; } = 0f;

  private UndoManager _undoManager;
  private Coroutine _currentProcess;

  [UsedImplicitly]
  private void Awake() {
    if (GameSession == null) GameSession = GetComponent<GameSession>();
    if (MoveAnimator == null) MoveAnimator = GetComponent<MoveAnimator>();
    if (UndoBehaviour == null) UndoBehaviour = GetComponent<UndoBehaviour>();

    _undoManager = UndoBehaviour.UndoManager;
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

  public void ClearInterrupt() {
    StopAllCoroutines();
    _currentProcess = null;
    Clear();
  }

  private void TryProcessQueue() {
    if (_currentProcess == null && MoveQueue.Count > 0) {
      _currentProcess = StartCoroutine(ProcessQueueRoutine());
    }
  }

  private IEnumerator ProcessQueueRoutine() {
    while (MoveQueue.Count > 0) {
      var move = MoveQueue.Dequeue();

      var beforeState = GameSession.CurrentState;
      GameSession.ApplyMoveToCurrentState(move, out GameObject playerObj, out GameObject crateObj);
      _undoManager?.RecordMove(beforeState, move);

      // 2. Animations (The "Over Time" part)
      var anims = new List<Coroutine>();

      if (playerObj != null)
        anims.Add(StartCoroutine(MoveAnimator.AnimateTransform(playerObj, move.playerTo)));

      if (move.type == MoveType.CratePush && crateObj != null) {
        // Handle the "fell in hole" logic here centrally
        bool fellInHole = GameSession.CurrentState.IsFilledHoleAt(move.crateTo.x, move.crateTo.y)
                          && !GameSession.CurrentState.IsCrateAt(move.crateTo.x, move.crateTo.y);

        anims.Add(
            fellInHole
                ? StartCoroutine(MoveAnimator.AnimateCrateFall(crateObj, move.crateTo))
                : StartCoroutine(MoveAnimator.AnimateTransform(crateObj, move.crateTo)));
      }

      // Wait for animations
      foreach (var c in anims) yield return c;

      // Optional delay (useful for solution playback)
      if (StepDelay > 0) yield return new WaitForSeconds(StepDelay);

      // Check win after every move
      GameSession.CheckWinCondition();
    }

    _currentProcess = null;
  }
}
