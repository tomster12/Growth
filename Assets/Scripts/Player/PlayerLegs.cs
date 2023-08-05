
using UnityEditor;
using UnityEngine;


public class PlayerLegs : MonoBehaviour
{
    [Header("References")]
    [SerializeField] PlayerController playerController;
    [SerializeField] Color[] gizmoLegColors;
    [SerializeField] float[] walkOffsets;

    [Header("Config")]
    [SerializeField] private float legOffset = 0.3f;
    [SerializeField] private float legWidth = 0.15f;
    [SerializeField] private float legGap = 0.35f;
    [SerializeField] private float kneeHeight = 0.2f;
    [SerializeField] private float stepSize = 1.5f;
    [SerializeField] private float stepThreshold = 0.85f;
    [SerializeField] private float isWalkingThreshold = 0.1f;
    [SerializeField] private float walkFootLerp = 20f;
    [SerializeField] private IKFabrik[] legIK;
    [SerializeField] private int walkDir = 0;
    [SerializeField] private bool drawGizmos = false;

    public bool isPointing = false;
    public int pointingLeg = -1;
    public Vector2 pointingPos;

    private bool legCurrentInit;
    private Vector2[] legTargets = new Vector2[4];
    private Vector2[] legCurrent = new Vector2[4];
    private bool[] haveStepped = new bool[4];


    private void Update()
    {
        float walkPct = Vector2.Dot(playerController.rb.velocity, playerController.rightDir.normalized);
        walkDir = (walkPct < -isWalkingThreshold) ? -1 : (walkPct > isWalkingThreshold) ? 1 : 0;

        for (int i = 0; i < 4; i++)
        {
            // Figure out where leg end should be
            GetLegEndRaycast(i, 0, out Vector2 pos, out bool isTouching);

            // Set leg to ground if touching
            if (isTouching)
            {
                if (walkDir != 0)
                {
                    GetLegEndRaycast(i, walkDir, out Vector2 frontPos, out bool frontIsTouching);

                    float pct = (frontPos - legTargets[i]).magnitude / stepSize;
                    if (!haveStepped[i]) pct += walkOffsets[i];

                    if (pct > stepThreshold)
                    {
                        if (frontIsTouching) legTargets[i] = frontPos;
                        else legTargets[i] = pos;
                        haveStepped[i] = true;
                    }
                }
                else
                {
                    legTargets[i] = pos;
                    haveStepped[i] = false;
                }
            }

            // Otherwise free float
            else
            {
                Vector2 dir = legTargets[i] - (Vector2)playerController.transform.position;
                dir = Vector2.ClampMagnitude(dir, legIK[i].totalLength);
                legTargets[i] = (Vector2)playerController.transform.position + dir;
                haveStepped[i] = false;
            }

            // lerp leg current
            Vector2 target = legTargets[i];
            if (isPointing && pointingLeg == i) target = pointingPos;
            if (!legCurrentInit) legCurrent[i] = target;
            else legCurrent[i] = Vector2.Lerp(legCurrent[i], target, Time.deltaTime * walkFootLerp);

            // Update IK variables
            legIK[i].targetPos = legCurrent[i];
            legIK[i].targetRot = Quaternion.identity;
            legIK[i].polePos = GetLegPole(i);
            legIK[i].poleRot = Quaternion.identity;
        }

        legCurrentInit = true;
    }


    private void GetLegEndRaycast(int legIndex, int walkDir, out Vector2 pos, out bool isTouching)
    {
        float horizontalMult = (legIndex <= 1) ? (-2 + legIndex) : (-1 + legIndex);
        int terrainMask = 1 << LayerMask.NameToLayer("Terrain");

        // Raycast sideways
        Vector2 sideFrom = playerController.transform.position;
        Vector2 sideDir = playerController.rightDir.normalized;
        float sideDistMax = horizontalMult * legGap + walkDir * stepSize * 0.5f;
        RaycastHit2D sideHit = Physics2D.Raycast(sideFrom, sideDir, sideDistMax, terrainMask);
        float sideDist = (sideHit.collider != null) ? sideHit.distance : sideDistMax;

        // Raycast downwards
        Vector2 downFrom = sideFrom + sideDir * sideDist;
        Vector2 downDir = playerController.groundDir.normalized;
        float downDistMax = playerController.groundedHeight;
        RaycastHit2D downHit = Physics2D.Raycast(downFrom, downDir, downDistMax, terrainMask);

        // Update out variables
        isTouching = downHit.collider != null;
        if (isTouching) pos = downHit.point;
        else pos = downFrom + downDir * downDistMax;
    }

