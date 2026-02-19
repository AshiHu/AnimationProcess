// ============================================================
//  SPIDER SYSTEM v4
//  1. Supprime tous tes anciens scripts spider
//  2. Copie CE fichier dans Assets/
//  3. Tools > Spider Setup > Glisse Body > GENERER > Play
// ============================================================

using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

// ============================================================
//  SPIDER CONTROLLER
// ============================================================
[RequireComponent(typeof(Rigidbody))]
public class SpiderController : MonoBehaviour
{
    [Header("Mouvement")]
    public float moveSpeed   = 3f;
    public float rotateSpeed = 90f;

    [Header("Sol")]
    public float hoverHeight = 0.35f;
    public float alignSpeed  = 6f;

    [Header("Camera")]
    public Transform cameraTarget;
    public float camSmooth = 5f;

    [HideInInspector] public Vector3 groundNormal = Vector3.up;

    Rigidbody _rb;
    Camera    _cam;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.interpolation  = RigidbodyInterpolation.Interpolate;
        _rb.linearDamping  = 5f;
        _rb.angularDamping = 5f;
        _cam = Camera.main;
        if (_cam) _cam.transform.SetParent(null);
    }

    void Update()
    {
        float h = Input.GetAxis("Horizontal");
        transform.Rotate(0f, h * rotateSpeed * Time.deltaTime, 0f, Space.Self);
        UpdateGroundNormal();
        AlignBodyRotation();
        MoveCamera();
    }

    void FixedUpdate()
    {
        float v = Input.GetAxis("Vertical");
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        _rb.linearVelocity = new Vector3(fwd.x * v * moveSpeed, _rb.linearVelocity.y, fwd.z * v * moveSpeed);

        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 4f))
        {
            float diff = (hit.point.y + hoverHeight) - transform.position.y;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, Mathf.Clamp(diff * 10f, -8f, 8f), _rb.linearVelocity.z);
        }
    }

    void UpdateGroundNormal()
    {
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out RaycastHit hit, 3f))
            groundNormal = Vector3.Lerp(groundNormal, hit.normal, Time.deltaTime * alignSpeed).normalized;
    }

    void AlignBodyRotation()
    {
        Quaternion t = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, t, Time.deltaTime * alignSpeed);
    }

    void MoveCamera()
    {
        if (!cameraTarget || !_cam) return;
        _cam.transform.position = Vector3.Lerp(_cam.transform.position, cameraTarget.position, Time.deltaTime * camSmooth);
        _cam.transform.LookAt(transform.position + Vector3.up * 0.3f);
    }
}

// ============================================================
//  SPIDER LEG
// ============================================================
public class SpiderLeg : MonoBehaviour
{
    [Header("Joints IK")]
    public Transform shoulderJoint;
    public Transform kneeJoint;

    [Header("Longueurs")]
    public float upperLen = 0.55f;
    public float lowerLen = 0.55f;

    [Header("Pole")]
    public Transform pole;

    [Header("Pas")]
    public float stepDist   = 0.32f;
    public float stepHeight = 0.15f;
    public float stepSpeed  = 10f;
    public float floorOff   = 0.04f;

    [Header("Repos local (body space)")]
    public Vector3 restLocal;

    [Header("Patte opposee")]
    public SpiderLeg opposite;

    [HideInInspector] public bool isStepping;

    Vector3 _cur, _tgt, _from;
    float   _t = 1f;
    SpiderController _body;

    void Start()
    {
        _body = GetComponentInParent<SpiderController>();
        // Initialise le pied sur le sol sous la position de repos
        Vector3 rest = RestWorld();
        if (Physics.Raycast(rest + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f))
            _cur = hit.point + Vector3.up * floorOff;
        else
            _cur = rest;
        _tgt = _cur;
    }

