# ROADMAP — ForgeHistory

> La vision produit (ce que le jeu **est**) vit dans [VISION.md](VISION.md)
> et prime en cas de conflit. Ce fichier dit **où on en est** et **dans quel
> ordre on avance**. Tout contributeur ou agent autorisé peut le corriger et
> le tenir à jour.

## Le jeu — cinq couches, dans l'ordre

Les couches viennent de `VISION.md` § « Roadmap par couches ». Statut au
2026-08-28, après la fusion du lot 041 :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** | `data/`, `sim/`, `viewer/` |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | non commencé | `sim/` |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencé | `sim/` |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencé | `sim/` |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencé | `sim/` |

**Couche 1 — état vrai** (au 2026-08-27, après la fusion du lot 039)

- La carte est **figée** : `data/world-1400.json`, un seul fichier lu par
  `sim/`. Elle porte 596 cellules, 1 364 arêtes d'adjacence, le relief en
  cinq classes, les déterminants du climat et 27 gisements nommés de 1400.
- `tools/map/` (ex-`pipeline/geo/`) est l'outil qui fabrique la carte. Il
  est hors du chemin quotidien : on ne le ressort que pour refaire la carte.
- `sim/` : amorçage, tick, commerce, survie, province dérivée, snapshot
  `v0a-2`, panier de marchandises. Depuis le lot 034, le tick reçoit la carte
  explicitement et ne porte plus d'état global caché. Il joue **le relief**,
  **le climat** par la durée du jour, et depuis le lot 038, **les gisements**
  — chaque gisement complet produit des kg de sa ressource dans le panier
  (population × débit × facteur de richesse). C'est la première couche de la
  carte qui entre dans le jeu après le climat. Mesure refaite le 2026-08-27 :
  relief `True`, climat `True`, gisements `True` — les trois couches sont
  consommées par le moteur. Depuis le lot 039, le commerce transporte **toute
  marchandise** présente dans le monde, pas seulement la nourriture.
- Ce que le monde ne sait pas encore faire : fabriquer (le minerai extrait
  reste sur place).
- `viewer/` : regard mince, preuve SVG.
- Unity : archivé, au commit `da1596d` (le tag `archive/2026-08` n'a
  jamais pu être poussé — voir `AGENTS.md` § « Les archives »).

## Le projet — phases F

| Phase | Contenu | Statut |
|---|---|---|
| **F1** — Fondations monde | carte figée complète : littoral, cellules, adjacence, relief, climat, gisements | **terminée** |
| **F2** — Moteur `sim/` couche 1 | amorçage, tick, survie, province, snapshot | **en cours** — relief, climat, gisements, marchandises multi-commerce, natalité et migration joués |
| **F3+** — Couches 2 à 5 | Villes, États, Armées, Batailles | à venir |

## Workflow courant

1. Choisir une tâche ci-dessous ou dans un brief existant.
2. Modifier directement les fichiers nécessaires.
3. Lancer les tests pertinents.
4. Mettre à jour la documentation factuelle si nécessaire.
5. Ouvrir une PR ou livrer le changement selon le contexte.

N'importe quel contributeur ou agent autorisé peut accomplir toutes ces
étapes. Les aides de `hermes/`, `harness/` et `control-plane/` sont
facultatives. La CI GitHub conserve les tests et le scan de sécurité. Le mode
d'emploi court est dans [`docs/MODE-EMPLOI.md`](docs/MODE-EMPLOI.md).

## Prochaines étapes (dans l'ordre)

Les lots écrits sous `harness/queue/briefs/` restent disponibles comme tâches
ou archives. La liste ci-dessous résume leur objectif produit.

| # | lot | en une phrase |
|---|---|---|
| 036 | `on-nait-aussi` | la population ne fait plus que mourir ✓ |
| 037 | `le-stock-devient-un-panier` | le stock cesse d'être un seul nombre de nourriture |
| 038 | `les-gisements-sortent-du-minerai` | les 27 gisements nommés produisent enfin quelque chose ✓ |
| 039 | `le-commerce-porte-tout` | le commerce transporte une marchandise quelconque, pas seulement la nourriture ✓ |
| 040 | `franchir-une-montagne-coute` | une arête de montagne ne transporte pas comme une arête de plaine ✓ |
| 041 | `on-s-en-va-quand-on-a-faim` | des habitants quittent une cellule affamée pour une voisine en surplus ✓ |
| 042 | `le-viewer-montre-ce-qui-joue` | le regard mince montre ce que le moteur joue vraiment |
| 043 | `le-convoi-a-l-echelle-de-la-cellule` | le commerce cesse d'être mille fois trop petit pour les cellules |
| 044 | `un-metier-le-mineur` | première division du travail : les mineurs ne labourent pas |

Dépendances restantes : 038 avant 044 (tenue). 040 avant 043 (le facteur de terrain se prouve plus simplement sur
une capacité constante ; il multiplie ensuite la capacité dérivée) · 043
avant tout lot de couche 2. Le prochain lot de la chaîne est 042 (viewer) ou 043 (convoi).

