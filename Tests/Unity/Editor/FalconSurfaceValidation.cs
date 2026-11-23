using System;
using Falcon.Haptics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

static class FalconSurfaceValidation
{
    public static void CheckDistributedDemo()
    {
        var scene = EditorSceneManager.OpenScene("Assets/DistributedFalconSurfaceDemo.unity", OpenSceneMode.Single);
        foreach (var root in scene.GetRootGameObjects())
            foreach (var child in root.GetComponentsInChildren<Transform>(true))
                Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) == 0, "Missing script in distributed demo.");
        var input = UnityEngine.Object.FindObjectOfType<FalconInput>();
        var surface = UnityEngine.Object.FindObjectOfType<FalconSurface>();
        var controller = UnityEngine.Object.FindObjectOfType<FalconSurfaceDemo>();
        Require(input != null && surface != null && controller != null, "Incomplete distributed demo.");
        var settings = new SerializedObject(input);
        Require(settings.FindProperty("backend").enumValueIndex == (int)FalconBackendMode.Simulation, "Demo selects hardware.");
        Require(settings.FindProperty("cursor").objectReferenceValue != null, "Cursor reference missing.");
        var controllerSettings = new SerializedObject(controller);
        Require(controllerSettings.FindProperty("falcon").objectReferenceValue == input &&
            controllerSettings.FindProperty("surface").objectReferenceValue == surface, "Demo controller references missing.");
        Require(surface.TryGetGeometry(out _), "Distributed geometry invalid: " + surface.Status);
        Require(surface.FrictionCoefficient == .5f, "Distributed friction preset missing.");
        Debug.Log("FALCON DISTRIBUTED DEMO PASS: no missing scripts, simulation selected, references and geometry valid.");
        EditorApplication.Exit(0);
    }

    public static void Run()
    {
        CheckExport();
        BuildDemo();
        FalconValidation.Run();
    }
    static void Require(bool condition, string message) { if (!condition) throw new Exception(message); }
    static System.Numerics.Vector3 N(Vector3 p) => new System.Numerics.Vector3(p.x, p.y, p.z);

    static void CheckExport()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = new GameObject("Export validation");
        var input = rig.AddComponent<FalconInput>();
        var workspace = rig.AddComponent<FalconWorkspace>();
        workspace.SetScale(new Vector3(10, 20, 15), true);
        rig.transform.SetPositionAndRotation(new Vector3(1, 2, 3), Quaternion.Euler(10, 30, 20));
        var wall = new GameObject("Export face");
        wall.transform.SetPositionAndRotation(new Vector3(.2f, .3f, .4f), Quaternion.Euler(25, -15, 10));
        wall.transform.localScale = new Vector3(2, .7f, 1.2f);
        var box = wall.AddComponent<BoxCollider>();
        box.center = new Vector3(.1f, -.1f, .05f);
        box.size = new Vector3(.8f, .6f, .2f);
        var surface = wall.AddComponent<FalconSurface>();
        foreach (FalconSurfaceFace face in Enum.GetValues(typeof(FalconSurfaceFace)))
        {
            surface.Configure(workspace, face);
            surface.SetFriction(.5f, .003f);
            Require(surface.TryGetGeometry(out var plane), "Cannot export " + face + ": " + surface.Status);
            Require(plane.FrictionCoefficient == .5f && plane.FrictionSpeed == .003f, "Friction not exported.");
            Vector3 normal = face == FalconSurfaceFace.PositiveZ ? Vector3.forward : face == FalconSurfaceFace.NegativeZ ? Vector3.back :
                face == FalconSurfaceFace.PositiveX ? Vector3.right : face == FalconSurfaceFace.NegativeX ? Vector3.left :
                face == FalconSurfaceFace.PositiveY ? Vector3.up : Vector3.down;
            var faceCentre = box.center + Vector3.Scale(normal, box.size * .5f);
            workspace.TryGetMapping(out var map);
            var outward = map.WorldToDevicePosition(N(wall.transform.TransformPoint(faceCentre + normal * .01f)));
            Require(plane.SignedDistance(outward) > 0, "Normal inversion for " + face);
            Require(Math.Abs(plane.SignedDistance(map.WorldToDevicePosition(N(wall.transform.TransformPoint(faceCentre))))) < .000001,
                "Plane location wrong for " + face);
            var start = plane.Centre + plane.Normal * .003f;
            var touch = plane.Centre + plane.Normal * .001f;
            var contact = new FalconSurfaceContact(plane, new FalconSample(start.X, start.Y, start.Z, 1));
            var force = contact.Step(new FalconSample(touch.X, touch.Y, touch.Z, 1), .002f, 100);
            Require(System.Numerics.Vector3.Dot(force, plane.Normal) > 0, "No repulsive force for " + face);
        }
        var duplicate = new GameObject("Duplicate surface").AddComponent<FalconSurface>();
        duplicate.Configure(workspace);
        Require(!duplicate.TryGetGeometry(out _), "Multiple surfaces incorrectly accepted");
        surface.enabled = false;
        Require(duplicate.TryGetGeometry(out _), "Ownership not released on disable");
        var body = duplicate.gameObject.AddComponent<Rigidbody>();
        body.isKinematic = true;
        Require(!duplicate.TryGetGeometry(out _), "Kinematic body incorrectly accepted");
        Debug.Log("FALCON SURFACE EXPORT PASS: six faces, transformed frames, ownership and Rigidbody rejection.");
    }

    static void BuildDemo()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var rig = new GameObject("Falcon · Simulation");
        var input = rig.AddComponent<FalconInput>();
        var workspace = rig.AddComponent<FalconWorkspace>();
        var inputSettings = new SerializedObject(input);
        inputSettings.FindProperty("backend").enumValueIndex = (int)FalconBackendMode.Simulation;
        inputSettings.FindProperty("simulationPositionMetres").vector3Value = new Vector3(0, 0, .006f);
        var cursor = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        cursor.name = "Measured cursor · physical radius 2 mm";
        cursor.transform.localScale = Vector3.one * .04f;
        var cursorCollider = cursor.GetComponent<Collider>();
        UnityEngine.Object.DestroyImmediate(cursorCollider);
        inputSettings.FindProperty("cursor").objectReferenceValue = cursor.transform;
        inputSettings.ApplyModifiedPropertiesWithoutUndo();
        var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = "Haptic face · negative Z";
        wall.transform.position = new Vector3(0, 0, .01f);
        wall.transform.localScale = new Vector3(.6f, .6f, .02f);
        var surface = wall.AddComponent<FalconSurface>();
        surface.Configure(workspace, FalconSurfaceFace.NegativeZ);
        surface.SetFriction(.5f);
        var controller = rig.AddComponent<FalconSurfaceDemo>();
        var settings = new SerializedObject(controller);
        settings.FindProperty("falcon").objectReferenceValue = input;
        settings.FindProperty("surface").objectReferenceValue = wall.GetComponent<FalconSurface>();
        settings.ApplyModifiedPropertiesWithoutUndo();
        var camera = new GameObject("Main Camera").AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.position = new Vector3(.3f, .25f, -1.1f);
        camera.transform.LookAt(Vector3.zero);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.08f, .1f, .13f);
        var light = new GameObject("Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.transform.rotation = Quaternion.Euler(35, -20, 0);
        EditorSceneManager.SaveScene(rig.scene, "Assets/FalconSurfaceDemo.unity");
        Debug.Log("FALCON SURFACE DEMO SAVED");
    }
}
