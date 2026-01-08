using UnityEngine;

public static class Vector2Extensions {
  public static bool EqualsWithThreshold(this Vector2 self, Vector2 other, float threshold) {
    return (self - other).sqrMagnitude < threshold * threshold;
  }
}
