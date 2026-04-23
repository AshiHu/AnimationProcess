// ============================================================
//  SpiderController.cs
//  Contrôleur du corps de l'araignée procédurale.
//  Gère : déplacement ZQSD, alignement surface, bob, et
//  toutes les réactions secondaires du corps (animation).
// ============================================================

using UnityEngine;

// [NOUVEAU] ── Enum des états animatoires ──────────────────────
public enum AnimState { Idle, Walking, Running, Turning }
// ──────────────────────────────────────────────────────────────

[RequireComponent(typeof(Rigidbody))]
public class SpiderController : MonoBehaviour
{
    // ════════════════════════════════════════════════════════
    //  PARAMÈTRES — DÉPLACEMENT
    // ════════════════════════════════════════════════════════

    [Header("── Déplacement ──")]
    [Tooltip("Vitesse de déplacement avant/arrière (unités/seconde)")]
    [SerializeField, Range(0.5f, 15f)] private float vitesseDeplacement = 3f;

    [Tooltip("Vitesse de rotation maximale (degrés/seconde)")]
    [SerializeField, Range(10f, 360f)] private float vitesseRotation = 90f;

    // ════════════════════════════════════════════════════════
    //  PARAMÈTRES — SURFACE
    // ════════════════════════════════════════════════════════

    [Header("── Détection de surface ──")]
    [Tooltip("Portée du Raycast vers le bas local")]
    [SerializeField, Range(0.5f, 6f)]  private float porteeDetection   = 2f;
    [Tooltip("Distance cible corps ↔ surface")]
    [SerializeField, Range(0.1f, 2f)]  private float hauteurCorps      = 0.5f;
    [Tooltip("Vitesse de lissage position + rotation sur la surface")]
    [SerializeField, Range(1f, 30f)]   private float vitesseAlignement = 10f;
    [Tooltip("Vitesse de chute si aucune surface détectée")]
    [SerializeField, Range(0.1f, 20f)] private float vitesseChute      = 9.81f;
    [Tooltip("Layers marchables")]
    [SerializeField] private LayerMask masqueSurface = ~0;

    // ════════════════════════════════════════════════════════
    //  PARAMÈTRES — BOB (corps)
    // ════════════════════════════════════════════════════════

