using System.Diagnostics;
using System.Numerics;
using Falcon.Haptics;

int passed = 0;
void Test(string name, Action body)
{
    body(); passed++; Console.WriteLine("PASS " + name);
}
void Require(bool condition, string message)
{
    if (!condition) throw new Exception(message);
}
void Until(Func<bool> condition, string message, int timeout = 3000)
{
    var timer = Stopwatch.StartNew();
    while (!condition())
    {
        if (timer.ElapsedMilliseconds > timeout) throw new Exception(message);
        Thread.Sleep(2);
    }
}
void UntilWithHeartbeat(FalconSession session, Func<bool> condition, string message) =>
    Until(() => { session.UpdateHeartbeat(); return condition(); }, message);

Test("alignment: all 48 signed axis permutations preserve directions, inverses and work", () =>
{
    var units = new[] { Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ };
    foreach (int a in new[] { 0, 1, 2 }) foreach (int b in new[] { 0, 1, 2 }) foreach (int c in new[] { 0, 1, 2 })
    {
        if (a == b || a == c || b == c) continue;
        for (int signs = 0; signs < 8; signs++)
        {
            var r = units[a] * ((signs & 1) == 0 ? 1 : -1);
            var u = units[b] * ((signs & 2) == 0 ? 1 : -1);
            var t = units[c] * ((signs & 4) == 0 ? 1 : -1);
            Require(FalconAxisAlignment.TryFromGestures(r * .02f, u * .02f, t * .02f, out var axes, out _), "Valid gestures rejected");
            var origin = new Vector3(.01f, -.02f, .03f);
            var map = new FalconWorkspaceMap(origin, new Vector3(10, 20, 5), true, Matrix4x4.Identity, axes);
            Require(Vector3.Distance(map.DeviceToWorldPosition(origin + r * .01f), new Vector3(.1f, 0, 0)) < .000001f, "Right mapped to wrong axis");
            Require(Vector3.Distance(map.DeviceToWorldPosition(origin + u * .01f), new Vector3(0, .2f, 0)) < .000001f, "Up mapped to wrong axis");
            Require(Vector3.Distance(map.DeviceToWorldPosition(origin + t * .01f), new Vector3(0, 0, -.05f)) < .000001f, "Depth sign wrong");
            var raw = origin + new Vector3(.003f, -.006f, .009f);
            Require(Vector3.Distance(map.WorldToDevicePosition(map.DeviceToWorldPosition(raw)), raw) < .000001f, "Roundtrip failed");
            var force = new Vector3(.2f, -.1f, .3f); var delta = new Vector3(.004f, .002f, -.003f);
            Require(Math.Abs(Vector3.Dot(map.WorldToDeviceForce(force), delta) - Vector3.Dot(force, map.DeviceToWorldVelocity(delta))) < .000001f, "Alignment broke work conservation");
        }
    }
});
Test("alignment: noisy gestures accepted; short, collinear, mixed and invalid gestures rejected", () =>
{
    Require(FalconAxisAlignment.TryFromGestures(new Vector3(.02f, .001f, 0), new Vector3(.001f, .02f, .001f), new Vector3(0, .001f, .02f), out _, out _), "Small gesture noise rejected");
    foreach (var gesture in new[] { Vector3.Zero, Vector3.UnitX * .004f, Vector3.UnitY * .02f, new Vector3(float.NaN), new Vector3(float.MaxValue) })
        Require(!FalconAxisAlignment.TryFromGestures(gesture, Vector3.UnitY * .02f, Vector3.UnitZ * .02f, out _, out _), "Invalid right gesture accepted");
    Require(!FalconAxisAlignment.TryFromGestures(Vector3.UnitX * .02f, Vector3.UnitY * .02f, new Vector3(.02f, 0, .002f), out _, out _), "Mixed depth accepted");
    try { _ = new FalconWorkspaceMap(Vector3.Zero, Vector3.One, true, Matrix4x4.Identity, default(FalconAxisAlignment)); throw new Exception("Default axes accepted"); }
    catch (ArgumentException) { }
});

