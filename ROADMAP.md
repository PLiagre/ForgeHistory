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
| 1 | **Monde vivant** — carte, terrain, climat, ressources, population, économie locale, commerce | **en préparation** : le pipeline géographique produit le littoral 1400 et les cellules/adjacence ; le moteur `sim/` n'est pas commencé | `pipeline/geo/`, `sim/` (stub) |
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
| **F2** — Moteur `sim/` couche 1 | Premier code de simulation : monde, terrain, population initiale amorcée historiquement (ADR-002), économie locale physique | à venir — premier brief à écrire par le CTO depuis cette roadmap |
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

1. **Provisionner les secrets CI** (`ANTHROPIC_API_KEY`, `OPENAI_API_KEY`,
   `CURSOR_API_KEY`) — sans eux, les workflows câblés consignent une
   dérogation et ne font rien (jamais d'échec silencieux).
2. **Rejouer la boucle sur un brief réel** : déclencher
   `pipeline-forge-run` sur un brief F1 restant et vérifier la chaîne
   complète Claude → Codex → gate → PR → critique Cursor.
3. **Premier brief F2** : le CTO écrit le brief d'amorçage de `sim/`
   (couche 1, monde vivant) depuis cette roadmap.
4. **Reprendre 004/005** (visuel carte) quand les logs Unity requis par le
   gate sont produits sur la machine propriétaire.

## Historique des révisions

| date | auteur | changement |
|---|---|---|
| 2026-08-12 | hermes (rédaction initiale déléguée à Cursor, décision propriétaire) | création — état F0/F1, couches jeu, workflow quatre acteurs |
