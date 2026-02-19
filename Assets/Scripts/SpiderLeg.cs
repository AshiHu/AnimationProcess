// ============================================================
//  Fichier : SpiderLeg.cs
//  Place ce fichier dans Assets/Script/
// ============================================================

using UnityEngine;

public class SpiderLeg : MonoBehaviour
{
    public Transform shoulderBone;
    public Transform kneeBone;
    public Transform poleTarget;

    public float upperLen  = 0.9f;
    public float lowerLen  = 0.9f;

    public float stepDist   = 0.5f;
    public float stepHeight = 0.25f;
    public float stepSpeed  = 9f;
    public float groundOff  = 0.04f;

    [Range(0.5f, 0.85f)]
    public float maxReach = 0.72f; // fraction de la longueur totale — 0.72 = genou toujours plie

    public Vector3   restLocal;
    public SpiderLeg opposite;

    [HideInInspector] public bool isStepping;

    Vector3          _footPos;
    Vector3          _footFrom;
    Vector3          _footTo;
    float            _stepT = 1f;
    SpiderController _body;

    void Start()
    {
        _body    = GetComponentInParent<SpiderController>();
        _footPos = SnapToGround(RestWorld());
        _footTo  = _footPos;
    }

    void LateUpdate()
    {
        if (!isStepping && (opposite == null || !opposite.isStepping))
        {
            if (Vector3.Distance(_footPos, RestWorld()) > stepDist)
                StartStep();
        }

        if (isStepping)
        {
            _stepT += Time.deltaTime * stepSpeed;
            float   c    = Mathf.Clamp01(_stepT);
            Vector3 flat = Vector3.Lerp(_footFrom, _footTo, c);
            _footPos = flat + Vector3.up * (Mathf.Sin(c * Mathf.PI) * stepHeight);
            if (c >= 1f) { _footPos = _footTo; isStepping = false; }
        }

        if (shoulderBone != null && kneeBone != null)
            Solve2BoneIK();
    }

    void StartStep()
    {
        _footFrom  = _footPos;
        _footTo    = SnapToGround(RestWorld());
        _stepT     = 0f;
        isStepping = true;
    }

    Vector3 SnapToGround(Vector3 worldPos)
    {
        Vector3 origin = worldPos + Vector3.up * 2f;
        if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 6f))
            return hit.point + Vector3.up * groundOff;
        return worldPos;
    }

    void Solve2BoneIK()
    {
        Vector3 root   = shoulderBone.position;
        Vector3 target = _footPos;

        float L1 = upperLen;
        float L2 = lowerLen;

        // Distance max limitee a maxReach * longueur totale
        // Empeche la patte de se tendre completement et garde le genou plie
        float maxR = (L1 + L2) * maxReach;
        float minR = Mathf.Abs(L1 - L2) + 0.001f;

        Vector3 toTarget = target - root;
        float   dist     = toTarget.magnitude;
        if (dist < 0.01f) return;

        dist = Mathf.Clamp(dist, minR, maxR);
        Vector3 dir           = toTarget / toTarget.magnitude;
        Vector3 clampedTarget = root + dir * dist;

        float cosAlpha = (L1 * L1 + dist * dist - L2 * L2) / (2f * L1 * dist);
        cosAlpha = Mathf.Clamp(cosAlpha, -1f, 1f);
        float alpha = Mathf.Acos(cosAlpha) * Mathf.Rad2Deg;

        Vector3 polePos = poleTarget != null
            ? poleTarget.position
            : root + Vector3.Cross(dir, Vector3.up).normalized * 0.5f + Vector3.up * 0.5f;

        Vector3 toPole   = polePos - root;
        Vector3 perpPole = toPole - Vector3.Dot(toPole, dir) * dir;
        if (perpPole.sqrMagnitude < 0.0001f)
            perpPole = Vector3.Cross(dir, Vector3.forward);
        perpPole.Normalize();

        Vector3 kneeDir = Quaternion.AngleAxis(
                              alpha,
                              Vector3.Cross(dir, perpPole).normalized) * dir;
        Vector3 kneePos = root + kneeDir * L1;

        kneeBone.position = kneePos;

        Vector3 up = _body != null ? _body.groundNormal : Vector3.up;

        Vector3 upperDir = kneePos - root;
        if (upperDir.sqrMagnitude > 0.0001f)
            shoulderBone.rotation = Quaternion.LookRotation(upperDir.normalized, up)
                                    * Quaternion.Euler(90f, 0f, 0f);

        Vector3 lowerDir = clampedTarget - kneePos;
        if (lowerDir.sqrMagnitude > 0.0001f)
            kneeBone.rotation = Quaternion.LookRotation(lowerDir.normalized, up)
                                * Quaternion.Euler(90f, 0f, 0f);
    }

    Vector3 RestWorld()
    {
        if (_body != null) return _body.transform.TransformPoint(restLocal);
        return transform.position + transform.TransformDirection(restLocal);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying) return;
        Gizmos.color = isStepping ? Color.red : Color.green;
        Gizmos.DrawSphere(_footPos, 0.06f);
        if (_body != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(RestWorld(), 0.05f);
        }
        if (shoulderBone != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(shoulderBone.position, _footPos);
        }
    }
}
