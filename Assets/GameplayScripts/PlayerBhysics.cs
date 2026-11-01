using UnityEngine;
using UnityEngine.Serialization;
using System.Collections;

public class PlayerBhysics : MonoBehaviour
{
    [Header("Movement Values")]
    [FormerlySerializedAs("MoveAccell"), SerializeField, Tooltip("Velocity added per physics step.")] [Min(0)] private float _MoveAccell = 0.5f;
    public float MoveAccell { get => Mathf.Max(0, _MoveAccell); set => _MoveAccell = Mathf.Max(0, value); }
    public AnimationCurve AccellOverSpeed;
    public float AccellShiftOverSpeed;
    [FormerlySerializedAs("MoveDecell"), SerializeField, Tooltip("Ground coasting divisor per physics step; 1 preserves speed.")] [Min(.01f)] private float _MoveDecell = 1.3f;
    public float MoveDecell { get => Mathf.Max(.01f, _MoveDecell); set => _MoveDecell = Mathf.Max(.01f, value); }
    [FormerlySerializedAs("AirDecell"), SerializeField, Tooltip("Air coasting divisor per physics step; 1 preserves speed.")] [Min(.01f)] private float _AirDecell = 1.05f;
    public float AirDecell { get => Mathf.Max(.01f, _AirDecell); set => _AirDecell = Mathf.Max(.01f, value); }
    public float TangentialDrag;
    public float TangentialDragShiftSpeed;
    public AnimationCurve TangDragOverSpeed;
    [FormerlySerializedAs("TopSpeed"), SerializeField, Tooltip("Speed up to which movement input accelerates the player.")] [Min(.01f)] private float _TopSpeed = 15;
    public float TopSpeed { get => Mathf.Max(.01f, _TopSpeed); set => _TopSpeed = Mathf.Max(.01f, value); }
    [FormerlySerializedAs("MaxSpeed"), SerializeField, Tooltip("Maximum horizontal movement speed.")] [Min(.01f)] private float _MaxSpeed = 30;
    public float MaxSpeed { get => Mathf.Max(.01f, _MaxSpeed); set => _MaxSpeed = Mathf.Max(.01f, value); }
    [FormerlySerializedAs("MaxFallingSpeed"), SerializeField, Tooltip("Terminal fall speed; legacy negative values are also supported.")] private float _MaxFallingSpeed = 30;
    public float MaxFallingSpeed { get => _MaxFallingSpeed; set => _MaxFallingSpeed = value; }
    [Range(.1f, 1f)]
    [FormerlySerializedAs("DescentGravityMultiplier"), SerializeField, Tooltip("Gravity scale while moving down; ascent uses the original gravity.")] private float _DescentGravityMultiplier = 1f;
    public float DescentGravityMultiplier { get => Mathf.Clamp(_DescentGravityMultiplier, .1f,1f); set => _DescentGravityMultiplier = Mathf.Clamp(value, .1f,1f); }
    public float m_JumpPower = 2;
    [FormerlySerializedAs("GroundStickingDistance"), SerializeField, Tooltip("Maximum distance for surface sticking.")] [Min(0)] private float _GroundStickingDistance = 1;
    public float GroundStickingDistance { get => Mathf.Max(0, _GroundStickingDistance); set => _GroundStickingDistance = Mathf.Max(0, value); }
    [FormerlySerializedAs("GroundStickingPower"), SerializeField, Tooltip("Velocity applied toward the surface; normally negative.")] private float _GroundStickingPower = -1;
    public float GroundStickingPower { get => _GroundStickingPower; set => _GroundStickingPower = value; }
    public float SlopeStandingLimit = 0.8f;
    [FormerlySerializedAs("SlopePower"), SerializeField, Tooltip("Acceleration on a slope.")] private float _SlopePower = 0.5f;
    public float SlopePower { get => _SlopePower; set => _SlopePower = value; }
    public float SlopeRunningAngleLimit = 0.5f;
    public float SlopeSpeedLimit = 10;
    [FormerlySerializedAs("UphillMultiplier"), SerializeField, Tooltip("Slope acceleration scale while climbing.")] [Min(0)] private float _UphillMultiplier = 0.5f;
    public float UphillMultiplier { get => Mathf.Max(0, _UphillMultiplier); set => _UphillMultiplier = Mathf.Max(0, value); }
    [FormerlySerializedAs("DownhillMultiplier"), SerializeField, Tooltip("Slope acceleration scale while descending.")] [Min(0)] private float _DownhillMultiplier = 2;
    public float DownhillMultiplier { get => Mathf.Max(0, _DownhillMultiplier); set => _DownhillMultiplier = Mathf.Max(0, value); }
    public float StartDownhillMultiplier = -7;
    public AnimationCurve SlopePowerOverSpeed;
    public float SlopePowerShiftSpeed;
    [FormerlySerializedAs("LandingConversionFactor"), SerializeField, Tooltip("Surface landing impulse scale.")] private float _LandingConversionFactor = 2;
    public float LandingConversionFactor { get => _LandingConversionFactor; set => _LandingConversionFactor = value; }

