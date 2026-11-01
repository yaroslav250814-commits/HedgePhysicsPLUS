using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StageConpleteControl : MonoBehaviour
{
    public static int LevelToGoNext;
    public Text StageTitle, ScoreValue, TimeValue, RingsValue, TotalValue, Prompt;
    public Text StageSubtitle, ClearTimeValue, RingCountValue, TotalRingsValue, BonusCaption;
    public CanvasGroup StatisticsGroup, BankGroup, TallyGroup;
    public RawImage Backdrop;
    public Button ContinueButton;
    public RectTransform Panel;
    public bool Finished { get; private set; }
    public int DisplayedTotal { get; private set; }
    StageScoreResult result;
    int timeRemaining, ringsRemaining;
    float age, ticks, finishedAt;
    bool leaving;

    void Start()
    {
        Time.timeScale = 1;
        result = ClassicScore.Result;
        if (result == null) result = new StageScoreResult("", ClassicScore.Total, 600, 0, LevelToGoNext);
        timeRemaining = result.TimeBonus; ringsRemaining = result.RingBonus;
        DisplayedTotal = result.Score;
        StageTitle.text = "1st MISSION CLEARED!";
        ClearTimeValue.text = LevelRunState.FormatTime(result.Time).Replace('.', ':');
        RingCountValue.text = result.Rings + " / " + result.MapRings;
        if (Backdrop) { Backdrop.texture = StageResultBackdrop.Texture; Backdrop.enabled = Backdrop.texture; }
        ScoreValue.text = result.Score.ToString();
        ContinueButton.onClick.AddListener(Confirm);
        Refresh();
    }

    void Update()
    {
        age += Time.deltaTime;
        float presentationTime = Finished ? Mathf.Max(age, 1.8f) : age;
        if (StatisticsGroup) StatisticsGroup.alpha = Mathf.Clamp01((presentationTime - .35f) / .25f);
        if (BankGroup) BankGroup.alpha = Mathf.Clamp01((presentationTime - .9f) / .25f);
        if (TallyGroup) TallyGroup.alpha = Mathf.Clamp01((presentationTime - 1.35f) / .25f);
        TotalRingsValue.text = ClassicScore.Add(result.PreviousTotalRings, Mathf.FloorToInt(result.Rings * Mathf.Clamp01((presentationTime - 1) / .6f))).ToString();
        if (age > 1.8f && !Finished)
        {
            ticks += Time.deltaTime * 60;
            while (ticks >= 1 && !Finished)
            {
                ticks -= 1;
                int step = Mathf.Max(100, Mathf.CeilToInt((result.TimeBonus + (long)result.RingBonus) / 18000f) * 100);
                int ringStep = Mathf.Max(10, Mathf.CeilToInt(result.RingBonus / 1200f) * 10);
                int time = Mathf.Min(step, timeRemaining), rings = timeRemaining == 0 ? Mathf.Min(ringStep, ringsRemaining) : 0;
                timeRemaining -= time; ringsRemaining -= rings;
                DisplayedTotal = ClassicScore.Add(DisplayedTotal, time + rings);
                if (timeRemaining == 0 && ringsRemaining == 0) FinishCounting();
            }
            Refresh();
        }
        if (HedgeInput.GetButtonDown("Submit") || HedgeInput.GetButtonDown("Start")) Confirm();
    }

    public void Confirm()
    {
        if (age < .7f || leaving) return;
        if (!Finished) { FinishCounting(); Refresh(); return; }
        if (age - finishedAt < .35f) return;
        leaving = true;
        if (result.NextStage == 3 || result.NextStage == 4) SceneController.ContinueStageLoading(result.NextStage);
        else SceneManager.LoadSceneAsync(result.NextStage >= 0 && result.NextStage < SceneManager.sceneCountInBuildSettings ? result.NextStage : 1);
    }

    void FinishCounting()
    {
        timeRemaining = ringsRemaining = 0;
        DisplayedTotal = result.Total;
        Finished = true; finishedAt = age;
        ClassicScore.Commit(result);
    }

    void Refresh()
    {
        bool countingRings = timeRemaining == 0 && ringsRemaining > 0;
        BonusCaption.text = countingRings ? "RING BONUS" : "TIME BONUS";
        TimeValue.text = (countingRings ? ringsRemaining : timeRemaining).ToString();
        TotalValue.text = DisplayedTotal.ToString();
        Prompt.text = Finished ? "ENTER / A  -  CONTINUE" : "ENTER / A  -  SKIP COUNT";
    }

    void OnDestroy() { StageResultBackdrop.Release(); }
}
