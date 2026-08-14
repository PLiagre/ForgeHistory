# Journal du Générateur — Brief 018 : la Province dérivée

**Authored**: 2026-08-14T06:25:00Z
**Author**: forge-generateur

> **Note de transparence.** Le rôle signataire est le rôle natif du harnais
> `forge-generateur`. L'acteur réel est un sous-agent Cursor Cloud, modèle
> Claude Opus 5 (`claude-opus-5-thinking-high`), orchestré par un agent Cursor
> Cloud (Grok 4.6) qui remplace le CTO Claude (plafond de quota atteint). La
> session est **distincte** de celle du Planificateur. Aucun suffixe n'est
> ajouté à la signature : le contrôle mécanique `verdict_is_not_self_authored`
> compare les acteurs de part et d'autre d'un lot, et un couple de signatures
> suffixées serait refusé.

Ce journal décrit ce qui a été construit et comment chaque compteur a été
réellement mesuré. Il ne prononce pas la recevabilité du lot : celui qui
produit ne juge pas.

---

## 1. Pré-vol

```
.venv/bin/python harness/budget.py split-check \
  --brief harness/queue/briefs/018-sim-province-derivee --estimated-calls 110
```

Sortie réelle :

```
advisory   : SIZE_OK   (advisory -- the Planificateur decides)
brief      : 018-sim-province-derivee
estimated  : 110
```

```
.venv/bin/python harness/budget.py status --brief harness/queue/briefs/018-sim-province-derivee
```

Sortie réelle :

```
status     : UNMEASURABLE
reason     : no agent transcript naming 018-sim-province-derivee under /home/ubuntu/.claude/projects/-workspace
Nothing is being enforced. This is not OK -- it is unmeasured.
```

C'est la dérogation prévue par le tableau des Waivers du brief : la machine
n'a pas de transcript de session Claude à compter. Elle est déclarée telle
quelle dans `manifest.json`, avec sa commande et son message. Le suivi de
progression a malgré tout été alimenté (`harness/budget.py progress`), qui
enregistre `tool_calls_at = -1` — la sentinelle « non mesuré », jamais un 0.

---

## 2. Ce qui a été construit

### `sim/aggregation.py` (nouveau module)

Le module lit, calcule et rend une vue. Il n'écrit rien : ni fichier, ni
attribut sur une cellule.

- `charger_positions()` — lit `centroid.lat` / `centroid.lon` de chaque cellule
  dans `pipeline/geo/artifacts/cells_g3.json` et rend
  `cell_id → (latitude, longitude)`.
- `charger_centres()` — lit le tableau `coordinates` de
  `pipeline/geo/legacy_game_data/province_coordinates.json` et rend une liste
  d'enregistrements `CentreAdministratif` (immuables).
- `charger_latitude_moyenne()` — lit `projection.mid_latitude` **du fichier**.
  Aucune valeur de latitude n'est recopiée dans un corps de fonction.
- `facteur_de_projection()` / `projeter()` — la projection équirectangulaire
  que le fichier documente lui-même : `x = lon × cos(mid_latitude)`,
  `y = −lat`. La conversion degrés → radians passe par `math.radians`
  (bibliothèque standard), jamais par un facteur écrit à la main.
- `derive_appartenance(positions, centres, latitude_moyenne)` — **la fonction
  pure**. Elle ne reçoit aucun objet `Cell`, ne modifie aucune entrée, n'écrit
  aucun fichier, et rend `cell_id → id du centre le plus proche`. Les
  distances sont comparées **au carré**. Le départage des égalités est écrit
  comme une condition explicite, `carré < meilleur` **ou**
  (`carré == meilleur` **et** `id < meilleur_id`), et non comme un « premier
  arrivé, premier servi » qui basculerait si l'on inversait l'ordre de
  parcours.
- `positions_du_monde(world, positions)` — l'adaptateur en lecture seule. Il
  lit `World.cells` et **refuse de deviner** : une cellule chargée sans
  position lève `PositionCelluleInconnue` en nommant la cellule.
- `regroupements_depuis_appartenance()` / `agregat_depuis_monde()` —
  construisent la vue dérivée : un `Regroupement` par centre lu, y compris
  ceux qui n'attirent aucune cellule.
