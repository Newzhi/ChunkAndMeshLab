using UnityEngine;

namespace EndlessWorld3D
{
    /// <summary>
    /// Simple fly camera controller intended for demos.
    /// Right mouse button: look around (cursor locked while held).
    /// WASD: move on XZ plane, Q/E: down/up. Shift: faster.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class FlyCameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float movementSpeed = 30f;
        public float sprintMultiplier = 2f;

        [Header("Look")]
        public float lookSpeed = 500f;
        public bool invertY;
        public float maxPitch = 89f;

        [Header("Input")]
        public KeyCode lookButton = KeyCode.Mouse1;
        public KeyCode sprintKey = KeyCode.LeftShift;
        public KeyCode upKey = KeyCode.E;
        public KeyCode downKey = KeyCode.Q;

        float _yaw;
        float _pitch;

        void Awake()
        {
            var euler = transform.rotation.eulerAngles;
            _yaw = euler.y;
            _pitch = NormalizePitch(euler.x);
        }

        void Update()
        {
            HandleLook();
            HandleMove();
        }

        void HandleLook()
        {
            if (!Input.GetKey(lookButton))
            {
                if (Cursor.lockState == CursorLockMode.Locked)
                {
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }
                return;
            }

            if (Cursor.lockState != CursorLockMode.Locked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            float dt = Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");

            float yawDelta = mouseX * (lookSpeed * dt);
            float pitchDelta = mouseY * (lookSpeed * dt) * (invertY ? 1f : -1f);

            _yaw += yawDelta;
            _pitch = Mathf.Clamp(_pitch + pitchDelta, -maxPitch, maxPitch);

            transform.rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        }

        void HandleMove()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            float h = Input.GetAxisRaw("Horizontal"); // A/D
            float v = Input.GetAxisRaw("Vertical");   // W/S
            float up = 0f;
            if (Input.GetKey(upKey)) up += 1f;
            if (Input.GetKey(downKey)) up -= 1f;

            Vector3 input = new Vector3(h, up, v);
            if (input.sqrMagnitude > 1f)
            {
                input.Normalize();
            }

            float speed = movementSpeed;
            if (Input.GetKey(sprintKey))
            {
                speed *= sprintMultiplier;
            }

            // Move relative to current orientation.
            Vector3 delta = (transform.right * input.x + transform.up * input.y + transform.forward * input.z) * (speed * dt);
            transform.position += delta;
        }

        static float NormalizePitch(float pitch)
        {
            // Unity returns pitch in [0,360). Convert to [-180,180).
            pitch %= 360f;
            if (pitch >= 180f) pitch -= 360f;
            return pitch;
        }
    }
}

