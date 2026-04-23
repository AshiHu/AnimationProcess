// ============================================================
//  SpiderLeg.cs
//  Gestion d'une patte individuelle.
//  IK 2-os, pas parabolique, gait, et couche animation riche.
// ============================================================

using System.Collections;
using UnityEngine;

public class SpiderLeg : MonoBehaviour
{
    // ════════════════════════════════════════════════════════
    //  CHAÎNE IK
    // ════════════════════════════════════════════════════════

    [Header("── Chaîne IK ──")]
    [SerializeField] private Transform hanche;
    [SerializeField] private Transform genou;
    [SerializeField] private Transform poleCible;
    [SerializeField, Min(0f)] private float longueurHanche = 0f;
    [SerializeField, Min(0f)] private float longueurGenou  = 0f;
    [SerializeField, Range(0.50f, 0.98f)] private float extensionMax    = 0.88f;
    [SerializeField] private Vector3 offsetRotationOs = new Vector3(90f, 0f, 0f);

    // ════════════════════════════════════════════════════════
    //  POSITION DE REPOS
    // ════════════════════════════════════════════════════════

    [Header("── Position de repos ──")]
    [SerializeField] private Transform ancreRepos;
    [SerializeField, Range(0.2f, 5f)]  private float porteeRaycast = 2f;
    [SerializeField, Range(0f, 0.1f)]  private float offsetSurface = 0.03f;

    // ════════════════════════════════════════════════════════
    //  PARAMÈTRES DE PAS (base)
    // ════════════════════════════════════════════════════════

