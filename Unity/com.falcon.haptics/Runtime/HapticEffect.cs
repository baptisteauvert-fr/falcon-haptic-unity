using System;
using System.Numerics;

namespace Falcon.Haptics
{

public enum HapticMode { Water, Brick, Resistance, Spring, Gravel, EngineLever }

internal sealed class HapticEffect
{
    const float TwoPi = 2 * (float)Math.PI;
    readonly HapticMode mode;
    public static float ForceLimit(HapticMode mode) => mode is HapticMode.Water or HapticMode.Brick or HapticMode.Resistance
        ? 1.8f : 1.2f;
    readonly Vector3 anchor;
    Vector3 previous;
    Vector3 velocity;
    float travel, age, engineTime;

    public HapticEffect(HapticMode mode, FalconSample origin)
    {
        this.mode = mode;
        anchor = previous = Position(origin);
    }

    public Vector3 Step(FalconSample sample, float seconds, int intensity)
    {
        var position = Position(sample);
        var delta = position - previous;
        previous = position;
        if (seconds <= 0 || seconds > .045f || delta.LengthSquared() > .03f * .03f)
            throw new InvalidOperationException("Lecture trop irrégulière. Réactivez le mode avec la poignée immobile.");

        var speed = delta / seconds;
        velocity = Vector3.Lerp(velocity, speed, seconds / (.025f + seconds));
        float speedLength = velocity.Length();
        travel = (travel + delta.Length()) % .009f;
        age = Math.Min(age + seconds, .20f);
        engineTime = (engineTime + seconds) % 1f;
        float depth = Math.Max(0, anchor.X - position.X - .004f);

        Vector3 force = mode switch
        {
            HapticMode.Water => -velocity * (6.6f + 22 * speedLength),
            HapticMode.Brick => depth > 0
                ? new Vector3(245 * depth + 4 * Math.Max(0, -velocity.X) * Math.Min(depth / .003f, 1), 0, 0)
                : Vector3.Zero,
            HapticMode.Resistance => -velocity * (9 + .9f / (.018f + speedLength)),
            HapticMode.Spring => -32 * (position - anchor) - 2 * velocity,
            HapticMode.Gravel => -velocity * (8 + 5 * MathF.Sin(travel * TwoPi / .009f)),
            HapticMode.EngineLever => -14 * (position - anchor) - 1.5f * velocity
                + new Vector3(0, (.70f + .12f * MathF.Sin(TwoPi * 3 * engineTime)) * MathF.Sin(TwoPi * 14 * engineTime), 0),
            _ => Vector3.Zero
        };

        float gain = Math.Clamp(intensity, 0, 100) / 100f;
        float cap = ForceLimit(mode) * gain;
        float scale = gain * (age / .20f);
        force *= scale;
        if (float.IsNaN(force.LengthSquared()) || float.IsInfinity(force.LengthSquared()))
            throw new InvalidOperationException("La force calculée est invalide.");

        float length = force.Length();
        return length > cap ? force * (cap / length) : force;
    }

    static Vector3 Position(FalconSample sample) => new((float)sample.X, (float)sample.Y, (float)sample.Z);
}
}