Test("workspace: position, velocity and force transforms preserve work", () =>
{
    var frame = Matrix4x4.CreateScale(2, 3, .7f) * Matrix4x4.CreateFromYawPitchRoll(.6f, -.3f, .4f) *
        Matrix4x4.CreateTranslation(1, 2, -3);
    frame.M12 += .2f; // Include shear, as produced by rotated non-uniform parent transforms.
    var origin = new Vector3(.01f, -.02f, .03f);
    foreach (bool flip in new[] { false, true })
    {
        var map = new FalconWorkspaceMap(origin, new Vector3(10, 20, 5), flip, frame);
        Require(Vector3.Distance(map.DeviceToWorldPosition(origin), new Vector3(1, 2, -3)) < .00001f, "Origin mapping wrong");
        for (int i = 0; i < 1000; i++)
        {
            var position = new Vector3(.03f * MathF.Sin(i), .02f * MathF.Cos(i), .01f * MathF.Sin(i * .3f));
            Require(Vector3.Distance(map.WorldToDevicePosition(map.DeviceToWorldPosition(position)), position) < .000001f,
                "Position roundtrip exceeded 1 micrometre");
            var velocity = position + new Vector3(.1f);
            var force = new Vector3(.3f, -.2f, .1f);
            var mappedVelocity = map.DeviceToWorldVelocity(velocity);
            var deviceForce = map.WorldToDeviceForce(force);
            float deviceWork = Vector3.Dot(deviceForce, velocity), worldWork = Vector3.Dot(force, mappedVelocity);
            Require(MathF.Abs(deviceWork - worldWork) < .00001f, "Work not preserved");
            Require(Vector3.Distance(map.DeviceToWorldForce(deviceForce), force) < .000001f, "Force roundtrip invalid");
        }
    }
    var simple = new FalconWorkspaceMap(Vector3.Zero, new Vector3(10), true, Matrix4x4.Identity);
    Require(simple.DeviceToWorldPosition(new Vector3(0, 0, .01f)).Z < 0, "Z inversion missing");
    Require(simple.WorldToDeviceForce(Vector3.UnitX).X == 10, "Force incorrectly transformed like a position");
});

Test("surface: repulsive contact, cap, ramp and free-space zero", () =>
{
    var plane = new FalconPlaneSurface(Vector3.Zero, new Vector3(.03f, 0, 0), new Vector3(.01f, .03f, 0), Vector3.UnitZ,
        stiffness: 1000, damping: 0, maximumForce: .6f, slewRate: 2);
    var contact = new FalconSurfaceContact(plane, new FalconSample(0, 0, .003, 1));
    var previous = Vector3.Zero;
    for (int i = 0; i < 200; i++)
    {
        var force = contact.Step(new FalconSample(0, 0, -.001, 1), .002f, 100);
        Require(force.Z >= 0 && force.X == 0 && force.Y == 0 && force.Length() <= .60001f, "Invalid contact force");
        Require((force - previous).Length() <= .00401f, "Rise-rate limit exceeded");
        previous = force;
    }
    Require(contact.InContact && Math.Abs(previous.Z - .6f) < .0001f, "No sustained capped contact");
    Require(contact.Step(new FalconSample(0, 0, .003, 1), .002f, 100) == Vector3.Zero && !contact.InContact,
        "Force retained in free space");
});

Test("surface: finite footprint, edge fade, side and penetration rejection", () =>
{
    var plane = new FalconPlaneSurface(Vector3.Zero, new Vector3(.02f, 0, 0), new Vector3(.01f, .02f, 0), Vector3.UnitZ);
    Require(plane.FootprintGain(Vector3.Zero) == 1 && plane.FootprintGain(new Vector3(.1f, 0, 0)) == 0, "Footprint invalid");
    Require(plane.FootprintGain(plane.HalfU * .99f) > 0 && plane.FootprintGain(plane.HalfU * .99f) < 1, "Edge fade absent");
    try { _ = new FalconSurfaceContact(plane, new FalconSample(0, 0, 0, 1)); throw new Exception("Back-side start accepted"); }
    catch (InvalidOperationException) { }
    var contact = new FalconSurfaceContact(plane, new FalconSample(0, 0, .003, 1));
    try { contact.Step(new FalconSample(0, 0, -.009, 1), .002f, 100); throw new Exception("Deep penetration accepted"); }
    catch (InvalidOperationException) { }
    try { _ = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, maximumForce: 2); throw new Exception("Unsafe cap accepted"); }
    catch (ArgumentException) { }
});

