using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class PendingAbilitySelectionTests
    {
        [Test]
        public void OpenSelection_ChangesPhaseAndStoresRequest()
        {
            MatchState state =
                CreateReadyState();

            TriggeredAbility triggered =
                CreateTargetedAbility(state);

            bool opened =
                state.TryOpenPendingSelection(
                    triggered);

            Assert.IsTrue(opened);

            Assert.AreEqual(
                MatchPhase.AbilitySelection,
                state.Phase);

            Assert.IsNotNull(
                state.PendingSelection);

            Assert.AreEqual(
                SeatId.Host,
                state.PendingSelection.ChoosingSeat);

            Assert.AreEqual(
                1UL,
                state.PendingSelection.RequestId);

            Assert.AreEqual(
                MatchPhase.Priority,
                state.PendingSelection.ResumePhase);
        }

        [Test]
        public void CloseSelection_RequiresMatchingRequestId()
        {
            MatchState state =
                CreateReadyState();

            TriggeredAbility triggered =
                CreateTargetedAbility(state);

            state.TryOpenPendingSelection(
                triggered);

            ulong requestId =
                state.PendingSelection.RequestId;

            Assert.IsFalse(
                state.TryClosePendingSelection(
                    requestId + 1));

            Assert.AreEqual(
                MatchPhase.AbilitySelection,
                state.Phase);

            Assert.IsTrue(
                state.TryClosePendingSelection(
                    requestId));

            Assert.IsNull(
                state.PendingSelection);

            Assert.AreEqual(
                MatchPhase.Priority,
                state.Phase);
        }

        private static MatchState CreateReadyState()
        {
            var state =
                new MatchState(SeatId.Host);

            state.HostMulliganDone = true;
            state.GuestMulliganDone = true;
            state.BeginFirstRound();

            return state;
        }

        private static TriggeredAbility
            CreateTargetedAbility(
                MatchState state)
        {
            var source =
                new CardInstance(
                    1,
                    "targeted_unit",
                    SeatId.Host,
                    CardZone.DrawHand,
                    3);

            state.Host.DrawHand.Add(source);

            var targetRequirement =
                new AbilityTargetRequirement(
                    AbilityTargetSlot.Primary,
                    TargetRelation.Enemy,
                    AbilityTargetZone.ActiveRoster);

            var ability =
                new AbilityDefinition(
                    "targeted_play",
                    AbilityTrigger.Play,
                    new[]
                    {
                        new EffectDefinition(
                            EffectKind.Kill,
                            EffectTarget.PrimarySelection)
                    },
                    requiredTargets:
                        new[] { targetRequirement });

            GameEvent gameEvent =
                GameEvent.FromCard(
                    GameEventType.UnitPlayed,
                    state.RoundNumber,
                    source);

            return new TriggeredAbility(
                source.InstanceId,
                source.Owner,
                ability,
                gameEvent);
        }
    }
}