    [Header("AirMovementExtras")]
    [FormerlySerializedAs("AirControlAmmount"), SerializeField, Tooltip("Strength of movement input while airborne.")] [Min(0)] private float _AirControlAmmount = 2;
    public float AirControlAmmount { get => Mathf.Max(0, _AirControlAmmount); set => _AirControlAmmount = Mathf.Max(0, value); }
    [FormerlySerializedAs("AirSkiddingForce"), SerializeField, Tooltip("Extra braking when steering against airborne momentum.")] [Min(0)] private float _AirSkiddingForce = 10;
    public float AirSkiddingForce { get => Mathf.Max(0, _AirSkiddingForce); set => _AirSkiddingForce = Mathf.Max(0, value); }
    [FormerlySerializedAs("StopAirMovementIfNoInput"), SerializeField, Tooltip("Apply air coasting drag when movement input is released.")] private bool _StopAirMovementIfNoInput = false;
    public bool StopAirMovementIfNoInput { get => _StopAirMovementIfNoInput; set => _StopAirMovementIfNoInput = value; }


    public bool Grounded { get; set; }
    public Vector3 GroundNormal { get; set; }
    public Vector3 CollisionPointsNormal { get; set; }

    public Rigidbody Body { get; set; }

    [FormerlySerializedAs("Gravity"), SerializeField, Tooltip("Velocity added by gravity per physics step.")] private Vector3 _Gravity;
    public Vector3 Gravity { get => _Gravity; set => _Gravity = value; }
    public Vector3 MoveInput { get; set; }

    [Header("Other Values")] public float GroundOffset;
    RaycastHit hit;
    public Transform CollisionPoint;
    public Collider CollisionSphere;
    public Collider CollisionCapsule;
    public Transform MainCamera;
    public Transform Colliders;
    public SonicSoundsControl sounds;

    public float RayToGroundDistance = 0.5f;

    public DebugUI Debui;


    [Header("Rolling Values")]
    [FormerlySerializedAs("RollingLandingBoost"), SerializeField, Tooltip("Landing impulse multiplier while rolling.")] [Min(0)] private float _RollingLandingBoost;
    public float RollingLandingBoost { get => Mathf.Max(0, _RollingLandingBoost); set => _RollingLandingBoost = Mathf.Max(0, value); }
    public float RollingDownhillBoost;
    public float RollingUphillBoost;
    [FormerlySerializedAs("RollingStartSpeed"), SerializeField, Tooltip("Legacy squared velocity threshold for starting a roll.")] [Min(0)] private float _RollingStartSpeed;
    public float RollingStartSpeed { get => Mathf.Max(0, _RollingStartSpeed); set => _RollingStartSpeed = Mathf.Max(0, value); }
    [FormerlySerializedAs("RollingTurningDecreace"), SerializeField, Tooltip("Steering drag scale while rolling.")] [Min(0)] private float _RollingTurningDecreace;
    public float RollingTurningDecreace { get => Mathf.Max(0, _RollingTurningDecreace); set => _RollingTurningDecreace = Mathf.Max(0, value); }
    public bool isRolling { get; set; }

    //Cache

    public float curvePosAcell { get; set; }
    public float curvePosTang { get; set; }
    public float curvePosSlope { get; set; }
    public float b_normalSpeed { get; set; }
    public Vector3 b_normalVelocity { get; set; }
    public Vector3 b_tangentVelocity { get; set; }

    //Etc
    [Header("Etc Values")] public bool UseSphereToGetNormal;

    Vector3 KeepNormal;
    Action02_Homing airAttack;
    float KeepNormalCounter;
    public bool WasOnAir { get; set; }
    public Vector3 PreviousInput { get; set; }
    public Vector3 RawInput { get; set; }
    public Vector3 PreviousRawInput { get; set; }
    public Vector3 PreviousRawInputForAnim { get; set; }
    public float SpeedMagnitude { get; set; }
    public float XZmag { get; set; }

