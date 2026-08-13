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
2026-08-12 :

| # | Couche | Statut | Où ça vit |
|---|---|---|---|
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **commencé** : le pipeline géographique produit le littoral 1400 et les cellules/adjacence ; le moteur `sim/` est amorcé (brief 011 : monde chargé depuis les cellules G3, population amorcée, tick déterministe, nourriture physique, chaîne faim→mortalité), vit (brief 012 : base de temps unique, rendement variable, déficit persistant, mortalité proportionnelle au manque, commerce physique entre cellules adjacentes — mesuré sur les 596 cellules réelles) et compte juste (brief 013, issu de l'audit de la PR #60 : le commerce précède la consommation — un kilogramme transféré ne nourrit qu'une fois —, transport limité à une arête par tick et invariant à l'ordre du fichier, mortalité continue plafonnée, seuil de survie dérivé du modèle) | `pipeline/geo/`, `sim/` |
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
| **F2** — Moteur `sim/` couche 1 | Premier code de simulation : monde, terrain, population initiale amorcée historiquement (ADR-002), économie locale physique | **en cours** — brief 011 (amorçage `sim/`) livré et accepté le 2026-08-12 ; brief 012 (base de temps, équilibre alimentaire mesuré, commerce inter-cellules) livré, accepté et fusionné le 2026-08-13 ; brief 013 (correction du P0 « la nourriture transférée nourrit deux fois », transport à une arête, mortalité continue) livré, accepté et fusionné le 2026-08-13 ; brief 014 (pipeline : le contre-audit comme porte observable, le refus fournisseur comme état avec repli) livré et accepté le 2026-08-13, PR en revue ; en file : graines 015/016 (seuil de survie du moteur, issues des audits de la PR #69) ; suites : agrégation Province dérivée, relief/climat/ressources côté geo |
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
Cursor (critique)                              relit chaque PR contre
  │                                            architecture/review-guidelines.md
  ▼
fusion (CI verte + gate ACCEPT + verdict d'un acteur ≠ producteur + audit Cursor)
```

Détail des rôles et des interdits : `docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md`
et `hermes/README.md`. Câblage runtime : `docs/rules/full-auto-pipeline.md`.

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
   verdict PASS à l'itération 2, gate ACCEPT. Suites F2 : agrégation
   Province dérivée, et un brief de harnais pour les points d'audit
   différés (traçage d'acteur des rôles, gate sur les fichiers déclarés
   hors dossier de brief).
5. **Reprendre 004/005** (visuel carte) quand les logs Unity requis par le
   gate sont produits sur la machine propriétaire.

## Historique des révisions

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
| 2026-08-12 | hermes (rédaction déléguée à Cursor, décision propriétaire « ok pour tout ») | reflet de la demande « tableau de bord unique et pilotage » (H1-H5, ADR-0011) ; correction factuelle : secrets CI provisionnés |
| 2026-08-12 | orchestrateur Cursor (remplaçant du CTO Claude, indisponible — instruction propriétaire) | correction factuelle uniquement : brief 011 (F2, amorçage `sim/`) livré et accepté — statuts couche 1, F2 et étape 4 mis à jour |
