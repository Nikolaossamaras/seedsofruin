using System;

namespace SoR.Gameplay
{
    [Serializable]
    public class CompanionRuntimeData
    {
        public string CompanionId;

        /// <summary>
        /// Constellation level from 0 (base) to 6 (max).
        /// </summary>
        public int ConstellationLevel;

        public int BondLevel;
        public int BondXP;

        public float CurrentHealth;

        /// <summary>
        /// True if this companion is currently in the active party slot.
        /// </summary>
        public bool IsActive;

        /// <summary>
        /// True if this companion is currently in the support party slot.
        /// </summary>
        public bool IsSupport;
    }
}