Test("surface: damping never attracts and invalid timing stops calculation", () =>
{
    var plane = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, stiffness: 0, damping: 2);
    var contact = new FalconSurfaceContact(plane, new FalconSample(0, 0, .003, 1));
    Require(contact.Step(new FalconSample(0, 0, .001, 1), .002f, 100).Z > 0, "Damping did not oppose entry");
    for (int i = 0; i < 100; i++)
        Require(contact.Step(new FalconSample(0, 0, .001 + i * .000005, 1), .002f, 100).Z >= 0, "Attractive damping");
    try { contact.Step(new FalconSample(0, 0, .001, 1), .1f, 100); throw new Exception("Stale interval accepted"); }
    catch (InvalidOperationException) { }
});

Test("surface session: geometry freshness, mutation and disabled surface disarm", () =>
{
    var backend = new SimulatedFalconBackend();
    backend.SetSample(new FalconSample(0, 0, .003, 0));
    using var session = new FalconSession(() => backend);
    var plane = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, frictionCoefficient: .5f);
    Until(() => session.State.Connected, "No connection");
    session.SetSurface(plane);
    Require(session.TryArmSurface(100), "Cannot arm surface");
    long released = session.State.Sequence;
    Until(() => { session.UpdateHeartbeat(); session.SetSurface(plane); return session.State.Sequence >= released + 3; }, "No release acknowledgment");
    backend.SetSample(new FalconSample(0, 0, .003, 1));
    Until(() => { session.UpdateHeartbeat(); session.SetSurface(plane); return session.State.EffectActive; }, "Surface not active");
    backend.SetSample(new FalconSample(0, 0, .001, 1));
    Until(() => { session.UpdateHeartbeat(); session.SetSurface(plane); return backend.CommandedForce.Z > 0 && session.State.SurfaceContact; }, "No contact force");
    backend.SetSample(new FalconSample(.001, 0, .001, 1));
    Until(() => { session.UpdateHeartbeat(); session.SetSurface(plane); return session.State.SurfaceFrictionPeakNewtons > 0; }, "Friction peak not recorded");
    // Keep the general heartbeat alive: expiration must come from geometry, not the application.
    UntilWithHeartbeat(session, () => !session.State.EffectArmed && !session.State.EffectActive, "Stale geometry did not stop output");
    Require(backend.CommandedForce == Vector3.Zero && !session.TryArmSurface() && session.State.SurfaceFrictionPeakNewtons == 0,
        "Stale geometry allowed force or retained its peak");
    session.SetSurface(plane);
    Require(session.TryArmSurface(), "Cannot explicitly rearm");
    session.SetSurface(new FalconPlaneSurface(new Vector3(0, 0, -.001f), Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ));
    Require(!session.State.EffectArmed, "Geometry mutation retained request");
    session.SetSurface(null);
    Require(!session.TryArmSurface(), "Removed surface allowed arming");
});

Test("workspace: reject invalid and singular mappings", () =>
{
    foreach (var scale in new[] { Vector3.Zero, new Vector3(-1, 1, 1), new Vector3(float.NaN, 1, 1) })
    {
        try { _ = new FalconWorkspaceMap(Vector3.Zero, scale, true, Matrix4x4.Identity); throw new Exception("Invalid scale accepted"); }
        catch (ArgumentException) { }
    }
    try { _ = new FalconWorkspaceMap(Vector3.Zero, Vector3.One, true, Matrix4x4.CreateScale(0)); throw new Exception("Singular map accepted"); }
    catch (ArgumentException) { }
    try { default(FalconWorkspaceMap).DeviceToWorldPosition(Vector3.Zero); throw new Exception("Default map accepted"); }
    catch (InvalidOperationException) { }
});