- `province_de_cellule()`, `nom_de_province_de_cellule()`,
  `identifiant_de_province_de_cellule()`,
  `appartenance_depuis_regroupements()`, `regroupements_non_vides()` — les
  lecteurs de production de la vue.

**Deux types déclarés, tous deux hors de `sim.model`** :
`CentreAdministratif` (`id`, `name`, `lon`, `lat`) et la vue dérivée
`Regroupement` (`id`, `name`, `cell_ids`). Aucun champ dont le nom normalisé
commence par `province`. Les deux héritent de `_NoBadSpatialField`, donc la
garde de l'ADR-0003 s'applique aussi à eux.

**Ce qui n'a pas été touché** : aucun champ ajouté à `Cell`, aucune dataclass
ajoutée à `sim.model`, `sim/engine.py` inchangé, `sim/world.py` inchangé,
`pipeline/geo/` en lecture seule.

### Tests livrés

| Fichier | Ce qu'il mesure |
|---|---|
| `sim/tests/test_province_aggregation.py` | couverture totale, refus de deviner (D5), balayage introspectif des champs interdits, variantes rouges de la garde, couverture d'écriture de la vue, égalités de distance du monde réel |
| `sim/tests/test_redessin_province.py` | le scénario de redessin : l'agrégat bouge, les cellules ne bougent pas |
| `sim/tests/test_determinisme_departage_purete.py` | déterminisme sur trois appels, départage stable dans les deux ordres, pureté de la fonction |
| `sim/tests/test_adr_compliance.py` | **élargi**, jamais réduit : les cinq cas existants sont intacts, un sixième cas introspectif a été ajouté |

Sur `test_adr_compliance.py`, la seule modification est une **addition** : le
test `test_aucune_dataclass_de_sim_model_ne_porte_de_province`, plus deux
lignes d'import (`inspect`, `sim.model`). Aucun cas retiré, aucune liste
blanche élargie, aucun changement du préfixe interdit — le nouveau test dérive
d'ailleurs ce préfixe de `_NoBadSpatialField._FORBIDDEN_PREFIX` au lieu de le
recopier, de sorte qu'il suit la garde si elle change.

### Documentation

- `sim/SEEDING.md` — nouvelle section « brief 018 », insérée avant la section
  « Référence de code ». Elle dit la provenance (données héritées du jeu, pas
  de frontières historiques de 1400, pas de source savante), la projection et
  le fait que son paramètre est lu du fichier, la règle de départage D4, la
  politique de refus D5, et la distinction entre le zéro mesuré et la
  sentinelle `-1`. Elle ne cite **aucun compteur mesuré de ce lot** : ni le
  nombre de cellules, ni le nombre de centres, ni le nombre de provinces
  peuplées. La question de l'ordre « doctrine avant mesure » ne se pose donc
  pas — il n'y a aucune mesure de ce lot dans le fichier.
- `sim/README.md` — mise à jour descriptive : la ligne du nouveau module dans
  le tableau, les deux sources supplémentaires lues en lecture seule, et le
  rappel ADR-0003. Aucune instruction adressée à un agent.

---

## 3. Comment chaque compteur a été mesuré

Tous les compteurs viennent d'une commande réellement exécutée. Aucun n'a été
saisi à la main.

### Scripts de mesure

```
.venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc1_018.py
```

Sortie réelle (code de sortie 0) :

```
=== Brief 018, SC1 — couverture de l'agregation derivee ===
monde mesure : World.from_g3(rng_seed=42)

cellules_chargees_g3 = 596 / 596   (denominateur : cell_count lu dans pipeline/geo/artifacts/stats_g3.json)
centroides_lus = 50 / 50   (denominateur : longueur du tableau coordinates, lue du fichier)
cellules_avec_province = 596 / 596   (denominateur : cellules chargees ; couverture totale attendue)
cellules_sans_province = 0 / 596   (denominateur : cellules chargees ; zero est une mesure reelle, la sentinelle est -1)
cellules_position_absente = 0 / 596   (denominateur : cellules chargees)
cellules_en_double = 0 / 596   (denominateur : cellules chargees ; 'exactement une' exclut aussi le deux)
provinces_non_vides = 50 / 50   (denominateur : centroides lus ; fait mesure, aucun plancher exige — D6)
refus_position_absente_leve = 1 / 1   (cas synthetique : position de la cellule 1175 retiree en memoire)
  message du refus : cellule 1175 : aucune position connue dans les artefacts géographiques. Le code refuse d'attribuer une province par défaut et refuse d'écarter la cellule en silence.

  [OK] cellules_chargees_g3 == cell_count du fichier
  [OK] centroides_lus == longueur du tableau coordinates
  [OK] cellules_avec_province == cellules_chargees_g3
  [OK] cellules_sans_province == 0
  [OK] cellules_en_double == 0
  [OK] cellules_position_absente == 0
  [OK] 0 < provinces_non_vides <= centroides_lus
  [OK] refus_position_absente_leve == 1
```

