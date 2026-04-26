using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerChunkMoveTest2D : MonoBehaviour
{
    [Header("Move")]
    [SerializeField] private float moveSpeed = 6f;
    [SerializeField] private float sprintMultiplier = 1.6f;
    [SerializeField] private bool normalizeDiagonal = true;

    [Header("Init")]
    [SerializeField] private bool snapToChunkCenterOnStart = true;
    [SerializeField] private ChunkConfig2D chunkConfig;

    [Header("Look")]
    [SerializeField] private bool faceMoveDirection = true;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;
    }

    private void Start()
    {
        if (!snapToChunkCenterOnStart)
        {
            return;
        }

        if (chunkConfig == null)
        {
            chunkConfig = FindFirstObjectByType<ChunkConfig2D>();
        }

        if (chunkConfig == null)
        {
            return;
        }

        ChunkSettings2D settings = chunkConfig.ToSettings();
        int size = Mathf.Max(1, settings.Size);

        Vector2 pos2 = new Vector2(transform.position.x, transform.position.y);
        ChunkCoord2D coord = ChunkUtil2D.WorldToChunkCoord(pos2, size);

        float centerX = coord.X * size + size * 0.5f;
        float centerY = coord.Y * size + size * 0.5f;

        rb.position = new Vector2(centerX, centerY);
        rb.velocity = Vector2.zero;
    }

    private void Update()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        moveInput = new Vector2(horizontal, vertical);

        if (normalizeDiagonal && moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void FixedUpdate()
    {
        float speed = moveSpeed;
        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
        {
            speed *= sprintMultiplier;
        }

        Vector2 targetVelocity = moveInput * speed;
        rb.velocity = targetVelocity;

        if (faceMoveDirection && targetVelocity.sqrMagnitude > 0.0001f)
        {
            // 让角色朝向移动方向（Z 轴朝外，2D 里使用 transform.up 作为“前”）
            float angle = Mathf.Atan2(targetVelocity.y, targetVelocity.x) * Mathf.Rad2Deg - 90f;
            rb.MoveRotation(angle);
        }
    }
}

