using UnityEngine;
using System.Collections;

public class HomingAttackControl : MonoBehaviour
{
    public bool HasTarget => PlayerAbilityFeatures.HomingAttack && IsValidTarget(TargetObject);
    public static GameObject TargetObject { get; set; }
    public ActionManager Actions;

    public float TargetSearchDistance = 10;
    public Transform Icon;
    public float IconScale;

    public static GameObject[] Targets;
    public GameObject[] TgtDebug;

    public Transform MainCamera;
    public float IconDistanceScaling;

    int HomingCount;
    [SerializeField, Min(0), Tooltip("Physics steps to wait after an attack before the next homing input is accepted.")]
    private int cooldownSteps = 3;
    public int CooldownSteps { get => Mathf.Max(0, cooldownSteps); set => cooldownSteps = Mathf.Max(0, value); }
    public bool HomingAvailable { get; set; }

    bool firstime = false;

    public void ReplenishAfterRebound()
    {
        HomingCount = CooldownSteps + 1;
        HomingAvailable = true;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { TargetObject = null; Targets = System.Array.Empty<GameObject>(); }

    public bool IsValidTarget(GameObject target)
    {
        if (!target || !target.activeInHierarchy || target.scene != gameObject.scene) return false;
        var enemy = target.GetComponentInParent<EnemyHealth>();
        return !enemy || !enemy.IsDefeated;
    }

    void Awake()
    {
        Actions = GetComponent<ActionManager>();
    }

    void Start()
    {
        var tgt = GameObject.FindGameObjectsWithTag("HomingTarget");
        Targets = tgt;
        TgtDebug = tgt;

        Icon.parent = null;
        UpdateHomingTargets();
    }

    void LateUpdate()
    {
        if (!firstime)
        {
            firstime = true;
            UpdateHomingTargets();
        }
    }

    void FixedUpdate()
    {
        UpdateHomingTargets();
        //Prevent Homing attack spamming

        HomingCount += 1;

        if (Actions.Action == 2)
        {
            HomingAvailable = false;
            HomingCount = 0;
        }
        if (HomingCount > CooldownSteps)
        {
            HomingAvailable = true;
        }

        //SetIconPosition

        TargetObject = GetClosestTarget(Targets, TargetSearchDistance);

        if (HasTarget)
        {
            Icon.position = TargetObject.transform.position;
            float camDist = Vector3.Distance(transform.position, MainCamera.position);
            Icon.localScale = (Vector3.one * IconScale) + (Vector3.one * (camDist * IconDistanceScaling));
        }
        else
        {
            Icon.localScale = Vector3.zero;
        }
    }

    //This function will look for every possible homing attack target in the whole level. 
    //And you can call it from other scritps via [ HomingAttackControl.UpdateHomingTargets() ]
    public static void UpdateHomingTargets()
    {
        var tgt = GameObject.FindGameObjectsWithTag("HomingTarget");
        Targets = tgt;
    }

    GameObject GetClosestTarget(GameObject[] tgts, float maxDistance)
    {
        GameObject[] gos = tgts;
        GameObject closest = null;
        float distance = maxDistance;
        Vector3 position = transform.position;
        if (gos == null) return null;
        foreach (GameObject go in gos)
        {
            if (!IsValidTarget(go)) continue;
            Vector3 diff = go.transform.position - position;
            float curDistance = diff.sqrMagnitude;

            if (curDistance < distance)
            {
                closest = go;
                distance = curDistance;
            }
        }
        //Debug.Log(closest);
        return closest;
    }
}
