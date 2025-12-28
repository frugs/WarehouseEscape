using UnityEngine;

public class MenuManager : MonoBehaviour {
  [SerializeField] private readonly GameObject WinMenu = null;

  public void WinGame() {
    WinMenu.SetActive(true);
    Time.timeScale = 0f;
  }

  public void ResumeGame() {
    WinMenu.SetActive(false);
    Time.timeScale = 1f;
  }
}