    [Header("── Bob du corps ──")]
    [SerializeField, Range(0f, 0.15f)] private float amplitudeBob = 0.06f;
    [SerializeField, Range(1f, 20f)]   private float vitesseBob   = 12f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] PARAMÈTRES — ÉTATS ANIMATOIRES
    // ════════════════════════════════════════════════════════

    [Header("── Animation — États ──")]
    [Tooltip("Vitesse minimale pour passer en Walking (unités/s)")]
    [SerializeField, Range(0.1f, 2f)]  private float seuilWalking        = 0.3f;
    [Tooltip("Vitesse pour passer en Running (unités/s)")]
    [SerializeField, Range(1f, 10f)]   private float seuilRunning         = 4f;
    [Tooltip("Vitesse angulaire (°/s) pour déclencher l'état Turning")]
    [SerializeField, Range(5f, 90f)]   private float seuilAngulaireTurning = 20f;
    [Tooltip("Durée de transition entre états (secondes)")]
    [SerializeField, Range(0.05f, 1f)] private float dureeTransition      = 0.3f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] PARAMÈTRES — RÉACTIONS SECONDAIRES DU CORPS
    // ════════════════════════════════════════════════════════

    [Header("── Animation — Corps ──")]
    [Tooltip("Amplitude de la compression/extension quand des pattes sont en vol")]
    [SerializeField, Range(0f, 0.2f)]  private float amplitudeSqueezeStretch = 0.08f;
    [Tooltip("Vitesse de réponse du squeeze/stretch")]
    [SerializeField, Range(1f, 20f)]   private float vitesseSqueezeStretch   = 6f;

    [Tooltip("Amplitude du micro-jitter de Perlin sur le corps")]
    [SerializeField, Range(0f, 0.05f)] private float amplitudeJitter   = 0.008f;
    [Tooltip("Fréquence Perlin axe X (différente des autres pour éviter la répétition)")]
    [SerializeField, Range(0.1f, 5f)]  private float frequenceJitterX  = 1.37f;
    [Tooltip("Fréquence Perlin axe Y")]
    [SerializeField, Range(0.1f, 5f)]  private float frequenceJitterY  = 0.89f;
    [Tooltip("Fréquence Perlin axe Z")]
    [SerializeField, Range(0.1f, 5f)]  private float frequenceJitterZ  = 1.61f;

    // [NOUVEAU] Inertie de rotation (spring-damper)
    [Tooltip("Force du ressort vers la vitesse de rotation cible (plus haut = plus réactif)")]
    [SerializeField, Range(1f, 30f)]   private float inertiePivot          = 8f;
    [Tooltip("Amortissement : ralentit la rotation quand l'input est relâché")]
    [SerializeField, Range(1f, 20f)]   private float amortissementPivot    = 5f;

    // ════════════════════════════════════════════════════════
    //  CAMÉRA
    // ════════════════════════════════════════════════════════

    [Header("── Caméra ──")]
    [SerializeField] private Transform cibleCamera;
    [SerializeField, Range(1f, 20f)] private float lissageCamera = 5f;

    // ════════════════════════════════════════════════════════
    //  ÉTAT INTERNE
    // ════════════════════════════════════════════════════════

    private Rigidbody   corps;
    private Camera      cam;
    private SpiderLeg[] pattes;

    // Surface
    private Vector3 normaleCorps   = Vector3.up;
    private float   hauteurCourante;
    private float   phaseBob;

    // [NOUVEAU] État animatoire courant
    private AnimState etatCourant = AnimState.Idle;

    // [NOUVEAU] Mesure de vitesse
    private Vector3 posPrecedente;
    private float   vitesseCorpsActuelle;    // unités/seconde
    private float   vitesseAngulaireActuelle; // degrés/seconde (inertie)
    private float   directionTourActuelle;   // -1..1

    // [NOUVEAU] Multiplicateurs d'animation (interpolés vers cibles par état)
    private float multiVitessePas  = 1f;
    private float multiHauteurPas  = 1f;
    private float multiJitter      = 1f;
    private float multiBob         = 1f;

    // [NOUVEAU] Squeeze/Stretch
    private float squeezeOffsetY;     // offset Y additif courant
    private float squeezeVelocity;    // pour le spring de squeezeOffsetY

    // [NOUVEAU] Seeds Perlin (différentes par axe pour décorréler)
    private float seedJitterX;
    private float seedJitterY;
    private float seedJitterZ;

    // [NOUVEAU] Coordination idle twitch entre pattes
    public int compteurSecousses; // incrémenté/décrémenté par les SpiderLeg

    // ════════════════════════════════════════════════════════
    //  PROPRIÉTÉS PUBLIQUES (consultées par SpiderLeg)
    // ════════════════════════════════════════════════════════

    public Vector3 NormaleCorps        => normaleCorps;
    public AnimState EtatCourant       => etatCourant;        // [NOUVEAU]
    public float VitesseCorps          => vitesseCorpsActuelle; // [NOUVEAU]
    public float DirectionTour         => directionTourActuelle; // [NOUVEAU] -1=gauche, +1=droite
    public float MultiVitessePas       => multiVitessePas;    // [NOUVEAU]
    public float MultiHauteurPas       => multiHauteurPas;    // [NOUVEAU]

    // ════════════════════════════════════════════════════════
    //  INITIALISATION
    // ════════════════════════════════════════════════════════

    void Awake()
    {
        corps = GetComponent<Rigidbody>();
        corps.isKinematic   = true;
        corps.interpolation = RigidbodyInterpolation.Interpolate;
        corps.freezeRotation = true;

        cam = Camera.main;
        if (cam != null) cam.transform.SetParent(null);

        hauteurCourante = hauteurCorps;
    }

    void Start()
    {
        pattes       = GetComponentsInChildren<SpiderLeg>();
        posPrecedente = transform.position;

        // [NOUVEAU] Seeds Perlin aléatoires pour chaque axe
        seedJitterX = Random.Range(0f, 100f);
        seedJitterY = Random.Range(100f, 200f);
        seedJitterZ = Random.Range(200f, 300f);
    }

    // ════════════════════════════════════════════════════════
    //  BOUCLE PRINCIPALE
    // ════════════════════════════════════════════════════════

    void Update()
    {
        DetecterEtat();              // [NOUVEAU]
        AlignerSurSurface();
        GererDeplacement();
        AppliquerSqueezeStretch();   // [NOUVEAU]
        AppliquerJitterPerlin();     // [NOUVEAU]
        AnimerBob();
        SuivreCamera();
    }

    // ════════════════════════════════════════════════════════
    //  DÉPLACEMENT (modifié : rotation avec inertie)
    // ════════════════════════════════════════════════════════

    private void AlignerSurSurface()
    {
        Vector3 dirVersBas = -transform.up;

        if (Physics.Raycast(transform.position, dirVersBas,
                            out RaycastHit hit, porteeDetection * 2f, masqueSurface))
        {
            normaleCorps = hit.normal;

            Vector3 positionCible = hit.point + hit.normal * hauteurCourante;
            transform.position = Vector3.Lerp(
                transform.position, positionCible, vitesseAlignement * Time.deltaTime);

            Quaternion deltaRot   = Quaternion.FromToRotation(transform.up, hit.normal);
            Quaternion rotCible   = deltaRot * transform.rotation;
            transform.rotation    = Quaternion.Slerp(
                transform.rotation, rotCible, vitesseAlignement * Time.deltaTime);
        }
        else
        {
            normaleCorps        = Vector3.up;
            transform.position += Vector3.down * vitesseChute * Time.deltaTime;
        }
    }

    private void GererDeplacement()
    {
        float avancer = 0f;
        if      (Input.GetKey(KeyCode.Z)) avancer =  1f;
        else if (Input.GetKey(KeyCode.S)) avancer = -1f;

        float tournerInput = 0f;
        if      (Input.GetKey(KeyCode.D)) tournerInput =  1f;
        else if (Input.GetKey(KeyCode.Q)) tournerInput = -1f;

        if (avancer != 0f)
        {
            Vector3 avantSurface = Vector3.ProjectOnPlane(
                transform.forward, normaleCorps).normalized;
            transform.position += avantSurface * avancer * vitesseDeplacement * Time.deltaTime;
        }

        // [NOUVEAU] Spring-damper sur la vitesse angulaire (inertie de rotation)
        float velAngCible = tournerInput * vitesseRotation;
        float erreur      = velAngCible - vitesseAngulaireActuelle;
        vitesseAngulaireActuelle += erreur * inertiePivot * Time.deltaTime;
        // Amortissement exponentiel : réduit la vitesse quand l'input est relâché
        vitesseAngulaireActuelle *= Mathf.Max(0f, 1f - amortissementPivot * Time.deltaTime);
        vitesseAngulaireActuelle  = Mathf.Clamp(vitesseAngulaireActuelle,
                                                 -vitesseRotation * 1.1f, vitesseRotation * 1.1f);

        if (Mathf.Abs(vitesseAngulaireActuelle) > 0.1f)
            transform.Rotate(normaleCorps, vitesseAngulaireActuelle * Time.deltaTime, Space.World);

        // Direction de virage normalisée (utilisée par les pattes pour le différentiel)
        directionTourActuelle = Mathf.Clamp(vitesseAngulaireActuelle / vitesseRotation, -1f, 1f);
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] DÉTECTION DE L'ÉTAT ANIMATOIRE
    // ════════════════════════════════════════════════════════

    private void DetecterEtat()
    {
        // Mesurer la vitesse de déplacement
        vitesseCorpsActuelle = (transform.position - posPrecedente).magnitude / Time.deltaTime;
        posPrecedente = transform.position;

        // Choisir l'état en fonction de la vitesse linéaire et angulaire
        AnimState nouvelEtat;
        float vitAng = Mathf.Abs(vitesseAngulaireActuelle);

        if      (vitAng > seuilAngulaireTurning)       nouvelEtat = AnimState.Turning;
        else if (vitesseCorpsActuelle > seuilRunning)  nouvelEtat = AnimState.Running;
        else if (vitesseCorpsActuelle > seuilWalking)  nouvelEtat = AnimState.Walking;
        else                                           nouvelEtat = AnimState.Idle;

        etatCourant = nouvelEtat;

        // Cibles de multiplicateurs par état
        float targetMultiVit, targetMultiHaut, targetMultiJitt, targetMultiBob;
        switch (etatCourant)
        {
            case AnimState.Idle:
                targetMultiVit  = 0.55f;
                targetMultiHaut = 0.70f;
                targetMultiJitt = 0.80f;
                targetMultiBob  = 0.20f;
                break;
            case AnimState.Running:
                targetMultiVit  = 2.20f;
                targetMultiHaut = 0.55f;
                targetMultiJitt = 2.50f;
                targetMultiBob  = 1.80f;
                break;
            case AnimState.Turning:
                targetMultiVit  = 1.10f;
                targetMultiHaut = 0.90f;
                targetMultiJitt = 1.20f;
                targetMultiBob  = 1.00f;
                break;
            default: // Walking
                targetMultiVit  = 1.00f;
                targetMultiHaut = 1.00f;
                targetMultiJitt = 1.00f;
                targetMultiBob  = 1.00f;
                break;
        }

        // Interpolation exponentielle vers les cibles (converge en ~dureeTransition secondes)
        float k = 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(dureeTransition, 0.001f));
        multiVitessePas = Mathf.Lerp(multiVitessePas, targetMultiVit,  k);
        multiHauteurPas = Mathf.Lerp(multiHauteurPas, targetMultiHaut, k);
        multiJitter     = Mathf.Lerp(multiJitter,     targetMultiJitt, k);
        multiBob        = Mathf.Lerp(multiBob,        targetMultiBob,  k);
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] SQUEEZE / STRETCH
    //  Le corps descend quand des pattes sont en vol, remonte
    //  quand elles atterrissent (compression musculaire).
    // ════════════════════════════════════════════════════════

    private void AppliquerSqueezeStretch()
    {
        int pattesEnVol = 0;
        if (pattes != null)
            foreach (var p in pattes)
                if (p != null && p.EnMouvement) pattesEnVol++;

        // Offset cible : 0 = normal, -amplitude = comprimé (pattes en vol)
        float targetOffset = -(pattesEnVol / 8f) * amplitudeSqueezeStretch;

        // Spring vers la cible avec amortissement critique
        float erreur      = targetOffset - squeezeOffsetY;
        squeezeVelocity  += erreur * vitesseSqueezeStretch * vitesseSqueezeStretch * Time.deltaTime;
        squeezeVelocity  *= Mathf.Max(0f, 1f - vitesseSqueezeStretch * 2f * Time.deltaTime);
        squeezeOffsetY   += squeezeVelocity * Time.deltaTime;

        // Appliquer sur la hauteur courante (utilisée par AlignerSurSurface)
        hauteurCourante = hauteurCorps + squeezeOffsetY;
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] MICRO-JITTER PERLIN
    //  Bruit organique sur XYZ avec fréquences différentes
    //  pour éviter tout pattern perceptible.
    // ════════════════════════════════════════════════════════

    private void AppliquerJitterPerlin()
    {
        float amp = amplitudeJitter * multiJitter;
        if (amp < 0.0001f) return;

        float t = Time.time;
        // PerlinNoise retourne [0,1] → décaler de -0.5 pour centrer sur 0
        float jX = (Mathf.PerlinNoise(t * frequenceJitterX, seedJitterX) - 0.5f) * 2f * amp;
        float jY = (Mathf.PerlinNoise(seedJitterY, t * frequenceJitterY) - 0.5f) * 2f * amp;
        float jZ = (Mathf.PerlinNoise(t * frequenceJitterZ, seedJitterZ) - 0.5f) * 2f * amp;

        // Appliquer en espace local du corps (suit les murs et plafonds)
        transform.position += transform.right    * jX
                            + transform.up       * jY
                            + transform.forward  * jZ;
    }

    // ════════════════════════════════════════════════════════
    //  BOB (modifié : amplitude selon l'état)
    // ════════════════════════════════════════════════════════

    private void AnimerBob()
    {
        int pattesEnMouvement = 0;
        if (pattes != null)
            foreach (var p in pattes)
                if (p != null && p.EnMouvement) pattesEnMouvement++;

        float vitesse = pattesEnMouvement > 0 ? vitesseBob : vitesseBob * 0.15f;
        phaseBob += Time.deltaTime * vitesse * multiBob;

        // Le bob s'ajoute à hauteurCourante après squeeze (déjà modifiée)
        float bobOffset = Mathf.Sin(phaseBob) * amplitudeBob * multiBob;
        hauteurCourante += bobOffset;
    }

    // ════════════════════════════════════════════════════════
    //  CAMÉRA
    // ════════════════════════════════════════════════════════

    private void SuivreCamera()
    {
        if (cibleCamera == null || cam == null) return;
        cam.transform.position = Vector3.Lerp(
            cam.transform.position, cibleCamera.position, lissageCamera * Time.deltaTime);
        cam.transform.LookAt(transform.position + normaleCorps * 0.4f);
    }

    // ════════════════════════════════════════════════════════
    //  DEBUG
    // ════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawRay(transform.position, -transform.up * porteeDetection * 2f);

        if (!Application.isPlaying) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(transform.position, normaleCorps * 0.7f);

        // [NOUVEAU] Afficher l'état courant en tant que label
#if UNITY_EDITOR
        if (Application.isPlaying)
        {
            var style = new GUIStyle();
            style.normal.textColor = Color.white;
            style.fontSize = 11;
            UnityEditor.Handles.Label(
                transform.position + Vector3.up * 1.2f,
                etatCourant.ToString(), style);
        }
#endif
    }
}
