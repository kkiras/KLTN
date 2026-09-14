using System;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class CardContentValidatorTests
    {
        [Test]
        public void Validate_AcceptsValidUnit()
        {
            var definition = new CardDefinition("ValidUnit", "Valid Unit", 1, 1, 1);

            Assert.DoesNotThrow(() =>
                CardContentValidator.Validate(new[] { definition })
            );
        }

        [Test]
        public void Validate_RejectsDuplicateDefinitionIds()
        {
            var first = new CardDefinition("SameId", "First", 1, 1, 1);

            var second = new CardDefinition("SameId", "Second", 1, 1, 1);

            Assert.Throws<InvalidOperationException>(() =>
                CardContentValidator.Validate(new[] { first, second })
            );
        }

        [Test]
        public void Validate_RejectsDeathScalingWithoutRevive()
        {
            var passive = new UnitPassiveRules(powerAndHealthPerDeath: 1);

            var definition = new CardDefinition(
                "InvalidPassive",
                "Invalid Passive",
                1,
                1,
                1,
                passiveRules: passive
            );

            Assert.Throws<InvalidOperationException>(() =>
                CardContentValidator.Validate(new[] { definition })
            );
        }

        [Test]
        public void Validate_RejectsCounterSpellBeforeSpellStack()
        {
            var ability = new AbilityDefinition(
                "counter_spell",
                AbilityTrigger.Play,
                new[]
                {
                    new EffectDefinition(EffectKind.CounterSpell, EffectTarget.Source),
                }
            );

            var definition = new CardDefinition(
                "CounterUnit",
                "Counter Unit",
                1,
                1,
                1,
                abilities: new[] { ability }
            );

            Assert.Throws<InvalidOperationException>(() =>
                CardContentValidator.Validate(new[] { definition })
            );
        }

        [Test]
        public void Validate_RejectsUnknownSummonDefinition()
        {
            var ability = new AbilityDefinition(
                "summon_missing",
                AbilityTrigger.Play,
                new[]
                {
                    new EffectDefinition(
                        EffectKind.Summon,
                        EffectTarget.SourceOwner,
                        cardDefinitionId: "MissingUnit"
                    ),
                }
            );

            var definition = new CardDefinition(
                "Summoner",
                "Summoner",
                1,
                1,
                1,
                abilities: new[] { ability }
            );

            Assert.Throws<InvalidOperationException>(() =>
                CardContentValidator.Validate(new[] { definition })
            );
        }
    }
}
