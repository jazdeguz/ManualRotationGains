using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    public const string GameplayScenePath = "Assets/CompactEnvironment/TerrainDemoScene_Compact.unity";
    public const string DemoScenePath = "Assets/Scenes/TestDemo.unity";

    [SerializeField] private Button rotationButton;
    [SerializeField] private Button joystickButton;
    [SerializeField] private Button continuousButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Button demoButton;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject homePanel;
    [SerializeField] private GameObject customOrderPanel;
    [SerializeField] private GameObject PIDSelectionPanel;
    [SerializeField] private Button experimentButton;
    [SerializeField] private Button customOrderButton;
    [SerializeField] private Button decreasePID;
    [SerializeField] private Button increasePID;
    [SerializeField] private Button backButton;
    [SerializeField] private Button PIDBackButton;
    [SerializeField] private Button startStudyButton;
    [SerializeField] private GameObject experimentReadyPanel;
    [SerializeField] private Text experimentReadyPID;
    [SerializeField] private Text PCOrder;
    [SerializeField] private Text TurnTypeOrder;
    [SerializeField] private Text PID;

    private readonly BButtonPress bButton = new BButtonPress();
    private MovementModeSelector.MovementMode selectedMode;
    private bool loading;
    private GameObject settingsPanel;
    private Text startingSpeedText;
    private Button decreaseSpeedButton;
    private Button increaseSpeedButton;
    private GameObject resumePanel;
    private Button resumeButton;
    private Text resumeStepText;
    private Text resumeMessage;
    private Button previousStepButton;
    private Button nextStepButton;
    private int resumeStep;

    private void Awake()
    {
        BuildSettingsPage();
        BuildResumePage();
        rotationButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.UseRotation));
        joystickButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.UseJoystick));
        continuousButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.ContinuousRotation));
        startButton.onClick.AddListener(StartGameplay);
        experimentButton.onClick.AddListener(ShowChangePIDMenu);
        demoButton.onClick.AddListener(StartDemo);
        customOrderButton.onClick.AddListener(ShowCustomOrder);
        backButton.onClick.AddListener(ShowHome);
        PIDBackButton.onClick.AddListener(ShowHome);
        startStudyButton.onClick.AddListener(StartStudy);
        decreasePID.onClick.AddListener(() => UpdatePID(-1));
        increasePID.onClick.AddListener(() => UpdatePID(1));
        experimentButton.interactable = true;
        SelectMode(MovementSessionSettings.HasSelection
            ? MovementSessionSettings.SelectedMode
            : MovementModeSelector.MovementMode.UseJoystick);
        UpdatePID(0);
        if (StudyFlow.Running)
            ShowExperimentReady();
        else
            ShowHome();
    }

    private void Update()
    {
        if (StudyFlow.Running && experimentReadyPanel.activeSelf && bButton.Read())
            ContinueStudy();
    }

    private void ShowExperimentReady()
    {
        settingsPanel.SetActive(false);
        homePanel.SetActive(false);
        customOrderPanel.SetActive(false);
        PIDSelectionPanel.SetActive(false);
        experimentReadyPanel.SetActive(true);
    }

    public void UpdatePID(int delta)
    {
        if (loading) return;
        MovementSessionSettings.SetParticipantId(MovementSessionSettings.ParticipantId + delta);
        PID.text = "Participant PID: " + MovementSessionSettings.ParticipantId;
        experimentReadyPID.text = PID.text;
        PCOrder.text = MovementSessionSettings.ConductorFirst ? "Conductor first" : "Passenger first";

        string order = "Current selected order:";
        for (int i = 0; i < MovementSessionSettings.TurnOrder.Count; i++)
            order += "\n" + (i + 1) + ". " + TurnLabel(MovementSessionSettings.TurnOrder[i]);
        TurnTypeOrder.text = order;
        resumeButton.gameObject.SetActive(StudyFlow.CanResume);
    }

    public static string TurnLabel(MovementModeSelector.MovementMode mode)
    {
        return mode == MovementModeSelector.MovementMode.UseRotation ? "Tilt"
            : mode == MovementModeSelector.MovementMode.UseJoystick ? "Joystick" : "Continuous Turn";
    }

    public void ShowChangePIDMenu()
    {
        if (loading) return;
        resumePanel.SetActive(false);
        settingsPanel.SetActive(false);
        homePanel.SetActive(false);
        PIDSelectionPanel.SetActive(true);
        UpdatePID(0);
    }

    public void ShowCustomOrder()
    {
        if (loading) return;
        settingsPanel.SetActive(false);
        homePanel.SetActive(false);
        customOrderPanel.SetActive(true);
        SelectMode(selectedMode);
        SelectButton(selectedMode == MovementModeSelector.MovementMode.UseRotation ? rotationButton
            : selectedMode == MovementModeSelector.MovementMode.UseJoystick ? joystickButton : continuousButton);
    }

    public void ShowHome()
    {
        if (loading) return;
        resumePanel.SetActive(false);
        settingsPanel.SetActive(false);
        customOrderPanel.SetActive(false);
        PIDSelectionPanel.SetActive(false);
        experimentReadyPanel.SetActive(false);
        homePanel.SetActive(true);
        SelectButton(customOrderButton);
    }

    private void BuildSettingsPage()
    {
        // Reuse the scene's UI style and existing world-space canvas for XR input.
        settingsPanel = new GameObject("Settings", typeof(RectTransform));
        settingsPanel.layer = homePanel.layer;
        var panelRect = (RectTransform)settingsPanel.transform;
        panelRect.SetParent(homePanel.transform.parent, false);
        var homeRect = (RectTransform)homePanel.transform;
        panelRect.anchorMin = homeRect.anchorMin;
        panelRect.anchorMax = homeRect.anchorMax;
        panelRect.pivot = homeRect.pivot;
        panelRect.sizeDelta = homeRect.sizeDelta;
        panelRect.anchoredPosition = homeRect.anchoredPosition;
        settingsPanel.SetActive(false);

        CreateSettingsButton(homePanel.transform, "Settings", new Vector2(0f, -180f),
            new Vector2(820f, 100f), ShowSettings);
        CreateSettingsText("Settings", 230f, 55f, 40);
        CreateSettingsText("Starting gain (degrees per second)", 155f, 55f, 30);
        startingSpeedText = CreateSettingsText("", 80f, 60f, 40);
        decreaseSpeedButton = CreateSettingsButton(panelRect, "- 1", new Vector2(-210f, 0f),
            new Vector2(380f, 75f), () => ChangeStartingSpeed(-1f));
        increaseSpeedButton = CreateSettingsButton(panelRect, "+ 1", new Vector2(210f, 0f),
            new Vector2(380f, 75f), () => ChangeStartingSpeed(1f));
        CreateSettingsText("Speed at full input: 5-120 deg/s.\nApplies to Joystick, Tilt, and Continuous Rotation.\nSaved automatically between sessions.",
            -100f, 110f, 25);
        CreateSettingsButton(panelRect, "Reset to 30", new Vector2(-210f, -220f),
            new Vector2(380f, 75f), () => SetStartingSpeed(MovementSessionSettings.DefaultStartingSpeed));
        CreateSettingsButton(panelRect, "Back", new Vector2(210f, -220f),
            new Vector2(380f, 75f), ShowHome);
    }

    private Button CreateSettingsButton(Transform parent, string label, Vector2 position,
        Vector2 size, UnityEngine.Events.UnityAction action)
    {
        Button button = Instantiate(customOrderButton, parent, false);
        button.name = label;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(action);
        Text buttonLabel = button.GetComponentInChildren<Text>(true);
        buttonLabel.text = label;
        buttonLabel.rectTransform.sizeDelta = new Vector2(size.x - 30f, 48f);
        var rect = (RectTransform)button.transform;
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
        button.gameObject.SetActive(true);
        return button;
    }

    private Text CreateSettingsText(string label, float y, float height, int fontSize, Transform parent = null)
    {
        Text text = Instantiate(statusText, parent != null ? parent : settingsPanel.transform, false);
        text.name = string.IsNullOrEmpty(label) ? "Starting speed" : label;
        text.text = label;
        text.fontSize = fontSize;
        text.resizeTextForBestFit = false;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.rectTransform.anchoredPosition = new Vector2(0f, y);
        text.rectTransform.sizeDelta = new Vector2(820f, height);
        text.gameObject.SetActive(true);
        return text;
    }

    private void BuildResumePage()
    {
        resumePanel = new GameObject("Resume experiment", typeof(RectTransform));
        resumePanel.layer = homePanel.layer;
        var rect = (RectTransform)resumePanel.transform;
        rect.SetParent(homePanel.transform.parent, false);
        rect.sizeDelta = ((RectTransform)homePanel.transform).sizeDelta;
        resumePanel.SetActive(false);

        // Fit all three actions along the existing PID screen's bottom row.
        var startRect = (RectTransform)startStudyButton.transform;
        startRect.anchoredPosition = new Vector2(0f, -265f);
        startRect.sizeDelta = new Vector2(260f, 66f);
        ((RectTransform)PIDBackButton.transform).anchoredPosition = new Vector2(-280f, -265f);
        resumeButton = CreateSettingsButton(PIDSelectionPanel.transform, "Resume", new Vector2(280f, -265f),
            new Vector2(260f, 66f), ShowResume);
        CreateSettingsText("Resume experiment", 230f, 55f, 36, rect);
        resumeStepText = CreateSettingsText("", 100f, 150f, 30, rect);
        previousStepButton = CreateSettingsButton(rect, "Previous step", new Vector2(-210f, -20f),
            new Vector2(380f, 70f), () => ChangeResumeStep(-1));
        nextStepButton = CreateSettingsButton(rect, "Next step", new Vector2(210f, -20f),
            new Vector2(380f, 70f), () => ChangeResumeStep(1));
        resumeMessage = CreateSettingsText("", -120f, 110f, 23, rect);
        CreateSettingsButton(rect, "Back", new Vector2(-210f, -240f),
            new Vector2(380f, 70f), ShowChangePIDMenu);
        CreateSettingsButton(rect, "Resume this step", new Vector2(210f, -240f),
            new Vector2(380f, 70f), ResumeStudy);
    }

    private void ShowResume()
    {
        if (loading || !StudyFlow.CanResume) return;
        PIDSelectionPanel.SetActive(false);
        resumePanel.SetActive(true);
        resumeStep = Mathf.Clamp(StudyFlow.SavedStep, 0, StudyFlow.GetSteps().Count - 1);
        ChangeResumeStep(0);
        SelectButton(nextStepButton.interactable ? nextStepButton : previousStepButton);
    }

    private void ChangeResumeStep(int delta)
    {
        if (loading) return;
        var schedule = StudyFlow.GetSteps();
        resumeStep = Mathf.Clamp(resumeStep + delta, 0, schedule.Count - 1);
        StudyStep step = schedule[resumeStep];
        resumeStepText.text = "PID " + MovementSessionSettings.ParticipantId + " - Step " +
            (resumeStep + 1) + " of " + schedule.Count + "\n" +
            (step.AsConductor ? "Conductor" : "Passenger") + " - " + TurnLabel(step.Mode) + "\n" +
            (step.Scene == StudyScene.Menu ? "Ready screen" : step.Scene == StudyScene.Demo ? "Test demo" : "Game area");
        previousStepButton.interactable = resumeStep > 0;
        nextStepButton.interactable = resumeStep < schedule.Count - 1;
        resumeMessage.text = (StudyFlow.SavedStep >= schedule.Count ? "This experiment was previously completed.\n" : "") +
            "Restarts the selected step from the beginning.\nReplayed areas replace their recordings.\nExisting files are backed up before resuming.";
    }

    private void ResumeStudy()
    {
        if (loading) return;
        StudyStep step = StudyFlow.GetSteps()[resumeStep];
        if (!Application.CanStreamedLevelBeLoaded(step.ScenePath))
        {
            resumeMessage.text = "This scene is unavailable. Add it to the build scene list.";
            return;
        }
        try
        {
            StudyFlow.Resume(resumeStep);
            if (step.Scene == StudyScene.Menu)
            {
                resumePanel.SetActive(false);
                ShowExperimentReady();
            }
            else LoadStudyStep();
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            resumeMessage.text = "Unable to resume. Check the saved participant files.\n" + exception.Message;
        }
    }

    public void ShowSettings()
    {
        if (loading) return;
        homePanel.SetActive(false);
        customOrderPanel.SetActive(false);
        PIDSelectionPanel.SetActive(false);
        experimentReadyPanel.SetActive(false);
        settingsPanel.SetActive(true);
        RefreshStartingSpeed();
        SelectButton(increaseSpeedButton.interactable ? increaseSpeedButton : decreaseSpeedButton);
    }

    private void ChangeStartingSpeed(float delta) =>
        SetStartingSpeed(MovementSessionSettings.StartingTurnDegreesPerSecond + delta);

    private void SetStartingSpeed(float value)
    {
        if (loading) return;
        MovementSessionSettings.SaveStartingTurnSpeed(value);
        RefreshStartingSpeed();
    }

    private void RefreshStartingSpeed()
    {
        float speed = MovementSessionSettings.StartingTurnDegreesPerSecond;
        startingSpeedText.text = speed.ToString("0.#") + " deg/s";
        decreaseSpeedButton.interactable = speed > MovementSessionSettings.MinimumStartingSpeed;
        increaseSpeedButton.interactable = speed < MovementSessionSettings.MaximumStartingSpeed;
    }

    private static void SelectButton(Button button)
    {
        if (EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    public void SelectMode(MovementModeSelector.MovementMode mode)
    {
        if (loading) return;
        selectedMode = mode;
        Highlight(rotationButton, mode == MovementModeSelector.MovementMode.UseRotation);
        Highlight(joystickButton, mode == MovementModeSelector.MovementMode.UseJoystick);
        Highlight(continuousButton, mode == MovementModeSelector.MovementMode.ContinuousRotation);
        string label = mode == MovementModeSelector.MovementMode.UseRotation ? "Use Tilt"
            : mode == MovementModeSelector.MovementMode.UseJoystick ? "Use Joystick" : "Continuous Rotation";
        statusText.text = "Selected: " + label;
    }

    private static void Highlight(Button button, bool selected)
    {
        button.image.color = selected ? new Color(0.12f, 0.48f, 0.58f) : new Color(0.16f, 0.20f, 0.28f);
    }

    public void StartGameplay()
    {
        LoadEnvironment(GameplayScenePath, "game");
    }

    public void StartDemo()
    {
        LoadEnvironment(DemoScenePath, "demo");
    }

    public void StartStudy()
    {
        if (loading) return;
        StudyFlow.Begin();
        LoadStudyStep();
    }

    public void ContinueStudy()
    {
        if (loading) return;
        // A menu step waits here for B. Any other step was left early with the menu button, so it resumes.
        if (StudyFlow.Current.Scene == StudyScene.Menu && !StudyFlow.Advance())
        {
            ShowHome();
            return;
        }
        LoadStudyStep();
    }

    private void LoadStudyStep()
    {
        StudyStep step = StudyFlow.Current;
        SelectMode(step.Mode);
        LoadEnvironment(step.ScenePath, step.Scene == StudyScene.Demo ? "demo" : "game");
    }

    private void LoadEnvironment(string scenePath, string label)
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(scenePath))
        {
            statusText.text = "The " + label + " scene is unavailable. Add it to the build scene list.";
            return;
        }

        loading = true;
        SetButtonsInteractable(false);
        statusText.text = "Loading " + label + "...";
        MovementSessionSettings.Select(selectedMode);
        try
        {
            if (SceneManager.LoadSceneAsync(scenePath, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Scene loading did not start.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            loading = false;
            SetButtonsInteractable(true);
            statusText.text = "Unable to load " + label + ". Please try again.";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        rotationButton.interactable = interactable;
        joystickButton.interactable = interactable;
        continuousButton.interactable = interactable;
        startButton.interactable = interactable;
        demoButton.interactable = interactable;
        backButton.interactable = interactable;
        PIDBackButton.interactable = interactable;
        startStudyButton.interactable = interactable;
    }
}
