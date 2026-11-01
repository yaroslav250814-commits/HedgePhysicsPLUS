using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

public class Action03_SpinDash : MonoBehaviour
{
    public Animator CharacterAnimator;
    public Animator BallAnimator;
    CameraControl Cam;
    [FormerlySerializedAs("BallAnimationSpeedMultiplier"), SerializeField, Tooltip("Spin ball animation speed per unit of charge.")] [Min(0)] private float _BallAnimationSpeedMultiplier;
    public float BallAnimationSpeedMultiplier { get => Mathf.Max(0, _BallAnimationSpeedMultiplier); set => _BallAnimationSpeedMultiplier = Mathf.Max(0, value); }

    ActionManager Actions;
    PlayerBhysics Player;
    public SonicSoundsControl sounds;
    public SonicEffectsControl effects;
    [FormerlySerializedAs("SpinDashChargedEffectAmm"), SerializeField, Tooltip("Dust velocity scale per unit of charge.")] [Min(0)] private float _SpinDashChargedEffectAmm;
    public float SpinDashChargedEffectAmm { get => Mathf.Max(0, _SpinDashChargedEffectAmm); set => _SpinDashChargedEffectAmm = Mathf.Max(0, value); }

    public SkinnedMeshRenderer[] PlayerSkin;
    public SkinnedMeshRenderer SpinDashBall;
    public Transform PlayerSkinTransform;


    [FormerlySerializedAs("SpinDashChargingSpeed"), SerializeField, Tooltip("Charge gained per physics step.")] [Min(0)] private float _SpinDashChargingSpeed = 0.3f;
    public float SpinDashChargingSpeed { get => Mathf.Max(0, _SpinDashChargingSpeed); set => _SpinDashChargingSpeed = Mathf.Max(0, value); }
    [FormerlySerializedAs("MinimunCharge"), SerializeField, Tooltip("Minimum charge that produces a ground Spin Dash.")] [Min(0)] private float _MinimunCharge = 10;
    public float MinimunCharge { get => Mathf.Max(0, _MinimunCharge); set => _MinimunCharge = Mathf.Max(0, value); }
    [FormerlySerializedAs("MaximunCharge"), SerializeField, Tooltip("Maximum charge and release speed of the ground Spin Dash.")] [Min(0)] private float _MaximunCharge = 100;
    public float MaximunCharge { get => Mathf.Max(0, _MaximunCharge); set => _MaximunCharge = Mathf.Max(0, value); }
    [FormerlySerializedAs("SpinDashStillForce"), SerializeField, Tooltip("Movement divisor while charging on the ground.")] [Min(.01f)] private float _SpinDashStillForce = 1.05f;
    public float SpinDashStillForce { get => Mathf.Max(.01f, _SpinDashStillForce); set => _SpinDashStillForce = Mathf.Max(.01f, value); }
    float charge;
    Vector3 RawPrevInput;
    Quaternion CharRot;

    [FormerlySerializedAs("ReleaseShakeAmmount"), SerializeField, Tooltip("Camera shake scale at release.")] [Min(0)] private float _ReleaseShakeAmmount;
    public float ReleaseShakeAmmount { get => Mathf.Max(0, _ReleaseShakeAmmount); set => _ReleaseShakeAmmount = Mathf.Max(0, value); }

    void Awake()
    {
        Player = GetComponent<PlayerBhysics>();
        Actions = GetComponent<ActionManager>();
        Cam = GetComponent<CameraControl>();
    }

    public void InitialEvents()
    {
        if (!PlayerAbilityFeatures.SpinDash) { Actions.ChangeAction(0); return; }
        sounds.SpinDashSound();
        charge = 0;
    }

    void FixedUpdate()
    {
        if (!PlayerAbilityFeatures.SpinDash) { Actions.ChangeAction(0); return; }
        charge = Mathf.Min(charge + SpinDashChargingSpeed, MaximunCharge);

        //Lock camera on behind
        Cam.Cam.FollowDirection(3, 14f, -10, 0);

        if (Player.RawInput.sqrMagnitude > 0.9f)
        {
            //RawPrevInput = Player.RawInput;
            RawPrevInput = CharacterAnimator.transform.forward;
        }
        else
        {
            //RawPrevInput = Vector3.Scale(PlayerSkinTransform.forward, Player.GroundNormal);
            //RawPrevInput = Player.PreviousRawInput;
            RawPrevInput = CharacterAnimator.transform.forward;
        }

        effects.DoSpindashDust(1, SpinDashChargedEffectAmm * charge);

        Player.Body.linearVelocity /= SpinDashStillForce;

        if (!HedgeInput.GetButton("B"))
        {
            Release();
        }

        //Stop if not grounded
        if (!Player.Grounded)
        {
            Actions.ChangeAction(0);
        }
    }

    void Release()
    {
        HedgeCamera.Shakeforce = (ReleaseShakeAmmount * charge) / 100;
        if (charge < MinimunCharge)
        {
            sounds.Source2.Stop();
            Actions.ChangeAction(0);
        }
        else
        {
            Player.isRolling = true;
            sounds.SpinDashReleaseSound();
            Player.Body.linearVelocity = charge * (RawPrevInput);
            Actions.ChangeAction(0);
        }
    }

    void Update()
    {
        //Set Animator Parameters
        CharacterAnimator.SetInteger("Action", 0);
        CharacterAnimator.SetFloat("YSpeed", Player.Body.linearVelocity.y);
        CharacterAnimator.SetFloat("GroundSpeed", 0);
        CharacterAnimator.SetBool("Grounded", true);
        CharacterAnimator.SetFloat("NormalSpeed", 0);
        ShowBall(charge);

        //Check if rolling
        //if (Player.Grounded && Player.isRolling) { CharacterAnimator.SetInteger("Action", 1); }
        //CharacterAnimator.SetBool("isRolling", Player.isRolling);

        //Rotation

        if (Player.RawInput.sqrMagnitude < 0.9f)
        {
            CharRot = Player.PreviousRawInput.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(Player.PreviousRawInput, Vector3.up)
                : CharacterAnimator.transform.rotation;
        }
        else
        {
            CharRot = Player.Body.linearVelocity.sqrMagnitude > 0.0001f
                ? Quaternion.LookRotation(Player.Body.linearVelocity, Player.GroundNormal)
                : CharacterAnimator.transform.rotation;
        }
        CharacterAnimator.transform.rotation = Quaternion.Lerp(CharacterAnimator.transform.rotation, CharRot,
            Time.deltaTime * Actions.Action00.skinRotationSpeed);
    }

    public void ShowBall(float visualCharge)
    {
        BallAnimator.SetFloat("SpinCharge", visualCharge);
        BallAnimator.speed = Mathf.Max(1, visualCharge * BallAnimationSpeedMultiplier);
        for (int i = 0; i < PlayerSkin.Length; i++)
        {
            PlayerSkin[i].enabled = false;
        }
        SpinDashBall.enabled = true;
    }

    public void ResetSpinDashVariables()
    {
        for (int i = 0; i < PlayerSkin.Length; i++)
        {
            PlayerSkin[i].enabled = true;
        }
        SpinDashBall.enabled = false;
        charge = 0;
    }
}
