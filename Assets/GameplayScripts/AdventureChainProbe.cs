#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AdventureChainProbe : MonoBehaviour
{
    readonly List<string> report = new List<string>();
    int failures;
    float started;
    LevelRunState run; PlayerBhysics player; ActionManager actions; Objects_Interaction interaction;
    GameObject effect;
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Install()
    {
        if (!File.Exists("Migration/adventure-chain-test.request")) return;
        File.Delete("Migration/adventure-chain-test.request");
        FindFirstObjectByType<LevelRunState>().gameObject.AddComponent<AdventureChainProbe>();
    }
    void OnEnable() { Application.logMessageReceived += Log; }
    void OnDisable() { Application.logMessageReceived -= Log; }
    void Log(string message, string stack, LogType type)
    { if (type == LogType.Error || type == LogType.Exception) Check(false, message + "\n" + stack); }
    void Update() { if (started > 0 && Time.realtimeSinceStartup - started > 45) { Check(false, "Timeout"); Finish(); } }
    IEnumerator Start()
    {
        started = Time.realtimeSinceStartup; Time.timeScale = 1; Application.runInBackground = true;
        yield return new WaitForSeconds(.8f);
        run = GetComponent<LevelRunState>(); player = GetComponent<PlayerBhysics>(); actions = GetComponent<ActionManager>(); interaction = GetComponent<Objects_Interaction>();
        GetComponent<PlayerBinput>().enabled = false; player.enabled = false;
        player.Body.useGravity = false; player.Body.linearVelocity = Vector3.zero;
        foreach(var collider in GetComponentsInChildren<Collider>())collider.enabled=false;
        effect = new GameObject("Inactive test explosion"); effect.SetActive(false);
        Check(run.Chain, "Technique chain is installed on the real player");
        Check(run.Chain.ComboWindowSeconds==5,"The default combo window is five seconds");
        run.Chain.ComboWindowSeconds=4;
        player.Grounded = false;
        var enemy = Hit(); enemy.DealDamage(1);
        Check(run.Score == 100 && run.Chain.Hits == 1 && run.Chain.PendingBonus == 0, "First defeated enemy starts a timed combo and gives base score once");
        Hit(); Check(run.Score == 200 && run.Chain.Hits == 2 && run.Chain.PendingBonus == 100, "Second hit creates Good chain without early bonus payout");
        Hit(); Check(run.Score == 300 && run.Chain.Hits == 3 && run.Chain.PendingBonus == 200, "Third hit upgrades chain to Nice");
        yield return new WaitForSeconds(.3f);
        var popup = FindFirstObjectByType<EnemyScorePopup>();
        Check(popup && popup.Caption.text == "Nice!", "Technique praise appears below the HUD");
        Check(FindObjectsByType<EnemyScorePopup>(FindObjectsSortMode.None).Length == 1, "Consecutive hits update one feedback plate without stacking");
        Check(popup && popup.GetComponent<CanvasGroup>().blocksRaycasts == false && popup.GetComponentsInChildren<Graphic>().All(g => !g.raycastTarget), "Feedback never intercepts mouse clicks");
        Check(popup && popup.Caption.GetComponent<AdventureTextGradient>() && popup.Caption.GetComponent<Outline>(), "Technique praise has tier-coloured lettering and a dark outline");
        Check(popup && !popup.GetComponentInChildren<AdventurePanel>() && popup.GetComponentsInChildren<Text>().Length == 1, "Technique praise contains no blue card, bonus number or debug subtitle");
        Capture("chain-nice.png");
        CapturePalette(popup.Caption);
        var timer=FindFirstObjectByType<ComboTimerDisplay>();
        Check(timer&&timer.Counter.text=="COMBO 3"&&timer.TimeBar.rectTransform.sizeDelta.x>0,"The active combo shows its count and a remaining-time bar");
        Check(timer&&timer.GetComponent<CanvasGroup>().blocksRaycasts==false&&timer.GetComponentsInChildren<Graphic>().All(g=>!g.raycastTarget),"Combo timer never intercepts input");
        float remaining=run.Chain.RemainingSeconds;float barWidth=timer.TimeBar.rectTransform.sizeDelta.x;
        float before = run.ElapsedSeconds; Vector2 position = ((RectTransform)popup.transform).anchoredPosition;
        Time.timeScale = 0; yield return new WaitForSecondsRealtime(.2f);
        Check(run.Chain.Hits == 3 && run.ElapsedSeconds == before && Mathf.Abs(run.Chain.RemainingSeconds-remaining)<.03f&&Vector2.Distance(((RectTransform)popup.transform).anchoredPosition, position) < .01f, "Pause freezes combo countdown, level timer and feedback animation"); Time.timeScale = 1;
        yield return new WaitForSeconds(1.4f);
        Check(run.Chain.Hits == 3 && !FindFirstObjectByType<EnemyScorePopup>()&&timer&&timer.TimeBar.rectTransform.sizeDelta.x<barWidth, "Short praise disappears while the visible combo timer keeps counting down");
        player.Grounded = true; yield return new WaitForSeconds(.12f);
        Check(run.Chain.Hits == 3 && run.Score == 300 && run.Chain.AwardedBonus == 0, "Landing neither breaks the combo nor pays its pending bonus");
        Hit(3,true);Check(run.Score==400&&run.Chain.Hits==4&&run.Chain.RemainingSeconds>3.9f,"Ground Spin Dash continues the same combo and refreshes its full window");
        Hit(0,true,true);Hit(1,false);Hit(2,false);Hit(6,false);
        Check(run.Score==800&&run.Chain.Hits==8&&run.Chain.PendingBonus==800,"Rolling, jump, targeted homing and Drop Dash kills all continue one combo");
        yield return null;Capture("chain-timed.png");
        run.Chain.ShowTimer=false;Check(!timer.gameObject.activeSelf&&run.Chain.Hits==8,"The optional timer display can be hidden without cancelling gameplay");run.Chain.ShowTimer=true;
        var durable=Enemy(2);remaining=run.Chain.RemainingSeconds;yield return new WaitForSeconds(.1f);durable.DealDamage(1);
        Check(!durable.IsDefeated&&run.Chain.Hits==8&&run.Score==800&&run.Chain.RemainingSeconds<remaining-.05f,"A nonlethal hit neither scores nor refreshes the combo");
        durable.DealDamage(1);durable.DealDamage(1);
        Check(run.Score==900&&run.Chain.Hits==9&&run.Chain.RemainingSeconds>3.9f,"A direct damage kill from another attack is counted exactly once");
        run.Chain.ComboWindowSeconds=.3f;Hit(3,true);Check(run.Chain.Hits==10&&run.Score==1000,"A newly configured combo window applies on the next defeat");
        yield return new WaitForSeconds(.4f);
        Check(run.Chain.Hits==0&&run.Score==2500&&run.Chain.AwardedBonus==1500&&!timer.gameObject.activeSelf,"Timeout pays only the highest combo bonus once and hides the timer");
        run.Chain.Finish();Check(run.Score==2500,"Finishing an expired combo cannot pay it twice");
        Hit(3,true);yield return new WaitForSeconds(.15f);Hit(0,true,true);yield return new WaitForSeconds(.2f);
        Check(run.Chain.Hits==2&&run.Score==2700,"A later defeat extends the deadline beyond the first kill's expiry");
        yield return new WaitForSeconds(.15f);Check(run.Chain.Hits==0&&run.Score==2800,"The renewed timer eventually banks its own combo bonus");

        // Exercise a defeat arriving before Update has processed an already-expired combo.
        run.Chain.ComboWindowSeconds=.1f;Hit();Hit();run.Chain.enabled=false;yield return new WaitForSeconds(.15f);int staleScore=run.Score;
        Hit(3,true);run.Chain.enabled=true;
        Check(run.Chain.Hits==1&&run.Score==staleScore+200,"A kill cannot revive an expired combo between timer updates");
        run.Chain.Cancel();run.Chain.ComboWindowSeconds=5;
        player.Grounded = false; Hit(); Hit(); int score = run.Score;
        Objects_Interaction.RingAmmount = 5; GetComponent<HurtControl>().GetHurt();
        Check(run.Chain.Hits == 0 && run.Score == score, "Damage cancels pending chain bonus");
        yield return null;
        Check(!FindFirstObjectByType<EnemyScorePopup>(), "Damage removes stale score feedback");
        GetComponent<HurtControl>().IsHurt = false; Hit(); Hit(); score = run.Score;
        GetComponent<HurtControl>().isDead = true;
        Check(run.Chain.Hits == 0 && run.Score == score && Objects_Interaction.RingAmmount == 0, "Death cancels chain, preserves earned score and clears rings");
        yield return null;
        GetComponent<HurtControl>().isDead = false; GetComponent<HurtControl>().enabled = false; actions.ChangeAction(0);
        player.Grounded = false;
        int[] bonus = { 0, 100, 200, 300, 400, 500, 600, 800, 1000, 1500, 2000, 2000 };
        bool all = true;
        for (int i = 0; i < 12; i++) { Hit(i%2==0?3:1,i%2==0); all &= run.Chain.PendingBonus == bonus[i]; }
        Check(all && run.Chain.BestHits == 12 && TechniqueChain.NameFor(12) == "Perfect!", "Chain advances through every technique tier and caps at Perfect");
        yield return new WaitForSeconds(.3f); Capture("chain-perfect.png");
        score = run.Score; Objects_Interaction.RingAmmount = 9; run.CompleteStage();
        Check(run.Score == score + 2000 && run.Chain.Hits == 0 && ClassicScore.Result.Score == run.Score, "Goal banks an active chain before freezing stage results");
        int total = ClassicScore.Result.Total; run.CompleteStage();
        Check(ClassicScore.Result.Total == total && run.Score == score + 2000, "Repeated goal calls cannot duplicate the chain bonus");
        Check(ClassicScore.Result.RingBonus == 900, "Existing end-of-stage ring bonus is retained");
        Finish();
    }
    EnemyHealth Enemy(int health=1)
    {
        var root = new GameObject("Technique test enemy");root.SetActive(false); root.transform.position = transform.position + transform.forward * 2;
        var enemy = root.AddComponent<EnemyHealth>();enemy.MaxHealth=health; enemy.Explosion = effect;
        var hitbox = new GameObject("Enemy hitbox"); hitbox.transform.SetParent(root.transform, false); hitbox.tag = "Enemy";
        var collider = hitbox.AddComponent<SphereCollider>(); collider.isTrigger = true;
        // Fixtures use the actual trigger handler without introducing stray physical contacts.
        collider.enabled = false;root.SetActive(true); return enemy;
    }
    EnemyHealth Hit(int action=1,bool grounded=false,bool rolling=false)
    {
        player.Grounded=grounded;actions.ChangeAction(action);player.isRolling=rolling;
        actions.Action00.CharacterAnimator.SetInteger("Action", action==3?0:1);
        var enemy = Enemy();
        if(action==2)
        {
            HomingAttackControl.TargetObject=enemy.GetComponentInChildren<Collider>().gameObject;
            player.PreviousRawInput=Vector3.zero;actions.Action02.IsAirDash=false;actions.Action02.InitialEvents();
        }
        if(action==6)actions.Action06.InitialEvents();
        interaction.OnTriggerEnter(enemy.GetComponentInChildren<Collider>());
        player.Body.linearVelocity = Vector3.zero; return enemy;
    }
    void Check(bool ok, string message) { report.Add((ok ? "PASS " : "FAIL ") + message); if (!ok) failures++; }
    void CapturePalette(Text template)
    {
        var canvas = run.ScoreLabel.canvas.rootCanvas;
        var panel = AdventureUi.Rect("Combo palette preview", canvas.transform, Vector2.zero, Vector2.zero);
        panel.anchorMin=Vector2.zero; panel.anchorMax=Vector2.one; panel.offsetMin=panel.offsetMax=Vector2.zero;
        panel.gameObject.AddComponent<Image>().color=Color.black;
        float height=((RectTransform)canvas.transform).rect.height;
        string[] words={"Good!","Great!","Nice!","Jammin'!","Cool!","Radical!","Tight!","Awesome!","Extreme!","Perfect!"};
        for(int i=0;i<words.Length;i++)
        {
            var label=Instantiate(template,panel); label.text=words[i]; label.fontSize=Mathf.RoundToInt(height/27);
            label.rectTransform.anchorMin=label.rectTransform.anchorMax=label.rectTransform.pivot=new Vector2(0,1);
            label.rectTransform.anchoredPosition=new Vector2(height*.1f,-height*(.06f+i*.085f));
            label.rectTransform.sizeDelta=new Vector2(height,height*.075f);
            TechniquePraisePalette.Apply(label.GetComponent<AdventureTextGradient>(),words[i]);
        }
        Capture("combo-colors.png");panel.gameObject.SetActive(false);Destroy(panel.gameObject);
    }
    void Finish()
    {
        Time.timeScale = 1; Directory.CreateDirectory("Migration/AdventurePresentation"); report.Add("Failures=" + failures);
        File.WriteAllLines("Migration/AdventurePresentation/chain-tests.txt", report);
        Directory.CreateDirectory("Migration/TimedCombo");File.WriteAllLines("Migration/TimedCombo/"+gameObject.scene.name+"-test.txt",report);
        EditorApplication.Exit(failures == 0 ? 0 : 1);
    }
    void Capture(string file)
    {
        if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
        var camera = Camera.main; var canvas = run.ScoreLabel.canvas.rootCanvas;
        var mode = canvas.renderMode; var world = canvas.worldCamera; float distance = canvas.planeDistance;
        var target = RenderTexture.GetTemporary(1280, 720, 24); var previous = camera.targetTexture; var active = RenderTexture.active;
        canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = camera; canvas.planeDistance = camera.nearClipPlane + .5f;
        camera.targetTexture = target; Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false); image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
        File.WriteAllBytes("Migration/AdventurePresentation/" + file, image.EncodeToPNG());
        camera.targetTexture = previous; RenderTexture.active = active; canvas.renderMode = mode; canvas.worldCamera = world; canvas.planeDistance = distance;
        RenderTexture.ReleaseTemporary(target); Destroy(image);
    }
}
#endif
