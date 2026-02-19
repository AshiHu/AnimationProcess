// ============================================================
//  SPIDER SYSTEM v5 - TOUT EN UN
//  1. Supprime tous les anciens scripts spider de ton projet
//  2. Mets CE fichier dans Assets/
//  3. Tools > Spider Setup > glisse Body > GENERER > Play
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
    public float hoverHeight = 0.4f;
    public float alignSpeed  = 5f;

    [Header("Camera")]
    public Transform cameraTarget;
    public float     camSmooth = 5f;

    [HideInInspector] public Vector3 groundNormal = Vector3.up;

    Rigidbody _rb;
    Camera    _cam;

    void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.freezeRotation = true;
        _rb.interpolation  = RigidbodyInterpolation.Interpolate;
        _rb.linearDamping  = 6f;
        _rb.angularDamping = 6f;
        _cam = Camera.main;
        if (_cam) _cam.transform.SetParent(null);
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

        // Incline le corps
        Quaternion tgt = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, tgt,
                                               Time.deltaTime * alignSpeed);

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
        // Avance/recule
        float v = Input.GetAxis("Vertical");
        Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, groundNormal).normalized;
        _rb.linearVelocity = new Vector3(fwd.x * v * moveSpeed,
                                         _rb.linearVelocity.y,
                                         fwd.z * v * moveSpeed);

        // Hover
        if (Physics.Raycast(transform.position + Vector3.up * 0.5f,
                            Vector3.down, out RaycastHit hit, 5f))
        {
            float diff = (hit.point.y + hoverHeight) - transform.position.y;
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x,
                                             Mathf.Clamp(diff * 12f, -10f, 10f),
                                             _rb.linearVelocity.z);
        }
    }
}

// ============================================================
//  SPIDER LEG  —  IK par positionnement direct des os
// ============================================================
public class SpiderLeg : MonoBehaviour
{
    // Transforms
    public Transform shoulderBone;  // os supérieur (cuisse)
    public Transform kneeBone;      // os inférieur (tibia)
    public Transform poleTarget;    // guide la flexion du genou

    // Longueurs
    public float upperLen = 0.55f;
    public float lowerLen = 0.55f;

    // Pas
    public float stepDist   = 0.32f;
    public float stepHeight = 0.16f;
    public float stepSpeed  = 9f;
    public float groundOff  = 0.04f;

    // Repos en espace local du body
    public Vector3 restLocal;

    // Synchronisation
    public SpiderLeg opposite;

    // ---- runtime ----
    [HideInInspector] public bool isStepping;
    Vector3          _footPos;   // position monde courante du pied
    Vector3          _footFrom;
    Vector3          _footTo;
    float            _stepT = 1f;
    SpiderController _body;

    // -------------------------------------------------------
    void Start()
    {
        _body = GetComponentInParent<SpiderController>();
        // Place le pied au sol sous la position de repos
        _footPos = SnapToGround(RestWorld());
        _footTo  = _footPos;
    }

    // -------------------------------------------------------
    void LateUpdate()
    {
        // --- Déclenche un pas ---
        if (!isStepping && (opposite == null || !opposite.isStepping))
        {
            if (Vector3.Distance(_footPos, RestWorld()) > stepDist)
                StartStep();
        }

        // --- Anime le pas ---
        if (isStepping)
        {
            _stepT += Time.deltaTime * stepSpeed;
            float c = Mathf.Clamp01(_stepT);
            Vector3 flat = Vector3.Lerp(_footFrom, _footTo, c);
            _footPos = flat + Vector3.up * (Mathf.Sin(c * Mathf.PI) * stepHeight);
            if (c >= 1f) { _footPos = _footTo; isStepping = false; }
        }

        // --- Résout l'IK ---
        if (shoulderBone != null && kneeBone != null)
            Solve2BoneIK();
    }

    // -------------------------------------------------------
    void StartStep()
    {
        _footFrom  = _footPos;
        _footTo    = SnapToGround(RestWorld());
        _stepT     = 0f;
        isStepping = true;
    }

