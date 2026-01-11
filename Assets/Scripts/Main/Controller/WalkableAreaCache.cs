using System;
using UnityEngine;

public class WalkableAreaCache : IDisposable {
  public readonly struct FloodFillScannerAdapter : IGridGraph {
    private readonly SokobanState _state;

    public int Width => _state.GridWidth;
    public int Height => _state.GridHeight;

    public FloodFillScannerAdapter(SokobanState state) {
      _state = state;
    }

    public bool IsValid(int x, int y) {
      return _state.CanPlayerWalk(x, y);
    }
  }

  private FloodFillScanner _floodFillScanner = new();
  private GameSession _gameSession;

  public WalkableAreaCache(GameSession gameSession) {
    _gameSession = gameSession;

    _floodFillScanner.Scan(
        new FloodFillScannerAdapter(gameSession.CurrentState),
        gameSession.CurrentState.PlayerPos);

    gameSession.StateChanged += OnStateChanged;
  }

  public bool IsReachable(Vector2Int pos) => _floodFillScanner.IsReached(pos);

  public void Dispose() {
    _gameSession.StateChanged -= OnStateChanged;
  }

  private void OnStateChanged() {
    _floodFillScanner.Scan(
        new FloodFillScannerAdapter(_gameSession.CurrentState),
        _gameSession.CurrentState.PlayerPos);
  }
}
