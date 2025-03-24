using UnityEngine;

namespace Rowing
{
    [RequireComponent(typeof(BoxCollider))]
    public class WaterSurfaceCollider : MonoBehaviour
    {
        public float waterHeight = 0f;
        public float surfaceSize = 1000f;

        private void Start()
        {
            SetupCollider();
        }

        private void SetupCollider()
        {
            BoxCollider collider = GetComponent<BoxCollider>();
            if (collider == null)
                collider = gameObject.AddComponent<BoxCollider>();

            // Create a thin collider at water height
            collider.center = new Vector3(0, 0, 0);
            collider.size = new Vector3(surfaceSize, 0.05f, surfaceSize);

            // Position the water plane at the correct height
            transform.position = new Vector3(0, waterHeight, 0);

            // Set as trigger so boats don't "bump" on water
            collider.isTrigger = true;

            // Make sure this object is on the Water layer
            gameObject.layer = LayerMask.NameToLayer("Water");
        }

        // Helper method to update water height at runtime if needed
        public void UpdateWaterHeight(float height)
        {
            waterHeight = height;
            transform.position = new Vector3(transform.position.x, waterHeight, transform.position.z);
        }
    }
}
