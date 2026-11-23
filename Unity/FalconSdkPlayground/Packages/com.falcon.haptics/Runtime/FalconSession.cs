using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace Falcon.Haptics
{
    public enum FalconConnectionState { Connecting, Ready, Armed, Active, Stale, Faulted, Stopping, Stopped }

    public readonly struct FalconSessionState
    {
        public readonly FalconConnectionState Connection;
        public readonly FalconSample? Sample;
        public readonly string Message, EffectError;
        public readonly long Sequence, TimestampTicks, IntervalCount;
        public readonly double MeanIntervalMilliseconds, MaximumIntervalMilliseconds;
        public readonly bool EffectArmed, EffectActive, SurfaceContact;
        public readonly float SurfaceFrictionPeakNewtons;
        public double SampleAgeSeconds => TimestampTicks == 0 ? double.PositiveInfinity :
            Math.Max(0, (Stopwatch.GetTimestamp() - TimestampTicks) / (double)Stopwatch.Frequency);
        public bool Connected => Sample.HasValue && SampleAgeSeconds <= FalconSession.SampleTimeoutSeconds &&
            (Connection == FalconConnectionState.Ready || Connection == FalconConnectionState.Armed ||
             Connection == FalconConnectionState.Active);

        internal FalconSessionState(FalconConnectionState connection, FalconSample? sample, string message,
            string error, long sequence, long timestamp, long count, double mean, double maximum,
            bool armed, bool active, bool contact = false, float frictionPeak = 0)
        {
            Connection = connection; Sample = sample; Message = message; EffectError = error;
            Sequence = sequence; TimestampTicks = timestamp; IntervalCount = count;
            MeanIntervalMilliseconds = mean; MaximumIntervalMilliseconds = maximum;
            EffectArmed = armed; EffectActive = active;
            SurfaceContact = contact;
            SurfaceFrictionPeakNewtons = frictionPeak;
        }
    }

    // No Unity dependency. Native calls are never made under gate or on the caller thread.
    public sealed class FalconSession : IDisposable
    {
        public const double SampleTimeoutSeconds = .1;
        public const double HeartbeatTimeoutSeconds = .24;
        const double MaximumEffectIntervalSeconds = .045;
        static readonly object registryGate = new object();
        static readonly List<FalconSession> sessions = new List<FalconSession>();
        readonly object gate = new object();
        readonly Func<IFalconBackend> factory;
        readonly Thread thread;
        readonly ManualResetEventSlim stop = new ManualResetEventSlim(false);
        bool stopping, active, surfaceContact;
        float frictionPeak;
        int intensity = 30;
        long heartbeat, sequence, timestamp, intervalCount;
        double intervalTotal, maximumInterval;
        string message = "Recherche du Falcon…", effectError;
        FalconSample? sample;
        FalconConnectionState connection = FalconConnectionState.Connecting;
        EffectRequest request;
        FalconPhysicalBounds? physicalBounds;
        FalconPlaneSurface? surface;
        long surfaceTimestamp;

        public FalconSession(Func<IFalconBackend> backendFactory)
        {
            factory = backendFactory ?? throw new ArgumentNullException(nameof(backendFactory));
            thread = new Thread(Loop) { IsBackground = true, Name = "Novint Falcon" };
            lock (registryGate) sessions.Add(this);
            try { thread.Start(); }
            catch { lock (registryGate) sessions.Remove(this); stop.Dispose(); throw; }
        }

        public bool IsRunning => thread.IsAlive;
        public static int ActiveSessionCount { get { lock (registryGate) return sessions.Count; } }
        public bool Stopping { get { lock (gate) return stopping; } }
        public FalconSessionState State
        {
            get
            {
                lock (gate)
                {
                    var current = connection;
                    if (!stopping && sample.HasValue && Age(timestamp) > SampleTimeoutSeconds)
                        current = FalconConnectionState.Stale;
                    return new FalconSessionState(current, sample, message, effectError, sequence, timestamp,
                        intervalCount, intervalCount == 0 ? 0 : intervalTotal / intervalCount,
                        maximumInterval, request != null, active, surfaceContact, frictionPeak);
                }
            }
        }

        public bool TryArmEffect(HapticMode mode, int gain = 30)
        {
            if (!Enum.IsDefined(typeof(HapticMode), mode)) return false;
            lock (gate)
            {
                if (stopping || !sample.HasValue || Age(timestamp) > SampleTimeoutSeconds ||
                    connection == FalconConnectionState.Faulted) return false;
                if (physicalBounds.HasValue && !physicalBounds.Value.Contains(new System.Numerics.Vector3(
                    (float)sample.Value.X, (float)sample.Value.Y, (float)sample.Value.Z))) return false;
                intensity = Math.Clamp(gain, 0, 100);
                effectError = null;
                heartbeat = Stopwatch.GetTimestamp();
                request = new EffectRequest(mode);
                return true;
            }
        }

        public void SetIntensity(int gain) { lock (gate) intensity = Math.Clamp(gain, 0, 100); }
        public void SetSurface(FalconPlaneSurface? value)
        {
            if (value.HasValue && !value.Value.IsValid) throw new ArgumentException("Surface invalide.");
            lock (gate)
            {
                if (!Nullable.Equals(surface, value)) request = null;
                surface = value;
                surfaceTimestamp = Stopwatch.GetTimestamp();
            }
        }
        public bool TryArmSurface(int gain = 30)
        {
            lock (gate)
            {
                if (!surface.HasValue || Age(surfaceTimestamp) > SampleTimeoutSeconds ||
                    !TryArmEffect(HapticMode.Spring, gain)) return false;
                request = new EffectRequest(surface.Value);
                return true;
            }
        }
        public void UpdateHeartbeat() { lock (gate) heartbeat = Stopwatch.GetTimestamp(); }
        public void Disarm() { lock (gate) request = null; }
        public void SetPhysicalBounds(FalconPhysicalBounds? bounds)
        {
            if (bounds.HasValue)
                _ = new FalconPhysicalBounds(bounds.Value.MinimumMetres, bounds.Value.MaximumMetres);
            lock (gate) { physicalBounds = bounds; request = null; }
        }
        public void RequestStop()
        {
            lock (gate)
            {
                if (stopping) return;
                stopping = true;
                request = null;
                if (!thread.IsAlive) return; // Keep a terminal fault visible when Stop is called afterwards.
                connection = FalconConnectionState.Stopping;
                message = "Arrêt en cours…";
                stop.Set();
            }
        }

        // False means the worker still owns the backend. Never abort or unload it in that case.
        public bool Stop(int timeoutMilliseconds = 1000)
        {
            if (timeoutMilliseconds < 0) throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds));
            RequestStop();
            return Thread.CurrentThread != thread && thread.Join(timeoutMilliseconds);
        }

        public void Dispose()
        {
            if (!Stop()) throw new TimeoutException("Le thread Falcon n’a pas confirmé son arrêt. La session reste réservée.");
            // The event is intentionally retained until GC: Stop/RequestStop may be called concurrently.
        }

        public static bool StopAll(int timeoutMilliseconds = 1000)
        {
            FalconSession[] copy;
            lock (registryGate) copy = sessions.ToArray();
            foreach (var session in copy) session.RequestStop();
            bool result = true;
            foreach (var session in copy) result &= session.Stop(timeoutMilliseconds);
            return result;
        }

        public static void DisarmAll()
        {
            lock (registryGate) foreach (var session in sessions) session.Disarm();
        }

        void Loop()
        {
            try
            {
                while (!stop.IsSet)
                {
                    try
                    {
                        lock (gate) { sample = null; request = null; connection = FalconConnectionState.Connecting; }
                        using (var backend = factory() ?? throw new InvalidOperationException("Backend Falcon absent."))
                        {
                            if (stop.IsSet) break;
                            backend.Connect();
                            string version = backend.SdkVersion;
                            using var playback = new HapticPlayback(backend);
                            long previous = 0;
                            while (!stop.IsSet)
                            {
                                long started = Stopwatch.GetTimestamp();
                                FalconSample value = backend.Read();
                                long now = Stopwatch.GetTimestamp();
                                if (!Finite(value.X) || !Finite(value.Y) || !Finite(value.Z))
                                    throw new InvalidOperationException("Position Falcon invalide.");
                                if (stop.IsSet) break;
                                double interval = previous == 0 ? 0 : (now - previous) / (double)Stopwatch.Frequency;
                                previous = now;
                                EffectRequest selected;
                                int gain;
                                lock (gate)
                                {
                                    if (Age(heartbeat) > HeartbeatTimeoutSeconds) request = null;
                                    if (request?.Surface != null && (Age(surfaceTimestamp) > SampleTimeoutSeconds ||
                                        !Nullable.Equals(request.Surface, surface))) request = null;
                                    if (physicalBounds.HasValue && !physicalBounds.Value.Contains(new System.Numerics.Vector3(
                                        (float)value.X, (float)value.Y, (float)value.Z))) request = null;
                                    selected = request;
                                    gain = intensity;
                                }
                                if (selected != null && (interval > MaximumEffectIntervalSeconds ||
                                    (now - started) / (double)Stopwatch.Frequency > MaximumEffectIntervalSeconds))
                                    throw new InvalidOperationException("Boucle Falcon trop lente : forces désarmées.");
                                bool output = false;
                                try { output = playback.Update(selected, value, gain); }
                                catch (Exception ex)
                                {
                                    lock (gate)
                                    {
                                        if (ReferenceEquals(request, selected)) request = null;
                                        effectError = ex.Message;
                                    }
                                    playback.Stop();
                                }
                                lock (gate)
                                {
                                    sample = value; timestamp = now; sequence++;
                                    if (interval > 0)
                                    {
                                        intervalCount++;
                                        intervalTotal += interval * 1000;
                                        maximumInterval = Math.Max(maximumInterval, interval * 1000);
                                    }
                                    active = output;
                                    surfaceContact = output && playback.InContact;
                                    frictionPeak = output ? playback.MaximumFrictionForce : 0;
                                    if (!stopping)
                                    {
                                        connection = output ? FalconConnectionState.Active :
                                            request != null ? FalconConnectionState.Armed : FalconConnectionState.Ready;
                                        message = version;
                                    }
                                }
                                if (stop.Wait(selected == null ? 8 : 2)) break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (gate)
                        {
                            request = null; active = false; surfaceContact = false; frictionPeak = 0; sample = null;
                            connection = stopping ? FalconConnectionState.Stopping : FalconConnectionState.Faulted;
                            message = ex.Message;
                            effectError = ex.Message;
                        }
                        if (ex is FalconCleanupException) break; // Require explicit intervention after uncertain cleanup.
                        if (stop.Wait(750)) break;
                    }
                }
            }
            finally
            {
                lock (gate)
                {
                    request = null; active = false; surfaceContact = false; frictionPeak = 0; sample = null;
                    connection = !stopping && !string.IsNullOrEmpty(effectError) ?
                        FalconConnectionState.Faulted : FalconConnectionState.Stopped;
                    message = string.IsNullOrEmpty(effectError) ? "Arrêté" : "Session terminée · " + effectError;
                }
                lock (registryGate) sessions.Remove(this);
            }
        }

        static double Age(long ticks) => ticks == 0 ? double.PositiveInfinity :
            (Stopwatch.GetTimestamp() - ticks) / (double)Stopwatch.Frequency;
        static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
    }
}
