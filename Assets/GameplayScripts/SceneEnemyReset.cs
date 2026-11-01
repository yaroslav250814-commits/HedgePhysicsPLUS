using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public sealed class SceneEnemyReset : MonoBehaviour
{
    EnemySpawner[] spawners;
    EnemySpawnerEternal[] eternal;
    readonly List<Placement> placed = new List<Placement>();
    Transform templates;
    public int SpawnPointCount => (spawners?.Length ?? 0) + (eternal?.Length ?? 0);
    sealed class Placement
    {
        public GameObject Instance, Template;
        public Transform Parent;
        public Vector3 Position, Scale;
        public Quaternion Rotation;
        public bool Active;
    }
    public void Capture()
    {
        var scene = gameObject.scene;
        var onceList = new List<EnemySpawner>(); var eternalList = new List<EnemySpawnerEternal>(); var enemies = new List<EnemyHealth>();
        foreach (var root in scene.GetRootGameObjects())
        {
            onceList.AddRange(root.GetComponentsInChildren<EnemySpawner>(true));
            eternalList.AddRange(root.GetComponentsInChildren<EnemySpawnerEternal>(true));
            enemies.AddRange(root.GetComponentsInChildren<EnemyHealth>(true));
        }
        spawners = onceList.ToArray(); eternal = eternalList.ToArray();
        if (enemies.Count == 0) return;
        var storage = new GameObject("Enemy restart templates");
        SceneManager.MoveGameObjectToScene(storage, scene); storage.SetActive(false); templates = storage.transform;
        foreach (var enemy in enemies)
        {
            if (enemy.SpawnReference) continue;
            var t = enemy.transform;
            var template = Instantiate(enemy.gameObject, templates); template.SetActive(false);
            placed.Add(new Placement { Instance = enemy.gameObject, Template = template, Parent = t.parent,
                Position = t.localPosition, Rotation = t.localRotation, Scale = t.localScale, Active = enemy.gameObject.activeSelf });
        }
    }
    public void Restore()
    {
        foreach (var spawner in spawners) if (spawner) spawner.ResetAfterPlayerDeath();
        foreach (var spawner in eternal) if (spawner) spawner.ResetAfterPlayerDeath();
        foreach (var item in placed)
        {
            if (item.Instance) { item.Instance.SetActive(false); Destroy(item.Instance); }
            var enemy = Instantiate(item.Template, item.Parent, false);
            if (!item.Parent) SceneManager.MoveGameObjectToScene(enemy, gameObject.scene);
            enemy.transform.localPosition = item.Position; enemy.transform.localRotation = item.Rotation; enemy.transform.localScale = item.Scale;
            enemy.SetActive(item.Active); item.Instance = enemy;
        }
        GetComponent<Objects_Interaction>().updateTargets = true;
    }
    void OnDestroy() { if (templates) Destroy(templates.gameObject); }
}
