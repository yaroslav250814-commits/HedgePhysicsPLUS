using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

public class LevelProgressControl : MonoBehaviour
{
    public Vector3 ResumePosition { get; set; }
    public Quaternion ResumeRotation { get; set; }
    public Transform ResumeTransform;
    ActionManager Actions;
    PlayerBhysics Player;
    CameraControl Cam;
    public GameObject CurrentCheckPoint { get; set; }

    public Material LampDone;
    public int LevelToGoNext = 0;
    public string NextLevelNameLeft;
    public string NextLevelNameRight;
    public AudioClip GoalRingTouchingSound;

    bool readyForNextStage = false;
    float readyCount = 0;

    bool firstime = false;

    void Start()
    {
        Cam = GetComponent<CameraControl>();
        Actions = GetComponent<ActionManager>();
        Player = GetComponent<PlayerBhysics>();
    }

    void Update()
    {
        LampDone.SetTextureOffset("_BaseMap", new Vector2(0, -Time.time) * 3);
        LampDone.SetTextureOffset("_EmissionMap", new Vector2(0, -Time.time) * 3);

        if (readyForNextStage)
        {
            Player.MoveInput = Vector3.zero;
            readyCount += Time.deltaTime;
            if (readyCount > 1.5f)
            {
                Actions.Action04Control.enabled = false;
                Color alpha = Color.black;
                Actions.Action04Control.FadeOutImage.color = Color.Lerp(Actions.Action04Control.FadeOutImage.color,
                    alpha, Time.fixedTime * 0.1f);
            }
            if (readyCount > 2.6f)
            {
                LoadingScreenControl.StageName1 = NextLevelNameLeft;
                LoadingScreenControl.StageName2 = NextLevelNameRight;
                SceneManager.LoadScene(5);
            }
        }
    }

    void LateUpdate()
    {
        if (!firstime)
        {
            ResumePosition = transform.position;
            ResumeRotation = transform.rotation;
            firstime = true;
        }
    }

    public void ResetToCheckPoint()
    {
        Debug.Log("Reset");
        GetComponent<LevelRunState>()?.RestoreRingsAfterDeath();
        GetComponent<LevelRunState>()?.RestoreCheckpointTime();
        Player.Body.position = ResumePosition;
        Player.Body.rotation = ResumeRotation;
        Player.Body.linearVelocity = Vector3.zero;
        Player.Body.angularVelocity = Vector3.zero;
        Player.MoveInput = Vector3.zero;
        Player.Grounded = false;
        Actions.Action04.deadCounter = 0;
        Actions.ChangeAction(0);
        Actions.Action02.ReplenishAirDash();
        Cam.Cam.SetCamera(-9);
    }

    public void SetCheckPoint(Transform position)
    {
        ResumePosition = position.position;
        ResumeRotation = position.rotation;
        ResumeTransform = position;
        GetComponent<LevelRunState>()?.SaveCheckpointTime();
    }

    public void OnTriggerEnter(Collider col)
    {
        if (Actions.Action04Control.isDead) return;
        if (col.tag == "Checkpoint")
        {
            if (col.GetComponent<CheckPointData>() != null)
            {
                //Set Object
                if (!col.GetComponent<CheckPointData>().IsOn)
                {
                    col.GetComponent<CheckPointData>().IsOn = true;
                    col.GetComponent<CheckPointData>().Renderer.material = LampDone;
                    col.GetComponent<AudioSource>().Play();
                    SetCheckPoint(col.GetComponent<CheckPointData>().CheckPos);
                    CurrentCheckPoint = col.gameObject;
                }
            }
        }
        if (col.CompareTag("GoalRing") && !readyForNextStage)
        {
            readyForNextStage = true;
            GetComponent<LevelRunState>()?.CompleteStage();
            StageResultBackdrop.Capture(Camera.main);
            SceneController.LevelToLoad = LevelToGoNext;
            StageConpleteControl.LevelToGoNext = LevelToGoNext;
            col.GetComponent<AudioSource>().clip = GoalRingTouchingSound;
            col.GetComponent<AudioSource>().loop = false;
            col.GetComponent<AudioSource>().Play();
        }
    }
}
