using Falcon.Haptics;
using UnityEngine;

namespace Falcon.Playground
{
    [DefaultExecutionOrder(200)]
    public sealed class FalconPlaygroundVisuals : MonoBehaviour
    {
        [SerializeField] FalconPlayground playground = null;
        [SerializeField] Material lineMaterial = null, idleMaterial = null, contactMaterial = null;
        [SerializeField] Renderer cursorRenderer = null;
        LineRenderer[] axes = new LineRenderer[3], edges = new LineRenderer[12];
        LineRenderer normal, force;
        readonly int[,] connections = { {0,1},{0,2},{0,4},{1,3},{1,5},{2,3},{2,6},{3,7},{4,5},{4,6},{5,7},{6,7} };
        readonly Vector3[] corners = new Vector3[8];

        void Start()
        {
            for (int i = 0; i < 3; i++) axes[i] = MakeLine("Axis " + i, i == 0 ? Color.red : i == 1 ? Color.green : Color.blue);
            for (int i = 0; i < 12; i++) edges[i] = MakeLine("Software bounds " + i, new Color(1, .65f, .15f));
            normal = MakeLine("Surface outward normal", Color.cyan);
            force = MakeLine("Simulation command vector", new Color(1, .3f, .65f));
        }
        LineRenderer MakeLine(string name, Color color)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var line = child.AddComponent<LineRenderer>();
            line.sharedMaterial = lineMaterial; line.positionCount = 2; line.useWorldSpace = true;
            line.startWidth = .0015f; line.endWidth = .001f;
            line.startColor = line.endColor = color;
            line.numCapVertices = 3;
            line.enabled = false;
            return line;
        }
        void LateUpdate()
        {
            if (playground == null || playground.Workspace == null || axes[0] == null) return;
            var workspace = playground.Workspace;
            if (!workspace.TryGetMapping(out var map)) { Hide(); return; }
            var origin = N(workspace.OriginMetres);
            var centre = U(map.DeviceToWorldPosition(origin));
            for (int i = 0; i < 3; i++)
            {
                var alignment = workspace.AxisAlignment;
                var axis = i == 0 ? alignment.Right : i == 1 ? alignment.Up : alignment.TowardsUser;
                Draw(axes[i], centre, U(map.DeviceToWorldPosition(origin + axis * .015f)));
            }
            if (workspace.UsePhysicalBounds)
            {
                var min = workspace.MinimumMetres; var max = workspace.MaximumMetres;
                for (int i = 0; i < 8; i++)
                    corners[i] = U(map.DeviceToWorldPosition(N(new Vector3((i & 1) == 0 ? min.x : max.x,
                        (i & 2) == 0 ? min.y : max.y, (i & 4) == 0 ? min.z : max.z))));
                for (int i = 0; i < 12; i++) Draw(edges[i], corners[connections[i, 0]], corners[connections[i, 1]]);
            }
            else foreach (var edge in edges) edge.enabled = false;
            if (playground.Surface.TryGetGeometry(out var data))
                Draw(normal, U(map.DeviceToWorldPosition(data.Centre)), U(map.DeviceToWorldPosition(data.Centre + data.Normal * .012f)));
            else normal.enabled = false;
            var input = playground.ActiveInput;
            cursorRenderer.sharedMaterial = input != null && input.SurfaceContact ? contactMaterial : idleMaterial;
            if (input != null && input.IsSimulation && input.Connected)
            {
                var start = map.DeviceToWorldPosition(N(input.PositionMetres));
                // Display gain only: 0.03 metres of arrow per commanded newton.
                var displacement = N(input.SimulatedForceNewtons) * .03f;
                Draw(force, U(start), U(start + map.DeviceToWorldVelocity(displacement)));
            }
            else force.enabled = false;
        }
        void Hide()
        {
            foreach (var axis in axes) axis.enabled = false;
            foreach (var edge in edges) edge.enabled = false;
            normal.enabled = force.enabled = false;
        }
        static void Draw(LineRenderer line, Vector3 a, Vector3 b)
        { line.enabled = true; line.SetPosition(0, a); line.SetPosition(1, b); }
        static System.Numerics.Vector3 N(Vector3 v) => new System.Numerics.Vector3(v.x, v.y, v.z);
        static Vector3 U(System.Numerics.Vector3 v) => new Vector3(v.X, v.Y, v.Z);
    }
}
