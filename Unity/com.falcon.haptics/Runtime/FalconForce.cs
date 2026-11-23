using System;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
namespace Falcon.Haptics
{

public sealed partial class FalconDevice
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int ForceVector(double x, double y, double z, sbyte device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int ForceLimit(double newtons, sbyte device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int SetOption(int value, sbyte device);
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    delegate int StatusCall([Out] int[] values, sbyte device);

    ForceVector writeForce;
    bool forceEnabled;
    float maximumForce;

    internal void BeginHaptics(HapticMode mode)
    {
        if (!Enum.IsDefined(typeof(HapticMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        sbyte device = DeviceId();
        writeForce ??= Export<ForceVector>("dhdSetForce");
        var getStatus = Export<StatusCall>("dhdGetStatus");
        var limit = Export<ForceLimit>("dhdSetMaxForce");
        var gravity = Export<SetOption>("dhdSetGravityCompensation");
        var status = new int[17];
        if (getStatus(status, device) < 0)
            throw Failure("Vérification du contrôleur");
        if (status[1] != 1 || status[3] == 1 || status[9] > 0)
            throw new InvalidOperationException("Initialisez la poignée en la tirant puis en la repoussant doucement.");

        maximumForce = HapticEffect.ForceLimit(mode);
        if (limit(maximumForce, device) < 0)
            throw Failure("Réglage de la limite d’effort");
        if (gravity(0, device) < 0)
            throw Failure("Réglage de la compensation de gravité");

        forceEnabled = true; // Attempt to disarm even if the SDK reports a partially failed enable.
        if (enableForce(1, device) < 0)
            throw Failure("Activation du retour de force");
        ApplyHaptics(Vector3.Zero);
    }

    internal void ApplyHaptics(Vector3 force)
    {
        float tolerance = maximumForce + .0001f;
        if (!forceEnabled || !(force.LengthSquared() <= tolerance * tolerance))
            throw new InvalidOperationException("Commande haptique hors limites.");
        if (writeForce(force.X, force.Y, force.Z, DeviceId()) < 0)
            throw Failure("Envoi du retour de force");
    }

    internal void EndHaptics()
    {
        if (!forceEnabled)
            return;
        sbyte device = DeviceId();
        var failures = new List<Exception>();
        try { if (writeForce(0, 0, 0, device) < 0) throw Failure("Remise à zéro du retour de force"); }
        catch (Exception ex) { failures.Add(ex); }
        try
        {
            if (enableForce(0, device) < 0) throw Failure("Arrêt du retour de force");
            forceEnabled = false;
        }
        catch (Exception ex) { failures.Add(ex); }
        if (failures.Count != 0) throw new FalconCleanupException("Arrêt du retour de force incomplet.", failures);
    }
}
}
