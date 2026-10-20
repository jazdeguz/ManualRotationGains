using UnityEngine;
using Unity.XR.CoreUtils;

public class Coin_Collected : MonoBehaviour
{
    AudioManager audioManager;
    Collider[] pickupColliders;
    Camera playerCamera;
    bool collected;
    [SerializeField, Min(0f)] float pickupDistance = 1f;
    CoinScoreUI scoreUI;

    private void Awake()
    {
        audioManager = FindFirstObjectByType<AudioManager>();
        pickupColliders = GetComponentsInChildren<Collider>();
        foreach (var collider in pickupColliders) collider.isTrigger = true;
        var body = GetComponent<Rigidbody>();
        if (body == null) body = gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;
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
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
