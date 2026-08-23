# Eval Rubric — Brief 030 : sim/ lit les gisements déclarés (R1)

**Authored**: 2026-08-23T09:45:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : même rôle signataire et même acteur que `brief.md`
de ce répertoire (Fable, session Planificateur).

---

## Guide de lecture

Pour chaque condition du brief :

- **Vérification** : commandes rejouables depuis la racine avec
  `.venv/bin/python`. Jamais l'alias nu.
- **Reconstruction indépendante** : l'Évaluateur re-dérive chaque valeur
  depuis les fichiers, sans reprendre un nombre du manifeste ni du journal
  du Générateur.
- **Contre-preuve disqualifiante** : sabotage dans une copie **hors du
  dépôt**. Si le contrôle reste vert, la condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire et décisions : uniquement dans `brief.md` de ce répertoire (et,
pour le vocabulaire des gisements, dans le brief 026 qu'il cite). Ce fichier
ne les reformule pas.

**Où se lit un compteur.** `deliverables/manifest.json` › `counters[]`
(valeur, `sample_size`, commande). Les JSON sous `deliverables/proofs/`
sont des photographies du monde, pas des supports de compteur.

---

## Condition 0 — Le lot 026 était fusionné avant la première écriture

**Vérification :** rejouer les deux `git ls-files` de SC0 sur `master` au
commit parent du lot : chacune rend une ligne. Vérifier dans
`generator-log.md` que le constat a été fait en premier.

**Contre-preuve :** dans un clone où `cells_resources_r1.json` est retiré de
l'index, la commande rend une sortie vide — le Générateur aurait dû
s'arrêter là sans produire un fichier.

**Résultat attendu :** `artefacts_r1_suivis_par_git == 2`.

---

## Condition 1 — La photographie consomme la couche R1

**Vérification :** rejouer la commande de SC1 du brief, code `0`. Lire
`schema_version` et le comparer à `SNAPSHOT_SCHEMA_VERSION` lu de
`sim/constants.py`. Lire `layers.resources_r1` : statut, chemins, deux
empreintes. Recalculer les deux empreintes (`hashlib.sha256` sur
`Path.read_bytes()`) depuis les artefacts sources et comparer.

**Reconstruction indépendante :** recompter soi-même, depuis
`pipeline/geo/artifacts/resources_1400_r1.json`, les gisements dont
`attachment` vaut `contained`, puis recompter les objets de gisement publiés
dans les cellules du snapshot : mêmes identifiants, chacun une seule fois.
Ne pas importer `sim/snapshot_export.py` pour ce comptage.

**Contre-preuve :** dans une copie hors dépôt, retirer une cellule de
`cells_resources_r1.json` puis relancer l'export — la couche doit passer à
`not_consumed` et toutes les cellules à `null`. Si elle reste `present`, la
condition est fausse.

**Résultat attendu :** `cellules_avec_cle_resources == cell_count` ;
`gisements_publies_snapshot` égal au recomptage indépendant ;
`gisements_publies_en_double == 0`.

---

## Condition 2 — L'absence se déclare (`absent` / `not_consumed`)

**Vérification :** ouvrir `deliverables/proofs/snapshot_seed0_tick0_sans_r1.json` :
`layers.resources_r1.status == "absent"`, toutes les cellules à
`resources: null`. Rejouer les tests des familles 1 à 4 de D7 :

```
.venv/bin/python -m pytest sim/tests/ -q
```

**Reconstruction indépendante :** reproduire soi-même la preuve `sans_r1`
(copie du dépôt hors arbre de travail, artefacts R1 retirés, même commande
d'export) et vérifier le statut obtenu.

**Contre-preuve :** dans une copie hors dépôt, faire pointer l'export vers
un `cells_resources_r1.json` qui liste un gisement inexistant — la couche
doit refuser (`not_consumed`). Si l'export publie le gisement fantôme ou
l'ignore en silence, la condition est fausse.

**Résultat attendu :** `cellules_null_quand_non_consomme == cell_count`
dans les deux modes dégradés ; aucun repli.

---

## Condition 3 — Vide et null distincts

**Vérification :** dans la preuve `present`, compter les `[]` et vérifier
qu'aucune cellule ne porte `null` ; dans la preuve `sans_r1`, vérifier
qu'aucune cellule ne porte `[]`.

**Contre-preuve :** remplacer un `[]` par `null` dans une copie de la preuve
`present` — le test de la famille 5 doit rougir sur cette copie.

**Résultat attendu :** `cellules_resources_liste_vide` rapporté avec
`cell_count` pour dénominateur ; zéro confusion dans les deux sens.

---

## Condition 4 — Tick intact, schéma fermé, aucune quantité

**Vérification :**

```
git diff --stat master -- sim/engine.py sim/model.py sim/world.py sim/aggregation.py sim/__main__.py
rg -c "resources" sim/engine.py || echo 0
```

Balayer chaque cellule du snapshot : exactement les douze clés attendues ;
chaque objet de gisement : exactement `id`, `resource`, `richness_class`.
Balayer toutes les clés du snapshot contre `R1_FORBIDDEN_QUANTITY_KEYS` et
`WORLD_TERMS_FORBIDDEN_KEYS` lues de `pipeline/geo/constants.py`.
Diff de `sim/constants.py` contre `deliverables/pre-edit/constants.py.orig` :
zéro ligne supprimée.

**Reconstruction indépendante :** le schéma fermé se juge sur le JSON
publié, pas sur un commentaire ni sur le code d'export.

**Contre-preuve :** injecter une clé `tonnage` dans un objet de gisement
d'une copie hors dépôt — le test de la famille 6 doit rougir. Convertir une
`richness_class` en entier — le balayage de schéma doit rougir.

**Résultat attendu :** les cinq compteurs de SC4 à `0` (et
`occurrences_resources_dans_engine == 0`).

---

## Condition 5 — Déterminisme, suites, README, rouges qui mordent

**Vérification :**

```
git ls-files harness/queue/briefs/030-sim-lit-gisements-r1/deliverables
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
.venv/bin/python -m sim --ticks 0 --seed 0 --snapshot-json /tmp/v0a2_eval.json
.venv/bin/python -m viewer --snapshot /tmp/v0a2_eval.json --proof-svg /tmp/carte_eval.svg
```

SHA256 des deux preuves `v0a2` : égales, non vides. Empreinte de la preuve
`sans_r1` : différente (couple `must_differ_from` du manifeste). Lire
`sim/README.md` : le schéma `v0a-2` documenté, la non-consommation par le
tick dite explicitement.

**Reconstruction indépendante :** relancer deux fois l'export dans des
fichiers temporaires hors dépôt et recomparer les empreintes soi-même.

**Contre-preuve :** relire le journal des huit familles de D7 : chaque
sabotage décrit doit faire rougir le test correspondant, rejoué dans une
copie hors dépôt. Un `red_proof` vide ou un rouge non reproduit disqualifie
la famille (règle n° 4).

**Résultat attendu :** suites vertes (SKIP Unity Linux déclarés) ;
`paires_sha_snapshot_identiques == 1` ;
`empreinte_avec_r1_differe_sans_r1 == 1` ;
`controles_rouges_mordants == 8` ; preuves suivies par git.

---

## Ce que cette rubrique ne juge pas

- La vérité historique de la liste des gisements ou de leur classe (bornée
  par le brief 026 et son amendement : c'est de la donnée déclarée).
- La manière dont le tick se servira un jour des gisements (décision
  propriétaire en attente — question `D3` de la proposition du 2026-08-23).
- Le rendu des gisements dans le viewer (lot 031).
- Le relief G6 et le climat observé.
- Un compteur recopié à la main dans le manifeste : présence n'est pas
  fonction (règle n° 7) — seule la reconstruction compte.
