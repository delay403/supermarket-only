using Game.Core.Content;

namespace Game.AnimalHospital.Config
{
    public static class AnimalHospitalResourcePaths
    {
        public const string ConfigAssetName = "AnimalHospitalConfig";

        public static string ConfigAssetPath =>
            $"{GameContentPaths.AnimalHospitalConfigRoot}/{ConfigAssetName}.asset";
    }
}
