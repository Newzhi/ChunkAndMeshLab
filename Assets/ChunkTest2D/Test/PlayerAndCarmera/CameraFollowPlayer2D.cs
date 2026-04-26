using UnityEngine;

// 2D 相机跟随：自动找 Player(tag)，平滑跟随目标；保持相机 Z 不变（或用 fixedZ）。
[DisallowMultipleComponent]
public class CameraFollowPlayer2D : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Follow")]
    [SerializeField] private Vector2 targetOffset = Vector2.zero;
    [SerializeField] private float followSmoothTime = 0.08f;

    [Header("Z")]
    [SerializeField] private bool keepCurrentZ = true;
    [SerializeField] private float fixedZ = -10f;

    private Vector3 followVelocity;

    private void Awake()
    {
        if (target == null)
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null)
            {
                target = playerObj.transform;
            }
        }
    }

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        float z = keepCurrentZ ? transform.position.z : fixedZ;
        Vector3 desired = new Vector3(
            target.position.x + targetOffset.x,
            target.position.y + targetOffset.y,
            z);

        transform.position = Vector3.SmoothDamp(transform.position, desired, ref followVelocity, followSmoothTime);
    }
}

