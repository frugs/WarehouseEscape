using JetBrains.Annotations;
using UnityEngine;

public class MenuController : MonoBehaviour {
  [Header("Dependencies")] [SerializeField]
  private GameSession GameSession;

  [SerializeField] private MoveScheduler MoveScheduler;

  [UsedImplicitly]
  private void Awake() {
    GameSession = GetComponent<GameSession>();
    MoveScheduler = GetComponent<MoveScheduler>();
  }


  [UsedImplicitly]
  public void NextLevel() {
    MoveScheduler.ClearInterrupt();
    GameSession.NextLevel();
  }


  [UsedImplicitly]
  public void ResetLevel() {
    MoveScheduler.ClearInterrupt();
    GameSession.ResetLevel();
  }
}
