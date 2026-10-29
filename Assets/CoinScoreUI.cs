using UnityEngine;
using UnityEngine.UI;

// Lives on the scene's player camera, so each new run starts at zero.
public sealed class CoinScoreUI : MonoBehaviour
{
    public int Score { get; private set; }
    Text scoreText;
    Text timerText;
    float remainingSeconds = 180f;
    int displayedSeconds = -1;
    bool timerExpired;

    public static CoinScoreUI ForCamera(Camera camera)
    {
        var counter = camera.GetComponent<CoinScoreUI>();
        return counter != null ? counter : camera.gameObject.AddComponent<CoinScoreUI>();
    }

    void Awake()
    {
        scoreText = CreateLabel("Coin Counter", "Score", 0.34f, new Color(1f, 0.83f, 0.3f));
        timerText = CreateLabel("Countdown Timer", "Time Remaining", -0.34f, Color.white);
        Refresh();
        RefreshTimer();
    }

    void Update()
    {
        if (timerExpired) return;
        remainingSeconds = Mathf.Max(0f, remainingSeconds - Time.deltaTime);
        RefreshTimer();
        if (remainingSeconds > 0f) return;

        // Use the same transition as the last coin, once per run. Continue's
        // loading guard also prevents a simultaneous pickup from advancing twice.
        timerExpired = true;
        var menu = FindFirstObjectByType<HoldToReturnToMenu>();
        if (menu != null) menu.Continue();
    }

    void RefreshTimer()
    {
        int seconds = Mathf.CeilToInt(remainingSeconds);
        if (seconds == displayedSeconds) return;
        displayedSeconds = seconds;
        timerText.text = $"TIME  {seconds / 60:00}:{seconds % 60:00}";
    }

    Text CreateLabel(string panelName, string labelName, float horizontalOffset, Color color)
    {
        var panel = new GameObject(panelName, typeof(RectTransform), typeof(Canvas), typeof(Image));
        panel.layer = LayerMask.NameToLayer("UI");
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.sizeDelta = new Vector2(300, 80);
        rect.localPosition = new Vector3(horizontalOffset, -0.25f, 1.2f);
        rect.localScale = Vector3.one * 0.0015f;
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetComponent<Camera>();
        canvas.sortingOrder = 100;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.06f, 0.85f);
        background.raycastTarget = false;

        var label = new GameObject(labelName, typeof(RectTransform), typeof(Text));
        label.layer = panel.layer;
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16, 8);
        labelRect.offsetMax = new Vector2(-16, -8);
        var text = label.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 38;
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = color;
        text.raycastTarget = false;
        return text;
    }

    public void AddCoin()
    {
        Score++;
        Refresh();
    }

    void Refresh() => scoreText.text = "COINS  " + Score;
}
