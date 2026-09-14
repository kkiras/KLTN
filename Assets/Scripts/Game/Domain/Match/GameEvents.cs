using System;
using System.Collections.Generic;

namespace KLTN.Game.Domain
{
    public enum GameEventType : byte
    {
        UnitPlayed,
        UnitSummoned,
        AttackDeclared,
        UnitSupported,
        RoundStarted,
        UnitDied,
    }

    public sealed class GameEvent
    {
        public GameEventType Type { get; }
        public int RoundNumber { get; }

        public ulong SourceCardInstanceId { get; }
        public SeatId? SourceOwner { get; }

        public ulong SubjectCardInstanceId { get; }
        public SeatId? SubjectOwner { get; }

        private GameEvent(
            GameEventType type,
            int roundNumber,
            ulong sourceCardInstanceId,
            SeatId? sourceOwner,
            ulong subjectCardInstanceId,
            SeatId? subjectOwner
        )
        {
            if (roundNumber <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(roundNumber));
            }

            Type = type;
            RoundNumber = roundNumber;

            SourceCardInstanceId = sourceCardInstanceId;

            SourceOwner = sourceOwner;

            SubjectCardInstanceId = subjectCardInstanceId;

            SubjectOwner = subjectOwner;
        }

        public static GameEvent FromCard(
            GameEventType type,
            int roundNumber,
            CardInstance source,
            CardInstance subject = null
        )
        {
            if (source == null)
            {
                throw new ArgumentNullException(nameof(source));
            }

            CardInstance actualSubject = subject ?? source;

            return new GameEvent(
                type,
                roundNumber,
                source.InstanceId,
                source.Owner,
                actualSubject.InstanceId,
                actualSubject.Owner
            );
        }

        public static GameEvent RoundStarted(int roundNumber)
        {
            return new GameEvent(
                GameEventType.RoundStarted,
                roundNumber,
                0,
                null,
                0,
                null
            );
        }
    }

    public sealed class GameEventBatch
    {
        public IReadOnlyList<GameEvent> Events { get; }

        public GameEventBatch(IReadOnlyList<GameEvent> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            var copy = new GameEvent[events.Count];

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i] == null)
                {
                    throw new ArgumentException(
                        "Event batch cannot contain null.",
                        nameof(events)
                    );
                }

                copy[i] = events[i];
            }

            Events = Array.AsReadOnly(copy);
        }

        public static GameEventBatch From(params GameEvent[] events)
        {
            return new GameEventBatch(events ?? Array.Empty<GameEvent>());
        }
    }
}
