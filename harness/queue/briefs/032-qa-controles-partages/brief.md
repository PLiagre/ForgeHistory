# Brief 032 — Contrôles partagés : cinq familles écrites une fois

**Authored**: 2026-08-23T19:40:00Z
**Author**: forge-planificateur

**Risque : R1.** Changement borné dans `pipeline/geo/`, sur des invariants
existants, sans nouvelle mécanique de monde et sans toucher au droit de
fusion. Ce lot ne réduit aucun contrôle : il en partage l'écriture.

**Taille de ce brief.** Il est court exprès. Le lot 026 a produit 1 139 lignes
de brief pour 27 gisements, dont la moitié réénonçait ce que le dépôt
contenait déjà. Ici, chaque constat porte la commande qui le reproduit, et
rien n'est recopié de `docs/rules/`.

## Problème mesuré

Cinq familles de contrôle sont réécrites lot après lot. Mesuré par lecture de
l'arbre syntaxique des trois modules de contrôle (`qa/checks.py`,
`qa/checks_c1.py`, `qa/checks_r1.py`) :

| famille | implémentations | fonctions | lignes |
|---|---|---|---|
| maillage inchangé | 3 | `g6e_mesh_unchanged`, `c1a_mesh_unchanged`, `r1f_cell_mesh_unchanged` | 68 |
| réversibilité déclarations actives / coupées | 4 | `g2b_b_reversibility`, `g5b_b_reversibility`, `g5cter_d_reversibility_to_g5b`, `r1d_reversibility` | 104 |
| absence d'omission silencieuse | 3 | `p1b_no_silent_omission`, `p2c_no_silent_omission_123_and_105`, `r1c_no_silent_omission` | 120 |
| rattachement par contenance | 4 | `g7cities_a_containment_only`, `p1c_containment_only`, `p2e_containment_only`, `r1b_containment_only` | 160 |
| clés interdites et vocabulaire fermé | 4 | `c1f_no_gameplay_keys`, `_key_matches_forbidden`, `_walk_forbidden_keys`, `r1e_no_bareme_ni_quantite` | 75 |

**Total : 18 fonctions, 527 lignes, pour cinq idées.**

Deux constats aggravants :

1. **Le cas rouge d'un contrôle déjà partagé est, lui, recopié.**
   `q10_determinism` est écrit une seule fois dans `qa/checks.py` — mais son
   cas rouge `red_q10` existe dans **sept** fichiers `tests/test_qa_red_*.py`,
   en **cinq versions distinctes** (les corps de `g2b`, `g3`, `g4`,
   `{c1, r1}` et `{g5, g6}` ont cinq empreintes différentes). La règle n° 4
   est donc réappliquée à neuf à chaque lot, et elle a déjà dérivé.
2. **Le squelette de preuve est copié verbatim.** La ligne
   `red_proof = str(proof.get("case") or "") if became_red else ""` apparaît
   à l'identique dans **cinq** des huit `tests/run_proof_*.py` ; les huit
   partagent la même structure de rapport `*_qa.json` et le même code de
   sortie.

**Le geste est déjà commencé.** `checks_c1.py` et `checks_r1.py` importent
déjà `CheckResult` et `q10_determinism` depuis `qa/checks.py`. Partager n'est
pas une nouveauté à introduire ici : c'est un geste à finir.

## Autorité et frontières

- Ce lot ne touche ni `sim/`, ni `viewer/`, ni `unity/`, ni la CI, ni le
  harnais.
- Il n'ajoute aucune mécanique de monde et ne produit aucune donnée nouvelle.
- Le producteur ne prononce pas la recevabilité de son propre travail
  (ADR-0014).

## La condition centrale : zéro dérive

Une factorisation qui change une sortie n'est pas une factorisation, c'est une
réécriture déguisée. Deux invariants, tous deux mécaniques.

**Invariant 1 — les artefacts ne bougent pas d'un octet.** Les neuf fichiers
produits par les lots C1 et R1 doivent avoir, après migration et rejeu,
exactement les empreintes SHA-256 qu'ils portent aujourd'hui dans `master` :