```
.venv/bin/python harness/queue/briefs/018-sim-province-derivee/deliverables/measure_sc3_018.py
```

Sortie réelle (code de sortie 0) :

```
=== Brief 018, SC3 — redessin d'un centre administratif ===
monde mesure : World.from_g3(rng_seed=42)
centre deplace : id=1 nom=Île-de-France
deplace sur la position exacte de la cellule 1175, qui relevait du centre 12

redessin_change_agregat = 1 / 1   (denominateur : 1 scenario de redessin)
cellules_changeant_de_province_apres_redessin = 22 / 596   (denominateur : cellules chargees ; fait mesure, strictement positif attendu)
redessin_cellules_intactes = 1 / 1   (denominateur : 1 comparaison portant sur 596 cellules serialisees)
attributs_dynamiques_sur_cellules = 0 / 596   (denominateur : cellules chargees ; attributs d'instance compares aux champs declares)
fichier_centroides_inchange_apres_redessin = 1 / 1   (denominateur : 1 comparaison des octets du fichier de centres, avant / apres)

  [OK] redessin_change_agregat == 1
  [OK] cellules_changeant_de_province_apres_redessin > 0
  [OK] la cellule cible releve desormais du centre deplace
  [OK] redessin_cellules_intactes == 1
  [OK] attributs_dynamiques_sur_cellules == 0
  [OK] fichier_centroides_inchange_apres_redessin == 1
```

### Compteurs venus des tests

```
.venv/bin/python -m pytest sim/tests/ -k province -v -s
```

extraits réels des sorties imprimées par les tests :

```
cellules_chargees_g3 = 596 / 596 (cell_count de stats_g3.json)
centroides_lus = 50 / 50 (longueur du tableau coordinates)
cellules_avec_province = 596 / 596
cellules_sans_province = 0 / 596 (zero = mesure reelle, sentinelle = -1)
cellules_position_absente = 0 / 596
cellules_en_double = 0 / 596
provinces_non_vides = 50 / 50 (fait mesure, aucun plancher exige)
cellules_reverifiees_par_recalcul_independant = 596 / 596
refus_position_absente_leve = 1 / 1
classes inspectees : ['Cell', 'CentreAdministratif', 'Regroupement']
prefixe interdit derive de la garde : 'province'
dataclasses_inspectees = 3 / 3
champs_province_sur_entites = 0 / 14
garde_prefixe_variantes_rouges = 5 / 5
CentreAdministratif : champs=['id', 'lat', 'lon', 'name'] construits=['id', 'lat', 'lon', 'name'] lus=['id', 'lat', 'lon', 'name']
Regroupement : champs=['cell_ids', 'id', 'name'] construits=['cell_ids', 'id', 'name'] lus=['cell_ids', 'id', 'name']
champs_vue_couverts = 7 / 7
egalites_de_distance_monde_reel = 0 / 596 (fait mesure, peut valoir 0)
```

```
.venv/bin/python -m pytest sim/tests/ -k "determinisme or departage or purete" -v -s
```

extraits réels :

```
cellules comparees = 596
cellules identiques sur les trois appels = 596 / 596
determinisme_agregation_deux_passes = 1 / 1
ordres essayes = 2
gagnants = [3, 3]
departage_egalite_plus_petit_id = 1 / 1 (2 ordres x 1 cas synthetique)
carres de distance = [0.45642212862617093, 0.45642212862617093]
positions_mutees = 0 / 596
centres_mutes = 0 / 50
fichiers_lus_mutes = 0 / 2
cellules_mutees_par_agregation = 0 / 1
```

