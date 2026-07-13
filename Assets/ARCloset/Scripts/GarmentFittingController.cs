using System;
using System.Collections.Generic;
using UnityEngine;

namespace ARCloset
{
    public sealed class GarmentFittingController : MonoBehaviour
    {
        [SerializeField] private Animator avatarAnimator;
        [SerializeField] private Transform garmentParent;
        [SerializeField] private GarmentCatalog catalog;

        private readonly Dictionary<string, Transform> avatarBones = new(StringComparer.OrdinalIgnoreCase);
        private GameObject currentGarment;
        private GarmentRuntimeRig currentRuntimeRig;

        public GarmentDefinition CurrentDefinition { get; private set; }
        public GarmentSlot CurrentSlot => CurrentDefinition != null ? CurrentDefinition.slot : GarmentSlot.Upper;
        public Transform GarmentParent => garmentParent;
        public int GarmentCount => catalog != null && catalog.garments != null ? catalog.garments.Count : 0;

        public int CurrentGarmentIndex
        {
            get
            {
                if (catalog == null || catalog.garments == null || CurrentDefinition == null)
                {
                    return -1;
                }

                return catalog.garments.IndexOf(CurrentDefinition);
            }
        }

        public GarmentDefinition GetGarmentDefinition(int index)
        {
            if (catalog == null || catalog.garments == null || index < 0 || index >= catalog.garments.Count)
            {
                return null;
            }

            return catalog.garments[index];
        }

        private void Awake()
        {
            if (avatarAnimator == null)
            {
                avatarAnimator = GetComponentInChildren<Animator>();
            }

            if (garmentParent == null)
            {
                garmentParent = transform;
            }

            CacheAvatarBones();
            AdoptExistingEquippedGarment();
        }

        public void EquipByIndex(int index)
        {
            if (catalog == null || index < 0 || index >= catalog.garments.Count)
            {
                Debug.LogWarning($"Garment index {index} is not available.");
                return;
            }

            Equip(catalog.garments[index]);
        }

        public void Equip(GarmentDefinition definition)
        {
            if (definition == null || definition.garmentPrefab == null)
            {
                Debug.LogWarning("Garment definition or prefab is missing.");
                return;
            }

            ClearEquippedGarments();

            CurrentDefinition = definition;
            currentGarment = Instantiate(definition.garmentPrefab, garmentParent);
            currentGarment.name = $"Equipped_{definition.displayName}";
            currentGarment.transform.localPosition = definition.localPositionOffset;
            currentGarment.transform.localRotation = Quaternion.Euler(definition.localEulerOffset);
            currentGarment.transform.localScale = definition.localScale;

            PrepareEquippedGarment();
        }

        private void ClearEquippedGarments()
        {
            if (currentGarment != null)
            {
                DestroyGarmentObject(currentGarment);
                currentGarment = null;
                currentRuntimeRig = null;
            }

            if (garmentParent == null)
            {
                return;
            }

            for (int i = garmentParent.childCount - 1; i >= 0; i--)
            {
                Transform child = garmentParent.GetChild(i);
                if (child != null && child.name.StartsWith("Equipped_", StringComparison.Ordinal))
                {
                    DestroyGarmentObject(child.gameObject);
                }
            }
        }

        private static void DestroyGarmentObject(GameObject target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }

        private void CacheAvatarBones()
        {
            avatarBones.Clear();

            Transform root = avatarAnimator != null ? avatarAnimator.transform : transform;
            foreach (Transform bone in root.GetComponentsInChildren<Transform>(true))
            {
                avatarBones[bone.name] = bone;
            }
        }

        private void AdoptExistingEquippedGarment()
        {
            if (currentGarment != null || garmentParent == null)
            {
                return;
            }

            for (int i = 0; i < garmentParent.childCount; i++)
            {
                Transform child = garmentParent.GetChild(i);
                if (child == null || !child.name.StartsWith("Equipped_", StringComparison.Ordinal))
                {
                    continue;
                }

                currentGarment = child.gameObject;
                CurrentDefinition = FindDefinitionForEquippedName(child.name);
                PrepareEquippedGarment();
                return;
            }
        }

        private void PrepareEquippedGarment()
        {
            if (currentGarment == null)
            {
                currentRuntimeRig = null;
                return;
            }

            RebindSkinnedMeshes(currentGarment);

            currentRuntimeRig = currentGarment.GetComponent<GarmentRuntimeRig>();
            if (currentRuntimeRig == null)
            {
                currentRuntimeRig = currentGarment.AddComponent<GarmentRuntimeRig>();
            }

            currentRuntimeRig.Configure(CurrentSlot);
        }

