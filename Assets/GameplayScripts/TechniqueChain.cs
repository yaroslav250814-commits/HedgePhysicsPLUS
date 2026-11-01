using UnityEngine;

[DisallowMultipleComponent]
public sealed class TechniqueChain : MonoBehaviour
{
    static readonly int[] Bonuses = { 0, 0, 100, 200, 300, 400, 500, 600, 800, 1000, 1500, 2000 };
    static readonly string[] Names = { "", "", "Good!", "Nice!", "Great!", "Jammin'!", "Cool!", "Radical!", "Tight!", "Awesome!", "Extreme!", "Perfect!" };
    [SerializeField, Min(.1f), Tooltip("Seconds allowed between enemy defeats. Every unique defeat refreshes this window, regardless of attack or ground contact.")]
    private float comboWindowSeconds = 5;
    [SerializeField, Tooltip("Display the active combo count and remaining-time bar below technique praise.")]
    private bool showTimer = true;
    public float ComboWindowSeconds { get => Mathf.Max(.1f, comboWindowSeconds); set => comboWindowSeconds = Mathf.Max(.1f, value); }
    public bool ShowTimer { get => showTimer; set { showTimer = value; if (display) display.gameObject.SetActive(value && Hits > 0); } }
    public int Hits { get; private set; }
    public int BestHits { get; private set; }
    public int AwardedBonus { get; private set; }
    public int PendingBonus => BonusFor(Hits);
    public float RemainingSeconds => Hits > 0 ? Mathf.Max(0, expiresAt - Time.time) : 0;
    public float RemainingFraction => Hits > 0 ? Mathf.Clamp01(RemainingSeconds / currentWindow) : 0;
    public static int BonusFor(int hits) => Bonuses[Mathf.Clamp(hits, 0, Bonuses.Length - 1)];
    public static string NameFor(int hits) => Names[Mathf.Clamp(hits, 0, Names.Length - 1)];
    LevelRunState run;
    HurtControl hurt;
    ComboTimerDisplay display;
    float expiresAt, currentWindow = 5;

    void Awake() { run = GetComponent<LevelRunState>(); hurt = GetComponent<HurtControl>(); }

    public void EnemyDefeated(int points)
    {
        if (!run || run.Completed || (hurt && hurt.isDead)) return;
        // A defeat arriving before this frame's Update must not revive an expired combo.
        if (Hits > 0 && RemainingSeconds <= 0) Finish();
        Hits++; BestHits = Mathf.Max(BestHits, Hits);
        currentWindow = ComboWindowSeconds;
        expiresAt = Time.time + currentWindow;
        if (Hits == 1) EnemyScorePopup.Hide(run);
        EnemyScorePopup.Show(run, points, Hits, true);
        if (ShowTimer)
        {
            if (!display) display = ComboTimerDisplay.Create(run, this);
            if (display) display.gameObject.SetActive(true);
        }
    }

    void Update()
    {
        if (Time.timeScale > 0 && Hits > 0 && RemainingSeconds <= 0) Finish();
    }

    public void Finish()
    {
        if (Hits == 0) return;
        int bonus = PendingBonus; Hits = 0; expiresAt = 0;
        if (display) display.Hide();
        if (bonus > 0)
        {
            run.AddScore(bonus); AwardedBonus = ClassicScore.Add(AwardedBonus, bonus);
            EnemyScorePopup.EndChain(run);
        }
        else EnemyScorePopup.EndChain(run);
    }

    public void Cancel()
    {
        Hits = 0; expiresAt = 0;
        if (display) display.Hide();
        EnemyScorePopup.Hide(run);
    }
}
