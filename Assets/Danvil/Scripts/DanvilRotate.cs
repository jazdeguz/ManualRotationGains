using UnityEngine;

namespace Danvil {
    public class DanvilRotate: MonoBehaviour
    {
        public float rotationSpeed = 20f;
    
        private void Update()
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.World);
        }
    }
}