Test("friction: dissipative tangent through turns and reversals", () =>
{
    var normal = Vector3.Normalize(new Vector3(1, 2, 3));
    var u = Vector3.Normalize(Vector3.Cross(normal, Vector3.UnitX)) * .03f;
    var v = Vector3.Normalize(Vector3.Cross(normal, u)) * .03f;
    var plane = new FalconPlaneSurface(Vector3.Zero, u, v, normal, stiffness: 100, damping: 0,
        frictionCoefficient: .7f, maximumForce: .3f);
    var previous = normal * .003f;
    var contact = new FalconSurfaceContact(plane, new FalconSample(previous.X, previous.Y, previous.Z, 1));
    int sliding = 0;
    for (int i = 0; i < 10000; i++)
    {
        var position = normal * .001f + u * (.2f * MathF.Sin(i * .03f)) + v * (.2f * MathF.Cos(i * .02f));
        var measuredVelocity = (position - previous) / .002f;
        var force = contact.Step(new FalconSample(position.X, position.Y, position.Z, 1), .002f, 100);
        var friction = contact.TangentialForceNewtons;
        Require(Vector3.Dot(friction, measuredVelocity) <= .0000001f, "Friction assisted motion");
        Require(Math.Abs(Vector3.Dot(friction, normal)) < .000001f, "Friction not tangent");
        Require(friction.Length() <= .7f * contact.NormalForceNewtons + .000001f, "Coulomb bound exceeded");
        Require(force.Length() <= .300001f && contact.NormalForceNewtons >= 0, "Shared budget exceeded");
        if (friction.Length() > .001f) sliding++;
        previous = position;
    }
    Require(sliding > 100, "No meaningful friction produced");
    contact.Step(new FalconSample(previous.X, previous.Y, previous.Z, 1), .002f, 100);
    Require(contact.TangentialForceNewtons == Vector3.Zero, "Friction retained at rest");
});

Test("friction: shared force cap gives normal contact priority", () =>
{
    var plane = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
        stiffness: 100, damping: 0, maximumForce: .15f, frictionCoefficient: 100, slewRate: 100);
    var contact = new FalconSurfaceContact(plane, new FalconSample(0, 0, .003, 1));
    for (int i = 0; i < 500; i++)
    {
        var force = contact.Step(new FalconSample(i * .00001, 0, .001, 1), .002f, 100);
        Require(force.Length() <= .150001f, "Combined force cap exceeded");
    }
    Require(Math.Abs(contact.NormalForceNewtons - .1f) < .00001f && contact.TangentialForceNewtons.Length() > .05f,
        "Normal priority or tangential budget wrong");
    contact.Step(new FalconSample(.005, 0, .001, 1), .002f, 0);
    Require(contact.NormalForceNewtons == 0 && contact.TangentialForceNewtons == Vector3.Zero, "Zero gain retained force");
});

Test("friction: smooth low-speed limit, loss of contact and disabled compatibility", () =>
{
    float Measure(float speed)
    {
        var plane = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ,
            stiffness: 100, damping: 0, frictionCoefficient: .5f, slewRate: 100);
        var contact = new FalconSurfaceContact(plane, new FalconSample(0, 0, .003, 1));
        for (int i = 0; i < 200; i++) contact.Step(new FalconSample(0, 0, .001, 1), .002f, 100);
        contact.Step(new FalconSample(speed * .002, 0, .001, 1), .002f, 100);
        float force = contact.TangentialForceNewtons.Length();
        var output = contact.Step(new FalconSample(speed * .002, 0, .003, 1), .002f, 100);
        Require(output == Vector3.Zero && contact.TangentialForceNewtons == Vector3.Zero, "Friction outside contact");
        return force;
    }
    float slow = Measure(.00001f), medium = Measure(.001f), fast = Measure(.02f);
    Require(slow > 0 && slow < medium && medium < fast && fast <= .05f, "Low-speed regularization invalid");
    var disabled = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ);
    var legacy = new FalconSurfaceContact(disabled, new FalconSample(0, 0, .003, 1));
    for (int i = 0; i < 200; i++)
    {
        var output = legacy.Step(new FalconSample(i * .00001, 0, .001, 1), .002f, 100);
        Require(output.X == 0 && output.Y == 0 && legacy.TangentialForceNewtons == Vector3.Zero, "Default changed legacy friction");
    }
});