### Précisions sur trois compteurs

- **`champs_vue_couverts = 7 / 7`.** Le balayage AST porte sur
  `sim/aggregation.py` et `sim/world.py`, et couvre **tous** les types
  dataclass déclarés par le module d'agrégation — donc les 4 champs de
  `CentreAdministratif` en plus des 3 champs de la vue `Regroupement`. C'est
  un sur-ensemble de ce que le brief exige, jamais un sous-ensemble : le
  contrôle est plus strict, pas plus laxe. Les types sont découverts par
  `inspect.getmembers`, jamais listés à la main.
- **`compteurs_en_dur_trouves = 0 / 41`.** Le dénominateur est le nombre de
  fonctions inspectées par `test_no_hardcoded.py` dans les six modules de
  `sim/` hors tests, recompté par un parcours AST identique à celui du test.
- **`egalites_de_distance_monde_reel = 0 / 596`.** Zéro est ici un résultat :
  sur le monde réel, aucune cellule n'est à distance exactement égale de deux
  centres. La sentinelle « non calculé » est `-1` et n'est employée nulle part
  dans ce manifeste.

Un mot sur `provinces_non_vides = 50 / 50` : les 50 centres se trouvent tous
peuplés, mais **aucun test n'impose ce résultat**. La seule assertion est
`0 < provinces_non_vides <= centroides_lus` — une borne du réel, pas un
plancher. Si une correction de coordonnées vidait un centre demain, le test
resterait vert et le compteur rapporterait le fait.

---

## 4. Preuves rouges (deux paires, sabotage hors dépôt)

Les deux sabotages ont été montés dans des copies de travail **hors du dépôt**
(`/tmp/forge-018-red-a/` et `/tmp/forge-018-red-b/`, contenant `sim/` et les
artefacts géographiques nécessaires). Le dépôt lui-même n'a jamais porté le
code saboté. Les quatre sorties sont en `.txt` — `.gitignore` exclut `*.log`,
et une preuve laissée là ne serait pas re-vérifiable depuis un clone frais.

### Paire A — la garde spatiale

Sabotage : ajout de `province_id: int = field(default=-1)` sur `Cell` dans
`/tmp/forge-018-red-a/sim/model.py`.

```
cd /tmp/forge-018-red-a && /workspace/.venv/bin/python -m pytest sim/tests/test_adr_compliance.py -v -s
```

Fin de sortie réelle, recopiée dans
`sim/tests/proof_red/run_garde_province_red.txt` :

```
FAILED sim/tests/test_adr_compliance.py::test_cell_has_no_province_id_field
FAILED sim/tests/test_adr_compliance.py::test_aucune_dataclass_de_sim_model_ne_porte_de_province
========================= 2 failed, 4 passed in 0.02s ==========================
```

Le vert correspondant, `sim/tests/proof_red/run_garde_province_green.txt`,
est le même test sur le code correct du dépôt : `6 passed`, dont
`champs_province_sur_entites = 0 / 7`.

### Paire B — le redessin sans réécriture

Sabotage : dans `/tmp/forge-018-red-b/sim/aggregation.py`, `agregat_depuis_monde`
estampille l'appartenance sur chaque cellule sous le nom **`zone_admin`**. Ce
nom est choisi pour **échapper** à la garde de préfixe : sans quoi ce serait la
règle de nom qui rougirait, et non le test de redessin. Un contrôle trop
grossier coûte aussi cher qu'un contrôle laxiste.

```
cd /tmp/forge-018-red-b && /workspace/.venv/bin/python -m pytest sim/tests/test_redessin_province.py -v -s
```

Fin de sortie réelle, recopiée dans `sim/tests/proof_red/run_redessin_red.txt` :

```
E       AssertionError: Au moins une cellule a acquis un attribut d'instance : l'appartenance a ete estampillee sur les cellules.
E       assert 596 == 0

sim/tests/test_redessin_province.py:125: AssertionError
=========================== short test summary info ============================
FAILED sim/tests/test_redessin_province.py::test_redessin_change_agregat_sans_reecrire_les_cellules
========================= 1 failed, 1 passed in 0.09s ==========================
```

