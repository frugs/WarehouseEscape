using System.Collections;
using UnityEngine;

public class MoveAnimator : MonoBehaviour {

  [SerializeField]
  private float MoveAnimationDuration = 0.2f;
  [SerializeField]
  private float FallAnimationDuration = 0.15f;

  public IEnumerator AnimateTransform(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    Vector3 startPos = obj.transform.position;
    Vector3 endPos = GridUtils.GridToWorld(targetGridPos.x, targetGridPos.y, 0.5f);
    float elapsed = 0f;

    while (elapsed < MoveAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / MoveAnimationDuration;
      // Quadratic ease-out for smoother feel
      t = t * (2 - t);
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;
  }

  public IEnumerator AnimateCrateFall(GameObject obj, Vector2Int targetGridPos) {
    if (obj == null) yield break;

    // 1. Slide to the hole position
    yield return AnimateTransform(obj, targetGridPos);

    // 2. Sink down
    Vector3 startPos = obj.transform.position;
    Vector3 endPos = startPos + Vector3.down * 1.0f; // Sink depth
    float elapsed = 0f;

    while (elapsed < FallAnimationDuration) {
      elapsed += Time.deltaTime;
      float t = elapsed / FallAnimationDuration;
      obj.transform.position = Vector3.Lerp(startPos, endPos, t);
      yield return null;
    }

    obj.transform.position = endPos;

    // Optional: Rename for debugging
    obj.name = $"FilledHole_{targetGridPos.x}_{targetGridPos.y}";
  }
}
