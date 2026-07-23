using UnityEngine;

public class SecurityRoomView : MonoBehaviour
{
  [SerializeField] private GameObject securityPanel;
  [SerializeField] private PlayerMovement playerMovement;

  private void Start()
  {
    securityPanel.SetActive(false);
  }

  public void Show()
  {
    securityPanel.SetActive(true);
    playerMovement.enabled = false;
  }

  public void Hide()
  {
    securityPanel.SetActive(false);
    playerMovement.enabled = true;
  }
}