        private GarmentDefinition FindDefinitionForEquippedName(string equippedName)
        {
            if (catalog == null || string.IsNullOrWhiteSpace(equippedName))
            {
                return null;
            }

            string displayName = equippedName.StartsWith("Equipped_", StringComparison.Ordinal)
                ? equippedName.Substring("Equipped_".Length)
                : equippedName;

            foreach (GarmentDefinition definition in catalog.garments)
            {
                if (definition != null && string.Equals(definition.displayName, displayName, StringComparison.OrdinalIgnoreCase))
                {
                    return definition;
                }
            }

            return null;
        }

        private void RebindSkinnedMeshes(GameObject garment)
        {
            SkinnedMeshRenderer[] renderers = garment.GetComponentsInChildren<SkinnedMeshRenderer>(true);

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Transform[] sourceBones = renderer.bones;
                Transform[] reboundBones = new Transform[sourceBones.Length];

                for (int i = 0; i < sourceBones.Length; i++)
                {
                    Transform sourceBone = sourceBones[i];
                    reboundBones[i] = sourceBone != null && avatarBones.TryGetValue(sourceBone.name, out Transform targetBone)
                        ? targetBone
                        : sourceBone;
                }

                renderer.bones = reboundBones;

                if (renderer.rootBone != null && avatarBones.TryGetValue(renderer.rootBone.name, out Transform rootBone))
                {
                    renderer.rootBone = rootBone;
                }
            }
        }

