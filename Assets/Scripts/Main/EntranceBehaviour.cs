using System.Collections;
using UnityEngine;

public class EntranceBehaviour : MonoBehaviour {
  public void RemoveEntrance() {
    StartCoroutine(RemoveEntranceCoroutine());
  }

  private IEnumerator RemoveEntranceCoroutine() {
    yield return null;

    var currentVel = Vector3.zero;

    while (transform.position.y > -50) {
      transform.position = Vector3.SmoothDamp(
          transform.position,
          Vector3.down * 100,
          ref currentVel,
          3f);
      yield return null;
    }
  }
}
