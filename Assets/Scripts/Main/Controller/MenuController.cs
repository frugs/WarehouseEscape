using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Handles menu-related game controls (level progression and reset).
/// Coordinates with GameSession to manage level transitions.
/// </summary>
public class MenuController : MonoBehaviour {
  [Header("Dependencies")] [SerializeField]
  private GameSession GameSession;

  [SerializeField] private MoveScheduler MoveScheduler;

  [UsedImplicitly]
  private void Awake() {
    if (GameSession == null) GameSession = GetComponent<GameSession>();
    if (MoveScheduler == null) MoveScheduler = GetComponent<MoveScheduler>();
  }

  /// <summary>
  /// Progresses to the next level.
  /// Clears any pending move operations before loading the next level.
  /// </summary>
  [UsedImplicitly]
  public void NextLevel() {
    if (GameSession == null) {
      Debug.LogError("[MenuController] GameSession is not assigned!");
      return;
    }

    if (MoveScheduler == null) {
      Debug.LogError("[MenuController] MoveScheduler is not assigned!");
      return;
    }

    MoveScheduler.ClearInterrupt();
    GameSession.LoadNextLevel();
  }

  /// <summary>
  /// Resets the current level to its initial state.
  /// Clears any pending move operations before reloading the level.
  /// </summary>
  [UsedImplicitly]
  public void ResetLevel() {
    if (GameSession == null) {
      Debug.LogError("[MenuController] GameSession is not assigned!");
      return;
    }

    if (MoveScheduler == null) {
      Debug.LogError("[MenuController] MoveScheduler is not assigned!");
      return;
    }

    MoveScheduler.ClearInterrupt();
    GameSession.ResetLevel();
  }

  [UsedImplicitly]
  public void GenerateLevel() {
    MoveScheduler.ClearInterrupt();
    GameSession.LoadGeneratedLevel();
  }
}
