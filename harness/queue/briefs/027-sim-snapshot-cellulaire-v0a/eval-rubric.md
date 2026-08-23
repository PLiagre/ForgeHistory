# Eval Rubric — Brief 027 : snapshot cellulaire déterministe (V0-A)

**Authored**: 2026-08-22T12:30:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : même rôle signataire et même acteur que
`brief.md` de ce répertoire. Aucun suffixe.

---

## Guide de lecture

Pour chaque condition du brief :

- **Vérification** : commandes rejouables depuis la racine avec
  `.venv/bin/python` (Linux) ou `py` (Windows). Jamais l'alias nu.
- **Reconstruction indépendante** : l'Évaluateur re-dérive la valeur
  depuis les fichiers, sans reprendre un nombre du manifeste ni du journal
  du Générateur.
- **Contre-preuve disqualifiante** : sabotage dans une copie **hors du
  dépôt**. Si le contrôle reste vert, la condition n'est pas satisfaite.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Vocabulaire et décisions : uniquement dans `brief.md` de ce répertoire.
Ce fichier ne les reformule pas.

**Où se lit un compteur.** `deliverables/manifest.json` › `counters[]`
(valeur, `sample_size`, commande). Les JSON sous `deliverables/proofs/`
sont des photographies du monde, pas des supports de compteur.

**Avertissement.** Les constats de contexte du brief (`596`, `473`, C1
complet) ne sont pas des cibles. Un contrôle qui s'y compare nomme sa
propre référence (règle n° 2).

---

## Condition 1 — La commande écrit le snapshot versionné

**Vérification :** rejouer la commande de SC1 du brief, code `0`. Lire
`schema_version`, `seed`, `tick`, `cell_count` dans le fichier écrit.
Comparer `cell_count` à `len(World.from_g3(rng_seed=0).cells)` et à
`len(cells)` du snapshot — les trois égaux. Recompter les `cell_id` G3
depuis `pipeline/geo/artifacts/cells_g3.json` : ensembles identiques.

**Reconstruction indépendante :** charger le snapshot par
`json.loads` ; ne pas importer le module d'export pour décider si le
schéma est fermé — appliquer D3 et D4 du brief au document.

**Contre-preuve :** retirer `--snapshot-json` : aucun des quatre fichiers
de D12 ne doit être requis pour que le CLI existant sorte encore `0` avec
`--ticks 0 --json`. Effacer le fichier de preuve puis relancer SC1 : il
doit réapparaître.

**Résultat attendu :** `code_sortie_snapshot_ok == 0` ;
`schema_version` égal à `SNAPSHOT_SCHEMA_VERSION` lu de
`sim/constants.py`.

---

## Condition 2 — Deux passes, même empreinte

**Vérification :** SHA256 des octets de
`deliverables/proofs/snapshot_seed0_tick0.json` et
`…/snapshot_seed0_tick0_b.json`. Égales, non vides. Relancer les deux
commandes de D12 dans des fichiers temporaires hors dépôt : même
égalité.

**Reconstruction indépendante :** `hashlib.sha256` sur
`Path.read_bytes()`, pas sur une chaîne texte.

**Contre-preuve :** inverser un octet du second fichier — l'égalité
doit échouer. Si le test du Générateur reste vert, la condition est
fausse.

**Résultat attendu :** `paires_sha_snapshot_identiques == 1`.

---

## Condition 3 — Graine et ticks changent les champs attendus

**Vérification :** comparer les trois autres preuves de D12 au
`seed0_tick0`. Recompter soi-même les cellules dont `population`
diffère (`seed1`) et dont un champ d'état diffère (`tick5`).

**Reconstruction indépendante :** ne pas croire le manifeste. Ouvrir les
quatre JSON. Les empreintes `seed1` et `tick5` doivent différer de
`seed0_tick0`. Les deux compteurs strictement positifs doivent porter
un dénominateur égal au nombre de cellules de `seed0_tick0`.

**Contre-preuve :** copier `population` de `seed0` vers `seed1` dans une
copie hors dépôt — le test « la graine change la population » doit
rougir. Idem en recopiant tous les champs d'état de `tick0` vers
`tick5`.

