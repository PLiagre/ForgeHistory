using Unity.Entities;

namespace VictoriaGame.World
{
    /// <summary>
    /// Lien province → villes (navigable). Buffer sur l'entité Province.
    /// Inverse : CityData.ProvinceId / CityData.Province.
    /// </summary>
    [InternalBufferCapacity(4)]
    public struct ProvinceCity : IBufferElementData
    {
        public int CityId;
        public Entity City;
    }
}