    Vector3 SnapToGround(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 1.5f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 4f))
            return hit.point + Vector3.up * groundOff;
        return worldPos;
    }

    // -------------------------------------------------------
    //  IK 2 os — positionnement direct, robuste
    // -------------------------------------------------------
    void Solve2BoneIK()
    {
        Vector3 root   = shoulderBone.position;
        Vector3 target = _footPos;

        float L1   = upperLen;
        float L2   = lowerLen;
        float maxR = L1 + L2 - 0.001f;
        float minR = Mathf.Abs(L1 - L2) + 0.001f;

        // Direction et distance vers le pied
        Vector3 toTarget = target - root;
        float dist = toTarget.magnitude;

        // Si le pied est trop proche ou à l'origine, on évite le NaN
        if (dist < 0.01f) return;

        // Clamp distance
        dist = Mathf.Clamp(dist, minR, maxR);
        Vector3 dir = toTarget / toTarget.magnitude;   // direction normalisée AVANT clamp
        Vector3 clampedTarget = root + dir * dist;

        // Angle à la racine (loi des cosinus)
        float cosAlpha = (L1 * L1 + dist * dist - L2 * L2) / (2f * L1 * dist);
        cosAlpha = Mathf.Clamp(cosAlpha, -1f, 1f);
        float alpha = Mathf.Acos(cosAlpha) * Mathf.Rad2Deg;

        // Vecteur vers le pôle pour définir le plan de l'IK
        Vector3 polePos = poleTarget != null
            ? poleTarget.position
            : root + Vector3.Cross(dir, Vector3.up).normalized * 0.5f + Vector3.up * 0.5f;

        Vector3 toPole = polePos - root;
        // Composante de toPole perpendiculaire à dir
        Vector3 perpPole = toPole - Vector3.Dot(toPole, dir) * dir;
        if (perpPole.sqrMagnitude < 0.0001f)
            perpPole = Vector3.Cross(dir, Vector3.forward);
        perpPole.Normalize();

        // Position du genou : rotation du vecteur (dir * L1) autour de perpPole
        Vector3 kneeDir = Quaternion.AngleAxis(alpha, Vector3.Cross(dir, perpPole).normalized) * dir;
        Vector3 kneePos = root + kneeDir * L1;

        // Place le joint du genou
        kneeBone.position = kneePos;

        // --- Rotations ---
        Vector3 up = _body != null ? _body.groundNormal : Vector3.up;

        // Épaule → genou
        Vector3 upperDir = kneePos - root;
        if (upperDir.sqrMagnitude > 0.0001f)
        {
            shoulderBone.rotation = Quaternion.LookRotation(upperDir.normalized, up)
                                    * Quaternion.Euler(90f, 0f, 0f);
        }

        // Genou → pied
        Vector3 lowerDir = clampedTarget - kneePos;
        if (lowerDir.sqrMagnitude > 0.0001f)
        {
            kneeBone.rotation = Quaternion.LookRotation(lowerDir.normalized, up)
                                * Quaternion.Euler(90f, 0f, 0f);
        }
    }

    // -------------------------------------------------------
    Vector3 RestWorld()
    {
        if (_body != null) return _body.transform.TransformPoint(restLocal);
        return transform.position + transform.TransformDirection(restLocal);
    }

    // -------------------------------------------------------
    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = isStepping ? Color.red : Color.green;
        Gizmos.DrawSphere(_footPos, 0.05f);
        if (_body != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(RestWorld(), 0.04f);
        }
        if (shoulderBone != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(shoulderBone.position, _footPos);
        }
    }
}

// ============================================================
//  EDITOR WINDOW
// ============================================================
#if UNITY_EDITOR
public class SpiderSetupWindow : EditorWindow
{
    GameObject _body;

    [MenuItem("Tools/Spider Setup")]
    public static void Open() => GetWindow<SpiderSetupWindow>("Spider Setup");

    void OnGUI()
    {
        GUILayout.Label("Spider Setup v5", EditorStyles.boldLabel);
        GUILayout.Space(6);
        _body = (GameObject)EditorGUILayout.ObjectField("Body", _body,
                                                         typeof(GameObject), true);
        GUILayout.Space(6);
        GUI.backgroundColor = new Color(1f, .4f, .4f);
        if (GUILayout.Button("Supprimer les pattes", GUILayout.Height(28)))
            if (_body) Cleanup();

        GUILayout.Space(4);
        GUI.backgroundColor = new Color(.4f, 1f, .5f);
        if (GUILayout.Button("GENERER L'ARAIGNEE", GUILayout.Height(44)))
        {
            if (!_body) { EditorUtility.DisplayDialog("Erreur","Glisse ton Body !","OK"); return; }
            Build();
        }
        GUI.backgroundColor = Color.white;
    }

    // -------------------------------------------------------
    void Cleanup()
    {
        for (int i = _body.transform.childCount - 1; i >= 0; i--)
        {
            var c = _body.transform.GetChild(i);
            if (c.name.StartsWith("Leg_") || c.name.StartsWith("Pole_") ||
                c.name == "CameraTarget")
                DestroyImmediate(c.gameObject);
        }
    }

