using System.Collections;
using JetBrains.Annotations;
using UnityEngine;

public class EntranceBehaviour : MonoBehaviour {
  [field: SerializeField]
  [UsedImplicitly]
  private float FadeDuration { get; set; } = 0.5f;

  [field: SerializeField]
  [UsedImplicitly]
  private AnimationCurve FadeCurve { get; set; } = AnimationCurve.EaseInOut(0, 1, 1, 0);

  private GameSession _gameSession;

  private bool _isRemoved;

  [UsedImplicitly]
  private void Awake() {
    _gameSession = FindAnyObjectByType<GameSession>();
  }

  [UsedImplicitly]
  private void Update() {
    if (_gameSession == null || _isRemoved) return;

    var pos = transform.position.WorldToGrid();
    if (pos != _gameSession.CurrentState.PlayerPos) {
      RemoveEntrance();
    }
  }

  private void RemoveEntrance() {
    _isRemoved = true;
    StartCoroutine(FadeOutCoroutine());
  }

  private IEnumerator FadeOutCoroutine() {
    // Gather all renderers (in case the entrance has multiple parts)
    var renderers = GetComponentsInChildren<Renderer>();
    float elapsed = 0f;

    while (elapsed < FadeDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / FadeDuration;
      float alpha = FadeCurve.Evaluate(t);

      foreach (var r in renderers) {
        foreach (var mat in r.materials) {
          // Ensure material rendering mode supports transparency (see Setup Note below)
          if (mat.HasProperty("_Color")) {
            Color c = mat.color;
            c.a = alpha;
            mat.color = c;
          }
        }
      }

      yield return null;
    }

    // Optional: Disable visual but keep object if needed, or Destroy
    gameObject.SetActive(false);
  }
}
