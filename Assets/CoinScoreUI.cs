using UnityEngine;
using UnityEngine.UI;

// Lives on the scene's player camera, so each new run starts at zero.
public sealed class CoinScoreUI : MonoBehaviour
{
    public int Score { get; private set; }
    Text scoreText;

    public static CoinScoreUI ForCamera(Camera camera)
    {
        var counter = camera.GetComponent<CoinScoreUI>();
        return counter != null ? counter : camera.gameObject.AddComponent<CoinScoreUI>();
    }

    void Awake()
    {
        var panel = new GameObject("Coin Counter", typeof(RectTransform), typeof(Canvas), typeof(Image));
        panel.layer = LayerMask.NameToLayer("UI");
        var rect = panel.GetComponent<RectTransform>();
        rect.SetParent(transform, false);
        rect.sizeDelta = new Vector2(300, 80);
        rect.localPosition = new Vector3(0.34f, -0.25f, 1.2f);
        rect.localScale = Vector3.one * 0.0015f;
        var canvas = panel.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.worldCamera = GetComponent<Camera>();
        canvas.sortingOrder = 100;
        var background = panel.GetComponent<Image>();
        background.color = new Color(0.025f, 0.04f, 0.06f, 0.85f);
        background.raycastTarget = false;

        var label = new GameObject("Score", typeof(RectTransform), typeof(Text));
        label.layer = panel.layer;
        var labelRect = label.GetComponent<RectTransform>();
        labelRect.SetParent(rect, false);
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16, 8);
        labelRect.offsetMax = new Vector2(-16, -8);
        scoreText = label.GetComponent<Text>();
        scoreText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        scoreText.fontSize = 38;
        scoreText.fontStyle = FontStyle.Bold;
        scoreText.alignment = TextAnchor.MiddleCenter;
        scoreText.color = new Color(1f, 0.83f, 0.3f);
        scoreText.raycastTarget = false;
        Refresh();
    }

    public void AddCoin()
    {
        Score++;
        Refresh();
    }

    void Refresh() => scoreText.text = "COINS  " + Score;
}
