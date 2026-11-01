using UnityEngine;
using System.Collections;

public class EnemyHealth : MonoBehaviour
{
    public int MaxHealth = 1;
    int HP;
    bool defeated;
    public bool IsDefeated => defeated;

    public GameObject Explosion;
    public EnemySpawnerEternal SpawnReference { get; set; }

    void Awake()
    {
        HP = MaxHealth;
    }

    public void DealDamage(int Damage)
    {
        if (defeated) return;
        HP -= Damage;
        if (HP <= 0)
        {
            defeated = true;
            var run = LevelRunState.Current;
            if (run && run.gameObject.scene == gameObject.scene) run.AwardEnemyScore(transform.position);
            if (SpawnReference != null)
            {
                SpawnReference.ResartSpawner();
            }
            GameObject.Instantiate(Explosion, transform.position, Quaternion.identity);
            gameObject.SetActive(false);
            Destroy(gameObject);
        }
    }
}