**Résultat attendu :** les deux empreintes différentes ; les deux
compteurs d'écart strictement positifs.

Le brief n'exige pas que **tous** les champs bougent, ni un nombre
précis de cellules. Il exige que ça bouge vraiment, sur un échantillon
non vide.

---

## Condition 4 — Géométrie, province, C1, G6

**Vérification :**

1. Recalculer l'empreinte de `pipeline/geo/artifacts/cells_g3.json` et
   la comparer à `geometry_source.sha256`.
2. Pour un échantillon d'au moins vingt `cell_id` couvrant les extrêmes
   d'identifiants, comparer `geometry` et `centroid` du snapshot à G3.
3. Recalculer l'appartenance par `sim.aggregation.agregat_depuis_monde`
   + `identifiant_de_province_de_cellule` sur un `World.from_g3` de
   même graine, **sans lire le module d'export**. Chaque `province.id`
   doit concorder.
4. Lire `sim/model.py` : aucun champ `province*`.
5. Lire `layers` : C1 `present`, G6 `not_consumed`, R1 `absent` sur ce
   dépôt. Aucune clé `elev_` / altitude dans une cellule.
6. Dans le diff `sim/`, aucune formule d'insolation nouvelle.

**Reconstruction indépendante :** la province se reconstruit depuis
`aggregation.py` déjà fusionné, pas depuis le JSON commenté.

**Contre-preuve :** injecter `centroid_elev_m: 0` dans une cellule —
le test « G6 non consommé » doit rougir. Remplacer un `province.id` par
un autre — le test d'appartenance doit rougir. Mettre
`climate_drivers: null` sur une cellule alors que C1 est `present` et
complet — le compteur de jointure doit cesser d'égaler `cell_count`.

**Résultat attendu :** les compteurs de SC4 du brief à leurs valeurs
d'acceptation.

---

## Condition 5 — Sentinelles, schéma fermé, pas de seconde clé spatiale

**Vérification :** balayer le document racine (clés D3) et chaque
cellule (clés D4). Balayer les clés pour `province_id`, `owner`,
`country`, `pays`. Comparer, sur un monde chargé, chaque champ moteur
égal à `-1` avec la valeur exportée.

**Reconstruction indépendante :** le schéma fermé se juge sur le JSON
publié, pas sur un commentaire.

**Contre-preuve :** ajouter `province_id` ; remplacer un `-1` par `0` ;
ajouter une clé racine `generated_at`. Les trois tests concernés doivent
rougir.

**Résultat attendu :** les quatre compteurs d'écart de SC5 à `0`.

`province` comme objet `{id, name}` est l'exception écrite en SC5 du
brief : ce n'est pas une clé `province_id`.

---

## Condition 6 — Preuves suivies, README, suites, rouges qui mordent

**Vérification :**

```
git ls-files harness/queue/briefs/027-sim-snapshot-cellulaire-v0a/deliverables
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
```

Lire `sim/README.md` : `--snapshot-json` documenté ; le texte dit que
le snapshot n'est ni un rendu ni une simulation parallèle.

Relire les six familles de D13 : chaque sabotage (copie hors dépôt)
doit faire échouer le test correspondant. Un `red_proof` vide ne compte
pas (règle n° 4).

`constants_lignes_supprimees` : diff contre
`deliverables/pre-edit/constants.py.orig` — zéro ligne supprimée.

**Contre-preuve :** un test « rouge » qui reste vert sous le sabotage
décrit en D13 disqualifie la famille entière.

**Résultat attendu :** suites vertes (SKIP Unity Linux déclarés) ;
`controles_rouges_mordants == 6` ; preuves suivies par git.

---

## Ce que cette rubrique ne juge pas

- La correction du relief G6.
- Le brief 026.
- Tout rendu, toute palette, tout serveur : hors lot, brief 028.
- La vérité historique des centres administratifs hérités (déjà bornée
  par le brief 018).
- Un compteur recopié à la main dans le manifeste : présence n'est pas
  fonction (règle n° 7) — seule la reconstruction compte.