    void LateUpdate()
    {
        // Declenche un pas si le pied est trop loin de sa position de repos
        if (!isStepping && (opposite == null || !opposite.isStepping))
        {
            if (Vector3.Distance(_cur, RestWorld()) > stepDist)
                BeginStep();
        }

        // Anime le pas
        if (isStepping)
        {
            _t += Time.deltaTime * stepSpeed;
            float c = Mathf.Clamp01(_t);
            _cur = Vector3.Lerp(_from, _tgt, c) + Vector3.up * (Mathf.Sin(c * Mathf.PI) * stepHeight);
            if (c >= 1f) { _cur = _tgt; isStepping = false; }
        }

        SolveIK();
    }

    void BeginStep()
    {
        Vector3 rest = RestWorld();
        if (Physics.Raycast(rest + Vector3.up * 1.5f, Vector3.down, out RaycastHit hit, 4f))
            _tgt = hit.point + Vector3.up * floorOff;
        else
            _tgt = rest;
        _from      = _cur;
        _t         = 0f;
        isStepping = true;
    }

    void SolveIK()
    {
        if (!shoulderJoint || !kneeJoint) return;

        Vector3 A       = shoulderJoint.position;
        Vector3 target  = _cur;
        Vector3 polePos = pole ? pole.position : A + Vector3.up;

        float L1   = upperLen;
        float L2   = lowerLen;
        float dist = Mathf.Clamp(Vector3.Distance(A, target),
                                 Mathf.Abs(L1 - L2) + 0.001f,
                                 L1 + L2 - 0.001f);

        Vector3 dir = (target - A).normalized;

        // Loi des cosinus -> angle epaule
        float cosA = Mathf.Clamp((L1*L1 + dist*dist - L2*L2) / (2f*L1*dist), -1f, 1f);
        float angA = Mathf.Acos(cosA) * Mathf.Rad2Deg;

        // Axe IK perpendiculaire au plan (dir, pole)
        Vector3 toPole = (polePos - A).normalized;
        Vector3 axis   = Vector3.Cross(dir, toPole);
        if (axis.sqrMagnitude < 0.0001f)
            axis = Vector3.Cross(dir, Vector3.up);
        axis.Normalize();

        // Position du genou
        Vector3 kneePos = A + Quaternion.AngleAxis(-angA, axis) * (dir * L1);
        kneeJoint.position = kneePos;

        // Reference UP = normale du sol
        Vector3 up = _body ? _body.groundNormal : Vector3.up;

        // Oriente epaule vers genou
        Vector3 d1 = kneePos - A;
        if (d1.sqrMagnitude > 0.0001f)
            shoulderJoint.rotation = Quaternion.LookRotation(d1.normalized, up) * Quaternion.Euler(90f, 0f, 0f);

        // Oriente genou vers pied
        Vector3 d2 = target - kneePos;
        if (d2.sqrMagnitude > 0.0001f)
            kneeJoint.rotation = Quaternion.LookRotation(d2.normalized, up) * Quaternion.Euler(90f, 0f, 0f);
    }

    Vector3 RestWorld() =>
        _body ? _body.transform.TransformPoint(restLocal) : transform.position;

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = isStepping ? Color.red : Color.green;
        Gizmos.DrawSphere(_cur, 0.05f);
        if (_body) { Gizmos.color = Color.yellow; Gizmos.DrawWireSphere(RestWorld(), 0.04f); }
        if (shoulderJoint) { Gizmos.color = Color.cyan; Gizmos.DrawLine(shoulderJoint.position, _cur); }
    }
}

// ============================================================
//  EDITOR
// ============================================================
#if UNITY_EDITOR
public class SpiderSetupWindow : EditorWindow
{
    GameObject _body;

    [MenuItem("Tools/Spider Setup")]
    public static void Open() => GetWindow<SpiderSetupWindow>("Spider Setup");

