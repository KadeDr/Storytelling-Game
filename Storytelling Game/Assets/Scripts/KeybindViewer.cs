using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class KeybindViewer : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerController _playerController;

    [Header("Settings")]
    [SerializeField] private float _fadeSpeed = 8f;
    [SerializeField] private Vector2 _offset = new Vector2(-20f, 20f);
    [SerializeField] private float _animationDuration = 0.2f;

    private CanvasGroup _viewerCanvasGroup;
    private RectTransform _viewerRect;
    private Transform _keybindContainer;
    private GameObject _modeIndicator;
    private List<GameObject> _currentKeybindItems = new List<GameObject>();
    private bool _isRotationMode = false;
    private bool _hasHeldItem = false;
    private Canvas _canvas;
    private bool _uiCreated = false;
    private Image _backgroundImage;
    private Coroutine _switchAnimationCoroutine;

    [System.Serializable]
    public class Keybind
    {
        public string action;
        public string[] keys;

        public Keybind(string action, params string[] keys)
        {
            this.action = action;
            this.keys = keys;
        }
    }

    private Keybind[] _normalKeybinds = new Keybind[]
    {
        new Keybind("Drop", "G"),
        new Keybind("Zoom", "Scroll"),
        new Keybind("Change Sensitivity", "Ctrl", "Scroll"),
        new Keybind("Rotate", "Alt")
    };

    private Keybind[] _rotationKeybinds = new Keybind[]
    {
        new Keybind("Drop", "G"),
        new Keybind("Rotate Z-Axis", "Scroll"),
        new Keybind("Rotate X/Y-Axis", "Move Mouse")
    };

    private void Awake()
    {
        CreateUI();
    }

    private void CreateUI()
    {
        if (_uiCreated) return;

        // Canvas
        _canvas = FindObjectOfType<Canvas>();
        if (_canvas == null)
        {
            GameObject canvasObj = new GameObject("Canvas");
            _canvas = canvasObj.AddComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // Viewer panel
        GameObject viewerObj = new GameObject("KeybindViewerPanel");
        viewerObj.transform.SetParent(_canvas.transform, false);

        _viewerRect = viewerObj.AddComponent<RectTransform>();
        _viewerCanvasGroup = viewerObj.AddComponent<CanvasGroup>();

        _viewerRect.anchorMin = new Vector2(1f, 0f);
        _viewerRect.anchorMax = new Vector2(1f, 0f);
        _viewerRect.pivot = new Vector2(1f, 0f);
        _viewerRect.anchoredPosition = _offset;
        _viewerRect.sizeDelta = new Vector2(300f, 220f);

        _backgroundImage = viewerObj.AddComponent<Image>();
        _backgroundImage.color = new Color(0f, 0f, 0f, 0.85f);
        _backgroundImage.material = new Material(Shader.Find("UI/Default")); // so gradients/shadows render

        Shadow panelShadow = viewerObj.AddComponent<Shadow>();
        panelShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        panelShadow.effectDistance = new Vector2(4f, -4f);

        // Keybind container
        GameObject containerObj = new GameObject("KeybindContainer");
        containerObj.transform.SetParent(viewerObj.transform, false);

        RectTransform containerRT = containerObj.AddComponent<RectTransform>();
        containerRT.anchorMin = new Vector2(0f, 0f);
        containerRT.anchorMax = new Vector2(1f, 1f);
        containerRT.offsetMin = new Vector2(16f, 50f);
        containerRT.offsetMax = new Vector2(-16f, -16f);

        VerticalLayoutGroup vlg = containerObj.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = 6f;
        vlg.childControlWidth = true;
        vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;
        vlg.padding = new RectOffset(4, 4, 4, 4);

        ContentSizeFitter fitter = containerObj.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _keybindContainer = containerObj.transform;

        // Mode indicator
        CreateModeIndicator(viewerObj.transform);

        _viewerCanvasGroup.alpha = 0f;
        _uiCreated = true;
    }

    private void CreateModeIndicator(Transform parent)
    {
        _modeIndicator = new GameObject("ModeIndicator");
        _modeIndicator.transform.SetParent(parent, false);

        RectTransform rt = _modeIndicator.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0f, 30f);

        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(_modeIndicator.transform, false);

        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = "Normal Mode";
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.7f, 0.7f, 0.7f, 1f);
        text.fontStyle = FontStyles.Italic;
    }

    private void Update()
    {
        if (_playerController == null || !_uiCreated) return;

        CheckPlayerState();
        UpdateDisplay();
    }

    private void CheckPlayerState()
    {
        bool hasItem = _playerController.HasHeldItem;
        bool isRotating = _playerController.IsRotatingItem;

        bool itemStateChanged = _hasHeldItem != hasItem;
        bool rotationChanged = hasItem && (_isRotationMode != isRotating);

        if (itemStateChanged || rotationChanged)
        {
            _hasHeldItem = hasItem;
            _isRotationMode = isRotating;

            if (_switchAnimationCoroutine != null)
                StopCoroutine(_switchAnimationCoroutine);
            _switchAnimationCoroutine = StartCoroutine(AnimateKeybindSwitch());

            UpdateModeIndicator();
            UpdateBackgroundColor();
        }
    }

    private void UpdateDisplay()
    {
        float targetAlpha = _hasHeldItem ? 1f : 0f;
        _viewerCanvasGroup.alpha = Mathf.Lerp(_viewerCanvasGroup.alpha, targetAlpha, Time.deltaTime * _fadeSpeed);
    }

    private void UpdateModeIndicator()
    {
        if (_modeIndicator == null) return;

        TextMeshProUGUI text = _modeIndicator.GetComponentInChildren<TextMeshProUGUI>();
        if (text != null)
        {
            text.text = _isRotationMode ? "Rotation Mode Active" : "Normal Mode";
            text.color = _isRotationMode ?
                new Color(1f, 0.42f, 0.42f, 1f) :
                new Color(0.7f, 0.7f, 0.7f, 1f);
            text.fontStyle = _isRotationMode ? FontStyles.Bold | FontStyles.Italic : FontStyles.Italic;
        }
    }

    private void UpdateBackgroundColor()
    {
        if (_backgroundImage != null)
        {
            _backgroundImage.color = _isRotationMode ?
                new Color(0.08f, 0f, 0f, 0.9f) :
                new Color(0f, 0f, 0f, 0.85f);
        }
    }

    private IEnumerator AnimateKeybindSwitch()
    {
        // Fade out old items
        float elapsed = 0f;
        List<CanvasGroup> oldGroups = new List<CanvasGroup>();

        foreach (var item in _currentKeybindItems)
        {
            if (item != null)
            {
                CanvasGroup cg = item.GetComponent<CanvasGroup>();
                if (cg == null) cg = item.AddComponent<CanvasGroup>();
                oldGroups.Add(cg);
            }
        }

        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _animationDuration;

            foreach (var cg in oldGroups)
            {
                if (cg != null)
                    cg.alpha = 1f - t;
            }

            yield return null;
        }

        foreach (var item in _currentKeybindItems)
        {
            if (item != null)
                Destroy(item);
        }
        _currentKeybindItems.Clear();

        if (!_hasHeldItem)
        {
            _switchAnimationCoroutine = null;
            yield break;
        }

        Keybind[] keybindsToShow = _isRotationMode ? _rotationKeybinds : _normalKeybinds;

        foreach (var keybind in keybindsToShow)
        {
            GameObject item = CreateKeybindItem(keybind);
            item.transform.SetParent(_keybindContainer, false);
            _currentKeybindItems.Add(item);
        }

        elapsed = 0f;
        while (elapsed < _animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / _animationDuration;

            foreach (var item in _currentKeybindItems)
            {
                if (item != null)
                {
                    CanvasGroup cg = item.GetComponent<CanvasGroup>();
                    if (cg != null)
                        cg.alpha = t;
                }
            }

            yield return null;
        }

        _switchAnimationCoroutine = null;
    }

    private GameObject CreateKeybindItem(Keybind keybind)
    {
        GameObject itemObj = new GameObject("KeybindItem");
        RectTransform rt = itemObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(0f, 32f);

        CanvasGroup cg = itemObj.AddComponent<CanvasGroup>();
        cg.alpha = 0f;

        HorizontalLayoutGroup hlg = itemObj.AddComponent<HorizontalLayoutGroup>();
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.spacing = 8f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = false;
        hlg.padding = new RectOffset(0, 0, 4, 4);

        // Action Text
        GameObject actionObj = new GameObject("ActionText");
        actionObj.transform.SetParent(itemObj.transform, false);
        RectTransform actionRT = actionObj.AddComponent<RectTransform>();
        actionRT.anchorMin = new Vector2(0f, 0f);
        actionRT.anchorMax = new Vector2(1f, 1f);
        actionRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI actionText = actionObj.AddComponent<TextMeshProUGUI>();
        actionText.text = keybind.action;
        actionText.fontSize = 14;
        actionText.color = new Color(0.8f, 0.8f, 0.8f, 1f);
        actionText.alignment = TextAlignmentOptions.MidlineLeft;
        actionText.fontStyle = FontStyles.Normal;
        actionText.enableWordWrapping = false;

        LayoutElement actionLE = actionObj.AddComponent<LayoutElement>();
        actionLE.flexibleWidth = 1;

        // Keys container
        GameObject keysObj = new GameObject("KeysContainer");
        keysObj.transform.SetParent(itemObj.transform, false);

        HorizontalLayoutGroup keysHlg = keysObj.AddComponent<HorizontalLayoutGroup>();
        keysHlg.spacing = 6f;
        keysHlg.childAlignment = TextAnchor.MiddleRight;
        keysHlg.childControlWidth = false;
        keysHlg.childControlHeight = false;
        keysHlg.childForceExpandWidth = false;
        keysHlg.childForceExpandHeight = false;

        foreach (var key in keybind.keys)
        {
            CreateKeyButton(key, keysObj.transform, key == "Alt" && _isRotationMode);
            if (key != keybind.keys[keybind.keys.Length - 1])
                CreatePlusSign(keysObj.transform);
        }

        return itemObj;
    }

    private void CreateKeyButton(string keyText, Transform parent, bool highlight = false)
    {
        GameObject keyObj = new GameObject("Key");
        keyObj.transform.SetParent(parent, false);

        RectTransform rt = keyObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(Mathf.Max(50f, keyText.Length * 10f), 28f);

        Image img = keyObj.AddComponent<Image>();
        img.color = highlight ? new Color(1f, 0.42f, 0.42f, 1f) : new Color(0.25f, 0.25f, 0.25f, 1f);
        img.type = Image.Type.Sliced;

        Shadow shadow = keyObj.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        shadow.effectDistance = new Vector2(0f, -2f);

        // Text
        GameObject textObj = new GameObject("Text");
        textObj.transform.SetParent(keyObj.transform, false);

        RectTransform textRT = textObj.AddComponent<RectTransform>();
        textRT.anchorMin = Vector2.zero;
        textRT.anchorMax = Vector2.one;
        textRT.sizeDelta = Vector2.zero;

        TextMeshProUGUI text = textObj.AddComponent<TextMeshProUGUI>();
        text.text = keyText;
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.fontStyle = FontStyles.Bold;
        text.enableWordWrapping = false;
    }

    private void CreatePlusSign(Transform parent)
    {
        GameObject plusObj = new GameObject("Plus");
        plusObj.transform.SetParent(parent, false);

        RectTransform rt = plusObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(12f, 28f);

        TextMeshProUGUI text = plusObj.AddComponent<TextMeshProUGUI>();
        text.text = "+";
        text.fontSize = 12;
        text.alignment = TextAlignmentOptions.Center;
        text.color = new Color(0.6f, 0.6f, 0.6f, 1f);
    }
}
