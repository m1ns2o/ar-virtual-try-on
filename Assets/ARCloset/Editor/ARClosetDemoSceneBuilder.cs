using ARCloset;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace ARClosetEditor
{
    public static class ARClosetDemoSceneBuilder
    {
        private const string RootFolder = "Assets/ARCloset";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string PrefabsFolder = RootFolder + "/Prefabs";
        private const string CatalogFolder = RootFolder + "/Catalog";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string MakeHumanFolder = RootFolder + "/MakeHuman";

        [MenuItem("AR Closet/Create Demo Scene")]
        public static void CreateDemoScene()
        {
            EnsureFolders();

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            Material bodyMaterial = CreateTransparentMaterial("M_BodyPreview", new Color(0.12f, 1.0f, 0.32f, 0.68f));
            Material poloMaterial = CreateTexturedMaterial("M_MH_PoloShirt", new Color(0.62f, 0.82f, 0.96f), MakeHumanFolder + "/PoloShirt/Polo_Base_Color.png");
            Material sweaterMaterial = CreateTexturedMaterial("M_MH_FishermanSweater", new Color(0.36f, 0.43f, 0.52f), MakeHumanFolder + "/FishermanSweater/shirt-knit.png");
            Material pantsMaterial = CreateTexturedMaterial("M_MH_WoolPants", new Color(0.20f, 0.24f, 0.30f), MakeHumanFolder + "/WoolPants/Pants_wool.png");
            Material dressMaterial = CreateTexturedMaterial("M_MH_ShiftDress", new Color(0.74f, 0.18f, 0.24f), MakeHumanFolder + "/ShiftDress/ShiftDress.png");
            Material kimonoMaterial = CreateTexturedMaterial("M_MH_Kimono", new Color(0.52f, 0.38f, 0.78f), MakeHumanFolder + "/Kimono/F_Kimono_COL.png");

            GameObject poloPrefab = CreateMakeHumanPrefab(
                "PF_MH_PoloShirt",
                MakeHumanFolder + "/PoloShirt/Polo_t-shirt.obj",
                poloMaterial,
                new Vector3(0f, -0.34f, 0f),
                Vector3.zero,
                Vector3.one * 0.22f);
            GameObject sweaterPrefab = CreateMakeHumanPrefab(
                "PF_MH_FishermanSweater",
                MakeHumanFolder + "/FishermanSweater/sweater_fisherman.obj",
                sweaterMaterial,
                new Vector3(0f, -0.34f, 0f),
                Vector3.zero,
                Vector3.one * 0.22f);
            GameObject pantsPrefab = CreateMakeHumanPrefab(
                "PF_MH_WoolPants",
                MakeHumanFolder + "/WoolPants/pants_wool.obj",
                pantsMaterial,
                new Vector3(0f, 0.08f, 0f),
                Vector3.zero,
                Vector3.one * 0.22f);
            GameObject shiftDressPrefab = CreateMakeHumanPrefab(
                "PF_MH_ShiftDress",
                MakeHumanFolder + "/ShiftDress/dress_shift.obj",
                dressMaterial,
                new Vector3(0f, 0.03f, 0f),
                Vector3.zero,
                Vector3.one * 0.22f);
            GameObject kimonoPrefab = CreateMakeHumanPrefab(
                "PF_MH_Kimono",
                MakeHumanFolder + "/Kimono/f_kimono.obj",
                kimonoMaterial,
                new Vector3(0f, 0.16f, 0f),
                Vector3.zero,
                Vector3.one * 0.22f);

            GarmentCatalog catalog = LoadOrCreateCatalog();
            catalog.garments.Clear();
            catalog.garments.Add(CreateGarmentDefinition("mh-polo-shirt", "MakeHuman Polo Shirt", GarmentSlot.Upper, poloPrefab));
            catalog.garments.Add(CreateGarmentDefinition("mh-fisherman-sweater", "MakeHuman Fisherman Sweater", GarmentSlot.Upper, sweaterPrefab));
            catalog.garments.Add(CreateGarmentDefinition("mh-wool-pants", "MakeHuman Wool Pants", GarmentSlot.Lower, pantsPrefab));
            catalog.garments.Add(CreateGarmentDefinition("mh-shift-dress", "MakeHuman Shift Dress", GarmentSlot.OnePiece, shiftDressPrefab));
            catalog.garments.Add(CreateGarmentDefinition("mh-kimono", "MakeHuman Kimono", GarmentSlot.Outerwear, kimonoPrefab));
            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            GarmentDefinition initialGarment = catalog.garments.Count > 0 ? catalog.garments[0] : null;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Camera overlayCamera = CreateCamera();
            CreateLight();
            Renderer videoBackgroundRenderer = CreateVideoBackground();

            GameObject bridge = new GameObject("MediaPipePoseBridge");
            MediaPipePoseReceiver poseReceiver = bridge.AddComponent<MediaPipePoseReceiver>();
            MediaPipeVideoReceiver videoReceiver = bridge.AddComponent<MediaPipeVideoReceiver>();
            MediaPipeUnityPoseSource unityPoseSource = bridge.AddComponent<MediaPipeUnityPoseSource>();
            SerializedObject poseReceiverObject = new SerializedObject(poseReceiver);
            poseReceiverObject.Update();
            poseReceiverObject.FindProperty("staleAfterSeconds").floatValue = 1.5f;
            poseReceiverObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(poseReceiver);
            SerializedObject videoReceiverObject = new SerializedObject(videoReceiver);
            videoReceiverObject.Update();
            videoReceiverObject.FindProperty("targetRenderer").objectReferenceValue = videoBackgroundRenderer;
            videoReceiverObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(videoReceiver);
            SerializedObject unityPoseSourceObject = new SerializedObject(unityPoseSource);
            unityPoseSourceObject.Update();
            unityPoseSourceObject.FindProperty("receiver").objectReferenceValue = poseReceiver;
            unityPoseSourceObject.FindProperty("previewRenderer").objectReferenceValue = videoBackgroundRenderer;
            unityPoseSourceObject.FindProperty("previewCamera").objectReferenceValue = overlayCamera;
            unityPoseSourceObject.FindProperty("modelFileName").stringValue = "pose_landmarker_full.bytes";
            unityPoseSourceObject.FindProperty("mirrorPreview").boolValue = true;
            unityPoseSourceObject.FindProperty("mirrorInput").boolValue = true;
            unityPoseSourceObject.FindProperty("allowRuntimeCameraSwitch").boolValue = true;
            unityPoseSourceObject.FindProperty("autoSwitchFlatCameraFeed").boolValue = true;
            unityPoseSourceObject.FindProperty("flatCameraVarianceThreshold").floatValue = 300f;
            unityPoseSourceObject.FindProperty("emptyCameraMeanEdgeThreshold").floatValue = 22f;
            unityPoseSourceObject.FindProperty("flatCameraAutoSwitchSeconds").floatValue = 4f;
            unityPoseSourceObject.FindProperty("minPoseDetectionConfidence").floatValue = 0.22f;
            unityPoseSourceObject.FindProperty("minPosePresenceConfidence").floatValue = 0.22f;
            unityPoseSourceObject.FindProperty("minTrackingConfidence").floatValue = 0.22f;
            unityPoseSourceObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(unityPoseSource);

            GameObject mannequin = new GameObject("TrackingMannequin");
            GameObject poseRigObject = new GameObject("PoseRig");
            poseRigObject.transform.SetParent(mannequin.transform);
            poseRigObject.transform.localPosition = Vector3.zero;
            poseRigObject.transform.localRotation = Quaternion.identity;
            poseRigObject.transform.localScale = Vector3.one;

            GameObject garmentAnchorObject = new GameObject("GarmentAnchor");
            garmentAnchorObject.transform.SetParent(mannequin.transform);
            garmentAnchorObject.transform.localPosition = Vector3.zero;
            garmentAnchorObject.transform.localRotation = Quaternion.identity;
            garmentAnchorObject.transform.localScale = Vector3.one;

            RigParts rigParts = CreateMannequinPreview(poseRigObject.transform, bodyMaterial);
            GameObject dynamicSleevesObject = new GameObject("DynamicGarmentSleeves");
            dynamicSleevesObject.transform.SetParent(mannequin.transform);
            dynamicSleevesObject.transform.localPosition = Vector3.zero;
            dynamicSleevesObject.transform.localRotation = Quaternion.identity;
            dynamicSleevesObject.transform.localScale = Vector3.one;
            SleeveParts sleeveParts = CreateDynamicSleeves(dynamicSleevesObject.transform, poloMaterial);

            GarmentFittingController fittingController = mannequin.AddComponent<GarmentFittingController>();
            GarmentHotkeys hotkeys = mannequin.AddComponent<GarmentHotkeys>();
            MediaPipePoseRigDriver rigDriver = mannequin.AddComponent<MediaPipePoseRigDriver>();
            GarmentCatalog sceneCatalog = AssetDatabase.LoadAssetAtPath<GarmentCatalog>(CatalogFolder + "/DemoGarmentCatalog.asset");
            if (sceneCatalog != null)
            {
                catalog = sceneCatalog;
            }

            SerializedObject fitting = new SerializedObject(fittingController);
            fitting.Update();
            fitting.FindProperty("garmentParent").objectReferenceValue = garmentAnchorObject.transform;
            fitting.FindProperty("catalog").objectReferenceValue = catalog;
            fitting.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(fittingController);

            SerializedObject hotkeysObject = new SerializedObject(hotkeys);
            hotkeysObject.Update();
            hotkeysObject.FindProperty("fittingController").objectReferenceValue = fittingController;
            hotkeysObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(hotkeys);

            SerializedObject rigDriverObject = new SerializedObject(rigDriver);
            rigDriverObject.Update();
            rigDriverObject.FindProperty("receiver").objectReferenceValue = poseReceiver;
            rigDriverObject.FindProperty("fittingController").objectReferenceValue = fittingController;
            rigDriverObject.FindProperty("mapToVideoRenderer").boolValue = true;
            rigDriverObject.FindProperty("trackingRenderer").objectReferenceValue = videoBackgroundRenderer;
            rigDriverObject.FindProperty("overlayCamera").objectReferenceValue = overlayCamera;
            rigDriverObject.FindProperty("mapToCameraViewport").boolValue = true;
            rigDriverObject.FindProperty("mirrorX").boolValue = false;
            rigDriverObject.FindProperty("showDebugRig").boolValue = true;
            rigDriverObject.FindProperty("debugRigZOffset").floatValue = -0.35f;
            rigDriverObject.FindProperty("smoothing").floatValue = 0.55f;
            rigDriverObject.FindProperty("fitGarmentByRendererBounds").boolValue = true;
            rigDriverObject.FindProperty("clampGarmentTargetToCamera").boolValue = true;
            rigDriverObject.FindProperty("fitScaleMultiplier").floatValue = 1.0f;
            rigDriverObject.FindProperty("fitOffset").vector2Value = Vector2.zero;
            rigDriverObject.FindProperty("minGarmentScale").floatValue = 0.12f;
            rigDriverObject.FindProperty("maxGarmentScale").floatValue = 8.0f;
            rigDriverObject.FindProperty("minFitVisibility").floatValue = 0.24f;
            rigDriverObject.FindProperty("normalizedOverscan").floatValue = 0.08f;
            rigDriverObject.FindProperty("hideGarmentWhenPoseLost").boolValue = true;
            rigDriverObject.FindProperty("showDynamicSleeves").boolValue = false;
            rigDriverObject.FindProperty("leftUpperSleeve").objectReferenceValue = sleeveParts.LeftUpperSleeve;
            rigDriverObject.FindProperty("leftForearmSleeve").objectReferenceValue = sleeveParts.LeftForearmSleeve;
            rigDriverObject.FindProperty("rightUpperSleeve").objectReferenceValue = sleeveParts.RightUpperSleeve;
            rigDriverObject.FindProperty("rightForearmSleeve").objectReferenceValue = sleeveParts.RightForearmSleeve;
            rigDriverObject.FindProperty("rigRoot").objectReferenceValue = poseRigObject.transform;
            rigDriverObject.FindProperty("garmentAnchor").objectReferenceValue = garmentAnchorObject.transform;
            rigDriverObject.FindProperty("torso").objectReferenceValue = rigParts.Torso;
            rigDriverObject.FindProperty("hips").objectReferenceValue = rigParts.Hips;
            rigDriverObject.FindProperty("head").objectReferenceValue = rigParts.Head;
            rigDriverObject.FindProperty("leftUpperArm").objectReferenceValue = rigParts.LeftUpperArm;
            rigDriverObject.FindProperty("leftForearm").objectReferenceValue = rigParts.LeftForearm;
            rigDriverObject.FindProperty("rightUpperArm").objectReferenceValue = rigParts.RightUpperArm;
            rigDriverObject.FindProperty("rightForearm").objectReferenceValue = rigParts.RightForearm;
            rigDriverObject.FindProperty("leftThigh").objectReferenceValue = rigParts.LeftThigh;
            rigDriverObject.FindProperty("leftCalf").objectReferenceValue = rigParts.LeftCalf;
            rigDriverObject.FindProperty("rightThigh").objectReferenceValue = rigParts.RightThigh;
            rigDriverObject.FindProperty("rightCalf").objectReferenceValue = rigParts.RightCalf;
            rigDriverObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(rigDriver);

            if (initialGarment != null && initialGarment.garmentPrefab != null)
            {
                fittingController.Equip(initialGarment);
            }
            else
            {
                PlaceInitialGarment(garmentAnchorObject.transform);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenesFolder + "/ARClosetDemo.unity");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorGUIUtility.PingObject(catalog);
        }

        private sealed class RigParts
        {
            public Transform Torso;
            public Transform Hips;
            public Transform Head;
            public Transform LeftUpperArm;
            public Transform LeftForearm;
            public Transform RightUpperArm;
            public Transform RightForearm;
            public Transform LeftThigh;
            public Transform LeftCalf;
            public Transform RightThigh;
            public Transform RightCalf;
        }

        private sealed class SleeveParts
        {
            public Transform LeftUpperSleeve;
            public Transform LeftForearmSleeve;
            public Transform RightUpperSleeve;
            public Transform RightForearmSleeve;
        }

        private static void EnsureFolders()
        {
            CreateFolderIfMissing(RootFolder);
            CreateFolderIfMissing(MaterialsFolder);
            CreateFolderIfMissing(PrefabsFolder);
            CreateFolderIfMissing(CatalogFolder);
            CreateFolderIfMissing(ScenesFolder);
        }

        private static void CreateFolderIfMissing(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "Assets";
            string name = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, name);
        }

        private static Material CreateMaterial(string name, Color color)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }

            material.color = color;
            material.SetColor("_Color", color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Glossiness", 0.22f);
            material.SetFloat("_Smoothness", 0.22f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTexturedMaterial(string name, Color color, string albedoPath)
        {
            Material material = CreateMaterial(name, color);
            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);

            if (albedo != null)
            {
                material.SetTexture("_BaseMap", albedo);
                material.SetTexture("_MainTex", albedo);
            }

            material.color = color;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material CreateTransparentMaterial(string name, Color color)
        {
            Material material = CreateMaterial(name, color);
            material.color = color;
            material.SetColor("_Color", color);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GarmentCatalog LoadOrCreateCatalog()
        {
            const string assetName = "DemoGarmentCatalog.asset";
            string path = CatalogFolder + "/" + assetName;
            GarmentCatalog catalog = AssetDatabase.LoadAssetAtPath<GarmentCatalog>(path);

            if (catalog == null)
            {
                catalog = ScriptableObject.CreateInstance<GarmentCatalog>();
                AssetDatabase.CreateAsset(catalog, path);
            }

            return catalog;
        }

        private static GarmentDefinition CreateGarmentDefinition(string id, string displayName, GarmentSlot slot, GameObject prefab)
        {
            string path = CatalogFolder + "/" + id + ".asset";
            GarmentDefinition definition = AssetDatabase.LoadAssetAtPath<GarmentDefinition>(path);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<GarmentDefinition>();
                AssetDatabase.CreateAsset(definition, path);
            }

            definition.garmentId = id;
            definition.displayName = displayName;
            definition.slot = slot;
            definition.garmentPrefab = prefab;
            definition.localScale = Vector3.one;
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static GameObject CreateMakeHumanPrefab(
            string prefabName,
            string modelPath,
            Material material,
            Vector3 localPosition,
            Vector3 localEuler,
            Vector3 localScale)
        {
            AssetDatabase.ImportAsset(modelPath, ImportAssetOptions.ForceSynchronousImport);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);

            if (modelAsset == null)
            {
                Debug.LogWarning($"Could not load MakeHuman model: {modelPath}");
                return null;
            }

            GameObject root = new GameObject(prefabName);
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "MakeHumanModel";
            model.transform.SetParent(root.transform);
            model.transform.localPosition = localPosition;
            model.transform.localRotation = Quaternion.Euler(localEuler);
            model.transform.localScale = localScale;
            AssignMaterial(model, material);
            return SavePrefab(root, prefabName);
        }

        private static void AssignMaterial(GameObject target, Material material)
        {
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static GameObject SavePrefab(GameObject root, string prefabName)
        {
            string path = PrefabsFolder + "/" + prefabName + ".prefab";
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return AssetDatabase.LoadAssetAtPath<GameObject>(path);
        }

        private static void PlaceInitialGarment(Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabsFolder + "/PF_MH_PoloShirt.prefab");
            if (prefab == null)
            {
                Debug.LogWarning("Initial garment prefab could not be loaded.");
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Equipped_MakeHuman Polo Shirt";
            instance.transform.SetParent(parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
        }

        private static RigParts CreateMannequinPreview(Transform parent, Material bodyMaterial)
        {
            GameObject torso = CreatePrimitive("TorsoPreview", PrimitiveType.Capsule, parent, new Vector3(0f, 0.42f, 0f), new Vector3(0.34f, 0.62f, 0.34f), bodyMaterial);
            GameObject hips = CreatePrimitive("HipsPreview", PrimitiveType.Sphere, parent, new Vector3(0f, -0.32f, 0f), new Vector3(0.72f, 0.34f, 0.42f), bodyMaterial);
            GameObject head = CreatePrimitive("HeadPreview", PrimitiveType.Sphere, parent, new Vector3(0f, 1.38f, 0f), new Vector3(0.34f, 0.34f, 0.34f), bodyMaterial);
            GameObject leftUpperArm = CreatePrimitive("LeftUpperArmPreview", PrimitiveType.Capsule, parent, new Vector3(-0.55f, 0.56f, 0f), new Vector3(0.09f, 0.36f, 0.09f), bodyMaterial);
            GameObject leftForearm = CreatePrimitive("LeftForearmPreview", PrimitiveType.Capsule, parent, new Vector3(-0.76f, 0.1f, 0f), new Vector3(0.075f, 0.38f, 0.075f), bodyMaterial);
            GameObject rightUpperArm = CreatePrimitive("RightUpperArmPreview", PrimitiveType.Capsule, parent, new Vector3(0.55f, 0.56f, 0f), new Vector3(0.09f, 0.36f, 0.09f), bodyMaterial);
            GameObject rightForearm = CreatePrimitive("RightForearmPreview", PrimitiveType.Capsule, parent, new Vector3(0.76f, 0.1f, 0f), new Vector3(0.075f, 0.38f, 0.075f), bodyMaterial);
            GameObject leftThigh = CreatePrimitive("LeftThighPreview", PrimitiveType.Capsule, parent, new Vector3(-0.2f, -0.74f, 0f), new Vector3(0.11f, 0.48f, 0.11f), bodyMaterial);
            GameObject leftCalf = CreatePrimitive("LeftCalfPreview", PrimitiveType.Capsule, parent, new Vector3(-0.2f, -1.36f, 0f), new Vector3(0.09f, 0.48f, 0.09f), bodyMaterial);
            GameObject rightThigh = CreatePrimitive("RightThighPreview", PrimitiveType.Capsule, parent, new Vector3(0.2f, -0.74f, 0f), new Vector3(0.11f, 0.48f, 0.11f), bodyMaterial);
            GameObject rightCalf = CreatePrimitive("RightCalfPreview", PrimitiveType.Capsule, parent, new Vector3(0.2f, -1.36f, 0f), new Vector3(0.09f, 0.48f, 0.09f), bodyMaterial);

            torso.name = "TorsoPreview";
            hips.name = "HipsPreview";
            head.name = "HeadPreview";

            return new RigParts
            {
                Torso = torso.transform,
                Hips = hips.transform,
                Head = head.transform,
                LeftUpperArm = leftUpperArm.transform,
                LeftForearm = leftForearm.transform,
                RightUpperArm = rightUpperArm.transform,
                RightForearm = rightForearm.transform,
                LeftThigh = leftThigh.transform,
                LeftCalf = leftCalf.transform,
                RightThigh = rightThigh.transform,
                RightCalf = rightCalf.transform,
            };
        }

        private static SleeveParts CreateDynamicSleeves(Transform parent, Material garmentMaterial)
        {
            GameObject leftUpper = CreatePrimitive("LeftUpperSleeveRig", PrimitiveType.Capsule, parent, new Vector3(-0.52f, 0.48f, -0.16f), new Vector3(0.14f, 0.28f, 0.14f), garmentMaterial);
            GameObject leftForearm = CreatePrimitive("LeftForearmSleeveRig", PrimitiveType.Capsule, parent, new Vector3(-0.74f, 0.08f, -0.16f), new Vector3(0.12f, 0.32f, 0.12f), garmentMaterial);
            GameObject rightUpper = CreatePrimitive("RightUpperSleeveRig", PrimitiveType.Capsule, parent, new Vector3(0.52f, 0.48f, -0.16f), new Vector3(0.14f, 0.28f, 0.14f), garmentMaterial);
            GameObject rightForearm = CreatePrimitive("RightForearmSleeveRig", PrimitiveType.Capsule, parent, new Vector3(0.74f, 0.08f, -0.16f), new Vector3(0.12f, 0.32f, 0.12f), garmentMaterial);

            SetRenderersEnabled(leftUpper.transform, false);
            SetRenderersEnabled(leftForearm.transform, false);
            SetRenderersEnabled(rightUpper.transform, false);
            SetRenderersEnabled(rightForearm.transform, false);

            return new SleeveParts
            {
                LeftUpperSleeve = leftUpper.transform,
                LeftForearmSleeve = leftForearm.transform,
                RightUpperSleeve = rightUpper.transform,
                RightForearmSleeve = rightForearm.transform,
            };
        }

        private static void SetRenderersEnabled(Transform target, bool enabled)
        {
            foreach (Renderer renderer in target.GetComponentsInChildren<Renderer>(true))
            {
                renderer.enabled = enabled;
            }
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            primitive.name = name;
            primitive.transform.SetParent(parent);
            primitive.transform.localPosition = localPosition;
            primitive.transform.localRotation = Quaternion.identity;
            primitive.transform.localScale = localScale;
            primitive.GetComponent<Renderer>().sharedMaterial = material;
            return primitive;
        }

        private static Camera CreateCamera()
        {
            GameObject cameraObject = new GameObject("Main Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            cameraObject.transform.rotation = Quaternion.identity;
            camera.orthographic = true;
            camera.orthographicSize = 3f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 40f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            return camera;
        }

        private static void CreateLight()
        {
            GameObject lightObject = new GameObject("Key Light");
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.95f;
            lightObject.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            RenderSettings.ambientLight = new Color(0.34f, 0.36f, 0.38f);
        }

        private static void CreateFloor()
        {
            Material floorMaterial = CreateMaterial("M_StudioFloor", new Color(0.22f, 0.24f, 0.25f));
            GameObject floor = CreatePrimitive("StudioFloor", PrimitiveType.Plane, null, new Vector3(0f, -1.66f, 0f), new Vector3(3.5f, 1f, 3.5f), floorMaterial);
            floor.transform.rotation = Quaternion.identity;
        }

        private static Renderer CreateVideoBackground()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialsFolder + "/M_VideoBackground.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Texture") ?? Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, MaterialsFolder + "/M_VideoBackground.mat");
            }

            material.color = Color.white;
            material.SetColor("_BaseColor", Color.white);
            material.SetColor("_Color", Color.white);
            material.SetInt("_Cull", 0);
            EditorUtility.SetDirty(material);

            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "LiveVideoBackground";
            quad.transform.position = new Vector3(0f, 0f, 2f);
            quad.transform.rotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(10.67f, 6f, 1f);

            Renderer renderer = quad.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return renderer;
        }
    }
}
