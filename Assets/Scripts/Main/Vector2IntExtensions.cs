using UnityEngine;

// ReSharper disable UnusedMember.Global
public static class Vector2IntExtensions {
  public static Vector2Int ToVector2Int(this Vector2 v) {
    return new Vector2Int(Mathf.RoundToInt(v.x), Mathf.RoundToInt(v.y));
  }
}
