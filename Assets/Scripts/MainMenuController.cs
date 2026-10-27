using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public const string GameplayScenePath = "Assets/TerrainDemoScene_HDRP/Scenes/TerrainDemoScene.unity";

    [SerializeField] private Button rotationButton;
    [SerializeField] private Button joystickButton;
    [SerializeField] private Button continuousButton;
    [SerializeField] private Button startButton;
    [SerializeField] private Text statusText;

    private MovementModeSelector.MovementMode selectedMode;
    private bool loading;

    private void Awake()
    {
        rotationButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.UseRotation));
        joystickButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.UseJoystick));
        continuousButton.onClick.AddListener(() => SelectMode(MovementModeSelector.MovementMode.ContinuousRotation));
        startButton.onClick.AddListener(StartGameplay);
        SelectMode(MovementSessionSettings.HasSelection
            ? MovementSessionSettings.SelectedMode
            : MovementModeSelector.MovementMode.UseJoystick);
    }

    public void SelectMode(MovementModeSelector.MovementMode mode)
    {
        if (loading) return;
        selectedMode = mode;
        Highlight(rotationButton, mode == MovementModeSelector.MovementMode.UseRotation);
        Highlight(joystickButton, mode == MovementModeSelector.MovementMode.UseJoystick);
        Highlight(continuousButton, mode == MovementModeSelector.MovementMode.ContinuousRotation);
        string label = mode == MovementModeSelector.MovementMode.UseRotation ? "Use Rotation"
            : mode == MovementModeSelector.MovementMode.UseJoystick ? "Use Joystick" : "Continuous Rotation";
        statusText.text = "Selected: " + label;
    }

    private static void Highlight(Button button, bool selected)
    {
        button.image.color = selected ? new Color(0.12f, 0.48f, 0.58f) : new Color(0.16f, 0.20f, 0.28f);
    }

    public void StartGameplay()
    {
        if (loading) return;
        if (!Application.CanStreamedLevelBeLoaded(GameplayScenePath))
        {
            statusText.text = "Terrain scene is unavailable. Add it to the build scene list.";
            return;
        }

        loading = true;
        SetButtonsInteractable(false);
        statusText.text = "Loading terrain...";
        MovementSessionSettings.Select(selectedMode);
        try
        {
            if (SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Single) == null)
                throw new InvalidOperationException("Scene loading did not start.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, this);
            loading = false;
            SetButtonsInteractable(true);
            statusText.text = "Unable to load terrain. Please try again.";
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        rotationButton.interactable = interactable;
        joystickButton.interactable = interactable;
        continuousButton.interactable = interactable;
        startButton.interactable = interactable;
    }
}