    [Header("── Paramètres de pas ──")]
    [SerializeField, Range(0.05f, 2f)]  private float seuilPas       = 0.45f;
    [SerializeField, Range(0.02f, 1f)]  private float hauteurArche   = 0.22f;
    [SerializeField, Range(0.05f, 0.6f)]private float dureePas       = 0.14f;
    [SerializeField, Range(0f, 0.8f)]   private float anticipation   = 0.15f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] ANIMATION — ARC ORGANIQUE
    // ════════════════════════════════════════════════════════

    [Header("── Animation — Arc du pas ──")]
    [Tooltip("Courbe de l'arche du pas. X=progression(0-1), Y=hauteur(0-1).\n" +
             "Défaut : montée rapide avec léger dépassement, descente douce, micro-rebond.")]
    [SerializeField] private AnimationCurve courbeArc;

    [Tooltip("Angle de rotation du pied en montée (griffe vers l'avant) et en descente (atterrissage plat)")]
    [SerializeField, Range(0f, 30f)] private float angleGriffe = 15f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] ANIMATION — IDLE TWITCH
    // ════════════════════════════════════════════════════════

    [Header("── Animation — Idle Twitch ──")]
    [Tooltip("Durée d'immobilité avant que les twitches ne commencent (secondes)")]
    [SerializeField, Range(0.5f, 5f)] private float delaiDebutTwitch  = 2f;
    [Tooltip("Amplitude du micro-soulèvement du twitch (unités)")]
    [SerializeField, Range(0.01f, 0.2f)] private float amplitudeTwitch  = 0.05f;
    [Tooltip("Durée d'un twitch (secondes)")]
    [SerializeField, Range(0.05f, 0.5f)] private float dureeTwitch      = 0.25f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] ANIMATION — TREMBLEMENT D'EXTENSION
    // ════════════════════════════════════════════════════════

    [Header("── Animation — Tremblement ──")]
    [Tooltip("Ratio d'extension (0-1) à partir duquel le tremblement apparaît")]
    [SerializeField, Range(0.5f, 1f)]  private float seuilTremblement     = 0.85f;
    [Tooltip("Amplitude maximale du tremblement en extension complète")]
    [SerializeField, Range(0f, 0.1f)]  private float amplitudeTremblement = 0.04f;
    [Tooltip("Fréquence du tremblement (Hz)")]
    [SerializeField, Range(10f, 80f)]  private float frequenceTremblement = 45f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] ANIMATION — DÉLAI DE DÉMARRAGE
    // ════════════════════════════════════════════════════════

    [Header("── Animation — Délai aléatoire ──")]
    [Tooltip("Délai aléatoire maximal avant déclenchement d'un pas (briser la sync parfaite)")]
    [SerializeField, Range(0f, 0.15f)] private float delaiMaxDemarrage = 0.08f;

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] POSITION SUR LE CORPS (pour le différentiel de virage)
    // ════════════════════════════════════════════════════════

    [Header("── Position sur le corps ──")]
    [Tooltip("Cocher si cette patte est sur le côté DROIT de l'araignée")]
    public bool estCoteDroit = false;

    // ════════════════════════════════════════════════════════
    //  GAIT
    // ════════════════════════════════════════════════════════

    [Header("── Synchronisation Gait ──")]
    public SpiderLeg patteOpposee;

    // ════════════════════════════════════════════════════════
    //  LAYERS
    // ════════════════════════════════════════════════════════

    [Header("── Layers ──")]
    [SerializeField] private LayerMask masqueSol = ~0;

    // ════════════════════════════════════════════════════════
    //  ÉTAT INTERNE
    // ════════════════════════════════════════════════════════

    private Vector3 positionPied;
    private bool    enMouvement  = false;
    private float   L1, L2;
    private SpiderController controleur;

    // [NOUVEAU] Rotation de griffe accumulée pendant le pas
    private float rotationGriffeActuelle;

    // [NOUVEAU] Timer idle twitch
    private float tempsImmobile;
    private float minuterieTwitch;
    private bool  twichEnCours;

    // [NOUVEAU] Paramètres effectifs (modifiés selon AnimState)
    private float seuilPasEffectif;
    private float hauteurArcheEffective;
    private float dureePasEffective;

    // ════════════════════════════════════════════════════════
    //  PROPRIÉTÉS PUBLIQUES
    // ════════════════════════════════════════════════════════

    public bool EnMouvement => enMouvement;

    // ════════════════════════════════════════════════════════
    //  UNITY — RESET (valeur par défaut de la courbe)
    // ════════════════════════════════════════════════════════

    // [NOUVEAU] Appelé quand le composant est ajouté en éditeur
    void Reset()
    {
        CreerCourbeDefaut();
    }

    // ════════════════════════════════════════════════════════
    //  INITIALISATION
    // ════════════════════════════════════════════════════════

    void Start()
    {
        controleur = GetComponentInParent<SpiderController>();

        L1 = longueurHanche > 0f
            ? longueurHanche
            : (hanche != null && genou != null
                ? Vector3.Distance(hanche.position, genou.position) : 0.9f);
        L2 = longueurGenou > 0f ? longueurGenou : L1;

        positionPied = TrouverPositionSurSol(PosReposMonde());

        // [NOUVEAU] Initialiser les paramètres effectifs
        seuilPasEffectif    = seuilPas;
        hauteurArcheEffective = hauteurArche;
        dureePasEffective   = dureePas;

        // [NOUVEAU] Minuterie twitch aléatoire initiale
        minuterieTwitch = Random.Range(1f, 4f);

        // [NOUVEAU] Créer la courbe si elle est vide
        if (courbeArc == null || courbeArc.length == 0)
            CreerCourbeDefaut();
    }

    // ════════════════════════════════════════════════════════
    //  BOUCLE PRINCIPALE
    // ════════════════════════════════════════════════════════

    void LateUpdate()
    {
        AdapterParametresAuState();  // [NOUVEAU]
        AnimerIdleTwitch();          // [NOUVEAU]

        // [NOUVEAU] Calcul du ratio d'extension pour le tremblement
        float distanceEtiree = (hanche != null)
            ? Vector3.Distance(hanche.position, positionPied)
            : 0f;
        float ratioExtension = (L1 + L2 > 0f) ? distanceEtiree / (L1 + L2) : 0f;

        // [NOUVEAU] Position avec tremblement superposé (visuel uniquement)
        Vector3 posAvecEffets = positionPied;
        if (ratioExtension > seuilTremblement)
        {
            float intensite = Mathf.InverseLerp(seuilTremblement, 1f, ratioExtension)
                              * amplitudeTremblement;
            float t = Time.time * frequenceTremblement;
            posAvecEffets += new Vector3(
                Mathf.Sin(t * 1.00f) * intensite,
                Mathf.Sin(t * 0.73f) * intensite * 0.5f,
                Mathf.Sin(t * 0.91f + 1f) * intensite * 0.7f);
        }

        ResoudreIK(posAvecEffets);

        // [NOUVEAU] Rotation de griffe après l'IK (pendant les pas)
        if (genou != null && Mathf.Abs(rotationGriffeActuelle) > 0.01f)
            genou.Rotate(rotationGriffeActuelle, 0f, 0f, Space.Self);

        // Déclenchement d'un pas
        if (!enMouvement && !twichEnCours && DoitEffectuerPas())
        {
            bool opposeeOccupee = patteOpposee != null && patteOpposee.EnMouvement;
            if (!opposeeOccupee)
                StartCoroutine(CoroutinePas());
        }
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] ADAPTATION AUX ÉTATS ANIMATOIRES
    // ════════════════════════════════════════════════════════

    private void AdapterParametresAuState()
    {
        if (controleur == null) return;

        float multiVit  = controleur.MultiVitessePas;
        float multiHaut = controleur.MultiHauteurPas;

        // [NOUVEAU] Différentiel de virage : inner vs outer legs
        float facteurTournant = 1f;
        if (controleur.EtatCourant == AnimState.Turning)
        {
            float dir = controleur.DirectionTour; // >0 = virage droite
            // Patte intérieure (près du pivot) = virage même côté
            bool estInterieure = (dir > 0f) == estCoteDroit;
            facteurTournant = estInterieure ? 0.65f : 1.35f;
        }

        seuilPasEffectif      = seuilPas;       // le seuil reste fixe
        hauteurArcheEffective = hauteurArche * multiHaut * facteurTournant;
        dureePasEffective     = dureePas / (multiVit * facteurTournant);
        dureePasEffective     = Mathf.Clamp(dureePasEffective, 0.04f, 0.6f);
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] IDLE TWITCH
    //  Micro-soulèvement aléatoire quand l'araignée est immobile.
    //  Maximum 1 patte à la fois, coordonné via le contrôleur.
    // ════════════════════════════════════════════════════════

    private void AnimerIdleTwitch()
    {
        if (controleur == null || controleur.EtatCourant != AnimState.Idle)
        {
            tempsImmobile = 0f;
            return;
        }

        tempsImmobile += Time.deltaTime;
        if (tempsImmobile < delaiDebutTwitch) return;
        if (enMouvement || twichEnCours) return;

        minuterieTwitch -= Time.deltaTime;
        if (minuterieTwitch <= 0f)
        {
            minuterieTwitch = Random.Range(1f, 4f);

            // S'assurer qu'aucune autre patte ne twitch actuellement
            if (controleur.compteurSecousses == 0)
                StartCoroutine(CoroutineTwitch());
        }
    }

    private IEnumerator CoroutineTwitch()
    {
        twichEnCours = true;
        controleur.compteurSecousses++;

        Vector3 posBase  = positionPied;
        Vector3 dirHaut  = controleur != null ? controleur.NormaleCorps : Vector3.up;
        float   progression = 0f;

        while (progression < 1f)
        {
            progression = Mathf.MoveTowards(progression, 1f, Time.deltaTime / dureeTwitch);
            float hauteur = Mathf.Sin(progression * Mathf.PI) * amplitudeTwitch;
            positionPied = posBase + dirHaut * hauteur;
            yield return null;
        }

        positionPied = posBase;
        controleur.compteurSecousses--;
        twichEnCours = false;
    }

    // ════════════════════════════════════════════════════════
    //  IK 2 OS
    // ════════════════════════════════════════════════════════

    private void ResoudreIK(Vector3 ciblePied)
    {
        float a = L1, b = L2;
        float porteeMax = (a + b) * extensionMax;
        float porteeMin = Mathf.Abs(a - b) + 0.001f;

        Vector3 versTarget = ciblePied - hanche.position;
        float   distance   = versTarget.magnitude;
        if (distance < 0.001f) return;

        float   distLimitee = Mathf.Clamp(distance, porteeMin, porteeMax);
        Vector3 dir         = versTarget / distance;
        Vector3 cibleClamp  = hanche.position + dir * distLimitee;

        float cosAlpha = Mathf.Clamp(
            (a * a + distLimitee * distLimitee - b * b) / (2f * a * distLimitee), -1f, 1f);
        float alpha = Mathf.Acos(cosAlpha) * Mathf.Rad2Deg;

        Vector3 posPole = poleCible != null
            ? poleCible.position
            : hanche.position + Vector3.Cross(dir, Vector3.up).normalized * 0.5f + Vector3.up * 0.5f;

        Vector3 versPole = posPole - hanche.position;
        Vector3 perpPole = versPole - Vector3.Dot(versPole, dir) * dir;
        if (perpPole.sqrMagnitude < 0.0001f) perpPole = Vector3.Cross(dir, Vector3.forward);
        perpPole.Normalize();

        Vector3 axeRot    = Vector3.Cross(dir, perpPole).normalized;
        Vector3 dirGenou  = Quaternion.AngleAxis(alpha, axeRot) * dir;
        Vector3 posGenou  = hanche.position + dirGenou * a;

        genou.position = posGenou;

        Vector3    haut    = controleur != null ? controleur.NormaleCorps : Vector3.up;
        Quaternion offset  = Quaternion.Euler(offsetRotationOs);

        Vector3 dH = posGenou - hanche.position;
        if (dH.sqrMagnitude > 0.0001f)
            hanche.rotation = Quaternion.LookRotation(dH.normalized, haut) * offset;

        Vector3 dG = cibleClamp - posGenou;
        if (dG.sqrMagnitude > 0.0001f)
            genou.rotation = Quaternion.LookRotation(dG.normalized, haut) * offset;
    }

    // ════════════════════════════════════════════════════════
    //  DÉTECTION DU PAS
    // ════════════════════════════════════════════════════════

    private bool DoitEffectuerPas()
    {
        return Vector3.Distance(positionPied, TrouverPositionSurSol(PosReposMonde()))
               > seuilPasEffectif;
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] COROUTINE PAS — ARC ORGANIQUE
    // ════════════════════════════════════════════════════════

    private IEnumerator CoroutinePas()
    {
        // [NOUVEAU] Marquer la patte occupée AVANT le délai pour éviter un double déclenchement
        enMouvement = true;

        // [NOUVEAU] Délai aléatoire pour briser la synchronisation dans le groupe diagonal
        float delaiDemarrage = Random.Range(0f, delaiMaxDemarrage);
        if (delaiDemarrage > 0.001f)
            yield return new WaitForSeconds(delaiDemarrage);

        Vector3 departPied = positionPied;

        // Cible avec anticipation
        Vector3 centreCible = PosReposMonde();
        if (controleur != null && anticipation > 0f)
        {
            Vector3 avantSurface = Vector3.ProjectOnPlane(
                controleur.transform.forward, controleur.NormaleCorps).normalized;
            centreCible += avantSurface * anticipation;
        }
        Vector3 arrivee  = TrouverPositionSurSol(centreCible);
        Vector3 dirHaut  = controleur != null ? controleur.NormaleCorps : Vector3.up;

        // [NOUVEAU] Adapter la durée à la vitesse du corps (plus vite = pas plus court)
        float vitesse = controleur != null ? Mathf.Max(0.5f, controleur.VitesseCorps) : 1f;
        float dureeAdaptee = dureePasEffective / (1f + vitesse * 0.3f);
        dureeAdaptee = Mathf.Clamp(dureeAdaptee, 0.04f, 0.55f);

        float hauteurEffective = hauteurArcheEffective;

        float progression = 0f;
        while (progression < 1f)
        {
            progression = Mathf.MoveTowards(progression, 1f, Time.deltaTime / dureeAdaptee);

            // [NOUVEAU] Position horizontale via Lerp standard
            Vector3 posPlane = Vector3.Lerp(departPied, arrivee, progression);

            // [NOUVEAU] Hauteur via AnimationCurve asymétrique (avec overshoot + micro-rebond)
            float hauteurNorm = courbeArc != null
                ? courbeArc.Evaluate(progression)
                : Mathf.Sin(progression * Mathf.PI);
            positionPied = posPlane + dirHaut * (hauteurNorm * hauteurEffective);

            // [NOUVEAU] Rotation de griffe : pied "griffe" en montée, s'aplatit en descente
            // sin(π·t·0.8) donne la cloche, * sign(0.5-t) inverse en descente
            float phaseGriffe = Mathf.Sin(progression * Mathf.PI * 0.9f);
            rotationGriffeActuelle = -angleGriffe * phaseGriffe;

            yield return null;
        }

        positionPied           = arrivee;
        rotationGriffeActuelle = 0f;
        enMouvement            = false;
    }

    // ════════════════════════════════════════════════════════
    //  UTILITAIRES
    // ════════════════════════════════════════════════════════

    private Vector3 PosReposMonde()
    {
        if (ancreRepos != null) return ancreRepos.position;
        if (controleur != null) return controleur.transform.TransformPoint(transform.localPosition);
        return transform.position;
    }

    private Vector3 TrouverPositionSurSol(Vector3 centre)
    {
        Vector3 versSurface  = controleur != null ? -controleur.NormaleCorps : Vector3.down;
        Vector3 origineRayon = centre - versSurface * porteeRaycast;

        if (Physics.Raycast(origineRayon, versSurface, out RaycastHit hit, porteeRaycast * 2f, masqueSol))
            return hit.point - versSurface * offsetSurface;

        return centre;
    }

    // ════════════════════════════════════════════════════════
    //  [NOUVEAU] CRÉATION DE LA COURBE PAR DÉFAUT
    //  Cloche asymétrique : montée rapide + léger overshoot,
    //  descente douce + micro-rebond à l'atterrissage.
    // ════════════════════════════════════════════════════════

    private void CreerCourbeDefaut()
    {
        courbeArc = new AnimationCurve(
            new Keyframe(0.00f, 0.00f,  0f,  4.5f),  // départ : montée rapide
            new Keyframe(0.32f, 1.10f,  1.5f, 0f),   // léger dépassement (overshoot)
            new Keyframe(0.48f, 1.00f,  0f, -1.5f),  // sommet réel
            new Keyframe(0.82f, 0.07f, -1.2f, 0.5f), // micro-rebond avant pose
            new Keyframe(1.00f, 0.00f, -0.8f, 0f)    // pose
        );
        // Mode WrapMode pour éviter les artefacts hors [0,1]
        courbeArc.preWrapMode  = WrapMode.Clamp;
        courbeArc.postWrapMode = WrapMode.Clamp;
    }

    // ════════════════════════════════════════════════════════
    //  DEBUG GIZMOS
    // ════════════════════════════════════════════════════════

    void OnDrawGizmos()
    {
        if (!Application.isPlaying)
        {
            if (hanche != null && genou != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(hanche.position, genou.position);
                Gizmos.DrawSphere(genou.position, 0.04f);
            }
            return;
        }

        // Pied (vert=posé, rouge=en mouvement, bleu=twitch)
        Gizmos.color = twichEnCours ? Color.blue : (enMouvement ? Color.red : Color.green);
        Gizmos.DrawSphere(positionPied, 0.06f);

        if (ancreRepos != null)
        {
            Gizmos.color = new Color(1f, 1f, 0f, 0.5f);
            Gizmos.DrawWireSphere(ancreRepos.position, seuilPas * 0.5f);
            Gizmos.color = Color.white;
            Gizmos.DrawLine(positionPied, ancreRepos.position);
        }

        if (hanche != null && genou != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(hanche.position, genou.position);
            Gizmos.DrawLine(genou.position, positionPied);
            Gizmos.DrawSphere(genou.position, 0.04f);
        }
    }
}
