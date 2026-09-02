using System;
using KLTN.Game.Domain;

namespace KLTN.Game.Networking
{
    public sealed class MatchClientProjection
    {
        #region Events and Properties

        public event Action<MatchSnapshotDto> SnapshotChanged;

        public event Action<CommandRejectionReason>
            CommandRejected;

        public MatchSnapshotDto Current { get; private set; }

        public CommandRejectionReason? LastRejection { get; private set; }

        #endregion

        #region State Updates

        public void Apply(MatchSnapshotDto snapshot)
        {
            if (snapshot == null) { return; }

            if (Current != null &&
                snapshot.revision < Current.revision)
            {
                return;
            }

            Current = snapshot;
            LastRejection = null;
            SnapshotChanged?.Invoke(snapshot);
        }

        public void Reject(CommandRejectionReason reason)
        {
            LastRejection = reason;
            CommandRejected?.Invoke(reason);
        }

        public void Reset()
        {
            Current = null;
            LastRejection = null;
        }

        #endregion
    }

    public static class MatchProjectionRegistry
    {
        public static MatchClientProjection Current { get; } = new MatchClientProjection();
    }
}
