namespace SoR.Systems.SaveLoad
{
    public interface ISaveable
    {
        /// <summary>
        /// Returns the current state as a JSON string.
        /// </summary>
        string GetSaveData();

        /// <summary>
        /// Restores state from a JSON string.
        /// </summary>
        void LoadSaveData(string json);
    }
}