### Pourquoi il n'y a pas de lot « ville »

Mesuré le 2026-08-26, avant d'écrire ces briefs, et toujours vrai au
2026-08-27 : **aucun mécanisme du moteur ne concentre la population.** Ni la
natalité, ni une migration de famine, ni une migration d'attraction ne font
monter la densité de la cellule la plus dense au-dessus de celle de la
médiane, à 365 comme à 1 000 ticks.

La cause est chiffrable. Une cellule médiane compte environ 96 000 habitants
et consomme près de 192 000 kg par tick ; une arête d'adjacence en transporte
200. Le commerce est **962 fois trop petit** pour l'échelle des cellules :
aucun endroit du monde ne peut être nourri par ses voisins, donc aucun endroit
ne peut abriter plus de monde qu'il n'en nourrit lui-même.

Une ville est précisément un endroit qui ne produit pas ce qu'il mange. Tant
que ce rapport tient, un brief « le bourg est une agrégation dérivée » porterait
sur un phénomène que le moteur ne peut pas produire, et son critère
d'acceptation serait invérifiable. Le lot 043 est ce qui lève le blocage ; le
bourg s'écrira après lui, sur une mesure et non sur une intention.

Les mesures et automatisations de suivi peuvent être lancées au besoin. Elles
ne conditionnent pas l'avancement du produit.

## Historique des révisions