Contrôle que le sabotage échappe bien à la règle de nom — dans la même copie
sabotée :

```
cd /tmp/forge-018-red-b && /workspace/.venv/bin/python -m pytest sim/tests/test_adr_compliance.py -q
```

```
......                                                                   [100%]
6 passed in 0.01s
```

La garde de préfixe reste verte : c'est bien le test de redessin qui tient la
propriété, pas le nom du champ.

Le vert correspondant, `sim/tests/proof_red/run_redessin_green.txt`, est le
même test sur le code correct : `2 passed`, avec
`attributs_dynamiques_sur_cellules = 0 / 596`.

Les deux paires sont déclarées dans `manifest.json` avec `must_differ_from`,
en chemins relatifs au dossier du brief.

---

## 5. Suites complètes

```
.venv/bin/python -m pytest sim/tests/ -v
```

Sortie réelle (code de sortie 0) :

```
============================= test session starts ==============================
platform linux -- Python 3.12.3, pytest-9.1.1, pluggy-1.6.0 -- /workspace/.venv/bin/python
cachedir: .pytest_cache
rootdir: /workspace
collecting ... collected 65 items

sim/tests/test_adr_compliance.py::test_cell_has_no_province_id_field PASSED [  1%]
sim/tests/test_adr_compliance.py::test_province_id_field_raises_explicit_error PASSED [  3%]
sim/tests/test_adr_compliance.py::test_province_id_variant_raises_explicit_error PASSED [  4%]
sim/tests/test_adr_compliance.py::test_province_short_name_raises_explicit_error PASSED [  6%]
sim/tests/test_adr_compliance.py::test_province_code_raises_explicit_error PASSED [  7%]
sim/tests/test_adr_compliance.py::test_aucune_dataclass_de_sim_model_ne_porte_de_province PASSED [  9%]
sim/tests/test_causal_chain.py::test_sc7a_stock_decreases_when_production_lt_consumption PASSED [ 10%]
sim/tests/test_causal_chain.py::test_sc7b_hunger_ticks_increments_when_stock_empty PASSED [ 12%]
sim/tests/test_causal_chain.py::test_sc7c_population_decreases_when_deficit_positive PASSED [ 13%]
sim/tests/test_causal_chain.py::test_sc7d_zero_yield_leads_to_population_decline PASSED [ 15%]
sim/tests/test_commerce.py::test_deficit_accumule_quand_manque PASSED    [ 16%]
sim/tests/test_commerce.py::test_conservation_masse_transport PASSED     [ 18%]
sim/tests/test_deficit_physique.py::test_deficit_reduction_infinitesimal PASSED [ 20%]
sim/tests/test_deficit_physique.py::test_deficit_reduction_proportionnel PASSED [ 21%]
sim/tests/test_deficit_physique.py::test_invariant_physique_reduction_bornee_par_le_surplus PASSED [ 23%]
sim/tests/test_determinisme_departage_purete.py::test_determinisme_agregation_deux_passes PASSED [ 24%]
sim/tests/test_determinisme_departage_purete.py::test_departage_egalite_plus_petit_id PASSED [ 26%]
sim/tests/test_determinisme_departage_purete.py::test_departage_egalite_est_bien_une_egalite_exacte PASSED [ 27%]
sim/tests/test_determinisme_departage_purete.py::test_purete_agregation_ne_mute_pas_les_entrees PASSED [ 29%]
sim/tests/test_engine.py::test_tick_determinisme PASSED                  [ 30%]
sim/tests/test_engine.py::test_tick_different_seeds_differ PASSED        [ 32%]
sim/tests/test_hunger_criterion.py::test_hunger_ticks_cellule_ravitaillee PASSED [ 33%]
sim/tests/test_hunger_criterion.py::test_penurie_reelle_incremente_toujours PASSED [ 35%]
sim/tests/test_hunger_criterion.py::test_penurie_retournee_est_le_manque_exact PASSED [ 36%]
sim/tests/test_kg_transportes_est_arrives.py::test_kg_transportes_egal_deltas_positifs PASSED [ 38%]
sim/tests/test_kg_transportes_est_arrives.py::test_kg_transportes_etoile PASSED [ 40%]
sim/tests/test_mortalite_accumulateur.py::test_champ_mortality_remainder_est_sentinelle PASSED [ 41%]
sim/tests/test_mortalite_accumulateur.py::test_famine_tue_en_borne_de_ticks PASSED [ 43%]
sim/tests/test_mortalite_accumulateur.py::test_precision_mortalite_sur_n_ticks PASSED [ 44%]
sim/tests/test_mortalite_continue.py::test_plafond_toute_population PASSED [ 46%]
sim/tests/test_mortalite_continue.py::test_deficit_non_efface_en_1_tick PASSED [ 47%]
sim/tests/test_no_hardcoded.py::test_no_hardcoded_numeric_literals PASSED [ 49%]
sim/tests/test_province_aggregation.py::test_province_couverture_totale_monde_reel PASSED [ 50%]
sim/tests/test_province_aggregation.py::test_province_consultation_rend_le_centre_le_plus_proche PASSED [ 52%]
sim/tests/test_province_aggregation.py::test_province_refus_position_absente PASSED [ 53%]
sim/tests/test_province_aggregation.py::test_province_aucun_champ_province_sur_entites PASSED [ 55%]
sim/tests/test_province_aggregation.py::test_province_garde_prefixe_variantes_rouges PASSED [ 56%]
sim/tests/test_province_aggregation.py::test_province_champs_vue_couverts PASSED [ 58%]
sim/tests/test_province_aggregation.py::test_province_egalites_de_distance_monde_reel PASSED [ 60%]
sim/tests/test_province_aggregation.py::test_province_agregation_ne_reference_aucune_cellule_modifiable PASSED [ 61%]
sim/tests/test_redessin_province.py::test_redessin_change_agregat_sans_reecrire_les_cellules PASSED [ 63%]
sim/tests/test_redessin_province.py::test_redessin_naffecte_pas_les_enregistrements_lus PASSED [ 64%]
sim/tests/test_rng.py::test_rng_etat_change_apres_tick PASSED            [ 66%]
sim/tests/test_rng.py::test_ticks_deterministes_meme_graine PASSED       [ 67%]
sim/tests/test_rng.py::test_ticks_differents_graines_rng_differentes PASSED [ 69%]
sim/tests/test_seeding.py::test_seeding_determinisme PASSED              [ 70%]
sim/tests/test_seeding.py::test_different_seeds_give_different_populations PASSED [ 72%]
sim/tests/test_sensibilite_survie.py::test_sensibilite_hds PASSED        [ 73%]
sim/tests/test_sensibilite_survie.py::test_sensibilite_drr_direction PASSED [ 75%]
sim/tests/test_sensibilite_survie.py::test_prediction_reagit_bien_a_la_production PASSED [ 76%]
sim/tests/test_survie_derivee.py::test_fraction_predite_analytique PASSED [ 78%]
sim/tests/test_survie_derivee.py::test_stationnaire_est_sous_la_capacite_de_charge PASSED [ 80%]
sim/tests/test_survie_stationnaire.py::test_horizon_est_au_dela_du_transitoire PASSED [ 81%]
sim/tests/test_survie_stationnaire.py::test_bornes_des_constantes_du_modele PASSED [ 83%]
sim/tests/test_survie_stationnaire.py::test_fraction_survie_dans_tolerance_stationnaire PASSED [ 84%]
sim/tests/test_tick_nourrit_une_fois.py::test_ecart_temoin_vs_receveuse PASSED [ 86%]
sim/tests/test_tick_nourrit_une_fois.py::test_chaine_1_2_3 PASSED        [ 87%]
sim/tests/test_tick_nourrit_une_fois.py::test_invariance_ordre_aretes PASSED [ 89%]
sim/tests/test_tick_nourrit_une_fois.py::test_recepteur_pas_sur_livre PASSED [ 90%]
sim/tests/test_world.py::test_cells_count_matches_stats PASSED           [ 92%]
sim/tests/test_world.py::test_adjacency_count_matches_file PASSED        [ 93%]
sim/tests/test_world.py::test_cells_have_required_fields PASSED          [ 95%]
sim/tests/test_write_coverage.py::test_all_dataclass_fields_have_write_and_read_sites PASSED [ 96%]
sim/tests/test_write_coverage.py::test_adjacency_is_read_by_engine PASSED [ 98%]
sim/tests/test_write_coverage.py::test_write_coverage_counter_etendu PASSED [100%]

============================== 65 passed in 5.53s ==============================
```