- `artifacts/MANIFEST_c1.json`, `artifacts/cells_climate_drivers_c1.json`,
  `artifacts/stats_c1.json`, `registry/climate_drivers_registry.json` ;
- `artifacts/MANIFEST_r1.json`, `artifacts/cells_resources_r1.json`,
  `artifacts/resources_1400_r1.json`, `artifacts/stats_r1.json`,
  `registry/resource_registry.json`.

**Invariant 2 — les identifiants de contrôle ne changent pas.**
`logs/v1_080_qa.json` porte sept identifiants (`Q10`, `C1-A` à `C1-F`) et
`logs/v1_081_qa.json` en porte huit (`Q10`, `R1-A` à `R1-G`) : quinze au
total, tous présents après migration, dans le même ordre, avec le même
`passed` et un `red_proof` non vide. L'identifiant est le contrat public d'un
contrôle — les rubriques des lots 025 et 026 le citent nommément. Seule son
implémentation se déplace.

Un contrôle qui resterait vert **sans** que son cas rouge le fasse virer au
rouge est un échec du lot, quelle que soit la ligne de rapport.

## Ce que le lot livre

1. **`pipeline/geo/qa/checks_common.py`** — les cinq familles, chacune
   paramétrée par ce qui varie réellement d'un lot à l'autre : les clés lues,
   le vocabulaire admis, la source du maillage. Jamais par le nom du lot.
   Règle n° 2 : un contrôle dérive, il ne se nomme jamais d'après sa cible.
   Une famille dont un paramètre s'appellerait `r1_...` est un échec.
2. **`pipeline/geo/tests/red_common.py`** — un lanceur de cas rouges piloté
   par table : une entrée = un identifiant de contrôle + la mutation qui doit
   le faire virer au rouge. `red_q10` y est écrit **une fois**, et les sept
   fichiers `test_qa_red_*.py` l'importent au lieu de le redéfinir.
3. **`pipeline/geo/tests/proof_common.py`** — l'assemblage du rapport
   `*_qa.json` et le code de sortie, communs aux huit preuves. Rien de
   spécifique à un lot n'y entre.
4. **Migration de C1 et R1** vers ces trois modules : `checks_c1.py`,
   `checks_r1.py`, `test_qa_red_c1.py`, `test_qa_red_r1.py`,
   `run_proof_c1.py`, `run_proof_r1.py`.
5. **Unification de `red_q10`** dans les sept fichiers rouges — y compris ceux
   des lots non migrés, puisque c'est le cas rouge d'un contrôle déjà commun.

## Conditions de succès

- **SC1** — `qa/checks_common.py` existe et expose les cinq familles ; aucune
  de ses signatures ne nomme un lot.
- **SC2** — `checks_c1.py` et `checks_r1.py` n'implémentent plus aucune des
  cinq familles : ils appellent `checks_common`. Les quinze identifiants de
  contrôle restent exposés sous leurs noms actuels.
- **SC3** — `tests/run_proof_c1.py` et `tests/run_proof_r1.py` sortent en code
  `0`, et les neuf empreintes d'artefacts sont **identiques** à celles de
  `master` (invariant 1).
- **SC4** — les quinze identifiants sont présents dans les deux `*_qa.json`
  régénérés, dans le même ordre, chacun avec `passed` vrai et un `red_proof`
  non vide (invariant 2).
- **SC5** — `red_q10` n'est plus défini que dans `tests/red_common.py`, et les
  sept `test_qa_red_*.py` l'importent. Les six preuves non migrées (`g2`,
  `g2b`, `g3`, `g4`, `g5`, `g6`) sortent toujours en code `0`, empreintes
  d'artefacts inchangées.
- **SC6** — le nombre de lignes des cinq familles, recompté par le même
  parcours d'arbre syntaxique qu'en « Problème mesuré », a diminué. Le
  Générateur écrit la valeur avant et après. Aucun seuil n'est imposé : c'est
  une mesure, pas une note.

## Hors périmètre

