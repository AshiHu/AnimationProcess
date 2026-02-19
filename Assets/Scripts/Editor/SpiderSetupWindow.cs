// ============================================================
//  Fichier : SpiderSetupWindow.cs
//  Place ce fichier dans Assets/Editor/
// ============================================================

#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

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

    void Cleanup()
    {
        for (int i = _body.transform.childCount - 1; i >= 0; i--)
        {
            Transform c = _body.transform.GetChild(i);
            if (c.name.StartsWith("Leg_") || c.name.StartsWith("Pole_") ||
                c.name == "CameraTarget")
                Object.DestroyImmediate(c.gameObject);
        }
    }

    void Build()
    {
        Cleanup();

        Rigidbody rb = _body.GetComponent<Rigidbody>() ?? _body.AddComponent<Rigidbody>();
        rb.mass                   = 1f;
        rb.linearDamping          = 6f;
        rb.angularDamping         = 6f;
        rb.freezeRotation         = true;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        if (!_body.GetComponent<Collider>())
            _body.AddComponent<SphereCollider>().radius = 0.38f;

        SpiderController ctrl = _body.GetComponent<SpiderController>()
                                ?? _body.AddComponent<SpiderController>();

        GameObject ct = new GameObject("CameraTarget");
        ct.transform.SetParent(_body.transform);
        ct.transform.localPosition = new Vector3(0f, 3f, -6f);
        ctrl.cameraTarget = ct.transform;

        // -------------------------------------------------------
        //  Definitions des 8 pattes
        //  (name, shoulder_local, rest_local, pole_local)
        //
        //  CORRECTION : les poles de L1/L2/R1/R2 (pattes avant)
        //  pointent vers l'avant-bas pour que le genou se plie
        //  correctement vers l'exterieur et non vers le haut.
        // -------------------------------------------------------
        var defs = new (string name, Vector3 sh, Vector3 rest, Vector3 pole)[]
        {
            // Gauche avant  — pole vers avant-exterieur-bas
            ("Leg_L1", new Vector3(-0.35f, 0f,  0.45f), new Vector3(-1.6f, -0.5f,  1.3f), new Vector3(-1.8f, -0.3f,  1.6f)),
            ("Leg_L2", new Vector3(-0.40f, 0f,  0.15f), new Vector3(-1.6f, -0.5f,  0.4f), new Vector3(-1.8f, -0.3f,  0.6f)),
            // Gauche arriere — pole vers arriere-exterieur-bas
            ("Leg_L3", new Vector3(-0.40f, 0f, -0.15f), new Vector3(-1.6f, -0.5f, -0.4f), new Vector3(-1.8f, -0.3f, -0.6f)),
            ("Leg_L4", new Vector3(-0.35f, 0f, -0.45f), new Vector3(-1.6f, -0.5f, -1.3f), new Vector3(-1.8f, -0.3f, -1.6f)),
            // Droite avant
            ("Leg_R1", new Vector3( 0.35f, 0f,  0.45f), new Vector3( 1.6f, -0.5f,  1.3f), new Vector3( 1.8f, -0.3f,  1.6f)),
            ("Leg_R2", new Vector3( 0.40f, 0f,  0.15f), new Vector3( 1.6f, -0.5f,  0.4f), new Vector3( 1.8f, -0.3f,  0.6f)),
            // Droite arriere
            ("Leg_R3", new Vector3( 0.40f, 0f, -0.15f), new Vector3( 1.6f, -0.5f, -0.4f), new Vector3( 1.8f, -0.3f, -0.6f)),
            ("Leg_R4", new Vector3( 0.35f, 0f, -0.45f), new Vector3( 1.6f, -0.5f, -1.3f), new Vector3( 1.8f, -0.3f, -1.6f)),
        };

        SpiderLeg[] legs = new SpiderLeg[8];
        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            legs[i] = MakeLeg(d.name, d.sh, d.rest, d.pole);
        }

        // Couples diagonaux
        Pair(legs[0], legs[7]);
        Pair(legs[1], legs[6]);
        Pair(legs[2], legs[5]);
        Pair(legs[3], legs[4]);

        EditorUtility.SetDirty(_body);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        EditorUtility.DisplayDialog("OK", "Araignee generee ! Lance Play → ZQSD", "Super !");
    }

    SpiderLeg MakeLeg(string legName, Vector3 shLocal, Vector3 restLocal, Vector3 poleLocal)
    {
        GameObject root = new GameObject(legName);
        root.transform.SetParent(_body.transform);
        root.transform.localPosition = shLocal;
        root.transform.localRotation = Quaternion.identity;

        // Epaule
        GameObject sh = new GameObject("Shoulder");
        sh.transform.SetParent(root.transform);
        sh.transform.localPosition = Vector3.zero;
        sh.transform.localRotation = Quaternion.identity;

        // Mesh cuisse
        GameObject thigh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        thigh.name = "Thigh";
        thigh.transform.SetParent(sh.transform);
        thigh.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        thigh.transform.localRotation = Quaternion.identity;
        thigh.transform.localScale    = new Vector3(0.10f, 0.45f, 0.10f);
        Object.DestroyImmediate(thigh.GetComponent<CapsuleCollider>());

        // Genou
        GameObject kn = new GameObject("Knee");
        kn.transform.SetParent(sh.transform);
        kn.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        kn.transform.localRotation = Quaternion.identity;

        // Mesh tibia
        GameObject shin = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        shin.name = "Shin";
        shin.transform.SetParent(kn.transform);
        shin.transform.localPosition = new Vector3(0f, 0.45f, 0f);
        shin.transform.localRotation = Quaternion.identity;
        shin.transform.localScale    = new Vector3(0.08f, 0.45f, 0.08f);
        Object.DestroyImmediate(shin.GetComponent<CapsuleCollider>());

        // Pied
        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foot.name = "Foot";
        foot.transform.SetParent(kn.transform);
        foot.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        foot.transform.localScale    = Vector3.one * 0.11f;
        Object.DestroyImmediate(foot.GetComponent<SphereCollider>());

        // Pole
        GameObject pole = new GameObject("Pole_" + legName);
        pole.transform.SetParent(_body.transform);
        pole.transform.localPosition = poleLocal;

        // SpiderLeg
        SpiderLeg leg    = root.AddComponent<SpiderLeg>();
        leg.shoulderBone = sh.transform;
        leg.kneeBone     = kn.transform;
        leg.poleTarget   = pole.transform;
        leg.restLocal    = restLocal;
        leg.upperLen     = 0.9f;
        leg.lowerLen     = 0.9f;
        leg.stepDist     = 0.5f;
        leg.stepHeight   = 0.25f;
        leg.stepSpeed    = 9f;
        leg.groundOff    = 0.04f;

        return leg;
    }

    void Pair(SpiderLeg a, SpiderLeg b) { a.opposite = b; b.opposite = a; }
}
#endif
