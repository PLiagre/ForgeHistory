using Unity.Entities;
using VictoriaGame.Core;

namespace VictoriaGame.Economy
{
    /// <summary>
    /// Site de production d'une province.
    /// Ajouté sur les entités Province par MapInitSystem.
    /// BuildingEfficiencySystem calcule Efficiency chaque tick.
    /// ProductionSystem calcule LastOutput = BaseOutput x Efficiency x laborFactor.
    /// </summary>
    public struct ProductionSite : IComponentData
    {
        /// <summary>Id du bien produit (référence goods.json). 0 = non initialisé.</summary>
        public int GoodId;

        /// <summary>
        /// Output de base par tick (1 tick = 1 mois).
        /// Initialisé depuis ProvinceDevelopment.Production.
        /// </summary>
        public float BaseOutput;

        /// <summary>
        /// Multiplicateur d'efficacité [0.1..2.0].
        /// Calculé par BuildingEfficiencySystem selon développement + bâtiments + tech.
        /// </summary>
        public float Efficiency;

        /// <summary>Output réel du dernier tick = BaseOutput x Efficiency x laborFactor.</summary>
        public float LastOutput;

        /// <summary>
        /// Population active de référence de la province, capturée au premier tick (0 = non initialisé).
        /// Sert de dénominateur pour le facteur travail labor / BaselineLabor.
        /// </summary>
        public float BaselineLabor;

        /// <summary>Méthode de production, affecte le ratio output/workers.</summary>
        public ProductionMethod Method;
    }
}