        public bool TryGetCurrentGarmentLocalBounds(out Bounds bounds)
        {
            bounds = default;

            if (currentGarment == null || garmentParent == null)
            {
                return false;
            }

            Renderer[] renderers = currentGarment.GetComponentsInChildren<Renderer>(true);
            bool hasBounds = false;

            foreach (Renderer renderer in renderers)
            {
                if (renderer == null)
                {
                    continue;
                }

                Bounds localBounds = renderer.localBounds;
                Vector3 min = localBounds.min;
                Vector3 max = localBounds.max;

                EncapsulateRendererPoint(renderer, new Vector3(min.x, min.y, min.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(min.x, min.y, max.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(min.x, max.y, min.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(min.x, max.y, max.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(max.x, min.y, min.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(max.x, min.y, max.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(max.x, max.y, min.z), ref bounds, ref hasBounds);
                EncapsulateRendererPoint(renderer, new Vector3(max.x, max.y, max.z), ref bounds, ref hasBounds);
            }

            return hasBounds;
        }

        public bool TryGetCurrentGarmentFitFrame(GarmentSlot slot, out GarmentFitFrame frame)
        {
            frame = default;

            if (!TryGetCurrentGarmentLocalBounds(out Bounds bounds) ||
                bounds.size.x <= 0.001f ||
                bounds.size.y <= 0.001f)
            {
                return false;
            }

            frame = GarmentFitFrame.FromBounds(bounds, slot);
            return true;
        }

        public void SetCurrentGarmentVisible(bool visible)
        {
            if (currentGarment == null)
            {
                AdoptExistingEquippedGarment();
            }

            if (currentGarment == null)
            {
                return;
            }

            foreach (Renderer renderer in currentGarment.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = visible;
            }
        }

        public bool TryGetCurrentGarmentMaterial(out Material material)
        {
            material = null;

            if (currentGarment == null)
            {
                AdoptExistingEquippedGarment();
            }

            if (currentGarment == null)
            {
                return false;
            }

            Renderer renderer = currentGarment.GetComponentInChildren<Renderer>(true);
            if (renderer == null)
            {
                return false;
            }

            material = renderer.sharedMaterial != null ? renderer.sharedMaterial : renderer.material;
            return material != null;
        }

        public bool ApplyCurrentGarmentColor(Color color)
        {
            if (currentGarment == null)
            {
                AdoptExistingEquippedGarment();
            }

            if (currentGarment == null)
            {
                return false;
            }

            bool applied = false;
            foreach (Renderer renderer in currentGarment.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (ApplyMaterialColor(materials[i], color))
                    {
                        applied = true;
                    }
                }
            }

            return applied;
        }

        public bool ApplyCurrentGarmentTexture(Texture texture)
        {
            if (currentGarment == null)
            {
                AdoptExistingEquippedGarment();
            }

            if (currentGarment == null)
            {
                return false;
            }

            bool applied = false;
            foreach (Renderer renderer in currentGarment.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null)
                {
                    continue;
                }

                Material[] materials = renderer.materials;
                for (int i = 0; i < materials.Length; i++)
                {
                    if (ApplyMaterialTexture(materials[i], texture))
                    {
                        applied = true;
                    }
                }
            }

            return applied;
        }

        private static bool ApplyMaterialColor(Material material, Color color)
        {
            if (material == null)
            {
                return false;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }

            if (material.HasProperty("_Color"))
            {
                material.SetColor("_Color", color);
            }

            return true;
        }

        private static bool ApplyMaterialTexture(Material material, Texture texture)
        {
            if (material == null)
            {
                return false;
            }

            material.mainTexture = texture;
            if (material.HasProperty("_BaseMap"))
            {
                material.SetTexture("_BaseMap", texture);
            }

            if (material.HasProperty("_MainTex"))
            {
                material.SetTexture("_MainTex", texture);
            }

            return true;
        }

        public void ApplyRuntimeRig(GarmentRuntimeRig.Pose pose)
        {
            if (currentGarment == null)
            {
                AdoptExistingEquippedGarment();
            }

            if (currentRuntimeRig == null && currentGarment != null)
            {
                PrepareEquippedGarment();
            }

            currentRuntimeRig?.ApplyPose(pose);
        }

        public void ResetRuntimeRig()
        {
            if (currentRuntimeRig == null && currentGarment != null)
            {
                PrepareEquippedGarment();
            }

            currentRuntimeRig?.ResetToRest();
        }

        private void EncapsulateRendererPoint(Renderer renderer, Vector3 rendererLocalPoint, ref Bounds bounds, ref bool hasBounds)
        {
            Vector3 worldPoint = renderer.transform.TransformPoint(rendererLocalPoint);
            Vector3 parentLocalPoint = garmentParent.InverseTransformPoint(worldPoint);

            if (!hasBounds)
            {
                bounds = new Bounds(parentLocalPoint, Vector3.zero);
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(parentLocalPoint);
            }
        }

        public struct GarmentFitFrame
        {
            public Bounds Bounds;
            public Vector3 AnchorLocal;
            public float FitWidth;
            public float FitHeight;

            public static GarmentFitFrame FromBounds(Bounds bounds, GarmentSlot slot)
            {
                float width = Mathf.Max(0.001f, bounds.size.x);
                float height = Mathf.Max(0.001f, bounds.size.y);
                float aspect = width / height;
                float centerX = bounds.center.x;
                float centerZ = bounds.center.z;
                float minY = bounds.min.y;
                float maxY = bounds.max.y;

                switch (slot)
                {
                    case GarmentSlot.Lower:
                    {
                        float waistY = maxY - height * 0.10f;
                        return new GarmentFitFrame
                        {
                            Bounds = bounds,
                            AnchorLocal = new Vector3(centerX, waistY, centerZ),
                            FitWidth = width * Mathf.Lerp(0.76f, 0.88f, Mathf.InverseLerp(0.36f, 0.82f, aspect)),
                            FitHeight = Mathf.Max(0.001f, waistY - minY),
                        };
                    }
                    case GarmentSlot.OnePiece:
                    {
                        // Dress shoulder seams sit close to the top, below collars and sleeve caps.
                        float shoulderY = maxY - height * 0.08f;
                        return new GarmentFitFrame
                        {
                            Bounds = bounds,
                            AnchorLocal = new Vector3(centerX, shoulderY, centerZ),
                            FitWidth = width * EstimateTorsoWidthFraction(aspect, 0.70f, 0.49f),
                            FitHeight = Mathf.Max(0.001f, shoulderY - minY),
                        };
                    }
                    case GarmentSlot.Outerwear:
                    {
                        float shoulderY = maxY - height * 0.18f;
                        return new GarmentFitFrame
                        {
                            Bounds = bounds,
                            AnchorLocal = new Vector3(centerX, shoulderY, centerZ),
                            FitWidth = width * EstimateTorsoWidthFraction(aspect, 0.70f, 0.48f),
                            FitHeight = Mathf.Max(0.001f, shoulderY - minY),
                        };
                    }
                    case GarmentSlot.Upper:
                    default:
                    {
                        // A fitted polo reaches its seam around 12% below max Y, while
                        // wide-sleeve knits reach it around 18%. Aspect separates both.
                        float shoulderInset = Mathf.Lerp(0.12f, 0.18f, Mathf.InverseLerp(1.10f, 1.75f, aspect));
                        float shoulderY = maxY - height * shoulderInset;
                        return new GarmentFitFrame
                        {
                            Bounds = bounds,
                            AnchorLocal = new Vector3(centerX, shoulderY, centerZ),
                            FitWidth = width * EstimateTorsoWidthFraction(aspect, 0.72f, 0.50f),
                            FitHeight = Mathf.Max(0.001f, shoulderY - minY),
                        };
                    }
                }
            }

            private static float EstimateTorsoWidthFraction(float aspect, float fittedFraction, float wideSleeveFraction)
            {
                float wideSleeve = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.82f, 1.70f, aspect));
                return Mathf.Lerp(fittedFraction, wideSleeveFraction, wideSleeve);
            }
        }
    }
}
