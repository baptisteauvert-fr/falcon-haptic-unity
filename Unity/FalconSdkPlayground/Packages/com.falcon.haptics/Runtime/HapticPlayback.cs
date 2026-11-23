using System;
using System.Diagnostics;

namespace Falcon.Haptics
{

internal sealed class EffectRequest
{
    public readonly HapticMode Mode;
    public readonly FalconPlaneSurface? Surface;

    public EffectRequest(HapticMode mode)
    {
        Mode = mode;
    }
    public EffectRequest(FalconPlaneSurface surface) { Mode = HapticMode.Spring; Surface = surface; }
}

internal sealed class HapticPlayback : IDisposable
{
    readonly IFalconBackend falcon;
    EffectRequest selection;
    HapticEffect active;
    FalconSurfaceContact surfaceContact;
    public bool InContact => surfaceContact?.InContact == true;
    public float MaximumFrictionForce { get; private set; }
    bool sawRelease;
    long lastUpdate;

    public HapticPlayback(IFalconBackend falcon)
    {
        this.falcon = falcon;
    }

    public bool Update(EffectRequest request, FalconSample sample, int intensity)
    {
        if (!ReferenceEquals(request, selection))
        {
            Stop();
            selection = request;
            sawRelease = false;
        }
        if (request == null)
            return false;

        if ((sample.Buttons & 1u) == 0)
        {
            Stop();
            sawRelease = true;
            return false;
        }
        if (!sawRelease)
            return false;

        long now = Stopwatch.GetTimestamp();
        if (active == null && surfaceContact == null)
        {
            // Validate the initial side before enabling motors. Spring's existing 1.2 N policy is used.
            if (request.Surface.HasValue) surfaceContact = new FalconSurfaceContact(request.Surface.Value, sample);
            falcon.BeginHaptics(request.Mode);
            if (!request.Surface.HasValue) active = new HapticEffect(request.Mode, sample);
            lastUpdate = Stopwatch.GetTimestamp();
            return true;
        }

        float seconds = (float)((now - lastUpdate) / (double)Stopwatch.Frequency);
        lastUpdate = now;
        falcon.ApplyHaptics(surfaceContact != null ? surfaceContact.Step(sample, seconds, intensity) : active.Step(sample, seconds, intensity));
        if (surfaceContact != null)
            MaximumFrictionForce = Math.Max(MaximumFrictionForce, surfaceContact.TangentialForceNewtons.Length());
        return true;
    }

    public void Stop()
    {
        falcon.EndHaptics();
        active = null;
        surfaceContact = null;
        MaximumFrictionForce = 0;
    }

    public void Dispose() => Stop();
}
}
