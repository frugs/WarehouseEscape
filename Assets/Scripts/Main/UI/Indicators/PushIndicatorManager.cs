using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

/// <summary>
/// Manages the collection of push indicators and hover detection.
/// Works in tandem with PlayerController to handle crate push interaction.
/// </summary>
public class PushIndicatorManager : MonoBehaviour {
  private class IndicatorData {
    public Vector2Int Direction { get; set; }
    public GameObject Visual { get; set; }
    public PushIndicatorAnimator Animator { get; set; }
  }

  private const string PUSH_INDICATOR_LAYER = "PushIndicator";

  private int _pushIndicatorLayerMask;

  private List<IndicatorData> _indicators = new List<IndicatorData>();
  private IndicatorData _previouslyHoveredIndicator;


  [field: SerializeField]
  [UsedImplicitly]
  private GameObject PushIndicatorPrefab { get; set; }

  [UsedImplicitly]
  private void Awake() {
    _pushIndicatorLayerMask = LayerMask.GetMask(PUSH_INDICATOR_LAYER);
  }

  /// <summary>
  /// Create a push indicator at the given position and direction.
  /// </summary>
  public void CreateIndicator(Vector2Int direction, Vector2Int cratePos) {
    var indicator = Instantiate(PushIndicatorPrefab);

    indicator.gameObject.name = $"PushIndicator_{direction}";
    indicator.layer = LayerMask.NameToLayer(PUSH_INDICATOR_LAYER);

    var basePosition = (cratePos + direction).GridToWorld(0.5f);
    indicator.transform.position = basePosition;
    indicator.transform.LookAt((cratePos + direction * 2).GridToWorld(0.5f));

    var animator = indicator.GetComponent<PushIndicatorAnimator>();
    if (animator != null) {
      animator.Initialize(basePosition, direction);
    }

    var indicatorData = new IndicatorData {
        Direction = direction, Visual = indicator, Animator = animator
    };

    _indicators.Add(indicatorData);
  }

  /// <summary>
  /// Update hover state based on mouse position.
  /// Single raycast against PushIndicator layer only.
  /// </summary>
  public void UpdateHoverOnMouseMove(Vector2 mousePos) {
    // Clear previous hover state
    if (_previouslyHoveredIndicator != null) {
      _previouslyHoveredIndicator.Animator.StopAnimation();
      _previouslyHoveredIndicator = null;
    }

    if (Camera.main == null) return;

    Ray ray = Camera.main.ScreenPointToRay(new Vector3(mousePos.x, mousePos.y, 0f));

    // Single raycast only against PushIndicator layer
    if (Physics.Raycast(ray, out RaycastHit hit, 100f, _pushIndicatorLayerMask)) {
      GameObject hitObject = hit.collider.gameObject;

      // Find the corresponding indicator
      foreach (var indicator in _indicators) {
        if (indicator.Visual == hitObject) {

          _previouslyHoveredIndicator = indicator;
          indicator.Animator.StartAnimation();
          break;
        }
      }
    }
  }

  /// <summary>
  /// Get the currently hovered indicator direction, if any.
  /// </summary>
  public Vector2Int? GetHoveredDirection() {
    return _previouslyHoveredIndicator?.Direction;
  }

  /// <summary>
  /// Clear all indicators.
  /// </summary>
  public void DismissAll() {
    foreach (var indicator in _indicators) {
      if (indicator.Visual != null) {
        Destroy(indicator.Visual);
      }
    }

    _indicators.Clear();
    _previouslyHoveredIndicator = null;
  }
}
