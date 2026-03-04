using IntelOrca.Biohazard.REE.Rsz;

namespace IntelOrca.Biohazard.BioRand.REE
{
    public interface IReeRandomizerContext
    {
        /// <summary>
        /// Gets whether we are exporting the mod variant of this patch, or generating a randomizer seed.
        /// </summary>
        bool ExportingMod { get; }

        /// <summary>
        /// Gets the RSZ type repository.
        /// </summary>
        RszTypeRepository TypeRepository { get; }

        /// <summary>
        /// Gets a custom service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        T GetService<T>();

        /// <summary>
        /// Gets a randomizer config option or the default value provided if not specified.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="defaultValue"></param>
        /// <returns></returns>
        T? GetConfigOption<T>(string key, T? defaultValue = default);

        /// <summary>
        /// Gets the data for a vanilla file, or the data for a replaced file.
        /// </summary>
        /// <param name="path">E.g. "natives/stm/_chainsaw/appsystem/ui/userdata/itemdefinitionuserdata.user.2"</param>
        /// <returns>The raw file data.</returns>
        byte[]? TryGetFile(string path);

        /// <summary>
        /// Replaces a file with new data.
        /// </summary>
        /// <param name="path">E.g. "natives/stm/_chainsaw/appsystem/ui/userdata/itemdefinitionuserdata.user.2"</param>
        /// <param name="data">The raw file data.</param>
        void SetFile(string path, byte[] data);

        /// <summary>
        /// Gets a supplement file, e.g. a zip file containing resources to use.
        /// </summary>
        /// <param name="path">E.g. "flamethrower.zip" or "wpstats.csv".</param>
        byte[]? GetSupplementFile(string path);
    }
}
