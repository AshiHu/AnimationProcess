using UnityEngine;
using UnityEditor;

public class SpiderGenerator : EditorWindow
{
    private GameObject spiderRoot;

    [MenuItem("Tools/Générateur Araignée")]
    public static void ShowWindow()
    {
        GetWindow<SpiderGenerator>("Générateur Araignée");
    }

    void OnGUI()
    {
        GUILayout.Label("🕷️ Générateur d'Araignée", EditorStyles.boldLabel);
        GUILayout.Space(10);

        spiderRoot = (GameObject)EditorGUILayout.ObjectField(
            "Objet Body (racine)",
            spiderRoot,
            typeof(GameObject), true);

        GUILayout.Space(10);

        if (GUILayout.Button("🗑️ Supprimer les pattes existantes", GUILayout.Height(30)))
        {
            if (spiderRoot != null) DeleteExistingLegs();
        }

        GUILayout.Space(5);

        if (GUILayout.Button("✅ Générer les 4 pattes", GUILayout.Height(40)))
        {
            if (spiderRoot == null)
            {
                EditorUtility.DisplayDialog("Erreur", "Assigne ton objet Body !", "OK");
                return;
            }
            GenerateSpider();
        }
    }

    void DeleteExistingLegs()
    {
        string[] legNames = {
            "DevantGauche", "DevantDroite", "DerrièreGauche", "DerrièreDroite",
            "Pivot_", "pivotGD", "pivotDD", "pivotDA", "pivotGA",
            "ArticulationCCDG", "CameraTarget"
        };

        for (int i = spiderRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = spiderRoot.transform.GetChild(i);
            foreach (string n in legNames)
            {
                if (child.name.StartsWith(n))
                {
                    DestroyImmediate(child.gameObject);
                    break;
                }
            }
        }
        Debug.Log("Pattes supprimées.");
    }

    void GenerateSpider()
    {
        DeleteExistingLegs();

        // shoulderOffset = position épaule (proche du corps)
        // footRestOffset = position repos du pied (loin, au sol)
        // poleOffset     = direction du genou
        var legs = new (string name, Vector3 shoulder, Vector3 footRest, Vector3 pole)[]
        {
            ("DevantGauche",
                new Vector3(-0.45f,  0f,  0.5f),
                new Vector3(-1.1f,  -0.5f,  1.1f),
                new Vector3(-1.3f,   0.5f,  0.8f)
            ),
            ("DevantDroite",
                new Vector3( 0.45f,  0f,  0.5f),
                new Vector3( 1.1f,  -0.5f,  1.1f),
                new Vector3( 1.3f,   0.5f,  0.8f)
            ),
            ("DerrièreGauche",
                new Vector3(-0.45f,  0f, -0.5f),
                new Vector3(-1.1f,  -0.5f, -1.1f),
                new Vector3(-1.3f,   0.5f, -0.8f)
            ),
            ("DerrièreDroite",
                new Vector3( 0.45f,  0f, -0.5f),
                new Vector3( 1.1f,  -0.5f, -1.1f),
                new Vector3( 1.3f,   0.5f, -0.8f)
            ),
        };

        SpiderLeg[] createdLegs = new SpiderLeg[legs.Length];

        for (int i = 0; i < legs.Length; i++)
        {
            var (name, shoulder, footRest, pole) = legs[i];
            createdLegs[i] = CreateLeg(name, shoulder, footRest, pole);
        }

        // Pattes opposées en diagonale : ne bougent pas en même temps
        AssignOpposites(createdLegs[0], createdLegs[3]); // AvantG <-> DerrièreD
        AssignOpposites(createdLegs[1], createdLegs[2]); // AvantD <-> DerrièreG

        // CameraTarget
        GameObject ct = new GameObject("CameraTarget");
        ct.transform.SetParent(spiderRoot.transform);
        ct.transform.localPosition = new Vector3(0, 2.5f, -5f);

        SpiderController sc = spiderRoot.GetComponent<SpiderController>();
        if (sc != null) sc.cameraTarget = ct.transform;

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("✅ Araignée générée !");
        EditorUtility.DisplayDialog("✅ Succès", "Araignée générée avec succès !", "OK");
    }

    SpiderLeg CreateLeg(string legName, Vector3 shoulderOffset, Vector3 footRestOffset, Vector3 poleOffset)
    {
        // Racine = position de l'épaule, collée au corps
        GameObject legRoot = new GameObject(legName);
        legRoot.transform.SetParent(spiderRoot.transform);
        legRoot.transform.localPosition = shoulderOffset;
        legRoot.transform.localRotation = Quaternion.identity;

        // Joint Épaule (pivot IK upper)
        GameObject shoulderJoint = new GameObject("Joint_Epaule");
        shoulderJoint.transform.SetParent(legRoot.transform);
        shoulderJoint.transform.localPosition = Vector3.zero;

        // Mesh cuisse
        GameObject cuisseMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cuisseMesh.name = "Cuisse_Mesh";
        cuisseMesh.transform.SetParent(shoulderJoint.transform);
        cuisseMesh.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        cuisseMesh.transform.localScale = new Vector3(0.1f, 0.25f, 0.1f);
        DestroyImmediate(cuisseMesh.GetComponent<CapsuleCollider>());

        // Joint Genou (pivot IK lower)
        GameObject kneeJoint = new GameObject("Joint_Genou");
        kneeJoint.transform.SetParent(shoulderJoint.transform);
        kneeJoint.transform.localPosition = new Vector3(0f, -0.5f, 0f);

        // Mesh molet
        GameObject moletMesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        moletMesh.name = "Molet_Mesh";
        moletMesh.transform.SetParent(kneeJoint.transform);
        moletMesh.transform.localPosition = new Vector3(0f, -0.25f, 0f);
        moletMesh.transform.localScale = new Vector3(0.08f, 0.25f, 0.08f);
        DestroyImmediate(moletMesh.GetComponent<CapsuleCollider>());

        // Pied
        GameObject foot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        foot.name = "Pied";
        foot.transform.SetParent(kneeJoint.transform);
        foot.transform.localPosition = new Vector3(0f, -0.5f, 0f);
        foot.transform.localScale = Vector3.one * 0.07f;
        DestroyImmediate(foot.GetComponent<SphereCollider>());

        // Pivot IK (contrôle direction du genou)
        GameObject pivot = new GameObject("Pivot_" + legName);
        pivot.transform.SetParent(spiderRoot.transform);
        pivot.transform.localPosition = poleOffset;

        // SpiderLeg
        SpiderLeg leg = legRoot.AddComponent<SpiderLeg>();
        leg.upperLeg     = shoulderJoint.transform;
        leg.lowerLeg     = kneeJoint.transform;
        leg.foot         = foot.transform;
        leg.polePivot    = pivot.transform;
        leg.restOffset   = footRestOffset; // ← DIFFERENT de shoulderOffset !
        leg.upperLength  = 0.55f;
        leg.lowerLength  = 0.55f;
        leg.stepDistance = 0.35f;
        leg.stepHeight   = 0.18f;
        leg.stepSpeed    = 9f;
        leg.groundOffset = 0.03f;

        return leg;
    }

    void AssignOpposites(SpiderLeg a, SpiderLeg b)
    {
        a.oppositeLeg = b;
        b.oppositeLeg = a;
    }
}