    private void Awake()
    {
        Body = GetComponent<Rigidbody>();
        airAttack = GetComponent<Action02_Homing>();
        Body.maxLinearVelocity = Mathf.Max(MaxSpeed, Mathf.Abs(MaxFallingSpeed)) * 4f;
        Body.interpolation = RigidbodyInterpolation.Interpolate;
        Body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        PreviousInput = transform.forward;
    }

    void FixedUpdate()
    {
        GeneralPhysics();
    }

    void Update()
    {
        InputChecks();
    }

    void InputChecks()
    {
        //Rolling
        if (PlayerAbilityFeatures.ManualRolling && HedgeInput.GetButton("R1") && Body.linearVelocity.sqrMagnitude > RollingStartSpeed)
        {
            isRolling = true;
        }
        if (PlayerAbilityFeatures.ManualRolling && HedgeInput.GetButtonUp("R1"))
        {
            isRolling = false;
        }
    }

    void GeneralPhysics()
    {
        //Set Previous input
        if (RawInput.sqrMagnitude >= 0.03f)
        {
            PreviousRawInputForAnim = RawInput * 90;
            PreviousRawInputForAnim = PreviousRawInputForAnim.normalized;
        }

        if (MoveInput.sqrMagnitude >= 0.9f)
        {
            PreviousInput = MoveInput;
        }
        if (RawInput.sqrMagnitude >= 0.9f)
        {
            PreviousRawInput = RawInput;
        }

        //Set Curve thingies
        curvePosAcell = Mathf.Lerp(curvePosAcell,
            AccellOverSpeed.Evaluate((Body.linearVelocity.sqrMagnitude / MaxSpeed) / MaxSpeed),
            Time.fixedDeltaTime * AccellShiftOverSpeed);
        curvePosTang = Mathf.Lerp(curvePosTang,
            TangDragOverSpeed.Evaluate((Body.linearVelocity.sqrMagnitude / MaxSpeed) / MaxSpeed),
            Time.fixedDeltaTime * TangentialDragShiftSpeed);
        curvePosSlope = Mathf.Lerp(curvePosSlope,
            SlopePowerOverSpeed.Evaluate((Body.linearVelocity.sqrMagnitude / MaxSpeed) / MaxSpeed),
            Time.fixedDeltaTime * SlopePowerShiftSpeed);

        // Apply Max Speed Limit
        XZmag = new Vector3(Body.linearVelocity.x, 0, Body.linearVelocity.z).magnitude;

        // Do it for X and Z
        if (XZmag > MaxSpeed)
        {
            Vector3 ReducedSpeed = Body.linearVelocity;
            float keepY = Body.linearVelocity.y;
            ReducedSpeed = Vector3.ClampMagnitude(ReducedSpeed, MaxSpeed);
            ReducedSpeed.y = keepY;
            Body.linearVelocity = ReducedSpeed;
        }

        // Check for Ground
        if (UseSphereToGetNormal)
        {
            Debug.DrawRay(transform.position, CollisionPointsNormal, Color.blue);
            if (Physics.Raycast(transform.position, -CollisionPointsNormal, out hit, RayToGroundDistance))
            {
                GroundNormal = hit.normal;
                Grounded = true;
                GroundMovement();
            }
            else
            {
                Grounded = false;
                GroundNormal = Vector3.zero;
                AirMovement();
            }
        }
        else
        {
            if (Physics.Raycast(transform.position, -transform.up, out hit, RayToGroundDistance))
            {
                GroundNormal = hit.normal;
                Grounded = true;
                GroundMovement();
            }
            else
            {
                Grounded = false;
                GroundNormal = Vector3.zero;
                AirMovement();
            }
        }

        //Rotate Colliders
        if (Grounded)
        {
            transform.rotation = Quaternion.FromToRotation(transform.up, GroundNormal) * transform.rotation;
            KeepNormal = GroundNormal;
            KeepNormalCounter = 0;
        }
        else
        {
            //Keep the rotation after exiting the ground for a while, to avoid collision issues.
            KeepNormalCounter += 1;
            if (KeepNormalCounter < 5)
            {
                transform.rotation = Quaternion.FromToRotation(transform.up, KeepNormal) * transform.rotation;
            }
            else
            {
                //transform.rotation = Quaternion.FromToRotation(transform.up, GroundNormal) * transform.rotation;
                //transform.rotation = Quaternion.LookRotation(transform.forward, Vector3.up);
                transform.rotation = Quaternion.Euler(0, transform.eulerAngles.y, 0);
                //Debug.Log("Rots");
            }
        }
    }