```
.venv/bin/python -m pytest harness/tests/ -q
```

Sortie réelle (code de sortie 0) :

```
........................................................................ [ 19%]
........................................................................ [ 39%]
........................................................................ [ 59%]
...............................................ssssssssssssssss......... [ 79%]
........................................................................ [ 98%]
....                                                                     [100%]
348 passed, 16 skipped in 17.12s
```

Les 16 `SKIP` sont ceux de `test_run_unity.py`, qui exigent Unity et
PowerShell sous Windows : ils sont attendus sur cette machine Linux et non un
échec.

Sélections rejouées par la rubrique :

```
.venv/bin/python -m pytest sim/tests/ -k province -q      → 16 passed, 49 deselected
.venv/bin/python -m pytest sim/tests/ -k redessin -q      → 2 passed, 63 deselected
.venv/bin/python -m pytest sim/tests/ -k "determinisme or departage or purete" -q → 6 passed, 59 deselected
.venv/bin/python -m pytest harness/tests/test_single_source_of_instruction.py -q → 1 passed
```

---

## 6. Registre de coût (SC8)

```
.venv/bin/python harness/backends/ledger.py append --backend cursor \
  --brief harness/queue/briefs/018-sim-province-derivee \
  --event generator-run
```

Ligne réellement ajoutée à `harness/queue/cost-ledger.jsonl` :

