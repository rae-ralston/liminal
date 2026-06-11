using UnityEngine;

public class SecurityRoomView : MonoBehaviour
{
  [SerializeField] private GameObject securityPanel;
  [SerializeField] private PlayerMovement playerMovement;
  private bool isOpen = false;

  private void Start()
  {
    securityPanel.SetActive(false);
  }

  public void Show()
  {
    isOpen = true;
    securityPanel.SetActive(true);
    playerMovement.enabled = false;
  }

  public void Hide()
  {
    isOpen = false;
    securityPanel.SetActive(false);
    playerMovement.enabled = true;
  }
}
