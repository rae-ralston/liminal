using System.Collections;
using UnityEngine;

public class FadeController : MonoBehaviour
{
  [SerializeField] private CanvasGroup canvasGroup;
  [SerializeField] private float fadeDuration = 0.3f;

  public IEnumerator FadeOut() => Fade(0f, 1f);
  public IEnumerator FadeIn() => Fade(1f, 0f);

  private IEnumerator Fade(float from, float to)
  {
    canvasGroup.blocksRaycasts = to > 0f;
    float elapsed = 0f;
    while (elapsed < fadeDuration)
    {
      elapsed += Time.deltaTime;
      canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / fadeDuration);
      yield return null;
    }
    canvasGroup.alpha = to;
  
  //  Debug.Log("fading");
  // yield return null;
  }
  
}