```
{"timestamp": "2026-08-14T06:22:44.781531", "backend": "cursor", "brief": "harness/queue/briefs/018-sim-province-derivee", "event": "generator-run"}
```

`.venv/bin/python harness/backends/ledger.py report` fait apparaître
`harness/queue/briefs/018-sim-province-derivee: cursor=1`. Aucun `--audit-id` :
ce brief naît de la feuille de route, pas d'un audit converti.

---

## 7. Périmètre respecté

- `sim/engine.py` : **non modifié**. Le pas de temps ne consomme pas
  l'agrégation (D7).
- `sim/model.py` : **non modifié**. Aucun champ ajouté à `Cell`, aucune
  dataclass ajoutée.
- `sim/world.py` : **non modifié**. L'adaptateur en lecture seule vit dans le
  nouveau module.
- `pipeline/geo/` : lecture seule. Le test de redessin et le script de mesure
  comparent les octets du fichier de centres avant et après ; ils sont
  identiques. Aucune valeur hexadécimale de condensé n'est recopiée nulle part.
- Archives des briefs 011 à 017 : non touchées.
- `harness/*.py`, `architecture/`, `unity/`, `VISION.md`, `ROADMAP.md`,
  `HANDOFF.md`, `.github/` : non touchés. La seule écriture hors `sim/` et hors
  du dossier du brief est la ligne ajoutée à `harness/queue/cost-ledger.jsonl`,
  prévue par SC8.
- `brief.md` et `eval-rubric.md` : non modifiés. Aucun `verdict.md` écrit.

---

## 8. Je n'ai ni committé, ni poussé, ni créé de branche

Aucun `git commit`, aucun `git push`, aucun `git checkout -b`, aucun
`git switch -c` n'a été exécuté pendant cette session. Tous les fichiers sont
laissés **non commités** sur la branche fournie par l'orchestrateur,
`forge/018-province-derivee-779a`. Aucune branche parasite n'a été créée.
L'orchestrateur seul dépose.

---

## 9. Auto-contrôle mécanique

```
.venv/bin/python harness/verdict_audit.py harness/queue/briefs/018-sim-province-derivee
```

Cet auto-contrôle est déterministe et non manipulable ; le lancer soi-même
n'est pas un verdict. Deux contrôles échouent nécessairement à ce stade,
`verdict_numbers_traceable` et `verdict_is_not_self_authored`, parce que
`verdict.md` n'existe pas encore : c'est l'Évaluateur qui l'écrit, et le
Générateur ne doit pas s'en charger. Les autres contrôles sont au vert.

**Celui qui produit ne prononce pas la recevabilité.** Ce journal rapporte des
mesures et des commandes ; le jugement appartient à l'Évaluateur.