> Cette section conserve les décisions et répartitions de rôles appliquées à
> l'époque. Elles sont historiques ; le workflow courant est celui décrit plus
> haut.

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
| 2026-08-12 | hermes (rédaction déléguée à Cursor, décision propriétaire « ok pour tout ») | reflet de la demande « tableau de bord unique et pilotage » (H1-H5, ADR-0011) ; correction factuelle : secrets CI provisionnés |
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté — statuts couche 1, F2 et étape 4 mis à jour |
| 2026-08-13 | hermes (rédaction déléguée à l'orchestrateur Cursor, décision propriétaire — `DEMANDE-20260813-audit-par-grandes-etapes.md`) | audit/contre-audit par grandes étapes (ADR-0012) : section « Grandes étapes — jalons d'audit » (E1-E6), chaîne quatre acteurs mise à jour (Cursor audite les jalons, plus chaque PR) |
| 2026-08-14 | hermes (rédaction déléguée, décision propriétaire — `DEMANDE-20260814-pilote-forgepilot.md`) | pilote ADR-0013 corrigé : Hermes léger, Claude Code Pro plan/revue en lecture seule, Cursor unique exécutant ; ancien full-auto en mode manuel |
| 2026-08-14 | hermes (rédaction déléguée, décision propriétaire) | hébergement progressif : trois lots locaux, VPS 4 Go seulement si concluant ; Render écarté pour Hermes |
| 2026-08-14 | hermes (rédaction déléguée, correction propriétaire — `DEMANDE-20260814-worker-unity-windows.md`) | correction de plateforme : Unity reste sous Windows ; pilote local Windows/WSL2, puis VPS facultatif + worker Unity Windows manuel et bloquant |
| 2026-08-16 | hermes (rédaction déléguée à Claude Code, rattrapage demandé par le propriétaire) | correction factuelle uniquement, aucune décision nouvelle : PR #106 fusionnée le 2026-08-14 (le texte la disait « en attente ») ; fleuves G5 livrés et fusionnés (brief 021, PR #107) ; état du pilote ajouté aux prochaines étapes (lot 022 fusionné sans verdict, brief 023 non lancé, ADR-0014 `proposed`). Rapports adossés : `hermes/reports/RAPPORT-20260816-*.md` |
| 2026-08-16 | hermes (rédaction déléguée à Claude Code, décision propriétaire — `DEMANDE-20260815-hermes-cerveau-du-pipeline.md`) | **ADR-0014 accepté** : Hermes déclenche et rend compte, Claude juge à la demande, Cursor exécute, le propriétaire garde le veto sur la fusion. Section « Workflow pilote » mise à jour ; point d'entrée unique `forge-start` puis `hermes chat -s forgehistory-suivi` |
| 2026-08-19 | hermes | bilan obligatoire des lots pilotes `021` à `023` écrit : pilote clos, trois verdicts finaux acceptés, proposition de conserver ForgePilot avec ajustements ; dette VPS avant bilan déclarée, budget Claude et ADR-0015 laissés à la décision du propriétaire |
| 2026-08-20 | hermes (décision propriétaire — `DEMANDE-20260820-abandon-budget-claude.md`) | abandon de l'enveloppe mensuelle Claude et de la cadence associée comme préalable ; les limites fournisseur restent des états opérationnels à signaler |
| 2026-08-20 | cursor-cloud (décision propriétaire — `DEMANDE-20260820-simulation-sans-unity-hermes-pilote.md`) | **ADR-0016** : `sim/` sans Unity est le produit vivant ; Unity en veille ; Hermes pilote et propose ; crons quotidiens de lecture autorisés. ADR-0015 accepté. |
| 2026-08-20 | cursor-cloud (ordre propriétaire) | `forgepilot enchaine` : un brief, un `--run`, draft PR, pas de fusion (ADR-0013 amendement 002). |
| 2026-08-21 | hermes | brief 025 C1 fusionné par PR #123 : insolation, durées de jour, continentalité et preuves déterministes livrées ; climat observé toujours ouvert ; brief 026 débloqué par la fusion |
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-repartition-modeles-grok-claude.md`) | pilote multi-modèle activé : Grok 4.6 High/XHigh planifie, Composer 2.5 exécute, GPT-5.6 Sol XHigh relit en contexte neuf ; Claude devient un témoin critique différé et sa limite ne bloque plus les lots ordinaires |
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-workflow-adaptatif-r0-r1-r2.md`) | vérifications proportionnées au risque : R0 documentaire, R1 produit borné par défaut avec CI/revue/contrôles en parallèle, R2 critique renforcé ; corrections et itérations deviennent conditionnelles |
| 2026-08-21 | hermes (décision propriétaire — `DEMANDE-20260821-visualiseur-web-v0.md`) | jalon transversal V0 inséré après le correctif 024 et avant le lot 026 : export cellulaire déterministe puis visualiseur web mince interactif ; Unity reste en veille |
| 2026-08-23 | cursor-cloud (correction factuelle après fusion #126) | #126 fusionné : G6 A1/A2 livré non consommé, snapshot `v0a-1`, viewer mince, ForgePilot accéléré. V0 première tranche livrée. Prochain pas unique : brief 026. Plus de « G6 encore en PR ». |
| 2026-08-23 | cursor-cloud (décision propriétaire — ADR-0017) | Grok 4.6 planifie et juge la PR finale ; Composer 2.5 code ; Claude Opus 5 témoin rare ; `forgepilot merge` si PASS + checks verts. Hermes principal : `openai/gpt-5.4`. |
| 2026-08-23 | hermes (correction factuelle après #130 et preuve VPS) | cache Copernicus complet vérifié `1110/1110` ; preuve Europe G6 verte et déterministe. Le relief est calculé mais reste `not_consumed` par `sim/`. Le prochain pas unique reste le brief 026. |
| 2026-08-25 | claude (correction factuelle, ADR-0018) | dégraissage : trois acteurs, carte figée, phases F et prochaines étapes réécrites sur l'état réel |
| 2026-08-25 | claude (**correction factuelle uniquement**, aucune décision nouvelle) | deux affirmations devenues fausses : le tag `archive/2026-08` n'existe pas sur `origin` (403 au push, deux sessions) — le commit `da1596d` le remplace ; et le prochain lot ne demande plus de re-dériver un modèle analytique de survie, celui-ci ayant été retiré au profit de trois propriétés mesurées. Renvoi ajouté vers `docs/MODE-EMPLOI.md`. |
| 2026-08-26 | claude (décision propriétaire du 2026-08-26 — **ADR-0019**) | Claude écrit désormais tous les briefs, Hermes pilote et cesse de rédiger. Corrections factuelles jointes : le relief est joué par le tick depuis la fusion du lot 033 (PR #137) ; onze lots écrits et listés dans « Prochaines étapes » ; et la mesure qui explique pourquoi aucun lot « ville » n'est écrit |
| 2026-08-26 | cursor (correction factuelle, ADR-0020 proposed) | un troisième workflow GitHub : ping worker PC en `workflow_dispatch` seulement ; ce n'est pas le retour du full-auto |
| 2026-08-26 | hermes (correction factuelle après fusion #142) | lot 034 fusionné : le moteur ne porte plus d'état global caché pendant le tick ; prochain lot unique 035 |
| 2026-08-26 | hermes (décision explicite du propriétaire — ADR-0021) | Claude reste disponible manuellement pour les briefs et revues, mais sort de toute orchestration Hermes/ForgePilot ; aucun backend, témoin, cron, skill ou sous-agent Hermes ne peut l'invoquer |
| 2026-08-27 | hermes (correction factuelle après fusion #163 du lot 039) | lot 039 fusionné : le commerce transporte toute marchandise, pas seulement la nourriture — capacité d'arête partagée entre marchandises, SC1 vérifié (sortie byte-identique). 92 tests (86→92), 13 tests commerce. Prochain lot 040 : franchir une montagne coûte. |
| 2026-08-28 | hermes (correction factuelle après fusion #167 du lot 041) | lots 036 (natalité), 040 (relief commerce), 041 (migration) fusionnés. Le monde naît, migre, et le commerce tient compte du relief. F2 : natalité + migration joués. Prochain lot : 042 (viewer) ou 043 (convoi). |
