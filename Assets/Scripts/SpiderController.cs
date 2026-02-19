// ============================================================
//  Fichier : SpiderController.cs
//  Place ce fichier dans Assets/
// ============================================================

using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class SpiderController : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveSpeed   = 3f;
    public float rotateSpeed = 90f;

    [Header("Sol")]
    public float hoverHeight = 0.8f;
    public float alignSpeed  = 5f;

    [Header("Camera")]
    public Transform cameraTarget;
    public float     camSmooth = 5f;

    [Header("Bob du corps")]
    public float bobAmount = 0.06f;   // amplitude verticale (metres)
    public float bobSpeed  = 12f;     // vitesse d'oscillation

    [HideInInspector] public Vector3 groundNormal = Vector3.up;

    Rigidbody   _rb;
    Camera      _cam;
    float       _bobPhase;
    float       _currentHover;
    SpiderLeg[] _legs;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.interpolation  = RigidbodyInterpolation.Interpolate;
        _rb.linearDamping  = 6f;
        _rb.angularDamping = 6f;
        _cam = Camera.main;
        if (_cam) _cam.transform.SetParent(null);
        _currentHover = hoverHeight;
    }

    void Start()
    {
        _legs = GetComponentsInChildren<SpiderLeg>();
    }

    void Update()
    {
        // Rotation
        float h = Input.GetAxis("Horizontal");
        transform.Rotate(0f, h * rotateSpeed * Time.deltaTime, 0f, Space.Self);

        // Normale du sol
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                            Vector3.down, out RaycastHit hit, 4f))
            groundNormal = Vector3.Lerp(groundNormal, hit.normal,
                                        Time.deltaTime * alignSpeed).normalized;

        // Incline le corps sur la normale
        Quaternion tgt = Quaternion.FromToRotation(transform.up, groundNormal)
                         * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, tgt,
                                               Time.deltaTime * alignSpeed);

        // ---- Bob naturel ----
        // S'anime plus vite quand des pattes sont en mouvement
        int steppingCount = 0;
        if (_legs != null)
            foreach (var leg in _legs)
                if (leg != null && leg.isStepping) steppingCount++;

        float phaseSpeed = steppingCount > 0 ? bobSpeed : bobSpeed * 0.25f;
        _bobPhase += Time.deltaTime * phaseSpeed;

        float bobOffset = Mathf.Sin(_bobPhase) * bobAmount;
        _currentHover   = hoverHeight + bobOffset;

        // Camera
        if (cameraTarget && _cam)
        {
            _cam.transform.position = Vector3.Lerp(_cam.transform.position,
                                                    cameraTarget.position,
                                                    Time.deltaTime * camSmooth);
            _cam.transform.LookAt(transform.position + Vector3.up * 0.4f);
        }
    }

    void FixedUpdate()
    {
        float   v   = Input.GetAxis("Vertical");
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;

        _rb.linearVelocity = new Vector3(fwd.x * v * moveSpeed,
                                         _rb.linearVelocity.y,
                                         fwd.z * v * moveSpeed);

        // Hover avec bob integre
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                            Vector3.down, out RaycastHit hit, 5f))
        {
            float diff = (hit.point.y + _currentHover) - transform.position.y;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x,
                                             Mathf.Clamp(diff * 10f, -8f, 8f),
                                             _rb.linearVelocity.z);
        }
    }
}