    void OnGUI()
    {
        GUILayout.Label("Spider Setup v4", EditorStyles.boldLabel);
        GUILayout.Space(6);
        _body = (GameObject)EditorGUILayout.ObjectField("Body", _body, typeof(GameObject), true);
        GUILayout.Space(6);

        GUI.backgroundColor = new Color(1f, .4f, .4f);
        if (GUILayout.Button("Supprimer les pattes", GUILayout.Height(28)))
            if (_body) Cleanup();

        GUILayout.Space(4);
        GUI.backgroundColor = new Color(.4f, 1f, .5f);
        if (GUILayout.Button("GENERER L'ARAIGNEE", GUILayout.Height(44)))
        {
            if (!_body) { EditorUtility.DisplayDialog("Erreur", "Glisse ton Body !", "OK"); return; }
            Build();
        }
        GUI.backgroundColor = Color.white;
    }

    void Cleanup()
    {
        for (int i = _body.transform.childCount - 1; i >= 0; i--)
        {
            var c = _body.transform.GetChild(i);
            if (c.name.StartsWith("Leg_") || c.name.StartsWith("Pole_") || c.name == "CameraTarget")
                DestroyImmediate(c.gameObject);
        }
    }

    void Build()
    {
        Cleanup();

        // Rigidbody
        Rigidbody rb = _body.GetComponent<Rigidbody>() ?? _body.AddComponent<Rigidbody>();
        rb.mass      = 1f;
        rb.linearDamping  = 5f;
        rb.angularDamping = 5f;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Collider
        if (!_body.GetComponent<Collider>())
            _body.AddComponent<SphereCollider>().radius = 0.38f;

        // Controller
        SpiderController ctrl = _body.GetComponent<SpiderController>() ?? _body.AddComponent<SpiderController>();

        // CameraTarget
        var ct = new GameObject("CameraTarget");
        ct.transform.SetParent(_body.transform);
        ct.transform.localPosition = new Vector3(0f, 2f, -4.5f);
        ctrl.cameraTarget = ct.transform;

        // -------------------------------------------------------
        // Définition des 8 pattes
        // shoulderLocal = position epaule sur le body
        // restLocal     = ou le pied doit reposer (body space)
        // poleLocal     = direction du genou (body space)
        // -------------------------------------------------------
        // Les pôles sont TOUJOURS du même côté que les pattes (X)
        // et VERS LE HAUT (Y+) pour que le genou plie correctement
        var defs = new (string name, Vector3 shoulder, Vector3 rest, Vector3 poleLocal)[]
        {
            // Gauche (X negatif) - avant vers arriere
            ("Leg_L1", new Vector3(-0.30f, 0f,  0.40f), new Vector3(-1.0f, -0.4f,  0.85f), new Vector3(-1.2f, 0.6f,  0.5f)),
            ("Leg_L2", new Vector3(-0.34f, 0f,  0.13f), new Vector3(-1.0f, -0.4f,  0.25f), new Vector3(-1.2f, 0.6f,  0.2f)),
            ("Leg_L3", new Vector3(-0.34f, 0f, -0.13f), new Vector3(-1.0f, -0.4f, -0.25f), new Vector3(-1.2f, 0.6f, -0.2f)),
            ("Leg_L4", new Vector3(-0.30f, 0f, -0.40f), new Vector3(-1.0f, -0.4f, -0.85f), new Vector3(-1.2f, 0.6f, -0.5f)),
            // Droite (X positif) - avant vers arriere
            ("Leg_R1", new Vector3( 0.30f, 0f,  0.40f), new Vector3( 1.0f, -0.4f,  0.85f), new Vector3( 1.2f, 0.6f,  0.5f)),
            ("Leg_R2", new Vector3( 0.34f, 0f,  0.13f), new Vector3( 1.0f, -0.4f,  0.25f), new Vector3( 1.2f, 0.6f,  0.2f)),
            ("Leg_R3", new Vector3( 0.34f, 0f, -0.13f), new Vector3( 1.0f, -0.4f, -0.25f), new Vector3( 1.2f, 0.6f, -0.2f)),
            ("Leg_R4", new Vector3( 0.30f, 0f, -0.40f), new Vector3( 1.0f, -0.4f, -0.85f), new Vector3( 1.2f, 0.6f, -0.5f)),
        };

        SpiderLeg[] legs = new SpiderLeg[8];
        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            legs[i] = MakeLeg(d.name, d.shoulder, d.rest, d.poleLocal);
        }

