using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;
using VictoriaGame.Politics;
using VictoriaGame.Presentation;

namespace VictoriaGame.EditModeTests
{
    /// <summary>
    /// ui_003 — contrats éditoriaux sur le texte UI Toolkit produit (pas le source seul).
    /// </summary>
    public class Ui003EditorialTests
    {
        [Test]
        public void Country_Panel_Is_French_Without_Debug_Tokens()
        {
            var detail =
                "--- IDENTITY ---\n" +
                "COUNTRY 0 FRA  France\n" +
                "CONTROL PLAYER\n" +
                "CAPITAL 1 Ile-de-France\n" +
                "PROVINCES 8  POP 1000\n" +
                "--- TREASURY ---\n" +
                "GOLD   38.2\n" +
                "DEBT   0.0  RATE 0.002\n" +
                "INC    7.4  EXP 6.0\n" +
                "--- TAX ---\n" +
                "RATE   0.002 %  [0 %..0.02 %]\n" +
                "LAST   7.4  (tax income last tick)\n" +
                "PLAYER — use Tax-/Tax+\n" +
                "--- MILITARY ---\n" +
                "ARMY   100\n" +
                "WARS   0\n" +
                "--- STATUS ---\n" +
                "PRESTIGE 50  INDUS 0.0\n" +
                "--- PROVINCES PROD ---\n" +
                "1 Ile-de-Franc grain\n";

            var panel = new VisualElement { name = "CountryPanel" };
            HudDetailPresenter.Populate(panel, detail, "Pays");
            var text = HudDetailPresenter.CollectVisibleText(panel);

            Assert.IsTrue(text.IndexOf("France", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Pays contrôlé", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsFalse(text.StartsWith("FRA "), "Titre ne doit plus préfixer FRA");
            Assert.IsTrue(text.IndexOf("Trésor", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Dette", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Dépenses", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Revenu fiscal", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Industrie", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(
                text.IndexOf("Île-de-France", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("Ile-de-France", System.StringComparison.OrdinalIgnoreCase) >= 0,
                text);

            Assert.IsFalse(
                HudDetailPresenter.ContainsForbiddenUserToken(text, out var hit),
                $"Token interdit '{hit}' dans:\n{text}");

            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "ui_003_editorial.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.WriteAllText(logPath, "country_panel:\n" + text + "\n", Encoding.UTF8);
        }

        [Test]
        public void Province_Panel_Aggregates_Farms_And_Localizes_Pops()
        {
            var detail =
                "--- IDENTITY ---\n" +
                "PROVINCE 1 LE-DE-FRANCE\n" +
                "OWNER  FRA  France\n" +
                "DEV    TAX=5  PROD=4  MAN=3\n" +
                "--- POPULATION ---\n" +
                "PEASANT    2513 FRENCH CATHOLIC\n" +
                "ARTISAN     471 FRENCH CATHOLIC\n" +
                "NOBLE       157 FRENCH CATHOLIC\n" +
                "--- PROD STOCKS ---\n" +
                "ACT  WOOD cap=400\n" +
                "STOCK GRAIN=42597 WINE=16\n" +
                "--- WHY HUNGRY ---\n" +
                "OK  no input deficit\n" +
                "--- BUILDINGS ---\n" +
                "FARM id=1 city=2 COMPLETE cap=2000\n" +
                "FARM id=2 city=2 COMPLETE cap=2000\n" +
                "FARM id=3 city=3 COMPLETE cap=2000\n" +
                "--- SATISFACTION ---\n" +
                "PHY=0.8  LOD=0.7  MIX=0.75  W=0.35\n";

            InGameHud.ShowDebugIds = false;
            var panel = new VisualElement { name = "ProvincePanel" };
            HudDetailPresenter.Populate(panel, detail, "Province");
            var text = HudDetailPresenter.CollectVisibleText(panel);

            Assert.IsTrue(text.IndexOf("Île-de-France", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Propriétaire", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Paysans", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Artisans", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Nobles", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Activité", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Stocks", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Approvisionnement", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("Ferme", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsTrue(text.IndexOf("×3", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsFalse(text.IndexOf("PHY", System.StringComparison.Ordinal) >= 0, text);
            Assert.IsFalse(text.IndexOf("FARM id", System.StringComparison.Ordinal) >= 0, text);

            Assert.IsFalse(
                HudDetailPresenter.ContainsForbiddenUserToken(text, out var hit),
                $"Token interdit '{hit}' dans:\n{text}");

            var tax = HudValueFormatter.FormatTaxPercent(TaxPolicyLimits.DefaultProductionTaxRate);
            Assert.IsFalse(HudValueFormatter.ContainsScientificNotation(tax), tax);

            var logPath = Path.Combine(Application.dataPath, "..", "Logs", "ui_003_editorial.log");
            Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
            File.AppendAllText(logPath, "province_panel:\n" + text + "\n", Encoding.UTF8);
        }
    }
}
