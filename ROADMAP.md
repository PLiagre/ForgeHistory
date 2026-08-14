# ROADMAP — ForgeHistory

> **Propriétaire de ce document : Hermes (chef de projet).** Toute évolution
> de la feuille de route passe par une demande écrite sous `hermes/requests/`
> (format : `hermes/README.md`), tranchée par le propriétaire, puis reflétée
> ici par Hermes. Personne d'autre ne réécrit ce fichier sur le fond ;
> une correction factuelle (statut devenu faux) est permise à tout acteur,
> en la signalant dans le message de commit.
>
> La vision produit (ce que le jeu **est**) vit dans [VISION.md](VISION.md)
> et prime en cas de conflit. Ce fichier dit **où on en est** et **dans quel
> ordre on avance** — jamais ce qu'il faut faire pour un brief donné (ça,
> c'est le brief lui-même, voir `CLAUDE.md` › Single Source of Instruction).

## Le jeu — cinq couches, dans l'ordre

Les couches viennent de `VISION.md` § « Roadmap par couches ». Statut au
2026-08-14 :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** : le pipeline géographique produit le littoral 1400 et les cellules/adjacence ; le moteur `sim/` est amorcé (brief 011), vit (brief 012, mesuré sur les 596 cellules réelles), compte juste (brief 013 : un kilogramme transféré ne nourrit qu'une fois ; brief 017, fusion des graines 015/016, PR #101 fusionnée le 2026-08-14 : seuil de survie honnête — prédiction stationnaire, accumulateur de mortalité, faim = pénurie, récupération physique) et agrège les terres en provinces dérivées (brief 018 : appartenance recalculée depuis les centroïdes, jamais un champ stocké — ADR-0003) | `pipeline/geo/`, `sim/` |
| 2 | **Villes** — urbanisation, entreprises, métiers, routes, infrastructures | non commencé | `sim/` |
| 3 | **États** — fiscalité, lois, diplomatie, technologies, culture, religion | non commencé | `sim/` |
| 4 | **Armées** — recrutement, logistique, ravitaillement, stratégie | non commencé | `sim/` |
| 5 | **Batailles tactiques** — sur les mêmes données que tout le reste | non commencé | `sim/` |

La couche présentation (Unity) est un client de rendu mince : le jeu
VictoriaProject a été porté (brief 003) et sert de base visuelle ; il ne
contiendra jamais de logique de simulation.

## Le projet — phases F

| Phase | Contenu | Statut |
|---|---|---|
| **F0** — Harnais | Trois rôles (Planificateur / Générateur / Évaluateur), gate mécanique `verdict_audit.py`, briefs 001→010, boucle d'audit Cursor, pipeline full-auto (FSM, orchestrateur, budgets) | **terminé** |
| **F1** — Fondations monde | Pipeline géographique (littoral `1400` ✓, cellules G3 ✓, suite : relief, climat, ressources), portage Unity ✓, refonte visuelle carte (briefs 004/005, reprise conditionnée aux logs Unity) | **en cours** |
| **F2** — Moteur `sim/` couche 1 | Premier code de simulation : monde, terrain, population initiale amorcée historiquement (ADR-002), économie locale physique | **en cours** — briefs 011, 012, 013 livrés et fusionnés ; brief 014 (pipeline : contre-audit comme porte, refus fournisseur comme état) livré, accepté et fusionné le 2026-08-13 (PR #83) ; brief 017 (seuil de survie honnête, fusion des graines 015/016) livré, accepté et fusionné le 2026-08-14 (PR #101, sans squash) ; brief 018 (Province dérivée, ADR-0003) livré et accepté le 2026-08-14, PR en revue ; les graines 015/016 ne s'exécutent plus (elles pointent vers 017) ; suites F2 moteur : aucune restante pour clôturer E2 ; reste F1 geo (relief/climat/ressources) |
| **F3+** — Couches 2 à 5 | Villes, États, Armées, Batailles — chaque couche émerge de la précédente | à venir |

## Le workflow pilote — Hermes, Grok, Cursor (ADR-0013)

Chaîne nominale pendant trois lots d'essai :

```
Propriétaire ──▶ Hermes léger                 point d'entrée et choix de la tâche
  ▼
Grok Build (lecture seule)                    plan pré-écrit et critères mesurables
  ▼
Cursor CLI (worktree agent/*)                 unique exécutant : code et tests
  ▼
CI                                             contrôles mécaniques
  ▼
Grok Build (nouvelle invocation, lecture seule) revue du diff contre le plan
  ▼
Propriétaire                                  décision de fusion
```

L'ancien pipeline ADR-0010 reste disponible en mode `manual` comme solution de
retour arrière. L'observateur Windows est suspendu. Le pilote n'utilise ni ACP,
ni cron, ni auto-merge : `control-plane/README.md` est le runbook.

## Grandes étapes — jalons d'audit (ADR-0012)

Décision propriétaire du 2026-08-13
(`hermes/requests/DEMANDE-20260813-audit-par-grandes-etapes.md`) : l'audit
Cursor et le contre-audit Claude ne tournent plus à chaque PR — trop de
jetons, trop d'allers-retours — mais à la **clôture de chaque grande
étape**, marquée par un fichier `hermes/milestones/ETAPE-NN-<slug>.md`
fusionné sur `master` (contrat : `hermes/milestones/README.md`). Un audit
ponctuel reste possible à tout moment par `workflow_dispatch` (incident,
changement structurel entériné par ADR, doute).

| jalon | ce que l'étape doit réunir pour être close | statut |
|---|---|---|
| **E1 — Fondations monde complètes** (clôt F1) | relief, climat et ressources livrés par `pipeline/geo/` (en plus du littoral ✓ et des cellules G3 ✓) ; artefacts consommables par `sim/` ; visuel carte repris (briefs 004/005) si les logs Unity sont disponibles | à venir |
| **E2 — Le monde vivant compte juste** (clôt F2, couche 1) | seuil de survie honnête (graines 015/016 traitées) ; agrégation Province dérivée (ADR-0003) ; monde mesuré stable et falsifiable sur les 596 cellules réelles | **clos** — critères réunis (017 + 018) ; la fusion de `hermes/milestones/ETAPE-02-monde-vivant-compte-juste.md` déclenche l'audit d'étape (ADR-0012) |
| **E3 — Villes** (couche 2) | urbanisation, entreprises, métiers, routes, infrastructures — émergeant de la couche 1 | à venir |
| **E4 — États** (couche 3) | fiscalité, lois, diplomatie, technologies, culture, religion | à venir |
| **E5 — Armées** (couche 4) | recrutement, logistique, ravitaillement, stratégie | à venir |
| **E6 — Batailles + rendu branché** (couche 5) | batailles tactiques sur les mêmes données ; Unity rend l'état du moteur (client mince, zéro logique) | à venir |

Moments cruciaux supplémentaires (audit par `workflow_dispatch`, sans
jalon) : tout changement structurel du harnais ou du pipeline entériné par
un ADR ; tout incident de boucle (perte de données, garde contournée) ;
toute veille de décision irréversible du propriétaire.

## Prochaines étapes (dans l'ordre)

1. Installer ForgePilot, Hermes, Grok Build et Cursor CLI sur un petit serveur
   Linux persistant ; authentifier Grok par le compte SuperGrok et Cursor par
   le compte Cursor. Ne pas fournir `XAI_API_KEY` au pilote.
2. Exécuter `forgepilot doctor`, puis un premier lot réel avec les commandes
   `plan`, `execute` et `review`, sans cron et sans auto-merge.
3. Refaire deux lots de portée comparable. Mesurer qualité du plan, retouches
   humaines, durée, limites d'usage et incidents d'authentification.
4. Après trois lots, décider : conserver, ajuster ou supprimer ForgePilot. Ne
   réactiver l'ancien full-auto que par une nouvelle décision propriétaire.
5. Côté produit, poursuivre F1 géographique (relief, climat, ressources), puis
   brancher VictoriaCityLab comme vue de ville sur les contrats ForgeHistory.

## Historique des révisions

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
| 2026-08-12 | hermes (rédaction déléguée à Cursor, décision propriétaire « ok pour tout ») | reflet de la demande « tableau de bord unique et pilotage » (H1-H5, ADR-0011) ; correction factuelle : secrets CI provisionnés |
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté — statuts couche 1, F2 et étape 4 mis à jour |
| 2026-08-13 | hermes (rédaction déléguée à l'orchestrateur Cursor, décision propriétaire — `DEMANDE-20260813-audit-par-grandes-etapes.md`) | audit/contre-audit par grandes étapes (ADR-0012) : section « Grandes étapes — jalons d'audit » (E1-E6), chaîne quatre acteurs mise à jour (Cursor audite les jalons, plus chaque PR) |
| 2026-08-14 | hermes (rédaction déléguée, décision propriétaire — `DEMANDE-20260814-pilote-forgepilot.md`) | pilote ADR-0013 : Hermes léger, Grok plan/revue en lecture seule, Cursor unique exécutant ; ancien full-auto en mode manuel |