- **Les six lots G2, G2b, G3, G4, G5, G6 ne sont pas migrés** (hors
  `red_q10`). Ce sera le lot **033**. Raison : migrer sept lots avec preuve de
  zéro dérive pour chacun dépasse les 150 appels d'outil au-delà desquels le
  contrat du Planificateur impose la scission. Le gain de ce lot est donc
  surtout **en aval** — le prochain lot produit ne réécrit plus ses contrôles,
  il les paramètre — et le lot 033 récupère l'arriéré.
- Aucun contrôle n'est supprimé, assoupli, fusionné ou renommé.
- `qa/checks.py` n'est pas réorganisé : les fonctions `gN`/`pN` y restent
  appelables sous leurs noms actuels jusqu'au lot 033.
- Aucun artefact n'est régénéré « pour le plaisir » : une sortie qui change
  est un échec, pas une mise à jour.
- Pas de commit, pas de push, pas de branche, pas de fusion, pas de verdict.

## Compteurs exigés

| nom | source d'échantillon | dénominateur |
|---|---|---|
| `familles_partagees` | les cinq familles nommées au tableau « Problème mesuré » | 5 |
| `implementations_avant` | parcours d'arbre syntaxique de `qa/checks.py`, `qa/checks_c1.py`, `qa/checks_r1.py`, sur les 18 fonctions nommées au tableau | 18 |
| `lignes_familles_avant` | somme des longueurs de ces 18 fonctions, même parcours | 18 fonctions ; 527 attendu, toute autre valeur s'explique et ne se recale pas |
| `lignes_familles_apres` | même parcours, après migration | les fonctions restantes des cinq familles |
| `identifiants_preserves` | identifiants lus dans `logs/v1_080_qa.json` et `logs/v1_081_qa.json` régénérés | 15 |
| `artefacts_empreinte_identique` | SHA-256 des neuf fichiers de l'invariant 1, avant (`git show`) et après | 9 |
| `red_q10_implementations_avant` | fichiers `tests/test_qa_red_*.py` définissant `red_q10` | 7 fichiers, 5 corps distincts |
| `red_q10_implementations_apres` | même comptage | 7 fichiers |
| `preuves_non_migrees_vertes` | les six `run_proof_g*.py` rejoués | 6 |

Chaque compteur est produit par une commande écrite dans
`deliverables/manifest.json`, et le script qui les reconstruit est
`deliverables/measure_032.py`. Aucun compteur ne vaut `0` ni `-1` pour une
affirmation réellement faite (règle n° 8).

## Dérogations acceptables

| affirmation | commande exigée | erreur exigée |
|---|---|---|
| « une famille ne peut pas être paramétrée sans changer un comportement » | le rejeu de la preuve du lot concerné | la ligne de diff nommant le fichier et l'empreinte qui bouge |
| « une preuve non migrée ne peut pas être rejouée dans cet environnement » | la commande de rejeu, telle quelle | le message d'erreur exact de l'environnement, jamais une paraphrase |

Une affirmation d'impossibilité sans commande **et** sans erreur n'est pas un
constat, c'est une abdication (règle n° 9).

## Contrat d'exécution

- Aucune étape Unity. Aucun accès réseau. Aucune donnée externe.
- `py`, jamais `python` (règle n° 1).
- Appels d'outil estimés pour ce lot : **110**. Sous le seuil de scission de
  150 — c'est précisément pourquoi les six lots G partent au lot 033.
- Tout fichier nommé dans `deliverables/manifest.json` est suivi par git, et
  ses chemins sont écrits **relativement au répertoire du brief** : la porte
  mécanique les résout depuis là, pas depuis la racine du dépôt. Le lot 026
  s'est trompé sur ce point et ses trois couples de preuve n'ont rien comparé.
- Les couples « doit différer » se déclarent en `must_differ_from_git`, avec
  une référence `<rev>:<path>` : git détient déjà l'état pré-édition. Aucune
  copie `.orig` n'est committée pour un fichier que git suit.
- Livrables : `manifest.json`, `generator-log.md` en français clair,
  `measure_032.py`.

## Interdictions pour le Générateur

Il ne prononce jamais la recevabilité de son propre travail, n'écrit aucun
`verdict.md`, ne modifie ni `brief.md` ni `eval-rubric.md`, ne commite pas, ne
pousse pas, ne crée ni ne change de branche, et ne fusionne rien.
