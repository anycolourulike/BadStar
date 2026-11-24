using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Rambler.Crew.UI
{
    //A ship model turning slowly in a UI panel. Show(prefab) copies only the prefab's meshes (no scripts or
    //colliders) onto a stage far below the level, filmed by its own camera into a RenderTexture on this RawImage.
    //Drag to turn it by hand. Runs on unscaled time, so it keeps turning while the game is paused.
    [RequireComponent(typeof(RawImage))]
    public class ShipPreview : MonoBehaviour, IDragHandler
    {
        [SerializeField] float spinSpeed = 16f;
        [Tooltip("Degrees the camera looks down at the ship.")]
        [SerializeField] float pitch = 18f;
        [SerializeField] float fieldOfView = 30f;
        [Tooltip("Spare layer only the preview camera draws.")]
        [SerializeField] int previewLayer = 31;
        [SerializeField] Vector3 stagePosition = new Vector3(0f, -5000f, 0f);
        [SerializeField] Color background = new Color(0.05f, 0.08f, 0.12f, 1f);
        [Tooltip("Seconds after a drag before it starts turning on its own again.")]
        [SerializeField] float resumeAfter = 2.5f;

        RawImage image;
        Camera cam;
        RenderTexture texture;
        Transform stage, pivot, model;
        GameObject shownPrefab;
        float yaw = 210f, lastDrag = -100f, distance, radius;
        Vector2 textureSize;

        void Awake()
        {
            image = GetComponent<RawImage>();
            image.color = background;
        }

        public void Show(ShipDefinition ship) => Show(ship != null ? ship.Prefab : null, ship != null ? ship.ModelRotation : Quaternion.identity);

        public void Show(GameObject prefab, Quaternion upright)
        {
            if (prefab == shownPrefab && model != null) return;
            EnsureStage();
            if (model != null) Destroy(model.gameObject);
            model = null;
            shownPrefab = prefab;
            if (prefab == null) { cam.enabled = false; image.texture = null; image.color = background; return; }

            pivot.localRotation = Quaternion.identity;
            model = new GameObject("Model " + prefab.name).transform;
            model.SetParent(pivot, false);
            CopyMeshes(prefab.transform, model, true);
            model.localRotation = upright * model.localRotation;

            //Centre it on the pivot and back the camera off to fit it
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                model.position -= b.center - pivot.position;
                radius = b.extents.magnitude;
            }
            cam.enabled = isActiveAndEnabled;
            image.color = Color.white;
            Fit();
        }

        //It turns, so fit the sphere round it inside the narrower of the two view angles
        void Fit()
        {
            if (cam == null || radius <= 0f) return;
            float aspect = textureSize.y > 0f ? textureSize.x / textureSize.y : 1f;
            float halfV = fieldOfView * 0.5f * Mathf.Deg2Rad;
            float halfH = Mathf.Atan(Mathf.Tan(halfV) * aspect);
            distance = radius / Mathf.Sin(Mathf.Min(halfV, halfH)) * 0.9f;
            cam.nearClipPlane = Mathf.Max(0.1f, distance - radius * 2f);
            cam.farClipPlane = distance + radius * 2f;
            PlaceCamera();
        }

        void EnsureStage()
        {
            if (stage != null) return;
            stage = new GameObject("Ship Preview Stage").transform;
            stage.position = stagePosition;
            pivot = new GameObject("Pivot").transform;
            pivot.SetParent(stage, false);

            var camGo = new GameObject("Preview Camera");
            camGo.transform.SetParent(stage, false);
            cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            cam.cullingMask = 1 << previewLayer;
            cam.fieldOfView = fieldOfView;
            cam.enabled = false;
            var data = camGo.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            MakeTexture();
        }

        void MakeTexture()
        {
            var rect = ((RectTransform)transform).rect;
            textureSize = new Vector2(Mathf.Max(64f, rect.width), Mathf.Max(64f, rect.height));
            if (texture != null) { cam.targetTexture = null; texture.Release(); Destroy(texture); }
            texture = new RenderTexture(Mathf.RoundToInt(textureSize.x), Mathf.RoundToInt(textureSize.y), 24) { antiAliasing = 4, name = "Ship Preview" };
            cam.targetTexture = texture;
            cam.aspect = textureSize.x / textureSize.y;
            image.texture = texture;
            Fit();
        }

        void LateUpdate()
        {
            if (cam == null || model == null) return;
            var rect = ((RectTransform)transform).rect;
            if (Mathf.Abs(rect.width - textureSize.x) > 2f || Mathf.Abs(rect.height - textureSize.y) > 2f) MakeTexture();
            if (Time.unscaledTime - lastDrag > resumeAfter) yaw += spinSpeed * Time.unscaledDeltaTime;
            pivot.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void PlaceCamera()
        {
            cam.transform.position = pivot.position + Quaternion.Euler(pitch, 0f, 0f) * (Vector3.back * distance);
            cam.transform.LookAt(pivot.position);
        }

        public void OnDrag(PointerEventData e)
        {
            yaw -= e.delta.x * 0.35f;
            lastDrag = Time.unscaledTime;
        }

        void OnEnable() { if (cam != null) cam.enabled = model != null; }
        void OnDisable() { if (cam != null) cam.enabled = false; }

        void OnDestroy()
        {
            if (stage != null) Destroy(stage.gameObject);
            if (texture != null) { texture.Release(); Destroy(texture); }
        }

        //Rebuilds the visible mesh hierarchy only, keeping each part's local placement
        void CopyMeshes(Transform source, Transform target, bool isRoot)
        {
            if (!isRoot)
            {
                target.localPosition = source.localPosition;
                target.localRotation = source.localRotation;
                target.localScale = source.localScale;
            }
            else { target.localRotation = source.localRotation; target.localScale = source.localScale; }
            target.gameObject.layer = previewLayer;

            Mesh mesh = null; Material[] mats = null;
            var mf = source.GetComponent<MeshFilter>();
            var mr = source.GetComponent<MeshRenderer>();
            if (mf != null && mr != null && mr.enabled) { mesh = mf.sharedMesh; mats = mr.sharedMaterials; }
            var smr = source.GetComponent<SkinnedMeshRenderer>();
            if (smr != null && smr.enabled) { mesh = smr.sharedMesh; mats = smr.sharedMaterials; }
            if (mesh != null)
            {
                target.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = target.gameObject.AddComponent<MeshRenderer>();
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            foreach (Transform child in source)
            {
                if (!child.gameObject.activeSelf) continue;
                var copy = new GameObject(child.name).transform;
                copy.SetParent(target, false);
                CopyMeshes(child, copy, false);
            }
        }
    }
}
