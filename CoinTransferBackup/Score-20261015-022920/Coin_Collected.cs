using UnityEngine;
using Unity.XR.CoreUtils;

public class Coin_Collected : MonoBehaviour
{
    AudioManager audioManager;
    Collider[] pickupColliders;
    Camera playerCamera;
    bool collected;

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
        if (other.GetComponentInParent<XROrigin>() != null) Collect();
    }

    void Update()
    {
        // The original XR rig has no body collider; also support physical walking.
        if (playerCamera == null) playerCamera = Camera.main;
        if (playerCamera == null || playerCamera.GetComponentInParent<XROrigin>() == null) return;
        var position = playerCamera.transform.position;
        foreach (var collider in pickupColliders)
        {
            if (collider.enabled && (collider.ClosestPoint(position) - position).sqrMagnitude <= 0.35f * 0.35f)
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
        if (audioManager != null) audioManager.PlaySFX(audioManager.coinCollectSound);
        gameObject.SetActive(false);
        Destroy(gameObject);
    }
}
