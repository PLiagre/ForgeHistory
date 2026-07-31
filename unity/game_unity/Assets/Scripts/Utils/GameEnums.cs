namespace VictoriaGame.Core
{
    public enum TerrainType : byte
    {
        Plains = 0, Hills = 1, Mountains = 2,
        Desert = 3, Forest = 4, Coastal = 5,
    }

    public enum ClimateType : byte
    {
        Temperate = 0, Mediterranean = 1, Cold = 2, Arid = 3, Tropical = 4,
    }

    public enum GoodType : byte
    {
        Food = 0, RawMaterial = 1, Manufactured = 2, Luxury = 3,
    }

    public enum ProductionMethod : byte
    {
        Feudal = 0,    // 1400-1600 : serfs, faible rendement
        Artisan = 1,   // 1500-1750 : artisans libres, rendement moyen
        Factory = 2,   // 1760+     : usines, haut rendement
    }

    public enum BuildingType : byte
    {
        Farm = 0,
        Fishery = 1,
        Mine = 2,
        Sawmill = 3,
        Workshop = 4,
        Marketplace = 5,
        Port = 6,
        Fort = 7,
        University = 8,
        TradingPost = 9,
        Factory = 10,
    }

    public enum RegimentType : byte
    {
        MedievalInfantry = 0, PikeAndShot = 1, MusketInfantry = 2,
        LineInfantry = 3, RifleInfantry = 4, LightCavalry = 5,
        HeavyCavalry = 6, Dragoon = 7, Hussar = 8,
        FieldArtillery = 9, SiegeArtillery = 10, Guard = 11,
    }

    public enum ArmyMission : byte
    {
        Advance = 0, Hold = 1, Support = 2,
        Regroup = 3, Besiege = 4, March = 5,
    }

    public enum NavyMission : byte
    {
        Patrol = 0, Blockade = 1, Battle = 2,
        Transport = 3, ConvoyEscort = 4, ConvoyRaid = 5,
    }

    public enum ShipType : byte
    {
        Galley = 0, Cog = 1, Carrack = 2, Galleon = 3,
        Frigate = 4, ShipOfLine = 5, ManOfWar = 6,
        SteamFrigate = 7, Ironclad = 8,
    }

    public enum MerchantMode : byte { Collect = 0, Steer = 1 }

    public enum PopType : byte
    {
        Peasant = 0, Noble = 1, Artisan = 2, Merchant = 3,
        Clergy = 4, Worker = 5, Capitalist = 6, Intellectual = 7,
    }

    public enum CasusBelli : byte
    {
        Conquest = 0, Reconquest = 1, Liberation = 2,
        HolyWar = 3, Domination = 4, Colonial = 5, TradeDispute = 6,
    }

    public enum IdeologyType : byte
    {
        Conservatism = 0, Liberalism = 1, Socialism = 2,
        Nationalism = 3, Reactionism = 4, Anarchism = 5, Imperialism = 6,
    }
}