Test("friction: reject invalid parameters and disarm on material mutation", () =>
{
    foreach (float invalid in new[] { -1f, float.NaN, float.PositiveInfinity })
    {
        try { _ = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, frictionCoefficient: invalid); throw new Exception("Invalid friction accepted"); }
        catch (ArgumentException) { }
    }
    foreach (float invalid in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
    {
        try { _ = new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, frictionSpeed: invalid); throw new Exception("Invalid regularization accepted"); }
        catch (ArgumentException) { }
    }
    var backend = new SimulatedFalconBackend();
    using var session = new FalconSession(() => backend);
    Until(() => session.State.Connected, "No connection");
    session.SetSurface(new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ));
    Require(session.TryArmSurface(), "Cannot arm");
    session.SetSurface(new FalconPlaneSurface(Vector3.Zero, Vector3.UnitX, Vector3.UnitY, Vector3.UnitZ, frictionCoefficient: .5f));
    Require(!session.State.EffectArmed, "Friction mutation did not disarm");
});

Test("calibration: observations, margin and minimum movement", () =>
{
    var capture = new FalconCalibrationCapture();
    Require(!capture.TryComplete(.005f, .001f, out _, out _), "Empty calibration accepted");
    for (int i = 0; i < 20; i++) capture.AddSample(new Vector3(i * .001f, i * .002f, i * .003f));
    Require(capture.TryComplete(.005f, .001f, out var result, out _), "Valid capture rejected");
    Require(result.SampleCount == 20 && Vector3.Distance(result.OriginMetres, new Vector3(.0095f, .019f, .0285f)) < .000001f,
        "Capture centre wrong");
    Require(Vector3.Distance(result.Bounds.MinimumMetres, new Vector3(.001f)) < .000001f, "Margin missing");
    Require(!capture.TryComplete(.005f, 1, out _, out _), "Excessive margin accepted");
    var flat = new FalconCalibrationCapture();
    for (int i = 0; i < 20; i++) flat.AddSample(new Vector3(i * .001f, 0, 0));
    Require(!flat.TryComplete(.005f, 0, out _, out _), "Unmoved axes accepted");
    try { flat.AddSample(new Vector3(float.NaN)); throw new Exception("NaN capture accepted"); }
    catch (ArgumentException) { }
    var huge = new FalconCalibrationCapture();
    for (int i = 0; i < 10; i++) huge.AddSample(new Vector3(i % 2 == 0 ? -float.MaxValue : float.MaxValue));
    Require(!huge.TryComplete(.005f, 0, out _, out _), "Overflowing capture accepted");
});

Test("workspace bounds: clamping and edge inclusion", () =>
{
    var bounds = new FalconPhysicalBounds(new Vector3(-.01f), new Vector3(.01f));
    Require(bounds.Contains(new Vector3(.01f, 0, 0)), "Boundary excluded");
    Require(!bounds.Contains(new Vector3(.011f, 0, 0)), "Out of bounds accepted");
    Require(bounds.Clamp(new Vector3(.1f, -.1f, 0)) == new Vector3(.01f, -.01f, 0), "Clamp wrong");
    try { _ = new FalconPhysicalBounds(Vector3.One, Vector3.Zero); throw new Exception("Inverted bounds accepted"); }
    catch (ArgumentException) { }
});

Test("simulation: finite input and bounded output", () =>
{
    using var backend = new SimulatedFalconBackend();
    backend.Connect();
    try { backend.SetSample(new FalconSample(double.NaN, 0, 0, 0)); throw new Exception("NaN accepted"); }
    catch (ArgumentException) { }
    backend.BeginHaptics(HapticMode.Spring);
    foreach (var value in new[] { new Vector3(float.NaN, 0, 0), new Vector3(float.PositiveInfinity, 0, 0), new Vector3(5, 0, 0) })
    {
        try { backend.ApplyHaptics(value); throw new Exception("Invalid force accepted"); }
        catch (InvalidOperationException) { }
    }
    backend.ApplyHaptics(new Vector3(.1f, 0, 0));
    backend.SetAvailable(false);
    Require(backend.CommandedForce == Vector3.Zero, "Disconnect retained output");
});