    private Vector2 GetLegPole(int legIndex)
    {
        float horizontalMult = (legIndex <= 1) ? (-2 + legIndex) : (-1 + legIndex);
        return (Vector2)playerController.transform.position
            + (playerController.rightDir.normalized * horizontalMult * legGap)
            + (playerController.upDir.normalized * kneeHeight * 2);
    }

    public Vector2 GetLegEnd(int legIndex)
    {
        if (legIndex < 0 || legIndex > 3) return Vector2.zero;
        return legIK[legIndex].bones[legIK[legIndex].boneCount - 1].position;
    }

    [ContextMenu("Set Leg Lengths")]
    private void Editor_SetLegLengths()
    {
        // Init bones
        for (int i = 0; i < 4; i++)
        {
            float horizontalMult = (i <= 1) ? (-2 + i) : (-1 + i);
            legIK[i].InitBones();

            Vector2 bone0Pos = (Vector2)playerController.transform.position
                + ((Vector2)playerController.transform.right * Mathf.Sign(horizontalMult) * legOffset);

            Vector2 bone1Pos = (Vector2)playerController.transform.position
                + ((Vector2)playerController.transform.right * Mathf.Sign(horizontalMult) * legOffset)
                + ((Vector2)playerController.transform.right * horizontalMult * legGap * 0.5f)
                + ((Vector2)playerController.transform.up * kneeHeight);

            Vector2 bone2Pos = (Vector2)playerController.transform.position
                + ((Vector2)playerController.transform.right * Mathf.Sign(horizontalMult) * legOffset)
                + ((Vector2)playerController.transform.right * horizontalMult * legGap)
                - ((Vector2)playerController.transform.up * playerController.groundedHeight);

            legIK[i].bones[0].up = bone1Pos - bone0Pos;
            legIK[i].bones[1].up = bone2Pos - bone1Pos;
            legIK[i].bones[2].up = playerController.transform.up;

            legIK[i].bones[0].position = bone0Pos;
            legIK[i].bones[1].position = bone1Pos;
            legIK[i].bones[2].position = bone2Pos;

            legIK[i].bones[0].localPosition = new Vector3(legIK[i].bones[0].localPosition.x, legIK[i].bones[0].localPosition.y, 0.0f);
            legIK[i].bones[1].localPosition = new Vector3(legIK[i].bones[1].localPosition.x, legIK[i].bones[1].localPosition.y, 0.0f);
            legIK[i].bones[2].localPosition = new Vector3(legIK[i].bones[2].localPosition.x, legIK[i].bones[2].localPosition.y, 0.0f);
            
            legIK[i].bones[0].GetChild(1).position = (legIK[i].bones[0].position + legIK[i].bones[1].position) / 2.0f;
            legIK[i].bones[0].GetChild(1).localScale = new Vector3(legWidth, (bone1Pos - bone0Pos).magnitude, 1.0f);
            legIK[i].bones[1].GetChild(1).position = (legIK[i].bones[1].position + legIK[i].bones[2].position) / 2.0f;
            legIK[i].bones[1].GetChild(1).localScale = new Vector3(legWidth, (bone2Pos - bone1Pos).magnitude, 1.0f);
        }
    }


    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        if (playerController != null)
        {
            Gizmos.color = Color.grey;
            Gizmos.DrawSphere(playerController.groundPosition, 0.1f);

            if (legTargets != null)
            {
                for (int i = 0; i < legTargets.Length; i++)
                {
                    Gizmos.color = gizmoLegColors[i];
                    Gizmos.DrawSphere(legTargets[i], 0.15f);
                }
            }
        }
    }
}
