namespace Game.DestroyCity.Config
{
    /// <summary>摧毁城市模块 Resources 路径（相对 Resources/，不含扩展名）。</summary>
    public static class DestroyCityResourcePaths
    {
        public const string Root = "DestroyCity";

        public const string NukeDatabase = Root + "/NukeDatabase";
        public const string FuelDatabase = Root + "/FuelDatabase";
        public const string FuelProducerDatabase = Root + "/FuelProducerDatabase";
        public const string MapDatabase = Root + "/DestroyCityMapDatabase";
        public const string BuildingDatabase = Root + "/DestroyCityBuildingDatabase";

        public const string NukesFolder = Root + "/Nukes";
        public const string FuelsFolder = Root + "/Fuels";
        public const string ProducersFolder = Root + "/Producers";
        public const string MapsFolder = Root + "/Maps";
        public const string BuildingsFolder = Root + "/Buildings";

        /// <summary>经验飘字预制体（Resources 回退路径，优先使用场景 DestroyCityXpFloaterSettings）。</summary>
        public const string XpFloatPrefab = Root + "/DestroyCityXpFloat";
    }
}