Test("DHD cleanup: zero, disable, close and quarantine after failed close", () =>
{
    foreach (string failure in new[] { "none", "zero", "disable", "close" })
    {
        var calls = new List<string>();
        var device = new FalconDevice(calls, failure);
        try
        {
            try { device.Dispose(); Require(failure == "none", "Cleanup error hidden"); }
            catch (FalconCleanupException) { Require(failure != "none", "Unexpected cleanup error"); }
            Require(calls.SequenceEqual(new[] { "zero", "disable", "close" }), "Cleanup order or fallback invalid");
            Require(device.ProbeLibraryRetained == (failure == "close"), "DLL quarantine invalid");
            Require(device.ProbeOwnsSession == (failure == "close"), "Session reservation invalid");
        }
        finally { device.ProbeAllowClose(); device.Dispose(); }
    }
});

Test("playback: release before activation, release stops force", () =>
{
    using var backend = new SimulatedFalconBackend();
    backend.Connect();
    using var playback = new HapticPlayback(backend);
    var request = new EffectRequest(HapticMode.Spring);
    Require(!playback.Update(request, new FalconSample(0, 0, 0, 1), 100), "Held button armed output");
    Require(!playback.Update(request, new FalconSample(0, 0, 0, 0), 100), "Release activated output");
    Require(playback.Update(request, new FalconSample(0, 0, 0, 1), 100), "Press did not activate");
    Thread.Sleep(5);
    playback.Update(request, new FalconSample(.002, 0, 0, 1), 100);
    Require(backend.CommandedForce.X < 0, "Spring direction invalid");
    Require(!playback.Update(request, new FalconSample(.002, 0, 0, 0), 100) &&
        backend.CommandedForce == Vector3.Zero, "Release did not zero output");
});

Test("session: sequence, metrics, coherent concurrent reads, confirmed stop", () =>
{
    var backend = new SimulatedFalconBackend();
    using var session = new FalconSession(() => backend);
    Until(() => session.State.Sequence > 5, "No samples");
    Parallel.For(0, 10000, i =>
    {
        backend.SetSample(new FalconSample(i, i, i, 0));
        var state = session.State;
        if (state.Sample is FalconSample sample)
            Require(sample.X == sample.Y && sample.Y == sample.Z, "Torn sample");
    });
    Require(session.State.IntervalCount > 0 && session.State.MaximumIntervalMilliseconds > 0, "No metrics");
    Require(session.Stop() && !session.IsRunning && session.State.Connection == FalconConnectionState.Stopped,
        "Stop not confirmed");
});

Test("session: blocked read becomes stale; stop times out without concurrent dispose", () =>
{
    var backend = new ProbeBackend();
    var session = new FalconSession(() => backend);
    try
    {
        Until(() => session.State.Connected, "No initial sample");
        backend.BlockRead = true;
        Require(backend.Entered.Wait(1000), "Read did not block");
        Until(() => session.State.Connection == FalconConnectionState.Stale, "Blocked sample stayed fresh");
        Require(!session.State.Connected && !session.TryArmEffect(HapticMode.Spring), "Stale state allowed arming");
        Require(!session.Stop(20) && session.IsRunning, "Blocked stop falsely confirmed");
        Require(!backend.Disposed, "Backend disposed concurrently with read");
    }
    finally { backend.Release.Set(); Require(session.Stop(), "Unblocked worker did not stop"); }
    Require(backend.Disposed && backend.AllCallsOnOwner, "Ownership violated");
});

Test("session: disconnect reconnects read-only", () =>
{
    var backend = new SimulatedFalconBackend();
    using var session = new FalconSession(() => backend);
    Until(() => session.State.Connected, "No connection");
    Require(session.TryArmEffect(HapticMode.Spring), "Cannot arm");
    backend.SetAvailable(false);
    Until(() => session.State.Connection == FalconConnectionState.Faulted, "Disconnect not surfaced");
    Require(!session.State.EffectArmed, "Disconnect kept request");
    backend.SetAvailable(true);
    Until(() => session.State.Connected, "No reconnect");
    Require(!session.State.EffectArmed && !session.State.EffectActive, "Reconnect rearmed output");
});

