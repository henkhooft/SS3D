namespace SS3D.Systems.Examine
{
    /// <summary>
    /// Localization keys for character health examine lines (Tier 0 public / Tier 1 self).
    /// </summary>
    public static class ExamineHealthKeys
    {
        public const string DeadSelf = "examine.health.dead.self";
        public const string DeadOther = "examine.health.dead.other";
        public const string CardiacArrestSelf = "examine.health.cardiac_arrest.self";
        public const string CardiacArrestOther = "examine.health.cardiac_arrest.other";
        public const string CriticalSelf = "examine.health.critical.self";
        public const string CriticalOther = "examine.health.critical.other";
        public const string UnconsciousSelf = "examine.health.unconscious.self";
        public const string UnconsciousOther = "examine.health.unconscious.other";

        // Public Tier 0 — single consolidated appearance line (others only).
        public const string OtherHurtAndBleeding = "examine.health.other.hurt_and_bleeding";
        public const string OtherBleeding = "examine.health.other.bleeding";
        public const string OtherHurt = "examine.health.other.hurt";
        public const string OtherMissing = "examine.health.other.missing";
        public const string OtherMissingAndHurtAndBleeding = "examine.health.other.missing_hurt_bleeding";
        public const string OtherMissingAndBleeding = "examine.health.other.missing_bleeding";
        public const string OtherMissingAndHurt = "examine.health.other.missing_hurt";

        // Self Tier 1 — per-zone / organ lines.
        public const string Severed = "examine.health.severed";
        public const string Bleeding = "examine.health.bleeding";
        public const string ZoneSeverity = "examine.health.zone_severity";

        public const string OrganStrained = "examine.health.organ_strained";
        public const string OrganFailing = "examine.health.organ_failing";
        public const string OrganCritical = "examine.health.organ_critical";
        public const string OrganDestroyed = "examine.health.organ_destroyed";

        public const string DeadSelfFallback = "You are dead.";
        public const string DeadOtherFallback = "He is dead.";
        public const string CardiacArrestSelfFallback = "You are in cardiac arrest.";
        public const string CardiacArrestOtherFallback = "He is in cardiac arrest.";
        public const string CriticalSelfFallback = "You are in critical condition.";
        public const string CriticalOtherFallback = "He is in critical condition.";
        public const string UnconsciousSelfFallback = "You are unconscious.";
        public const string UnconsciousOtherFallback = "He is unconscious.";

        // Interim masculine pronouns — roster is male-only for now.
        public const string OtherHurtAndBleedingFallback = "He seems to be hurt and bleeding from the {0}.";
        public const string OtherBleedingFallback = "He seems to be bleeding from the {0}.";
        public const string OtherHurtFallback = "He seems to be hurt.";
        public const string OtherMissingFallback = "He is missing his {0}.";
        public const string OtherMissingAndHurtAndBleedingFallback =
            "He is missing his {0} and seems to be hurt and bleeding from the {1}.";
        public const string OtherMissingAndBleedingFallback =
            "He is missing his {0} and seems to be bleeding from the {1}.";
        public const string OtherMissingAndHurtFallback =
            "He is missing his {0} and seems to be hurt.";

        public const string SeveredFallback = "Missing {0}.";
        public const string BleedingFallback = "Bleeding from the {0}.";
        public const string ZoneSeverityFallback = "{0} is {1}.";

        public const string OrganStrainedFallback = "{0} is strained.";
        public const string OrganFailingFallback = "{0} is failing.";
        public const string OrganCriticalFallback = "{0} is critically damaged.";
        public const string OrganDestroyedFallback = "{0} has failed.";
    }
}
