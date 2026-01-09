using JetBrains.Annotations;
using UnityEngine;

public class UndoBehaviour : MonoBehaviour {
  [field: SerializeField]
  [UsedImplicitly]
  public GameSession GameSession { get; set; }

  public UndoManager UndoManager { get; } = new UndoManager();

  [UsedImplicitly]
  private void Awake() {
    if (GameSession == null) GameSession = GetComponent<GameSession>();

    if (GameSession != null) {
      GameSession.StateReset += OnStateReset;
    }
  }

  [UsedImplicitly]
  private void OnDestroy() {
    UndoManager.Clear();

    if (GameSession != null) {
      GameSession.StateReset -= OnStateReset;
    }
  }

  private void OnStateReset() {
    UndoManager.Clear();
  }
}
