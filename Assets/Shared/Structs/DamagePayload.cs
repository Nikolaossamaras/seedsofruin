namespace SoR.Shared
{
    public readonly struct DamagePayload
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly Element Element;
        public readonly bool IsCrit;
        public readonly float StaggerDamage;

        public DamagePayload(float amount, DamageType type, Element element, bool isCrit, float staggerDamage)
        {
            Amount = amount;
            Type = type;
            Element = element;
            IsCrit = isCrit;
            StaggerDamage = staggerDamage;
        }
    }
}
