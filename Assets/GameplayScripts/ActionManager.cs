using UnityEngine;
using System.Collections;

public class ActionManager : MonoBehaviour
{
    public int Action { get; set; }

    //Action Scrips, Always leave them in the correct order;

    public Action00_Regular Action00;
    public Action01_Jump Action01;
    public Action02_Homing Action02;
    public Action03_SpinDash Action03;
    public HomingAttackControl Action02Control;
    public Action04_Hurt Action04;
    public HurtControl Action04Control;
    public Action05_Rail Action05;
    public Action06_DropDash Action06;

    //Etc

    PlayerBhysics Phys;

    void Awake()
    {
        Phys = GetComponent<PlayerBhysics>();
        if (!Action06) Action06 = GetComponent<Action06_DropDash>();
        if (!Action06) Action06 = gameObject.AddComponent<Action06_DropDash>();
        Action06.enabled = false;
    }

    public bool TryStartDropDash()
    {
        if (!PlayerAbilityFeatures.DropDash || Time.timeScale <= 0 || (Action != 0 && Action != 1) ||
            Phys.Grounded || Action04Control.isDead || !HedgeInput.GetButtonDown("B")) return false;
        ChangeAction(6);
        Action06.InitialEvents();
        return true;
    }

    public bool TryStartJump()
    {
        if (!PlayerAbilityFeatures.Jump || Time.timeScale <= 0 || Action04Control.isDead ||
            Action != 0 || !Phys.Grounded || !HedgeInput.GetButton("A")) return false;
        Action01.InitialEvents();
        ChangeAction(1);
        return true;
    }

    public bool TryStartSpinDash()
    {
        if (!PlayerAbilityFeatures.SpinDash || Time.timeScale <= 0 || Action04Control.isDead ||
            Action != 0 || !Phys.Grounded || !HedgeInput.GetButtonDown("B")) return false;
        ChangeAction(3);
        Action03.InitialEvents();
        return true;
    }

    public bool TryStartAirAttack()
    {
        if (Time.timeScale <= 0 || Action04Control.isDead || (Action != 0 && Action != 1) || Phys.Grounded ||
            !HedgeInput.GetButtonDown("A") || !Action02.HomingAvailable || !Action02Control.HomingAvailable) return false;
        bool targeted = PlayerAbilityFeatures.HomingAttack && Action02Control.HasTarget;
        if (!targeted && !Action02.AirDashAvailable) return false;
        Action02.IsAirDash = !targeted;
        ChangeAction(2);
        Action02.InitialEvents();
        if (Action != 2) return false;
        if (Action02.IsAirDash) Phys.sounds.AirDashSound();
        else Phys.sounds.HomingAttackSound();
        return true;
    }

    void Start()
    {
        Phys = GetComponent<PlayerBhysics>();
        ChangeAction(0);
    }

    void DeactivateAllActions()
    {
        //Put all actions here
        //Also put variables that you want re-set out of actions
        Action00.enabled = false;
        Action01.enabled = false;
        Action02.enabled = false;
        Action02.ResetHomingVariables();
        Action03.enabled = false;
        Action03.ResetSpinDashVariables();
        Action04.enabled = false;
        Action05.enabled = false;
        Action06.enabled = false;
    }

    //Call this function to change the action

    public void ChangeAction(int ActionToChange)
    {
        Action = ActionToChange;
        DeactivateAllActions();

        //Put an case for all your actions here
        switch (ActionToChange)
        {
            case 0:
                Action00.enabled = true;
                break;
            case 1:
                Action01.enabled = true;
                break;
            case 2:
                Action02.enabled = true;
                break;
            case 3:
                Action03.enabled = true;
                break;
            case 4:
                Action04.enabled = true;
                break;
            case 5:
                Action05.enabled = true;
                break;
            case 6:
                Action06.enabled = true;
                break;
            default:
                Debug.Log("Action is not available.");
                break;
        }
    }
}
