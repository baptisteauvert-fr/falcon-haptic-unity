using System;
using System.Numerics;
using System.Collections.Generic;

namespace Falcon.Haptics
{
    public sealed class FalconCleanupException : AggregateException
    {
        public FalconCleanupException(string message, IEnumerable<Exception> failures) : base(message, failures) { }
    }

    // Connect, Read, force operations and Dispose belong to one worker thread.
    public interface IFalconBackend : IDisposable
    {
        string SdkVersion { get; }
        void Connect();
        FalconSample Read();
        void BeginHaptics(HapticMode mode);
        void ApplyHaptics(Vector3 force);
        void EndHaptics();
    }
}
