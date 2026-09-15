using System;
using System.Collections.Generic;
using KLTN.Game.Domain;
using UnityEngine;

namespace KLTN.Game.Content
{
    [Serializable]
    public sealed class AbilityTargetRequirementData
    {
        public AbilityTargetSlot slot = AbilityTargetSlot.Primary;

        public TargetRelation relation = TargetRelation.Ally;

        public AbilityTargetZone zones = AbilityTargetZone.ActiveRoster;

        [Min(1)]
        public int count = 1;

        public bool excludeSource = true;
    }

    [Serializable]
    public sealed class CardEffectData
    {
        public EffectKind kind;

        public EffectTarget target = EffectTarget.Source;

        public int amount;
        public int secondaryAmount;

        [Min(1)]
        public int count = 1;

        public EffectDuration duration = EffectDuration.Permanent;

        public string cardDefinitionId;
    }

    [Serializable]
    public sealed class CardPassiveData
    {
        [Min(0)]
        public int costReductionPerAlliedDeathThisGame;

        public bool revivesAtRoundStart;

        [Min(0)]
        public int powerAndHealthPerDeath;

        public bool joinsAttackFromGraveyard;

        public bool requiresEphemeralAttacker;
    }

    [Serializable]
    public sealed class CardAbilityData
    {
        [Tooltip("Stable ASCII ID, unique inside this card.")]
        public string abilityId;

        public AbilityTrigger trigger;

        public AbilityCondition condition = AbilityCondition.None;

        public bool oncePerRound;

        public bool oncePerMatch;

        [Tooltip("What happens when a targeted Play ability has no valid targets.")]
        public MissingTargetPolicy missingTargetPolicy = MissingTargetPolicy.RejectPlay;

        public List<AbilityTargetRequirementData> requiredTargets =
            new List<AbilityTargetRequirementData>();

        public List<CardEffectData> effects = new List<CardEffectData>();
    }
}
