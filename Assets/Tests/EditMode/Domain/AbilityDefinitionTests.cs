using System.Collections.Generic;
using NUnit.Framework;

namespace KLTN.Game.Domain.Tests
{
    public sealed class AbilityDefinitionTests
    {
        [Test]
        public void CardDefinition_DefaultsToNoRules()
        {
            var definition =
                new CardDefinition(
                    "plain_unit",
                    "Plain Unit",
                    3,
                    2,
                    1);

            Assert.AreEqual(
                UnitKeyword.None,
                definition.Keywords);

            Assert.AreEqual(
                0,
                definition.Abilities.Count);

            Assert.AreEqual(
                string.Empty,
                definition.RulesText);
        }

        [Test]
        public void KeywordFlags_CanBeCombined()
        {
            UnitKeyword keywords =
                UnitKeyword.Ephemeral |
                UnitKeyword.CannotBlock |
                UnitKeyword.Lifesteal;

            Assert.IsTrue(
                (keywords & UnitKeyword.Ephemeral) != 0);

            Assert.IsTrue(
                (keywords & UnitKeyword.CannotBlock) != 0);

            Assert.IsTrue(
                (keywords & UnitKeyword.Lifesteal) != 0);

            Assert.IsFalse(
                (keywords & UnitKeyword.Fearsome) != 0);
        }

        [Test]
        public void AbilityDefinition_CopiesInputCollections()
        {
            var targets =
                new List<AbilityTargetRequirement>
                {
                    new AbilityTargetRequirement(
                        AbilityTargetSlot.Primary,
                        TargetRelation.Ally,
                        AbilityTargetZone.ActiveRoster),

                    new AbilityTargetRequirement(
                        AbilityTargetSlot.Secondary,
                        TargetRelation.Enemy,
                        AbilityTargetZone.ActiveRoster)
                };

            var effects =
                new List<EffectDefinition>
                {
                    new EffectDefinition(
                        EffectKind.Sacrifice,
                        EffectTarget.PrimarySelection),

                    new EffectDefinition(
                        EffectKind.Kill,
                        EffectTarget.SecondarySelection)
                };

            var ability =
                new AbilityDefinition(
                    "than_trung_play",
                    AbilityTrigger.Play,
                    effects,
                    requiredTargets: targets);

            targets.Clear();
            effects.Clear();

            Assert.AreEqual(
                2,
                ability.RequiredTargets.Count);

            Assert.AreEqual(
                2,
                ability.Effects.Count);
        }

        [Test]
        public void CardDefinition_CopiesAbilityCollection()
        {
            var effects =
                new[]
                {
                    new EffectDefinition(
                        EffectKind.Draw,
                        EffectTarget.SourceOwner,
                        amount: 2)
                };

            var ability =
                new AbilityDefinition(
                    "ong_ke_draw",
                    AbilityTrigger.Play,
                    effects);

            var abilities =
                new List<AbilityDefinition>
                {
                    ability
                };

            var definition =
                new CardDefinition(
                    "ong_ke",
                    "Ông Kẹ",
                    1,
                    4,
                    4,
                    abilities: abilities);

            abilities.Clear();

            Assert.AreEqual(
                1,
                definition.Abilities.Count);

            Assert.AreSame(
                ability,
                definition.Abilities[0]);
        }
    }
}