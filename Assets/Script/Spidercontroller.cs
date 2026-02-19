using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpiderController : MonoBehaviour
{
    [Header("Déplacement")]
    public float moveSpeed = 4f;
    public float rotateSpeed = 120f;

    [Header("Alignement au sol")]
    public float groundCheckDist = 1.5f;
    public float bodyAlignSpeed = 5f;   // vitesse d'alignement à la normale du sol
    public float bodyHoverHeight = 0.3f; // hauteur du corps au-dessus du sol

    [Header("Caméra")]
    public Transform cameraTarget;  // le CameraTarget qu'on a créé
    public float camSmooth = 5f;

    private Rigidbody _rb;
    private Camera _cam;
    private Vector3 _groundNormal = Vector3.up;

    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _cam = Camera.main;

        // Désactive la rotation automatique du Rigidbody
        _rb.freezeRotation = true;

        // Place la caméra dans le monde
        if (cameraTarget)
            _cam.transform.SetParent(null); // caméra indépendante
    }

    void Update()
    {
        HandleRotation();
        SmoothBodyToGround();
        FollowCamera();
    }

    void FixedUpdate()
    {
        HandleMovement();
    }

    void HandleMovement()
    {
        float h = Input.GetAxis("Horizontal"); // A/D ou ←/→
        float v = Input.GetAxis("Vertical");   // W/S ou ↑/↓

        // Déplacement dans la direction où on regarde (projété sur le sol)
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, _groundNormal).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, _groundNormal).normalized;

        Vector3 move = (forward * v + right * h) * moveSpeed;
        _rb.linearVelocity = new Vector3(move.x, _rb.linearVelocity.y, move.z);
    }

    void HandleRotation()
    {
        float h = Input.GetAxis("Horizontal");
        // Rotation sur l'axe Y (tourner l'araignée)
        transform.Rotate(Vector3.up, h * rotateSpeed * Time.deltaTime);
    }

    void SmoothBodyToGround()
    {
        // Raycast vers le bas pour trouver le sol
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                            -_groundNormal, out RaycastHit hit, groundCheckDist + 1f))
        {
            _groundNormal = Vector3.Lerp(_groundNormal, hit.normal,
                                         Time.deltaTime * bodyAlignSpeed).normalized;

            // Colle le corps à la bonne hauteur
            Vector3 targetPos = hit.point + _groundNormal * bodyHoverHeight;
            transform.position = Vector3.Lerp(transform.position, targetPos,
                                              Time.deltaTime * bodyAlignSpeed);
        }

        // Aligne la rotation du corps à la normale du sol
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, _groundNormal)
                               * transform.rotation;
        transform.rotation = Quaternion.Lerp(transform.rotation, targetRot,
                                             Time.deltaTime * bodyAlignSpeed);
    }

    void FollowCamera()
    {
        if (!cameraTarget || !_cam) return;

        _cam.transform.position = Vector3.Lerp(
            _cam.transform.position,
            cameraTarget.position,
            Time.deltaTime * camSmooth
        );
        _cam.transform.LookAt(transform.position + Vector3.up * 0.3f);
    }
}