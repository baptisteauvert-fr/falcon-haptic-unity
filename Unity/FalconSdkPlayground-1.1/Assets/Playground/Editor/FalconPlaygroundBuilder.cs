using System;
using System.IO;
using Falcon.Haptics;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Falcon.Playground.Editor
{
    public static class FalconPlaygroundBuilder
    {
        public const string ScenePath = "Assets/Scenes/FalconPlayground.unity";
        [MenuItem("Tools/Falcon/Ouvrir le banc d’essai")]
        public static void Open()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath);
        }
        public static void BuildAndValidate()
        {
            BuildScene(); ValidateScene();
            Debug.Log("FALCON PLAYGROUND PROJECT PASS: scene, embedded SDK, simulation default and references valid.");
        }
        static void Field(UnityEngine.Object target, string name, UnityEngine.Object value)
        {
            var so = new SerializedObject(target); so.FindProperty(name).objectReferenceValue = value; so.ApplyModifiedPropertiesWithoutUndo();
        }
        static Material Material(string name, Color color, bool unlit = false)
        {
            string path = "Assets/Playground/Materials/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                var shader = Shader.Find(unlit ? "Sprites/Default" : "Standard");
                if (shader == null) throw new Exception("Required built-in shader unavailable.");
                material = new Material(shader); AssetDatabase.CreateAsset(material, path);
            }
            material.color = color; EditorUtility.SetDirty(material); return material;
        }
        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
        static GameObject Primitive(PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            var result = GameObject.CreatePrimitive(type); result.name = name;
            result.transform.position = position; result.transform.localScale = scale;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(result.GetComponent<Collider>());
            return result;
        }
        public static void BuildScene()
        {
            EnsureFolder("Assets/Scenes"); EnsureFolder("Assets/Calibration"); EnsureFolder("Assets/Playground/Materials");
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var orange = Material("Cursor Idle", new Color(1, .58f, .12f));
            var green = Material("Cursor Contact", new Color(.25f, 1, .55f));
            var wallMaterial = Material("Surface", new Color(.22f, .52f, .65f));
            wallMaterial.color = new Color(.22f, .52f, .65f, .22f);
            wallMaterial.SetFloat("_Mode", 3); wallMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            wallMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); wallMaterial.SetInt("_ZWrite", 0);
            wallMaterial.DisableKeyword("_ALPHATEST_ON"); wallMaterial.EnableKeyword("_ALPHABLEND_ON"); wallMaterial.renderQueue = 3000;
            EditorUtility.SetDirty(wallMaterial);
            var floorMaterial = Material("Platform", new Color(.12f, .17f, .24f));
            var lines = Material("Lines", Color.white, true);
            var controller = new GameObject("Falcon · Panneau de test").AddComponent<FalconPlayground>();
            controller.gameObject.AddComponent<FalconPlaygroundSelfTest>();
            var cursor = Primitive(PrimitiveType.Sphere, "Curseur mesuré · rayon 2 mm", new Vector3(0, 0, -.06f), Vector3.one * .04f, orange, false);
            var host = new GameObject("Périphériques · un seul actif");
            var simulation = CreateRig("Falcon · Simulation", host.transform, cursor.transform, true);
            var hardware = CreateRig("Falcon · Matériel (désactivé au départ)", host.transform, cursor.transform, false);
            var wall = Primitive(PrimitiveType.Cube, "Surface · sélectionner pour les matériaux", new Vector3(0, 0, .01f), new Vector3(.6f, .6f, .02f), wallMaterial, true);
            var surface = wall.AddComponent<FalconSurface>();
            surface.Configure(simulation.GetComponent<FalconWorkspace>(), FalconSurfaceFace.NegativeZ);
            surface.SetFriction(.5f);
            Primitive(PrimitiveType.Cube, "Socle visuel · sans collider", new Vector3(0, -.34f, 0), new Vector3(.85f, .025f, .65f), floorMaterial, false);
            var profile = AssetDatabase.LoadAssetAtPath<FalconCalibrationProfile>("Assets/Calibration/ExampleCalibration.asset");
            if (profile == null)
            { profile = ScriptableObject.CreateInstance<FalconCalibrationProfile>(); AssetDatabase.CreateAsset(profile, "Assets/Calibration/ExampleCalibration.asset"); }
            profile.SetCalibration(Vector3.zero, false, Vector3.zero, Vector3.zero);
            EditorUtility.SetDirty(profile);
            Field(controller, "simulation", simulation); Field(controller, "hardware", hardware);
            Field(controller, "surface", surface); Field(controller, "exampleCalibration", profile); Field(controller, "cursor", cursor.transform);
            var visuals = new GameObject("Repères et commandes visuelles").AddComponent<FalconPlaygroundVisuals>();
            Field(visuals, "playground", controller); Field(visuals, "lineMaterial", lines);
            Field(visuals, "idleMaterial", orange); Field(visuals, "contactMaterial", green); Field(visuals, "cursorRenderer", cursor.GetComponent<Renderer>());
            var camera = new GameObject("Main Camera").AddComponent<Camera>(); camera.tag = "MainCamera";
            camera.transform.position = new Vector3(0, 0, -1.35f); camera.transform.LookAt(Vector3.zero);
            camera.orthographic = true; camera.orthographicSize = .5f;
            camera.fieldOfView = 42; camera.nearClipPlane = .01f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.035f, .055f, .085f);
            camera.rect = new Rect(.46f, 0, .54f, 1);
            var backdrop = new GameObject("Fond de fenêtre").AddComponent<Camera>();
            backdrop.depth = -10; backdrop.cullingMask = 0;
            backdrop.clearFlags = CameraClearFlags.SolidColor; backdrop.backgroundColor = camera.backgroundColor;
            var light = new GameObject("Éclairage").AddComponent<Light>(); light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(35, -25, 0); light.intensity = 1.2f;
            RenderSettings.ambientLight = new Color(.45f, .5f, .6f);
            PlayerSettings.companyName = "Falcon SDK"; PlayerSettings.productName = "Falcon SDK Playground";
            PlayerSettings.bundleVersion = "1.1.0";
            PlayerSettings.defaultScreenWidth = 1440; PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            EditorSceneManager.SaveScene(controller.gameObject.scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath);
            AssetDatabase.SaveAssets();
        }
        static FalconInput CreateRig(string name, Transform host, Transform cursor, bool simulated)
        {
            var root = new GameObject(name); root.transform.SetParent(host, false);
            if (!simulated) root.SetActive(false);
            var input = root.AddComponent<FalconInput>();
            var settings = new SerializedObject(input);
            settings.FindProperty("backend").enumValueIndex = simulated ? 1 : 0;
            settings.FindProperty("cursor").objectReferenceValue = cursor;
            settings.FindProperty("simulationPositionMetres").vector3Value = new Vector3(0, 0, .006f);
            settings.ApplyModifiedPropertiesWithoutUndo();
            root.AddComponent<FalconWorkspace>();
            return input;
        }
        public static void ValidateScene()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var child in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0) throw new Exception("Missing script: " + child.name);
            var controller = UnityEngine.Object.FindFirstObjectByType<FalconPlayground>();
            if (controller == null) throw new Exception("Playground panel missing.");
            var so = new SerializedObject(controller);
            foreach (string field in new[] { "simulation", "hardware", "surface", "exampleCalibration", "cursor" })
                if (so.FindProperty(field).objectReferenceValue == null) throw new Exception("Missing reference: " + field);
            var sim = (FalconInput)so.FindProperty("simulation").objectReferenceValue;
            var hw = (FalconInput)so.FindProperty("hardware").objectReferenceValue;
            if (!sim.gameObject.activeSelf || hw.gameObject.activeSelf) throw new Exception("Unsafe startup selection.");
            if (new SerializedObject(sim).FindProperty("backend").enumValueIndex != 1 || new SerializedObject(hw).FindProperty("backend").enumValueIndex != 0)
                throw new Exception("Backend selection incorrect.");
            var surface = (FalconSurface)so.FindProperty("surface").objectReferenceValue;
            if (!surface.TryGetGeometry(out var geometry) || geometry.FrictionCoefficient != .5f) throw new Exception("Surface invalid.");
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(FalconInput).Assembly);
            if (package == null || package.version != "1.2.0-preview.5" || package.source.ToString() != "Embedded")
                throw new Exception("SDK must be embedded at version 1.2.0-preview.5: " + package?.source);
        }
        public static void BuildWindows()
        {
            ValidateScene();
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            string path = Path.GetFullPath("Builds/Windows/FalconPlayground.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var result = BuildPipeline.BuildPlayer(new[] { ScenePath }, path, BuildTarget.StandaloneWindows64, BuildOptions.None);
            if (result.summary.result != BuildResult.Succeeded) throw new Exception("Player build failed: " + result.summary.result);
            Debug.Log("FALCON PLAYGROUND WINDOWS BUILD PASS");
        }
    }

    [InitializeOnLoad]
    static class FalconPlaygroundStartup
    {
        static FalconPlaygroundStartup()
        {
            if (Application.isBatchMode) return;
            EditorApplication.delayCall += () =>
            {
                if (SessionState.GetBool("Falcon.Playground.Opened", false)) return;
                SessionState.SetBool("Falcon.Playground.Opened", true);
                var active = EditorSceneManager.GetActiveScene();
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !active.isDirty && string.IsNullOrEmpty(active.path) &&
                    File.Exists(FalconPlaygroundBuilder.ScenePath)) EditorSceneManager.OpenScene(FalconPlaygroundBuilder.ScenePath);
            };
        }
    }
}