Test("session: heartbeat expiry and invalid requests", () =>
{
    var backend = new SimulatedFalconBackend();
    using var session = new FalconSession(() => backend);
    Until(() => session.State.Connected, "No connection");
    Require(!session.TryArmEffect((HapticMode)999), "Invalid mode accepted");
    Require(session.TryArmEffect(HapticMode.Spring), "Cannot arm");
    Until(() => session.State.EffectArmed, "No armed state");
    long released = session.State.Sequence;
    UntilWithHeartbeat(session, () => session.State.Sequence >= released + 3, "Released samples not processed");
    backend.SetSample(new FalconSample(0, 0, 0, 1));
    UntilWithHeartbeat(session, () => session.State.EffectActive, "Effect never activated");
    backend.SetSample(new FalconSample(.003, 0, 0, 1));
    UntilWithHeartbeat(session, () => backend.CommandedForce.X < 0, "No force before expiry");
    Until(() => !session.State.EffectArmed, "Heartbeat did not expire");
    Until(() => backend.CommandedForce == Vector3.Zero && !session.State.EffectActive,
        "Expired heartbeat kept force");
});

Test("session: leaving workspace disarms in the worker; return does not rearm", () =>
{
    var backend = new SimulatedFalconBackend();
    using var session = new FalconSession(() => backend);
    session.SetPhysicalBounds(new FalconPhysicalBounds(new Vector3(-.01f), new Vector3(.01f)));
    Until(() => session.State.Connected, "No connection");
    Require(session.TryArmEffect(HapticMode.Spring), "Cannot arm inside bounds");
    long released = session.State.Sequence;
    UntilWithHeartbeat(session, () => session.State.Sequence >= released + 3, "Released samples not processed");
    backend.SetSample(new FalconSample(0, 0, 0, 1));
    UntilWithHeartbeat(session, () => session.State.EffectActive, "No active effect");
    backend.SetSample(new FalconSample(.02, 0, 0, 1));
    UntilWithHeartbeat(session, () => !session.State.EffectArmed && !session.State.EffectActive, "Bounds did not disarm");
    Require(backend.CommandedForce == Vector3.Zero && !session.TryArmEffect(HapticMode.Spring), "Outside workspace allowed force");
    backend.SetSample(new FalconSample(0, 0, 0, 1));
    Until(() => session.State.Sample?.X == 0, "No return sample");
    Require(!session.State.EffectArmed, "Return rearmed effect");
    Require(session.TryArmEffect(HapticMode.Spring), "Cannot explicitly rearm");
    session.SetPhysicalBounds(null);
    Until(() => !session.State.EffectArmed && !session.State.EffectActive, "Bounds change did not disarm");
});

Test("effects: all six formulas remain finite and bounded", () =>
{
    foreach (HapticMode mode in Enum.GetValues<HapticMode>())
    {
        var origin = new FalconSample(0, 0, 0, 0);
        var effect = new HapticEffect(mode, origin);
        for (int i = 0; i < 2000; i++)
        {
            var sample = new FalconSample(.01 * Math.Sin(i * .03), .01 * Math.Sin(i * .02),
                .01 * Math.Sin(i * .04), 1);
            var force = effect.Step(sample, .002f, i % 101);
            Require(float.IsFinite(force.LengthSquared()) && force.Length() <= HapticEffect.ForceLimit(mode) + .0001f,
                "Invalid force for " + mode);
        }
    }
});

Test("session: slow read prevents activation", () =>
{
    var backend = new ProbeBackend();
    using var session = new FalconSession(() => backend);
    Until(() => session.State.Connected, "No connection");
    Require(session.TryArmEffect(HapticMode.Spring), "Cannot arm");
    backend.DelayMilliseconds = 70;
    Until(() => session.State.Connection == FalconConnectionState.Faulted, "Slow read accepted");
    Require(!session.State.EffectArmed && !session.State.EffectActive, "Slow loop kept output armed");
});

Test("session: disposal failure is reported", () =>
{
    var backend = new ProbeBackend { FailDispose = true };
    var session = new FalconSession(() => backend);
    Until(() => session.State.Connected, "No connection");
    Require(session.Stop(), "Stop failed");
    Require(session.State.EffectError?.Contains("injected cleanup failure") == true, "Cleanup error hidden");
});

