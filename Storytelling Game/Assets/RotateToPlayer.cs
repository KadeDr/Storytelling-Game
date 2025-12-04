using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class RotateToPlayer : MonoBehaviour
{
    [Header("References")]
    public Transform target; 
    public Camera cam; 
    public CanvasGroup canvasGroup; 
    public RectTransform rect;
    public Image keybindImage;

    [Header("Settings")]
    public Vector3 offset = new Vector3(0, 1.2f, 0);
    public float showDistance = 3f;

    [Header("Animation")]
    public float fadeInTime = 0.15f;
    public float fadeOutTime = 0.35f;
    public float slideDistance = 20f;

    [Tooltip("Curve used only for fade-out (makes it smooth/glide)")]
    public AnimationCurve fadeOutCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    private float baseYPos;
    private Coroutine animRoutine = null;
    private Transform player;

    // Tracks if we’ve shown this at least once
    private bool hasPoppedUpOnce = false;

    private void Start()
    {
        if (cam == null) cam = Camera.main;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (rect == null)
            rect = GetComponent<RectTransform>();

        baseYPos = rect.anchoredPosition.y;

        player = cam.transform;
        canvasGroup.alpha = 0;
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Skip popup if item is held (parent changed)
        if (target.parent != null && target.parent != transform.root)
        {
            // Force hide
            if (canvasGroup.alpha > 0f)
                PlayHideAnimation();
            return;
        }

        // Follow the item
        transform.position = target.position + offset;

        // Always face camera
        transform.rotation = Quaternion.LookRotation(
            transform.position - cam.transform.position
        );

        // Distance check
        float dist = Vector3.Distance(player.position, target.position);
        bool shouldShow = dist <= showDistance;

        if (shouldShow && canvasGroup.alpha < 1f)
        {
            // Pop up instantly first time
            if (!hasPoppedUpOnce)
            {
                canvasGroup.alpha = 1f;
                rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, baseYPos);
                hasPoppedUpOnce = true;
            }
            else
            {
                PlayShowAnimation();
            }
        }
        else if (!shouldShow && canvasGroup.alpha > 0f)
        {
            PlayHideAnimation();
        }
    }

    // ───────────────────────────────────────────────
    //  ANIMATION ROUTINES
    // ───────────────────────────────────────────────

    private void PlayShowAnimation()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(ShowRoutine());
    }

    private void PlayHideAnimation()
    {
        if (animRoutine != null) StopCoroutine(animRoutine);
        animRoutine = StartCoroutine(HideRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        float t = 0f;
        float startAlpha = canvasGroup.alpha;
        float startY = rect.anchoredPosition.y;
        float endY = baseYPos;

        while (t < fadeInTime)
        {
            t += Time.deltaTime;
            float lerp = Mathf.Clamp01(t / fadeInTime);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 1f, lerp);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                Mathf.Lerp(startY, endY, lerp));

            yield return null;
        }

        canvasGroup.alpha = 1f;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, endY);
        animRoutine = null;
    }

    private IEnumerator HideRoutine()
    {
        float t = 0f;

        float startAlpha = canvasGroup.alpha;
        float startY = rect.anchoredPosition.y;
        float endY = baseYPos - slideDistance;

        while (t < fadeOutTime)
        {
            t += Time.deltaTime;
            float normalized = Mathf.Clamp01(t / fadeOutTime);
            float curveAlpha = fadeOutCurve.Evaluate(normalized);

            canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, curveAlpha);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x,
                Mathf.Lerp(startY, endY, curveAlpha));

            yield return null;
        }

        canvasGroup.alpha = 0f;
        rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, endY);
        animRoutine = null;
    }
}
