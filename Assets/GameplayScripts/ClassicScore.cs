using System;
using UnityEngine;

public static class ClassicScore
{
    public static int Total { get; private set; }
    public static StageScoreResult Result { get; private set; }
    public static int TotalRings { get; private set; }

    public static int TimeBonus(float seconds)
    {
        // Sonic 1 time table, in displayed points:
        // https://github.com/sonicretro/s1disasm/blob/AS/_incObj/0D%20Signpost.asm
        if (seconds < 30) return 50000;
        if (seconds < 45) return 10000;
        if (seconds < 60) return 5000;
        if (seconds < 90) return 4000;
        if (seconds < 120) return 3000;
        if (seconds < 180) return 2000;
        if (seconds < 240) return 1000;
        if (seconds < 300) return 500;
        return 0;
    }
    public static int Add(int a, int b) => (int)Math.Min(int.MaxValue, (long)a + b);
    public static int RingBonus(int rings) => (int)Math.Min(int.MaxValue, Math.Max(0L, rings) * 100);
    public static void Reset() { Total = TotalRings = 0; Result = null; StageResultBackdrop.Release(); }
    public static void Finish(LevelRunState run, LevelProgressControl progress)
    {
        Result = new StageScoreResult(run.gameObject.scene.name, run.Score, run.ElapsedSeconds,
            Objects_Interaction.RingAmmount, progress ? progress.LevelToGoNext : 1, run.MapRingCount);
    }
    public static void Commit(StageScoreResult result)
    {
        if (result == null || result.Committed) return;
        result.Committed = true;
        Total = result.Total;
        TotalRings = Add(result.PreviousTotalRings, result.Rings);
    }
}

public sealed class StageScoreResult
{
    public readonly string Stage;
    public readonly int Score, Rings, TimeBonus, RingBonus, Total, NextStage;
    public readonly int MapRings, PreviousTotalRings;
    public readonly float Time;
    public bool Committed { get; internal set; }
    public StageScoreResult(string stage, int score, float time, int rings, int nextStage, int mapRings = 0)
    {
        Stage = stage; Score = score; Time = time; Rings = Mathf.Max(0, rings); NextStage = nextStage;
        MapRings = Mathf.Max(Rings, mapRings); PreviousTotalRings = ClassicScore.TotalRings;
        TimeBonus = ClassicScore.TimeBonus(time); RingBonus = ClassicScore.RingBonus(Rings);
        Total = ClassicScore.Add(ClassicScore.Add(Score, TimeBonus), RingBonus);
    }
}
