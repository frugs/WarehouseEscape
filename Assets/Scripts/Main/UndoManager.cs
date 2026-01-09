using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Manages undo history for Sokoban moves.
/// Records state snapshots before each move to enable rewinding.
///
/// Avoids cyclic dependency: PlayerController -> UndoManager -> GameSession
/// (GameSession doesn't reference UndoManager; it just fires callbacks)
/// </summary>
public class UndoManager {
  // Stack of (before-state, move) pairs
  private readonly Stack<(SokobanState beforeState, SokobanMove move)> _history =
      new Stack<(SokobanState, SokobanMove)>();

  /// <summary>
  /// Records a move before it's applied to the state.
  /// Call this BEFORE applying the move to _currentState.
  /// </summary>
  /// <param name="beforeState">The state before the move was applied</param>
  /// <param name="move">The move that was executed</param>
  public void RecordMove(SokobanState beforeState, SokobanMove move) {
    _history.Push((beforeState, move));
  }

  /// <summary>
  /// Can an undo operation be performed?
  /// </summary>
  public bool CanUndo => _history.Count > 0;

  /// <summary>
  /// Undoes the last move.
  /// Restores to the before-state and returns info for animation.
  /// </summary>
  /// <param name="currentState">The current state (ignored, kept for API clarity)</param>
  /// <param name="moveToReverseAnimate">The move that was undone (for reverse animation)</param>
  /// <returns>The state to restore to (before the undone move)</returns>
  public SokobanState Undo(SokobanState currentState, out SokobanMove moveToReverseAnimate) {
    moveToReverseAnimate = default;

    if (!CanUndo) {
      Debug.LogWarning("[UndoManager] No moves to undo");
      return currentState;
    }

    var (beforeState, move) = _history.Pop();
    moveToReverseAnimate = move;

    return beforeState;
  }

  /// <summary>
  /// Clears the undo history.
  /// Called by GameSession when loading/resetting a level.
  /// </summary>
  public void Clear() {
    _history.Clear();
  }
}
