using UnityEngine;
using System.Collections;
using UnityEngine.SceneManagement;

public class EnemySpawnerEternal : MonoBehaviour
{
    public float Distance;
    Transform Player;

    public float RespawnTime;
    float counter;

    public GameObject Enemy;
    public bool HasSpawned { get; set; }
    public GameObject TeleportSparkle;
    public GameObject SpawnedEnemy { get; private set; }


    void Start()
    {
        HasSpawned = false;
        Player = GameObject.FindWithTag("Player").transform;
        GetComponent<MeshRenderer>().enabled = false;
        counter = RespawnTime;
    }

    void LateUpdate()
    {
        if (!Player || (LevelRunState.Current && LevelRunState.Current.GetComponent<HurtControl>().isDead)) return;
        counter += Time.deltaTime;
        if (Vector3.Distance(Player.position, transform.position) < Distance)
        {
            if (!HasSpawned)
            {
                if (counter > RespawnTime)
                {
                    SpawnInNormal();
                }
            }
        }
    }

    void SpawnInNormal()
    {
        HasSpawned = true;
        Instantiate(TeleportSparkle, transform.position, transform.rotation);
        GameObject em = (GameObject) Instantiate(Enemy, transform.position, transform.rotation);
        SceneManager.MoveGameObjectToScene(em, gameObject.scene); SpawnedEnemy = em;
        em.GetComponent<EnemyHealth>().SpawnReference = this;
        HomingAttackControl.UpdateHomingTargets();
    }

    public void ResartSpawner()
    {
        HasSpawned = false;
        counter = 0;
    }
    public void ResetAfterPlayerDeath()
    {
        if (SpawnedEnemy) { SpawnedEnemy.SetActive(false); Destroy(SpawnedEnemy); }
        SpawnedEnemy = null; HasSpawned = false; counter = RespawnTime + 1;
    }
}
