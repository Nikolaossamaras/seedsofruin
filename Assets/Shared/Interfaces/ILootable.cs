using System.Collections.Generic;

namespace SoR.Shared
{
    public interface ILootable
    {
        List<LootDrop> GetLootTable();
        void OnLooted();
    }
}
