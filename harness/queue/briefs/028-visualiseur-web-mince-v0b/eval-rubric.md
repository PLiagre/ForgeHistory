# Eval Rubric — Brief 028 : visualiseur web mince (V0-B)

**Authored**: 2026-08-22T12:30:00Z
**Author**: forge-planificateur

Ce document est rédigé par le Planificateur AVANT tout code.
L'Évaluateur l'applique sans le modifier.
Voir `docs/rules/harness-roles.md` et `docs/rules/simulation-principles.md`.

Note de transparence : même rôle signataire et même acteur que
`brief.md` de ce répertoire. Aucun suffixe.

Le schéma du snapshot se juge **contre le brief 027 fusionné**, jamais
contre une liste recopiée ici.

---

## Guide de lecture

Pour chaque condition du brief :

- **Vérification** : commandes rejouables, `.venv/bin/python` ou `py`.
- **Reconstruction indépendante** : re-dériver depuis les fichiers,
  sans les nombres du manifeste.
- **Contre-preuve disqualifiante** : sabotage hors dépôt.
- **Résultat attendu** : ce que le Générateur doit avoir produit.

Décisions : uniquement dans `brief.md` de ce répertoire.

**Où se lit un compteur.** `deliverables/manifest.json` › `counters[]`.

---

## Condition 0 — Le préalable 027 est réel

**Vérification :** les deux commandes de D0 du brief. Si
`--snapshot-json` ou `SNAPSHOT_SCHEMA_VERSION` manque, le lot devait
s'arrêter sans créer `viewer/`. Un `viewer/` présent malgré un SC0
rouge est un dépassement.

**Reconstruction :** ne pas se fier au journal. Exécuter D0.

**Contre-preuve :** sans objet ici si 027 est bien fusionné. Si 027
n'est pas fusionné et que ce lot a quand même produit du code, la
condition 0 échoue.

**Résultat attendu :** les deux compteurs de SC0 à `1`.

---

## Condition 1 — Démarrage local et refus

**Vérification :**

```
.venv/bin/python -m viewer
.venv/bin/python -m viewer --snapshot /tmp/inexistant.json
```

(chemins adaptés). Codes `2`. Puis `--proof-svg` sur
`deliverables/proofs/snapshot_a.json` : code `0`. Lire
`viewer/README.md` : les deux commandes de D2 y sont.

**Reconstruction :** produire un JSON minimal dont
`schema_version` vaut `"v0a-999"` et le rester du document est
sinon valide — le refus doit avoir lieu **avant** tout dessin.

**Contre-preuve :** un mode qui accepte une version inconnue et
dessine quand même disqualifie.

**Résultat attendu :** codes de SC1 du brief.

---

## Condition 2 — Une cellule du fichier, un polygone

**Vérification :** `cell_count` lu de `snapshot_a.json`. Compter dans
`carte_population.svg` les éléments qui représentent une cellule.
Les deux nombres égaux. Tout `cell_id` du JSON apparaît dans le SVG
(attribut, `id`, ou table jointe déterministe documentée dans le
journal — l'Évaluateur doit pouvoir refaire la jointure).

**Reconstruction :** ne pas utiliser `596`. Lire le fichier.

**Contre-preuve :** retirer une cellule du snapshot dans une copie,
relancer `--proof-svg` : le compte de polygones doit suivre le
nouveau `cell_count`, pas l'ancien.

**Résultat attendu :** `cellules_snapshot_non_dessinees == 0`.

---

## Condition 3 — `null` et `-1` restent visibles comme tels

**Vérification :** rejouer les fonctions de classification et de
différence (D9) sur des cas `0`, `null`, `-1`. Lire le SVG et le
journal : trois signes distincts (D7 du brief).

**Reconstruction :** appeler les fonctions pures, pas une capture
commentée.

**Contre-preuve :** les sabotages 2 et 5 de D9 doivent faire rougir
les tests concernés. Un coloriage où `null` reçoit la teinte du zéro
est un échec même si pytest est vert — l'Évaluateur **regarde** le
SVG (règle n° 11).

**Résultat attendu :** les trois compteurs de SC3 à `0`.

---

## Condition 4 — Yeux seulement, pas de second monde

**Vérification :** `rg` (ou lecture) sous `viewer/` :

- `http://` / `https://` dans html, js, css ;
- `pipeline/geo` dans py et js ;
- imports `sim` autres que `SNAPSHOT_SCHEMA_VERSION` ;
- sous-chaînes de clés spatiales concurrentes.

**Reconstruction :** le parcours est celui de D11 / SC4 du brief.

**Contre-preuve :** ajouter une URL CDN dans `viewer/` statique — le
contrôle doit rougir. Importer `sim.engine` — idem.

**Résultat attendu :** les quatre compteurs de SC4 à `0`.

Le viewer a le droit d'**afficher** `mortality_remainder` lu du
snapshot. Il n'a pas le droit de s'en servir dans une multiplication.
Un doute se tranche en lisant l'usage, pas le nom.

---

## Condition 5 — Comparaison déterministe

**Vérification :** SHA256 des deux passes SVG A+B ; SHA256 du SVG A
seul. Rejouer `--proof-svg` hors dépôt, mêmes snapshots : mêmes
empreintes.

**Reconstruction :** `Path.read_bytes()` + `hashlib`. Aucune
empreinte recopiée (règle n° 12).

**Contre-preuve :** altérer un octet d'une passe — l'égalité doit
échouer. Forcer la différence incomparable à `0` dans une copie —
le SVG ou la fonction de différence doit cesser d'être honnête, et
le test D9.5 rougir.

**Résultat attendu :** `paires_sha_svg_identiques == 1` ;
empreintes A et A+B différentes. Le journal décrit ce que l'œil
voit : fenêtre peuplée, dégradé non plat, légende des absents.

---

## Condition 6 — Suites, preuves suivies, rouges qui mordent

**Vérification :**

```
.venv/bin/python -m pytest viewer/tests/ -q
.venv/bin/python -m pytest sim/tests/ -q
.venv/bin/python -m pytest harness/tests/ -q
git ls-files harness/queue/briefs/028-visualiseur-web-mince-v0b/deliverables
```

Les snapshots de preuve ont été produits par `-m sim --snapshot-json`,
pas édités à la main (comparer en relançant l'export vers un temporaire
et en égalant les empreintes).

Les cinq familles de D9 mordent sous sabotage hors dépôt.

`sim/` n'apparaît pas dans le diff de ce lot.

**Contre-preuve :** un « rouge » qui reste vert sous le sabotage
décrit disqualifie la famille.

**Résultat attendu :** suites vertes (SKIP Unity Linux déclarés) ;
`controles_rouges_mordants_028 == 5` ; `sim/` intact.

---

## Ce que cette rubrique ne juge pas

- La forme du schéma 027 (déjà jugée par le lot 027).
- La correction G6, les gisements 026, Unity.
- La beauté du dégradé au-delà de : non plat, trois sentinelles
  distinctes, pas de barème vert/rouge.
- Un serveur qui tourne encore après les tests : hors preuve ; la
  preuve est `--proof-svg` et les fonctions pures.
