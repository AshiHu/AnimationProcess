using UnityEngine;

public static class TwoBoneIK
{
    /// <summary>
    /// Résout un IK 2-os (épaule → coude → pied)
    /// </summary>
    public static void Solve(Transform root, Transform mid, Transform tip,
                              Vector3 target, Vector3 pole, float upperLen, float lowerLen)
    {
        float totalLen = upperLen + lowerLen;
        Vector3 dir = target - root.position;
        float dist = Mathf.Clamp(dir.magnitude, 0.001f, totalLen - 0.001f);

        // Loi des cosinus pour trouver l'angle au coude
        float cosAngle = (upperLen * upperLen + dist * dist - lowerLen * lowerLen)
                         / (2f * upperLen * dist);
        float angle = Mathf.Acos(Mathf.Clamp(cosAngle, -1f, 1f)) * Mathf.Rad2Deg;

        // Axe de rotation via le pôle (contrôle la direction du genou)
        Vector3 poleDir = pole - root.position;
        Vector3 perpAxis = Vector3.Cross(dir.normalized, poleDir).normalized;
        Vector3 normal = Vector3.Cross(dir.normalized, perpAxis);

        // Appliquer les rotations
        root.rotation = Quaternion.LookRotation(dir.normalized, normal);
        root.rotation *= Quaternion.Euler(angle, 0, 0); // lever le genou

        Vector3 midToTarget = target - mid.position;
        mid.rotation = Quaternion.LookRotation(midToTarget.normalized, normal);

        tip.position = target;
    }
}