using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public sealed class LevelRunState : MonoBehaviour
{
    public static LevelRunState Current { get; private set; }
    public Text ScoreLabel;
    public Text TimeLabel;
    public int EnemyPoints = 100;
    public int Score { get; private set; }
    public float ElapsedSeconds { get; private set; }
    public float CheckpointSeconds { get; private set; }
    public bool Completed { get; private set; }
    public int TrackedRingCount => rings.Count;
    public int MapRingCount { get; private set; }
    HurtControl hurt;
    public TechniqueChain Chain { get; private set; }
    public SceneEnemyReset Enemies { get; private set; }
    int displayedScore = -1, displayedTime = -1;
    readonly Dictionary<GameObject, RingPlacement> rings = new Dictionary<GameObject, RingPlacement>();

    struct RingPlacement
    {
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public bool Active;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { Current = null; Objects_Interaction.RingAmmount = 0; ClassicScore.Reset(); }

    void Awake()
    {
        Current = this;
        Score = ClassicScore.Total;
        hurt = GetComponent<HurtControl>();
        Chain = GetComponent<TechniqueChain>();
        if (!Chain) Chain = gameObject.AddComponent<TechniqueChain>();
        Enemies = gameObject.AddComponent<SceneEnemyReset>();
        Objects_Interaction.RingAmmount = 0;
        Monitors_Interactions.HasShield = false;
    }

    void Start()
    {
        Enemies.Capture();
        foreach (var root in gameObject.scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
            {
                var monitor = child.GetComponent<MonitorData>();
                if (child.CompareTag("Ring")) MapRingCount++;
                if (child.CompareTag("Ring") || (monitor && monitor.Type == MonitorType.Ring)) RememberRing(child.gameObject);
            }
        RefreshHud();
    }

    void Update()
    {
        if (Time.timeScale > 0 && !Completed && (!hurt || !hurt.isDead)) ElapsedSeconds += Time.deltaTime;
        RefreshHud();
    }

    void OnDestroy() { if (Current == this) Current = null; }

    public void RememberRing(GameObject ring)
    {
        if (!ring || rings.ContainsKey(ring)) return;
        rings.Add(ring, new RingPlacement {
            Position = ring.transform.localPosition, Rotation = ring.transform.localRotation,
            Scale = ring.transform.localScale, Active = ring.activeSelf
        });
    }

    public void AddScore(int points)
    {
        if (points <= 0 || Completed || (hurt && hurt.isDead)) return;
        Score = (int)Math.Min(int.MaxValue, (long)Score + points);
        RefreshHud();
    }

    public void AwardEnemyScore(Vector3 position)
    {
        if (Completed || (hurt && hurt.isDead) || EnemyPoints <= 0) return;
        AddScore(EnemyPoints);
        Chain.EnemyDefeated(EnemyPoints);
    }

    public void BeginDeath()
    {
        Chain?.Cancel();
        Objects_Interaction.RingAmmount = 0;
        Monitors_Interactions.HasShield = false;
        foreach (var ring in FindObjectsByType<MovingRing>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (ring.gameObject.scene == gameObject.scene) Destroy(ring.gameObject);
        foreach (var giver in FindObjectsByType<RingGiverControl>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (giver.gameObject.scene == gameObject.scene) Destroy(giver.gameObject);
    }

    public void SaveCheckpointTime()
    {
        if (Completed || (hurt && hurt.isDead)) return;
        CheckpointSeconds = ElapsedSeconds;
    }

    public void RestoreCheckpointTime()
    {
        if (Completed) return;
        ElapsedSeconds = CheckpointSeconds;
        displayedTime = -1;
        RefreshHud();
    }

    public void RestoreRingsAfterDeath()
    {
        Objects_Interaction.RingAmmount = 0;
        foreach (var pair in rings)
        {
            if (!pair.Key) continue;
            var target = pair.Key.transform;
            target.localPosition = pair.Value.Position;
            target.localRotation = pair.Value.Rotation;
            target.localScale = pair.Value.Scale;
            pair.Key.SetActive(pair.Value.Active);
        }
        GetComponent<Objects_Interaction>().updateTargets = true;
        Enemies.Restore();
    }

    public void CompleteStage()
    {
        if (Completed) return;
        Chain.Finish();
        Completed = true;
        ClassicScore.Finish(this, GetComponent<LevelProgressControl>());
        RefreshHud();
    }

    public static string FormatClassicTime(float seconds)
    {
        int value = Mathf.FloorToInt(Mathf.Max(0, seconds));
        return string.Format(CultureInfo.InvariantCulture, "{0}:{1:00}", value / 60, value % 60);
    }

    public static string FormatTime(float seconds)
    {
        int centiseconds = Mathf.FloorToInt(Mathf.Max(0, seconds) * 100);
        return string.Format(CultureInfo.InvariantCulture, "{0:00}:{1:00}.{2:00}",
            centiseconds / 6000, centiseconds / 100 % 60, centiseconds % 100);
    }

    void RefreshHud()
    {
        int centiseconds = Mathf.FloorToInt(ElapsedSeconds * 100);
        if (ScoreLabel && Score != displayedScore)
            ScoreLabel.text = Score.ToString(CultureInfo.InvariantCulture);
        if (TimeLabel && centiseconds != displayedTime)
            TimeLabel.text = FormatClassicTime(ElapsedSeconds);
        displayedScore = Score; displayedTime = centiseconds;
    }
}