    void HandleGroundControl(float deltaTime, Vector3 input)
    {
        //By Damizean

        // We assume input is already in the Player's local frame...
        // If there is some HedgeInput...

        if (input.sqrMagnitude != 0.0f)
        {
            // Normalize to get input direction.

            var inputDirection = input.normalized;
            var inputMagnitude = input.magnitude;

            // Fetch velocity in the Player's local frame, decompose into lateral and vertical
            // motion, and decompose lateral motion further into normal and tangential components.

            var velocity = Body.linearVelocity;
            var localVelocity = transform.InverseTransformDirection(velocity);

            var lateralVelocity = new Vector3(localVelocity.x, 0.0f, localVelocity.z);
            var verticalVelocity = new Vector3(0.0f, localVelocity.y, 0.0f);

            var normalSpeed = Vector3.Dot(lateralVelocity, inputDirection);
            var normalVelocity = inputDirection * normalSpeed;
            var tangentVelocity = lateralVelocity - normalVelocity;

            // Note: normalSpeed is the magnitude of normalVelocity, with the added
            // bonus that it's signed. If positive, the speed goes towards the same
            // direction than the input :)


            if (normalSpeed < TopSpeed)
            {
                // Accelerate towards the input direction.
                if (!isRolling)
                {
                    normalSpeed += MoveAccell * deltaTime * inputMagnitude;
                    normalSpeed = Mathf.Min(normalSpeed, TopSpeed);
                }
                else
                {
                    normalSpeed += 0 * deltaTime * inputMagnitude;
                    normalSpeed = Mathf.Min(normalSpeed, TopSpeed);
                }

                // Rebuild back the normal velocity with the correct modulus.
                if (normalSpeed >= 0f)
                {
                    normalVelocity = inputDirection * normalSpeed;
                }
                else
                {
                    // (Reverse the inpit of inputdirection (on x and z, here)
                    normalVelocity = inputDirection * normalSpeed;
                }
            }

            // Additionally, we can apply some drag on the tangent directions for
            // tighter control.

            float curvePosTang = (Body.linearVelocity.sqrMagnitude / MaxSpeed) / MaxSpeed;

            if (!isRolling)
            {
                tangentVelocity = Vector3.MoveTowards(tangentVelocity, Vector3.zero,
                    (TangentialDrag * TangDragOverSpeed.Evaluate(curvePosTang)) * deltaTime * inputMagnitude);
            }
            else
            {
                tangentVelocity = Vector3.MoveTowards(tangentVelocity, Vector3.zero,
                    ((TangentialDrag * RollingTurningDecreace) * TangDragOverSpeed.Evaluate(curvePosTang)) * deltaTime *
                    inputMagnitude);
            }


            // Compose local velocity back and compute velocity back into the Global frame.
            // You probably want to delay doing this to the end of the physics processing,
            // as transformations can incur into numerical damping of the velocities.
            // The last step is included only for the sake of completeness.

            localVelocity = normalVelocity + tangentVelocity + verticalVelocity;
            velocity = transform.TransformDirection(localVelocity);
            Body.linearVelocity = velocity;

            //Export nescessary variables

            b_normalSpeed = normalSpeed;
            b_normalVelocity = normalVelocity;
            b_tangentVelocity = tangentVelocity;

            //DEBUG VARIABLES

            Debui.inputDirection = inputDirection;
            Debui.inputMagnitude = inputMagnitude;
            Debui.velocity = Body.linearVelocity;
            Debui.localVelocity = localVelocity;
            Debui.normalSpeed = normalSpeed;
            Debui.normalVelocity = normalVelocity;
            Debui.tangentVelocity = tangentVelocity;
        }
    }

    void GroundMovement()
    {
        //Stop Rolling
        if (Body.linearVelocity.sqrMagnitude < 20)
        {
            isRolling = false;
        }

        //Slope Physics
        SlopePlysics();

        // Call Ground Control
        HandleGroundControl(1, MoveInput * curvePosAcell);

        //Reduce speed
        if (MoveInput == Vector3.zero)
        {
            Body.linearVelocity = Body.linearVelocity / MoveDecell;
        }

        //Set magnitude reference variable
        SpeedMagnitude = Body.linearVelocity.magnitude;
    }