    // -------------------------------------------------------
    void Build()
    {
        Cleanup();

        // Rigidbody
        var rb = _body.GetComponent<Rigidbody>() ?? _body.AddComponent<Rigidbody>();
        rb.mass       = 1f;
        rb.linearDamping  = 6f;
        rb.angularDamping = 6f;
        rb.freezeRotation = true;
        rb.interpolation  = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        // Collider
        if (!_body.GetComponent<Collider>())
            _body.AddComponent<SphereCollider>().radius = 0.38f;

        // Controller
        var ctrl = _body.GetComponent<SpiderController>()
                   ?? _body.AddComponent<SpiderController>();

        // CameraTarget
        var ct = new GameObject("CameraTarget");
        ct.transform.SetParent(_body.transform);
        ct.transform.localPosition = new Vector3(0f, 2f, -4.5f);
        ctrl.cameraTarget = ct.transform;

        // -------------------------------------------------------
        // 8 pattes
        // shoulder = position épaule (local body)
        // rest     = position repos pied (local body)
        // pole     = hint direction genou (local body)
        // -------------------------------------------------------
        var defs = new[]
        {
            // Gauche
            ("Leg_L1", new Vector3(-0.30f,0f, 0.40f), new Vector3(-1.0f,-0.4f, 0.85f), new Vector3(-1.1f,0.5f, 0.6f)),
            ("Leg_L2", new Vector3(-0.34f,0f, 0.13f), new Vector3(-1.0f,-0.4f, 0.25f), new Vector3(-1.1f,0.5f, 0.1f)),
            ("Leg_L3", new Vector3(-0.34f,0f,-0.13f), new Vector3(-1.0f,-0.4f,-0.25f), new Vector3(-1.1f,0.5f,-0.1f)),
            ("Leg_L4", new Vector3(-0.30f,0f,-0.40f), new Vector3(-1.0f,-0.4f,-0.85f), new Vector3(-1.1f,0.5f,-0.6f)),
            // Droite
            ("Leg_R1", new Vector3( 0.30f,0f, 0.40f), new Vector3( 1.0f,-0.4f, 0.85f), new Vector3( 1.1f,0.5f, 0.6f)),
            ("Leg_R2", new Vector3( 0.34f,0f, 0.13f), new Vector3( 1.0f,-0.4f, 0.25f), new Vector3( 1.1f,0.5f, 0.1f)),
            ("Leg_R3", new Vector3( 0.34f,0f,-0.13f), new Vector3( 1.0f,-0.4f,-0.25f), new Vector3( 1.1f,0.5f,-0.1f)),
            ("Leg_R4", new Vector3( 0.30f,0f,-0.40f), new Vector3( 1.0f,-0.4f,-0.85f), new Vector3( 1.1f,0.5f,-0.6f)),
        };

        SpiderLeg[] legs = new SpiderLeg[8];
        for (int i = 0; i < defs.Length; i++)
        {
            var (n, sh, rest, pl) = defs[i];
            legs[i] = MakeLeg(n, sh, rest, pl);
        }

        // Couples diagonaux
        Pair(legs[0], legs[7]);
        Pair(legs[1], legs[6]);
        Pair(legs[2], legs[5]);
        Pair(legs[3], legs[4]);

        EditorUtility.SetDirty(_body);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("OK","Araignee generee ! Lance Play → ZQSD","Super !");
    }

    // -------------------------------------------------------
    SpiderLeg MakeLeg(string legName, Vector3 shLocal, Vector3 restLocal, Vector3 poleLocal)
    {
        // Racine
        var root = new GameObject(legName);
        root.transform.SetParent(_body.transform);
        root.transform.localPosition = shLocal;
        root.transform.localRotation = Quaternion.identity;

        // Épaule (joint 1)
        var sh = new GameObject("Shoulder");
        sh.transform.SetParent(root.transform);
        sh.transform.localPosition = Vector3.zero;
        sh.transform.localRotation = Quaternion.identity;

        // Mesh cuisse
        var thigh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        thigh.name = "Thigh";
        thigh.transform.SetParent(sh.transform);
        thigh.transform.localPosition = new Vector3(0f, 0.27f, 0f);
        thigh.transform.localRotation = Quaternion.identity;
        thigh.transform.localScale    = new Vector3(0.09f, 0.27f, 0.09f);
        DestroyImmediate(thigh.GetComponent<CapsuleCollider>());

        // Genou (joint 2)
        var kn = new GameObject("Knee");
        kn.transform.SetParent(sh.transform);
        kn.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        kn.transform.localRotation = Quaternion.identity;

        // Mesh tibia
        var shin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shin.name = "Shin";
        shin.transform.SetParent(kn.transform);
        shin.transform.localPosition = new Vector3(0f, 0.27f, 0f);
        shin.transform.localRotation = Quaternion.identity;
        shin.transform.localScale    = new Vector3(0.07f, 0.27f, 0.07f);
        DestroyImmediate(shin.GetComponent<CapsuleCollider>());

        // Pied
        var foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foot.name = "Foot";
        foot.transform.SetParent(kn.transform);
        foot.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        foot.transform.localScale    = Vector3.one * 0.09f;
        DestroyImmediate(foot.GetComponent<SphereCollider>());

        // Pole
        var pole = new GameObject("Pole_" + legName);
        pole.transform.SetParent(_body.transform);
        pole.transform.localPosition = poleLocal;

        // Composant
        var leg = root.AddComponent<SpiderLeg>();
        leg.shoulderBone = sh.transform;
        leg.kneeBone     = kn.transform;
        leg.poleTarget   = pole.transform;
        leg.restLocal    = restLocal;
        leg.upperLen     = 0.55f;
        leg.lowerLen     = 0.55f;
        leg.stepDist     = 0.32f;
        leg.stepHeight   = 0.16f;
        leg.stepSpeed    = 9f;
        leg.groundOff    = 0.04f;

        return leg;
    }

    void Pair(SpiderLeg a, SpiderLeg b) { a.opposite = b; b.opposite = a; }
}
#endif
