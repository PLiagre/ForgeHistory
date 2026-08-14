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

## Le workflow — quatre acteurs (ADR-0010)

Chaîne nominale d'une évolution, du besoin à la fusion :

```
Propriétaire ──▶ Hermes (chef de projet)      point d'entrée, contexte global,
  │                                            tient ROADMAP.md + hermes/
  ▼
Claude Code (CTO)                              lit la roadmap, écrit les briefs,
  │                                            orchestre /forge-run, évalue, ouvre les PR
  ▼
Codex — GPT-5.6 Sol (exécutant)                Générateur : code, tests, mesures
  │                                            (backend `--backend codex`)
  ▼
Claude Code (CTO)                              gate mécanique + verdict + PR
  ▼
Cursor (critique)                              audite chaque GRANDE ÉTAPE close
  │                                            (jalon hermes/milestones/, ADR-0012)
  ▼                                            — plus jamais chaque PR
fusion (CI verte + gate ACCEPT + verdict d'un acteur ≠ producteur)
```

Détail des rôles et des interdits : `docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md`
et `hermes/README.md`. Câblage runtime : `docs/rules/full-auto-pipeline.md`.

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

1. ~~**Provisionner les secrets CI**~~ — **fait le 2026-08-12** (quota
   d'abonnement : `CLAUDE_CODE_OAUTH_TOKEN`, `CODEX_AUTH_JSON`,
   `CURSOR_API_KEY` ; premiers tours réels consignés dans `HANDOFF.md`).
2. **Rejouer la boucle sur un brief réel** : déclencher
   `pipeline-forge-run` sur un brief F1 restant et vérifier la chaîne
   complète Claude → Codex → gate → PR → critique Cursor.
3. **Hermes tableau unique** (demande du 2026-08-12, tranchée « ok pour
   tout » — `hermes/requests/DEMANDE-20260812-hermes-tableau-de-bord-pilotage.md`,
   ADR-0011) :
   - le propriétaire branche son Hermes local en lecture (H1, configuration
     hors dépôt — skill de suivi + crons) ;
   - le CTO écrit les briefs H2 (export machine du tableau de bord +
     section agents Cursor réellement interrogée) et H3 (liste d'attentes
     propriétaire exhaustive) ;
   - le câblage « console du propriétaire » (H4) se fait dans
     l'installation locale, dans les bornes d'ADR-0011.
4. ~~**Premier brief F2**~~ — **fait le 2026-08-12** : brief 011
   (`harness/queue/briefs/011-sim-monde-vivant-amorcage/`) écrit et passé
   par la boucle trois rôles ; verdict PASS à l'itération 2, gate ACCEPT.
   ~~Suite F2 : commerce inter-cellules~~ — **fait le 2026-08-13** :
   brief 012 (`harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/`,
   issu de l'audit `CURSOR-3b47ffe`) passé par la boucle trois rôles ;
   verdict PASS à l'itération 2, gate ACCEPT. ~~Seuil de survie honnête~~ —
   **fait le 2026-08-14** : brief 017 (`017-sim-seuil-survie-honnete/`,
   fusion des graines 015/016, PR #101). ~~Agrégation Province dérivée~~ —
   **fait le 2026-08-14** : brief 018 (`018-sim-province-derivee/`).
   Les critères du jalon E2 sont réunis ; le fichier
   `hermes/milestones/ETAPE-02-monde-vivant-compte-juste.md` clôt l'étape
   (fusion = déclencheur d'audit ADR-0012). Suites hors E2 : brief de harnais pour les points d'audit
   différés (traçage d'acteur des rôles, gate sur les fichiers déclarés
   hors dossier de brief) ; F1 geo (relief/climat/ressources).
5. **Reprendre 004/005** (visuel carte) quand les logs Unity requis par le
   gate sont produits sur la machine propriétaire.

## Historique des révisions

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
| 2026-08-12 | hermes (rédaction déléguée à Cursor, décision propriétaire « ok pour tout ») | reflet de la demande « tableau de bord unique et pilotage » (H1-H5, ADR-0011) ; correction factuelle : secrets CI provisionnés |
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté — statuts couche 1, F2 et étape 4 mis à jour |
| 2026-08-13 | hermes (rédaction déléguée à l'orchestrateur Cursor, décision propriétaire — `DEMANDE-20260813-audit-par-grandes-etapes.md`) | audit/contre-audit par grandes étapes (ADR-0012) : section « Grandes étapes — jalons d'audit » (E1-E6), chaîne quatre acteurs mise à jour (Cursor audite les jalons, plus chaque PR) |
