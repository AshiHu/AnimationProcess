// ============================================================
//  SpiderSetupWindow.cs — Assets/Scripts/Editor/
//  Fenêtre Unity (Tools → Spider Setup) qui génère automatiquement
//  la hiérarchie complète de l'araignée procédurale.
//
//  UTILISATION :
//  1. Ouvre la fenêtre : menu Unity → Tools → Spider Setup
//  2. Laisse le champ "Corps" vide pour créer un tout nouvel objet,
//     ou glisses-y un GameObject existant pour y greffer les pattes.
//  3. Clique sur GÉNÉRER.
//  4. Lance Play et utilise ZQSD pour te déplacer.
//     (Place ton araignée au-dessus d'un sol avec un Collider !)
// ============================================================

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SpiderSetupWindow : EditorWindow
{
    // ── Paramètres de la fenêtre ──
    private GameObject corpsGO;

    [MenuItem("Tools/Spider Setup")]
    public static void Open() => GetWindow<SpiderSetupWindow>("Spider Setup");

    // ════════════════════════════════════════════════════════
    //  INTERFACE GRAPHIQUE
    // ════════════════════════════════════════════════════════

    void OnGUI()
    {
        GUILayout.Space(6);
        GUILayout.Label("Générateur d'Araignée Procédurale", EditorStyles.boldLabel);
        GUILayout.Space(4);

        EditorGUILayout.HelpBox(
            "Laisse 'Corps' vide → un nouveau GameObject 'Araignee' sera créé.\n" +
            "Assigne un objet existant → les pattes y seront ajoutées.",
            MessageType.Info);

        GUILayout.Space(8);

        corpsGO = (GameObject)EditorGUILayout.ObjectField(
            "Corps (optionnel)", corpsGO, typeof(GameObject), true);

        GUILayout.Space(10);

        // Bouton : supprimer les pattes existantes
        GUI.backgroundColor = new Color(1f, 0.45f, 0.45f);
        if (GUILayout.Button("Supprimer les pattes", GUILayout.Height(28)))
        {
            if (corpsGO != null)
            {
                Cleanup();
                EditorUtility.SetDirty(corpsGO);
                EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            }
        }

        GUILayout.Space(4);

        // Bouton : générer l'araignée complète
        GUI.backgroundColor = new Color(0.4f, 1f, 0.5f);
        if (GUILayout.Button("GÉNÉRER L'ARAIGNÉE COMPLÈTE", GUILayout.Height(50)))
            Build();

        GUI.backgroundColor = Color.white;
    }

    // ════════════════════════════════════════════════════════
    //  NETTOYAGE
    // ════════════════════════════════════════════════════════

    void Cleanup()
    {
        // Supprimer tous les enfants générés par ce script
        for (int i = corpsGO.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = corpsGO.transform.GetChild(i);
            if (child.name.StartsWith("Leg_")   ||
                child.name.StartsWith("Repos_")  ||
                child.name.StartsWith("Pole_")   ||
                child.name == "CameraTarget")
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    // ════════════════════════════════════════════════════════
    //  CONSTRUCTION DE L'ARAIGNÉE
    // ════════════════════════════════════════════════════════

    void Build()
    {
        // Créer le corps si aucun n'est fourni
        if (corpsGO == null)
        {
            corpsGO = new GameObject("Araignee");
            Undo.RegisterCreatedObjectUndo(corpsGO, "Créer Araignée");
        }

        Undo.RecordObject(corpsGO, "Générer Araignée");
        Cleanup();

        // ── Composants du corps ──
        ConfigurerCorps();

        // ── CameraTarget ──
        var camTarget = new GameObject("CameraTarget");
        camTarget.transform.SetParent(corpsGO.transform);
        camTarget.transform.localPosition = new Vector3(0f, 3f, -6f);
        camTarget.transform.localRotation = Quaternion.identity;

        // Assigner la cibleCamera via SerializedObject (champ private [SerializeField])
        var ctrl = corpsGO.GetComponent<SpiderController>();
        var soCtrl = new SerializedObject(ctrl);
        soCtrl.FindProperty("cibleCamera").objectReferenceValue = camTarget.transform;
        soCtrl.ApplyModifiedProperties();

        // ════════════════════════════════════════════════════════
        //  DÉFINITIONS DES 8 PATTES
        //
        //  Toutes les positions sont en espace LOCAL du corps.
        //  (nom, pos_epaule, pos_repos, pos_pole)
        //
        //  • pos_epaule : racine de la patte (là où elle se fixe au corps)
        //  • pos_repos  : position idéale du pied au repos (sur le sol)
        //  • pos_pole   : direction vers laquelle le genou doit pointer
        // ════════════════════════════════════════════════════════
        var defs = new (string nom, Vector3 epaule, Vector3 repos, Vector3 pole)[]
        {
            // ── Gauche avant (L1, L2) ──
            ("Leg_L1", new Vector3(-0.35f, 0f,  0.45f),
                       new Vector3(-1.55f, -0.5f,  1.25f),
                       new Vector3(-1.8f,  0.3f,   1.5f)),

            ("Leg_L2", new Vector3(-0.40f, 0f,  0.15f),
                       new Vector3(-1.55f, -0.5f,  0.40f),
                       new Vector3(-1.8f,  0.3f,   0.5f)),

            // ── Gauche arrière (L3, L4) ──
            ("Leg_L3", new Vector3(-0.40f, 0f, -0.15f),
                       new Vector3(-1.55f, -0.5f, -0.40f),
                       new Vector3(-1.8f,  0.3f,  -0.5f)),

            ("Leg_L4", new Vector3(-0.35f, 0f, -0.45f),
                       new Vector3(-1.55f, -0.5f, -1.25f),
                       new Vector3(-1.8f,  0.3f,  -1.5f)),

            // ── Droite avant (R1, R2) ──
            ("Leg_R1", new Vector3( 0.35f, 0f,  0.45f),
                       new Vector3( 1.55f, -0.5f,  1.25f),
                       new Vector3( 1.8f,  0.3f,   1.5f)),

            ("Leg_R2", new Vector3( 0.40f, 0f,  0.15f),
                       new Vector3( 1.55f, -0.5f,  0.40f),
                       new Vector3( 1.8f,  0.3f,   0.5f)),

            // ── Droite arrière (R3, R4) ──
            ("Leg_R3", new Vector3( 0.40f, 0f, -0.15f),
                       new Vector3( 1.55f, -0.5f, -0.40f),
                       new Vector3( 1.8f,  0.3f,  -0.5f)),

            ("Leg_R4", new Vector3( 0.35f, 0f, -0.45f),
                       new Vector3( 1.55f, -0.5f, -1.25f),
                       new Vector3( 1.8f,  0.3f,  -1.5f)),
        };

        SpiderLeg[] pattes = new SpiderLeg[8];
        for (int i = 0; i < defs.Length; i++)
        {
            var d = defs[i];
            pattes[i] = CreerPatte(d.nom, d.epaule, d.repos, d.pole);
        }

        // ── Couplage diagonal pour le gait (pattes alternées) ──
        // Les paires diagonales ne bougent jamais en même temps
        CouplePattes(pattes[0], pattes[7]); // L1 ↔ R4
        CouplePattes(pattes[1], pattes[6]); // L2 ↔ R3
        CouplePattes(pattes[2], pattes[5]); // L3 ↔ R2
        CouplePattes(pattes[3], pattes[4]); // L4 ↔ R1

        // Finaliser
        EditorUtility.SetDirty(corpsGO);
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());

        EditorUtility.DisplayDialog(
            "Araignée générée !",
            "Hiérarchie complète créée avec succès.\n\n" +
            "ÉTAPES SUIVANTES :\n" +
            "① Crée un plan (Plane) sous l'araignée avec un collider\n" +
            "② Lance Play (▶)\n" +
            "③ Déplace-toi avec Z / S / Q / D",
            "Super !");
    }

    // ════════════════════════════════════════════════════════
    //  CONFIGURATION DU CORPS
    // ════════════════════════════════════════════════════════

    void ConfigurerCorps()
    {
        // Collider sphérique pour le corps
        if (!corpsGO.GetComponent<Collider>())
        {
            var col = corpsGO.AddComponent<CapsuleCollider>();
            col.radius = 0.35f;
            col.height = 0.6f;
        }

        // Rigidbody cinématique (le déplacement est géré entièrement par SpiderController)
        var rb = corpsGO.GetComponent<Rigidbody>() ?? corpsGO.AddComponent<Rigidbody>();
        rb.mass                   = 1f;
        rb.isKinematic            = true;
        rb.interpolation          = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.freezeRotation         = true;

        // SpiderController
        if (!corpsGO.GetComponent<SpiderController>())
            corpsGO.AddComponent<SpiderController>();

        // Petit mesh pour visualiser le corps (ne pas recréer si déjà présent)
        if (corpsGO.transform.Find("Mesh_Corps") == null)
        {
            var corps = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            corps.name = "Mesh_Corps";
            corps.transform.SetParent(corpsGO.transform);
            corps.transform.localPosition = Vector3.zero;
            corps.transform.localScale    = new Vector3(0.7f, 0.4f, 1.0f);
            DestroyImmediate(corps.GetComponent<SphereCollider>());
        }
    }

    // ════════════════════════════════════════════════════════
    //  CRÉATION D'UNE PATTE
    // ════════════════════════════════════════════════════════

    /// <summary>
    /// Crée la hiérarchie complète d'une patte et configure son SpiderLeg
    /// via SerializedObject pour accéder aux champs private [SerializeField].
    /// </summary>
    SpiderLeg CreerPatte(string nom, Vector3 localEpaule, Vector3 localRepos, Vector3 localPole)
    {
        // ── Racine de la patte (enfant du corps) ──
        var racine = new GameObject(nom);
        racine.transform.SetParent(corpsGO.transform);
        racine.transform.localPosition = localEpaule;
        racine.transform.localRotation = Quaternion.identity;

        // ── Os Hanche ──
        var hancheGO = new GameObject("Hanche_" + nom);
        hancheGO.transform.SetParent(racine.transform);
        hancheGO.transform.localPosition = Vector3.zero;
        hancheGO.transform.localRotation = Quaternion.identity;

        // Mesh cuisse (Capsule orientée Y, l'IK la réorientera en Z via offsetRotationOs)
        var cuisse = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        cuisse.name = "Cuisse";
        cuisse.transform.SetParent(hancheGO.transform);
        cuisse.transform.localPosition = new Vector3(0f, 0.5f, 0f); // Centre du segment
        cuisse.transform.localRotation = Quaternion.identity;
        cuisse.transform.localScale    = new Vector3(0.10f, 0.50f, 0.10f);
        DestroyImmediate(cuisse.GetComponent<CapsuleCollider>());

        // ── Os Genou (à la fin du segment hanche, en Y local) ──
        var genouGO = new GameObject("Genou_" + nom);
        genouGO.transform.SetParent(hancheGO.transform);
        genouGO.transform.localPosition = new Vector3(0f, 1.0f, 0f); // = longueurHanche
        genouGO.transform.localRotation = Quaternion.identity;

        // Mesh tibia
        var tibia = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tibia.name = "Tibia";
        tibia.transform.SetParent(genouGO.transform);
        tibia.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        tibia.transform.localRotation = Quaternion.identity;
        tibia.transform.localScale    = new Vector3(0.08f, 0.50f, 0.08f);
        DestroyImmediate(tibia.GetComponent<CapsuleCollider>());

        // Sphère de pied (extrémité visuelle)
        var piedMesh = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        piedMesh.name = "Pied";
        piedMesh.transform.SetParent(genouGO.transform);
        piedMesh.transform.localPosition = new Vector3(0f, 1.0f, 0f); // = longueurGenou
        piedMesh.transform.localScale    = Vector3.one * 0.12f;
        DestroyImmediate(piedMesh.GetComponent<SphereCollider>());

        // ── Ancre de repos ──
        // Enfant du corps (localPosition = en espace corps), indique où le pied doit reposer.
        var reposGO = new GameObject("Repos_" + nom);
        reposGO.transform.SetParent(corpsGO.transform);  // CORPS, pas racine
        reposGO.transform.localPosition = localRepos;
        reposGO.transform.localRotation = Quaternion.identity;

        // ── Cible de pôle ──
        // Indique la direction vers laquelle le genou doit pointer.
        var poleGO = new GameObject("Pole_" + nom);
        poleGO.transform.SetParent(corpsGO.transform);   // CORPS, pas racine
        poleGO.transform.localPosition = localPole;
        poleGO.transform.localRotation = Quaternion.identity;

        // ── Composant SpiderLeg ──
        var leg = racine.AddComponent<SpiderLeg>();

        // Utiliser SerializedObject pour assigner les champs private [SerializeField]
        // (les champs publics comme patteOpposee sont assignés directement)
        var so = new SerializedObject(leg);

        so.FindProperty("hanche").objectReferenceValue      = hancheGO.transform;
        so.FindProperty("genou").objectReferenceValue       = genouGO.transform;
        so.FindProperty("poleCible").objectReferenceValue   = poleGO.transform;
        so.FindProperty("ancreRepos").objectReferenceValue  = reposGO.transform;

        // Longueurs des segments (doivent correspondre aux positions des os ci-dessus)
        so.FindProperty("longueurHanche").floatValue    = 1.0f;
        so.FindProperty("longueurGenou").floatValue     = 1.0f;
        so.FindProperty("extensionMax").floatValue      = 0.88f;

        // Offset de rotation des os
        // (90, 0, 0) car les capsules Unity sont modélisées le long de l'axe Y,
        // mais LookRotation oriente l'axe Z → on compense avec un pivot de 90° sur X
        so.FindProperty("offsetRotationOs").vector3Value = new Vector3(90f, 0f, 0f);

        // Paramètres de pas
        so.FindProperty("seuilPas").floatValue      = 0.50f;
        so.FindProperty("hauteurArche").floatValue  = 0.22f;
        so.FindProperty("dureePas").floatValue      = 0.14f;
        so.FindProperty("anticipation").floatValue  = 0.15f;
        so.FindProperty("offsetSurface").floatValue = 0.03f;
        so.FindProperty("porteeRaycast").floatValue = 2.0f;

        so.ApplyModifiedProperties();

        // [NOUVEAU] Côté du corps (L = gauche, R = droite) pour le différentiel de virage
        leg.estCoteDroit = nom.Contains("_R");

        return leg;
    }

    // ════════════════════════════════════════════════════════
    //  COUPLAGE DES PATTES (GAIT)
    // ════════════════════════════════════════════════════════

    /// <summary>Relie deux pattes : elles ne bougeront jamais simultanément.</summary>
    void CouplePattes(SpiderLeg a, SpiderLeg b)
    {
        a.patteOpposee = b;
        b.patteOpposee = a;
        EditorUtility.SetDirty(a);
        EditorUtility.SetDirty(b);
    }
}
#endif