    void SlopePlysics()
    {
        //ApplyLandingSpeed
        if (WasOnAir && Grounded)
        {
            if (airAttack) airAttack.ReplenishAirDash();
            Vector3 Addsped;

            if (!isRolling)
            {
                Addsped = GroundNormal * LandingConversionFactor;
                StickToGround(GroundStickingPower);
            }
            else
            {
                Addsped = (GroundNormal * LandingConversionFactor) * RollingLandingBoost;
                StickToGround(GroundStickingPower * RollingLandingBoost);
                sounds.SpinningSound();
            }

            Addsped.y = 0;
            AddVelocity(Addsped);
            WasOnAir = false;
        }

        //Get out of slope if speed it too low
        if (Body.linearVelocity.sqrMagnitude < SlopeSpeedLimit && SlopeRunningAngleLimit > GroundNormal.y)
        {
            transform.rotation = Quaternion.identity;
            AddVelocity(GroundNormal * 3);
        }
        else
        {
            //Sticking to ground power
            StickToGround(GroundStickingPower);
        }

        //Apply slope power
        if (Grounded && GroundNormal.y < SlopeStandingLimit)
        {
            if (Body.linearVelocity.y > StartDownhillMultiplier)
            {
                if (!isRolling)
                {
                    Vector3 force = new Vector3(0, (SlopePower * curvePosSlope) * UphillMultiplier, 0);
                    AddVelocity(force);
                }
                else
                {
                    Vector3 force = new Vector3(0, (SlopePower * curvePosSlope) * UphillMultiplier, 0) *
                                    RollingUphillBoost;
                    AddVelocity(force);
                }
            }
            else
            {
                if (MoveInput != Vector3.zero && b_normalSpeed > 0)
                {
                    if (!isRolling)
                    {
                        Vector3 force = new Vector3(0, (SlopePower * curvePosSlope) * DownhillMultiplier, 0);
                        AddVelocity(force);
                    }
                    else
                    {
                        Vector3 force = new Vector3(0, (SlopePower * curvePosSlope) * DownhillMultiplier, 0) *
                                        RollingDownhillBoost;
                        AddVelocity(force);
                    }
                }
                else
                {
                    Vector3 force = new Vector3(0, SlopePower * curvePosSlope, 0);
                    AddVelocity(force);
                }
            }
        }
    }

    public void StickToGround(float StickingPower)
    {
        CollisionPoint.LookAt(transform.position);
        if (Physics.Raycast(CollisionPoint.position, -Colliders.up, out hit, GroundStickingDistance) &&
            !HedgeInput.GetButton("A"))
        {
            Vector3 force = hit.normal * StickingPower;
            AddVelocity(force);
        }
    }

    public void AddVelocity(Vector3 force)
    {
        Body.linearVelocity = Body.linearVelocity + force;
    }

    void AirMovement()
    {
        //AddSpeed
        HandleGroundControl(AirControlAmmount, MoveInput);

        //Get out of roll
        isRolling = false;

        //Apply Gravity
        Vector3 down = Gravity.sqrMagnitude > .0001f ? Gravity.normalized : Vector3.down;
        bool descending = Vector3.Dot(Body.linearVelocity, down) > 0;
        Body.linearVelocity += Gravity * (descending ? DescentGravityMultiplier : 1);

        //Reduce speed
        if (MoveInput == Vector3.zero && StopAirMovementIfNoInput)
        {
            Vector3 ReducedSpeed = Body.linearVelocity;
            ReducedSpeed.x = ReducedSpeed.x / AirDecell;
            ReducedSpeed.z = ReducedSpeed.z / AirDecell;
            Body.linearVelocity = ReducedSpeed;
        }

        //Get set for landing
        WasOnAir = true;

        //Air Skidding  
        if (b_normalSpeed < 0 && !Grounded)
        {
            HandleGroundControl(1, (MoveInput * AirSkiddingForce) * MoveAccell);
        }

        //Max Falling Speed
        float fallingSpeed = Vector3.Dot(Body.linearVelocity, down);
        float limit = Mathf.Abs(MaxFallingSpeed);
        if (fallingSpeed > limit) Body.linearVelocity -= down * (fallingSpeed - limit);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        //Gizmos.DrawRay(DownRay);
    }

    public void OnCollisionStay(Collision col)
    {
        Vector3 Prevnormal = GroundNormal;
        foreach (ContactPoint contact in col.contacts)
        {
            Debug.DrawRay(contact.point, contact.normal, Color.white);

            //Set Middle Point
            Vector3 pointSum = Vector3.zero;
            Vector3 normalSum = Vector3.zero;
            for (int i = 0; i < col.contacts.Length; i++)
            {
                pointSum = pointSum + col.contacts[i].point;
                normalSum = normalSum + col.contacts[i].normal;
            }

            pointSum = pointSum / col.contacts.Length;
            CollisionPointsNormal = normalSum / col.contacts.Length;

            if (Body.linearVelocity.normalized != Vector3.zero)
            {
                CollisionPoint.position = pointSum;
            }
        }
    }
}
