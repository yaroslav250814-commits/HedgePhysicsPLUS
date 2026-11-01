using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class EnemySpawner : MonoBehaviour
{
    public float Distance;
    Transform Player;
    public GameObject TeleportSparkle;

    public GameObject Enemy;
    public GameObject SpawnedEnemy { get; private set; }
    public bool HasSpawned { get; private set; }

    void Start()
    {
        Player = GameObject.FindWithTag("Player").transform;
        GetComponent<MeshRenderer>().enabled = false;
    }

    void LateUpdate()
    {
        if (!Player || HasSpawned || (LevelRunState.Current && LevelRunState.Current.GetComponent<HurtControl>().isDead)) return;
        if (Vector3.Distance(Player.position, transform.position) < Distance)
        {
            SpawnInNormal();
        }
    }

    void SpawnInNormal()
    {
        Instantiate(TeleportSparkle, transform.position, transform.rotation);
        SpawnedEnemy = Instantiate(Enemy, transform.position, transform.rotation);
        SceneManager.MoveGameObjectToScene(SpawnedEnemy, gameObject.scene);
        HasSpawned = true;
        HomingAttackControl.UpdateHomingTargets();
    }

    public void ResetAfterPlayerDeath()
    {
        if (SpawnedEnemy) { SpawnedEnemy.SetActive(false); Destroy(SpawnedEnemy); }
        SpawnedEnemy = null; HasSpawned = false;
    }
}