Test("session: uncertain cleanup prevents automatic reconnect", () =>
{
    int attempts = 0;
    var backend = new ProbeBackend { FailDispose = true, CleanupFailure = true };
    var session = new FalconSession(() => { Interlocked.Increment(ref attempts); return backend; });
    Until(() => session.State.Connected, "No connection");
    backend.FailRead = true;
    Until(() => !session.IsRunning, "Cleanup failure left retry loop running");
    Require(attempts == 1 && session.State.Connection == FalconConnectionState.Faulted,
        "Uncertain cleanup allowed reconnect");
    session.Dispose();
    Require(session.State.Connection == FalconConnectionState.Faulted, "Dispose hid the terminal fault");
});

Test("session: stop registry and repeated restarts", () =>
{
    for (int i = 0; i < 100; i++)
    {
        using var session = new FalconSession(() => new SimulatedFalconBackend());
        Require(session.Stop(), "Restart left a worker alive");
    }
    var first = new FalconSession(() => new SimulatedFalconBackend());
    var second = new FalconSession(() => new SimulatedFalconBackend());
    Require(FalconSession.StopAll() && !first.IsRunning && !second.IsRunning, "Registry missed worker");
});

Console.WriteLine($"{passed} tests passed. No hardware loaded or driven.");

sealed class ProbeBackend : IFalconBackend
{
    public volatile bool BlockRead, Disposed, FailRead;
    public volatile int DelayMilliseconds;
    public bool FailDispose, CleanupFailure;
    public bool AllCallsOnOwner = true;
    public readonly ManualResetEventSlim Entered = new(false), Release = new(false);
    int owner;
    public string SdkVersion { get { CheckThread(); return "Probe"; } }
    public void Connect() { owner = Environment.CurrentManagedThreadId; CheckThread(); }
    public FalconSample Read()
    {
        CheckThread();
        if (FailRead) throw new InvalidOperationException("injected read failure");
        if (BlockRead) { Entered.Set(); Release.Wait(); }
        Thread.Sleep(DelayMilliseconds);
        return new FalconSample(0, 0, 0, 0);
    }
    public void BeginHaptics(HapticMode mode) => CheckThread();
    public void ApplyHaptics(Vector3 force) => CheckThread();
    public void EndHaptics() => CheckThread();
    public void Dispose()
    {
        CheckThread(); Disposed = true;
        if (FailDispose)
        {
            var error = new InvalidOperationException("injected cleanup failure");
            if (CleanupFailure) throw new FalconCleanupException(error.Message, new[] { error });
            throw error;
        }
    }
    void CheckThread() { AllCallsOnOwner &= owner == Environment.CurrentManagedThreadId; }
}

namespace Falcon.Haptics
{
    public sealed partial class FalconDevice
    {
        // Test-only constructor: holds kernel32 solely to exercise real FreeLibrary bookkeeping.
        // No DHD library, driver or device is opened. All device exports are managed fakes.
        internal FalconDevice(List<string> calls, string failure)
        {
            library = LoadLibraryExW(Path.Combine(Environment.SystemDirectory, "kernel32.dll"), IntPtr.Zero, 0);
            if (library == IntPtr.Zero) throw new Exception("Cannot load test module");
            id = 0;
            forceEnabled = true;
            getError = () => IntPtr.Zero;
            writeForce = (x, y, z, device) => { calls.Add("zero"); return failure == "zero" ? -1 : 0; };
            enableForce = (value, device) => { calls.Add("disable"); return failure == "disable" ? -1 : 0; };
            close = device => { calls.Add("close"); return failure == "close" ? -1 : 0; };
            if (Interlocked.CompareExchange(ref sessionOwner, this, null) != null)
                throw new Exception("Another session owns the test module");
        }
        internal bool ProbeLibraryRetained => library != IntPtr.Zero;
        internal bool ProbeOwnsSession => ReferenceEquals(sessionOwner, this);
        internal void ProbeAllowClose() { close = device => 0; enableForce = (value, device) => 0; }
    }
}
