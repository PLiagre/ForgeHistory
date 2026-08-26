# Generator log — lot 035

## Rouge avant correction

Sur le SHA de base `9df4917b8e3a4c804c9263eac5973912a8a77092`, la production
au solstice d'été et au solstice d'hiver était identique (`ecart_ete_hiver_avant=0`).
La sonde déclarait `climat.utilisee_par_le_moteur == false`.

## Fichiers modifiés

- `sim/constants.py` — constantes saisonnières niveau 2 et fonctions `jour_de_tick`,
  `duree_jour_h`, `facteur_saison`, `facteur_saison_moyen_annuel`.
- `sim/engine.py` — `tick(world, rng, numero_tick=None)` ; production relief × saison ;
  erreur `ClimatInvalideError` nommée ; plafond via facteur moyen annuel.
- `sim/__main__.py` — transmission du numéro de tick à la boucle CLI.
- `sim/tests/test_monde.py` — cas SC1 à SC6 ajoutés (relief inchangé).
- `deliverables/measure_035.py`, `manifest.json`, sorties textuelles.

## Commandes jouées

```bash
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m sim --ticks 365 --seed 0 --json
.venv/bin/python harness/queue/briefs/035-la-saison-joue-le-rendement/deliverables/measure_035.py --write-manifest
```

## Résultats mesurés

- `ecart_ete_hiver_apres` non nul sur la cellule d'amplitude maximale.
- `couches.relief.utilisee_par_le_moteur` et `couches.climat.utilisee_par_le_moteur` à true.
- `gisements` inchangé par rapport au SHA de base.
- 76 tests `sim/tests/` verts ; `noms_de_constantes_saison_dans_engine=0`.
- Sortie CLI 365 ticks déterministe et différente du SHA de base sur 3 champs dérivés.

## Correctif — manifeste lu par la porte

La relecture a trouvé un échantillon vide : `must_differ_from_git` était un
tableau maison, hors du schéma `files[].must_differ_from_git` que
`harness/verdict_audit.py` lit. `captures_differ_when_should` passait sans
comparer aucune paire.

Le manifeste déclare maintenant `files` comme les lots 033/034 : sources
produit contre le SHA de base, sorties CLI d'après contre l'archive d'avant.

## Limites

- Les appelants sans `numero_tick` (sonde, `test_survie`) emploient le facteur saisonnier
  **moyen annuel** (≈ 1 au niveau 2), pas le jour calendaire du premier tick.
- `production_du_tick_kg` à trois arguments conserve le relief seul ; la saison exige un
  `jour` explicite (compatibilité des ratios de relief en flottant).
