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

    private void Awake()
    {
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
        if (StudyFlow.Running && bButton.Read())
            ContinueStudy();
    }

    private void ShowExperimentReady()
    {
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
    }

    public static string TurnLabel(MovementModeSelector.MovementMode mode)
    {
        return mode == MovementModeSelector.MovementMode.UseRotation ? "Tilt"
            : mode == MovementModeSelector.MovementMode.UseJoystick ? "Joystick" : "Continuous Turn";
    }

    public void ShowChangePIDMenu()
    {
        if (loading) return;
        homePanel.SetActive(false);
        PIDSelectionPanel.SetActive(true);
    }

    public void ShowCustomOrder()
    {
        if (loading) return;
        homePanel.SetActive(false);
        customOrderPanel.SetActive(true);
        SelectMode(selectedMode);
        SelectButton(selectedMode == MovementModeSelector.MovementMode.UseRotation ? rotationButton
            : selectedMode == MovementModeSelector.MovementMode.UseJoystick ? joystickButton : continuousButton);
    }

    public void ShowHome()
    {
        if (loading) return;
        customOrderPanel.SetActive(false);
        PIDSelectionPanel.SetActive(false);
        experimentReadyPanel.SetActive(false);
        homePanel.SetActive(true);
        SelectButton(customOrderButton);
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
