using Unity.Entities;

namespace VictoriaGame.World
{
    /// <summary>
    /// Une province voisine. Buffer rempli par MapInitSystem depuis
    /// data/province_adjacency.json, une entrée par arête.
    ///
    /// IsStrait distingue les deux natures de contact :
    ///   false → adjacence terrestre, une armée peut marcher.
    ///   true  → franchissement maritime (Manche, Bosphore, Messine...) :
    ///           infranchissable à pied, réservé au transport naval.
    ///
    /// Tout système terrestre (front, mouvement, ravitaillement) doit donc
    /// filtrer sur !IsStrait, sans quoi les armées traverseraient la mer.
    /// </summary>
    [InternalBufferCapacity(8)]
    public struct ProvinceNeighbor : IBufferElementData
    {
        public int NeighborProvinceId;
        public bool IsStrait;
    }
}
