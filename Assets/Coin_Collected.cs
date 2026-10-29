using UnityEngine;
using Unity.XR.CoreUtils;

public class Coin_Collected : MonoBehaviour
{
    AudioManager audioManager;
    Collider[] pickupColliders;
    Camera playerCamera;
    bool collected;
    [SerializeField, Min(0f)] float pickupDistance = 1f;
    [SerializeField, Min(0f), Tooltip("Spin speed in degrees per second.")]
    float spinSpeed = 30f;
    [SerializeField, Min(0f), Tooltip("Maximum vertical offset from the placed position, in metres.")]
    float floatHeight = 0.12f;
    [SerializeField, Min(0f), Tooltip("Up-and-down cycles per second.")]
    float floatFrequency = 0.4f;
    Rigidbody body;
    Vector3 restingPosition;
    Quaternion restingRotation;
    float motionTime;
    CoinScoreUI scoreUI;

    private void Awake()
    {
        audioManager = FindFirstObjectByType<AudioManager>();
        pickupColliders = GetComponentsInChildren<Collider>();
        foreach (var collider in pickupColliders) collider.isTrigger = true;
        restingPosition = transform.position;
        restingRotation = transform.rotation;
        body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
        body.interpolation = RigidbodyInterpolation.Interpolate;
    }

    void FixedUpdate()
    {
        if (collected) return;
        motionTime += Time.fixedDeltaTime;
        // Anchor the bob to the placed height so the coin never drifts away.
        float offset = Mathf.Sin(motionTime * floatFrequency * 2f * Mathf.PI) * floatHeight;
        body.MovePosition(restingPosition + Vector3.up * offset);
        body.MoveRotation(Quaternion.AngleAxis(motionTime * spinSpeed, Vector3.up) * restingRotation);
    }

    void OnTriggerEnter (Collider other)
    {
        var origin = other.GetComponentInParent<XROrigin>();
        if (origin == null) return;
        if (origin.Camera != null) scoreUI = CoinScoreUI.ForCamera(origin.Camera);
        Collect();
    }

    void Update()
    {
        // The original XR rig has no body collider; also support physical walking.
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null || playerCamera.GetComponentInParent<XROrigin>() == null) return;
        if (scoreUI == null) scoreUI = CoinScoreUI.ForCamera(playerCamera);
        var position = playerCamera.transform.position;
        foreach (var collider in pickupColliders)
        {
            if (collider.enabled && (collider.ClosestPoint(position) - position).sqrMagnitude <= pickupDistance * pickupDistance)
            {
                Collect();
                return;
            }
        }
    }

    void Collect()
    {
        if (collected) return;
        collected = true;
        if (scoreUI != null) scoreUI.AddCoin();
        if (audioManager != null) audioManager.PlaySFX(audioManager.coinCollectSound);
        if (StudyFlow.Running && StudyFlow.Current.Scene == StudyScene.Area)
            StudyLog.RecordCoin(name, Time.timeSinceLevelLoad);
        gameObject.SetActive(false);
        Destroy(gameObject);

        // Inactive coins are skipped, so none found means this was the last one.
        if (FindObjectsByType<Coin_Collected>(FindObjectsSortMode.None).Length > 0) return;
        var menu = FindFirstObjectByType<HoldToReturnToMenu>();
        if (menu != null) menu.Continue();
    }
}
