using UnityEngine;

public class SpiderLeg : MonoBehaviour
{
    [Header("Références IK")]
    public Transform upperLeg;
    public Transform lowerLeg;
    public Transform foot;

    [Header("Longueurs des os")]
    public float upperLength = 0.6f;
    public float lowerLength = 0.6f;

    [Header("Pôle IK (direction du genou)")]
    public Transform polePivot; // point qui guide la courbure

    [Header("Paramètres de pas")]
    public float stepDistance = 0.4f;   // distance max avant de faire un pas
    public float stepHeight = 0.25f;  // hauteur de l'arc du pas
    public float stepSpeed = 8f;     // vitesse d'animation du pas
    public float groundOffset = 0.05f;  // légère hauteur au-dessus du sol

    [Header("Synchronisation")]
    public SpiderLeg oppositeLeg;

    [Header("Position de repos (locale au corps)")]
    public Vector3 restOffset = new Vector3(1f, 0f, 0.5f);


    // -- Privé --
    private Vector3 _currentFootPos;    // position actuelle du pied
    private Vector3 _targetFootPos;     // là où on veut poser le pied
    private Vector3 _stepStartPos;      // départ du dernier pas
    private float _stepProgress = 1f; // 0→1, 1 = pas terminé
    [HideInInspector] public bool _isStepping;

    // Référence au spider pour accéder au sol et à la vitesse
    private SpiderController _spider;

    void Start()
    {
        _spider = GetComponentInParent<SpiderController>();

        // Initialise le pied à la position de repos projetée au sol
        _currentFootPos = GetRestWorldPosition();
        _targetFootPos = _currentFootPos;
    }

    void Update()
    {
        // --- Vérifie si on doit faire un pas ---
        Vector3 restWorld = GetRestWorldPosition();
        float dist = Vector3.Distance(_currentFootPos, restWorld);

        if (!_isStepping && dist > stepDistance && (oppositeLeg == null || !oppositeLeg._isStepping))
        {
            TriggerStep(restWorld);
        }

        // --- Anime le pas en cours ---
        if (_isStepping)
        {
            _stepProgress += Time.deltaTime * stepSpeed;
            float t = Mathf.Clamp01(_stepProgress);

            // Arc parabolique : x,z interpolé, y en cloche
            Vector3 flatPos = Vector3.Lerp(_stepStartPos, _targetFootPos, t);
            float arc = Mathf.Sin(t * Mathf.PI) * stepHeight;

            _currentFootPos = new Vector3(flatPos.x, flatPos.y + arc, flatPos.z);

            if (t >= 1f)
            {
                _isStepping = false;
                _currentFootPos = _targetFootPos;
            }
        }

        // --- Résout l'IK ---
        Vector3 pole = polePivot != null ? polePivot.position
                       : upperLeg.position + upperLeg.right * 0.5f;

        TwoBoneIK.Solve(upperLeg, lowerLeg, foot,
                        _currentFootPos, pole,
                        upperLength, lowerLength);
    }

    void TriggerStep(Vector3 toward)
    {
        // Projette la position de repos sur le sol
        if (Physics.Raycast(toward + Vector3.up * 1f, Vector3.down, out RaycastHit hit, 3f))
        {
            _targetFootPos = hit.point + Vector3.up * groundOffset;
        }
        else
        {
            _targetFootPos = toward;
        }

        _stepStartPos = _currentFootPos;
        _stepProgress = 0f;
        _isStepping = true;
    }

    Vector3 GetRestWorldPosition()
    {
        // La position de repos est relative au corps de l'araignée
        return _spider.transform.TransformPoint(restOffset);
    }

    // Gizmo pour visualiser la position du pied en éditeur
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = _isStepping ? Color.red : Color.green;
        Gizmos.DrawSphere(_currentFootPos, 0.05f);
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(GetRestWorldPosition(), 0.04f);
    }
}