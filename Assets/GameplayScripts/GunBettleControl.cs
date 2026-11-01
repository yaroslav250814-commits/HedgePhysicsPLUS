using UnityEngine;
using System.Collections;

public class GunBettleControl : MonoBehaviour
{
    Transform Player;

    void Start()
    {
        Player = GameObject.FindWithTag("Player").transform;
    }

    void Update()
    {
        var lookPos = Player.position - transform.position;
        lookPos.y = 0;
        if (lookPos.sqrMagnitude < .0001f) return;
        var rotation = Quaternion.LookRotation(lookPos);
        transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.deltaTime * 4);
    }
}
