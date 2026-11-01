using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public static class StageResultBackdrop
{
    public static RenderTexture Texture { get; private set; }
    public static void Capture(Camera camera)
    {
        Release();
        if (!camera || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return;
        var request = new UniversalRenderPipeline.SingleCameraRequest();
        if (!RenderPipeline.SupportsRenderRequest(camera, request)) return;
        int width = Mathf.Clamp(Screen.width, 640, 1600);
        int height = Mathf.RoundToInt(width / Mathf.Max(.5f, camera.aspect));
        Texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32) { name = "Stage clear backdrop" };
        Texture.Create(); request.destination = Texture;
        Vector3 oldPosition = camera.transform.position; Quaternion oldRotation = camera.transform.rotation;
        float oldFov = camera.fieldOfView;
        try
        {
            var run = LevelRunState.Current;
            var animator = run ? run.GetComponent<Action00_Regular>().CharacterAnimator : null;
            if (animator)
            {
                bool found = false; Bounds bounds = default;
                foreach (var renderer in animator.GetComponentsInChildren<SkinnedMeshRenderer>())
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy)
                    { if (!found) bounds = renderer.bounds; else bounds.Encapsulate(renderer.bounds); found = true; }
                if (found && bounds.size.y > .05f && bounds.size.y < 20)
                {
                    var up = run.transform.up;
                    var front = Vector3.ProjectOnPlane(animator.transform.forward, up).normalized;
                    if (front.sqrMagnitude < .5f) front = run.transform.forward;
                    var direction = (front + Vector3.Cross(up, front) * .18f).normalized;
                    camera.fieldOfView = 38;
                    float distance = bounds.size.y * .5f / Mathf.Tan(19 * Mathf.Deg2Rad) * 1.35f;
                    camera.transform.position = bounds.center + direction * distance + up * bounds.size.y * .06f;
                    camera.transform.rotation = Quaternion.LookRotation(bounds.center - camera.transform.position, up);
                }
            }
            RenderPipeline.SubmitRenderRequest(camera, request);
        }
        finally { camera.transform.SetPositionAndRotation(oldPosition, oldRotation); camera.fieldOfView = oldFov; }
    }
    public static void Release()
    {
        if (!Texture) return;
        Texture.Release(); Object.Destroy(Texture); Texture = null;
    }
}