        // Couples diagonaux
        Pair(legs[0], legs[7]); // L1 <-> R4
        Pair(legs[1], legs[6]); // L2 <-> R3
        Pair(legs[2], legs[5]); // L3 <-> R2
        Pair(legs[3], legs[4]); // L4 <-> R1

        EditorUtility.SetDirty(_body);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("OK", "Araignee generee !\nLance Play et utilise ZQSD.", "Super !");
    }

    SpiderLeg MakeLeg(string legName, Vector3 shoulderLocal, Vector3 restLocal, Vector3 poleLocal)
    {
        // --- Racine de la patte ---
        var root = new GameObject(legName);
        root.transform.SetParent(_body.transform);
        root.transform.localPosition = shoulderLocal;

        // Oriente la racine VERS le pied de repos (en world space)
        // => les capsules partent dans le bon sens des la génération
        Vector3 worldShoulder = _body.transform.TransformPoint(shoulderLocal);
        Vector3 worldRest     = _body.transform.TransformPoint(restLocal);
        Vector3 legDir        = (worldRest - worldShoulder).normalized;
        if (legDir == Vector3.zero) legDir = Vector3.right;
        root.transform.rotation = Quaternion.LookRotation(legDir, Vector3.up);

        // --- Joint épaule ---
        var sh = new GameObject("Shoulder");
        sh.transform.SetParent(root.transform);
        sh.transform.localPosition = Vector3.zero;
        sh.transform.localRotation = Quaternion.identity;

        // Mesh cuisse — le long de Z local (vers le pied)
        var thigh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        thigh.name = "Thigh";
        thigh.transform.SetParent(sh.transform);
        thigh.transform.localPosition = new Vector3(0f, 0f, 0.27f);
        thigh.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        thigh.transform.localScale    = new Vector3(0.09f, 0.27f, 0.09f);
        DestroyImmediate(thigh.GetComponent<CapsuleCollider>());

        // --- Joint genou ---
        var kn = new GameObject("Knee");
        kn.transform.SetParent(sh.transform);
        kn.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        kn.transform.localRotation = Quaternion.identity;

        // Mesh tibia
        var shin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shin.name = "Shin";
        shin.transform.SetParent(kn.transform);
        shin.transform.localPosition = new Vector3(0f, 0f, 0.27f);
        shin.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        shin.transform.localScale    = new Vector3(0.07f, 0.27f, 0.07f);
        DestroyImmediate(shin.GetComponent<CapsuleCollider>());

        // Pied visuel
        var foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foot.name = "Foot";
        foot.transform.SetParent(kn.transform);
        foot.transform.localPosition = new Vector3(0f, 0f, 0.55f);
        foot.transform.localScale    = Vector3.one * 0.09f;
        DestroyImmediate(foot.GetComponent<SphereCollider>());

        // --- Pole hint ---
        var pole = new GameObject("Pole_" + legName);
        pole.transform.SetParent(_body.transform);
        pole.transform.localPosition = poleLocal;

        // --- Composant SpiderLeg ---
        var leg = root.AddComponent<SpiderLeg>();
        leg.shoulderJoint = sh.transform;
        leg.kneeJoint     = kn.transform;
        leg.pole          = pole.transform;
        leg.restLocal     = restLocal;
        leg.upperLen      = 0.55f;
        leg.lowerLen      = 0.55f;
        leg.stepDist      = 0.32f;
        leg.stepHeight    = 0.15f;
        leg.stepSpeed     = 10f;
        leg.floorOff      = 0.04f;

        return leg;
    }

    void Pair(SpiderLeg a, SpiderLeg b) { a.opposite = b; b.opposite = a; }
}
#endif
