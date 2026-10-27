using System;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR;

public sealed class HoldToReturnToMenu : MonoBehaviour
{
    public const string MenuScenePath = "Assets/Scenes/MainMenu.unity";
    [SerializeField, Min(0.25f)] private float holdDuration = 2f;

    private readonly MenuReturnHold hold = new MenuReturnHold();
    private GameObject panel;
    private Text label;
    private Image progress;
    private bool loading;
    private bool focused = true;
    private bool paused;
    private float errorUntil;

    private void Start()
    {
        var origin = GetComponent<XROrigin>();
        if (origin == null || origin.Camera == null)
        {
            Debug.LogError("HoldToReturnToMenu requires an XR Origin with a camera.", this);
            enabled = false;
            return;
        }
        CreateFeedback(origin.Camera);
    }

    private void Update()
    {
        if (loading || panel == null) return;
        var device = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
        bool pressed = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool menu) && menu;
        pressed |= Keyboard.current != null && Keyboard.current.escapeKey.isPressed;

        if (!focused || paused)
        {
            ResetHold();
            return;
        }

        if (Time.unscaledTime < errorUntil) { hold.Reset(); return; }
        bool complete = hold.Tick(pressed, Time.unscaledDeltaTime, Mathf.Max(0.25f, holdDuration));
        panel.SetActive(hold.Elapsed > 0f);
        if (hold.Elapsed > 0f)
        {
            label.text = "Returning to menu...\nRelease to cancel";
            SetProgress(Mathf.Clamp01(hold.Elapsed / Mathf.Max(0.25f, holdDuration)));
        }
        if (complete) ReturnToMenu();
    }

    private void ReturnToMenu()
    {
        if (!Application.CanStreamedLevelBeLoaded(MenuScenePath))
        {
            ShowError("Main Menu is missing from the build scene list.");
            return;
        }
        var selector = GetComponent<MovementModeSelector>();
        if (selector != null) MovementSessionSettings.Select(selector.movementMode);
        loading = true;
        panel.SetActive(true);
        label.text = "Loading menu...";
        SetProgress(1f);
        try
        {
            if (SceneManager.LoadSceneAsync(MenuScenePath, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Menu loading did not start.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            loading = false;
            ShowError("Could not load menu. Release and try again.");
        }
    }

    private void SetProgress(float value) => progress.rectTransform.localScale = new Vector3(value, 1f, 1f);

    private void ShowError(string message)
    {
        hold.Reset();
        errorUntil = Time.unscaledTime + 3f;
        panel.SetActive(true);
        label.text = message;
        SetProgress(0f);
        Debug.LogWarning(message, this);
    }

    private void OnApplicationFocus(bool value) { focused = value; ResetHold(); }
    private void OnApplicationPause(bool value) { paused = value; ResetHold(); }
    private void OnDisable() { ResetHold(); }
    private void OnDestroy() { if (panel != null) Destroy(panel); }

    private void ResetHold()
    {
        hold.Reset();
        if (panel != null && !loading) panel.SetActive(false);
    }

    private void CreateFeedback(Camera camera)
    {
        panel = new GameObject("Return To Menu Progress", typeof(RectTransform), typeof(Canvas), typeof(Image));
        panel.layer = LayerMask.NameToLayer("UI");
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(camera.transform, false);
        rect.localPosition = new Vector3(0f, -0.12f, 1.2f);
        rect.localScale = Vector3.one * 0.0015f;
        rect.sizeDelta = new Vector2(540, 130);
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = camera;
        canvas.sortingOrder = 110;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.06f, 0.95f);
        background.raycastTarget = false;

        var textObject = new GameObject("Message", typeof(RectTransform), typeof(Text));
        textObject.layer = panel.layer;
        var textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(rect, false);
        textRect.sizeDelta = new Vector2(510, 90);
        textRect.anchoredPosition = new Vector2(0, 12);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 28;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        var bar = new GameObject("Hold Progress", typeof(RectTransform), typeof(Image));
        bar.layer = panel.layer;
        var barRect = bar.GetComponent<RectTransform>();
        barRect.SetParent(rect, false);
        barRect.sizeDelta = new Vector2(490, 12);
        barRect.pivot = new Vector2(0f, 0.5f);
        barRect.anchoredPosition = new Vector2(-245, -48);
        progress = bar.GetComponent<Image>();
        progress.color = new Color(0.2f, 0.8f, 0.9f);
        progress.raycastTarget = false;
        // A solid image can be cropped with RectTransform scaling without a sprite.
        panel.SetActive(false);
    }
}

/// <summary>Requires release before arming; cancellation never carries time into the next hold.</summary>
public sealed class MenuReturnHold
{
    public float Elapsed { get; private set; }
    private bool armed;

    public bool Tick(bool pressed, float deltaTime, float duration)
    {
        if (!pressed) { Elapsed = 0f; armed = true; return false; }
        if (!armed) return false;
        Elapsed += Math.Max(0f, deltaTime);
        if (Elapsed < duration) return false;
        armed = false;
        return true;
    }

    public void Reset() { Elapsed = 0f; armed = false; }
}

