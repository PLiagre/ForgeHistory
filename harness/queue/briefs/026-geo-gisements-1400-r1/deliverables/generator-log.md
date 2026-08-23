# Journal générateur — brief 026 (R1 gisements extractifs 1400)

## Ce qui a été fait

- SC0 constaté avant toute écriture : amendement suivi par git, demande propriétaire
  présente, A1/A2/A3 tranchés dans l'amendement.
- Bloc `R1_*` ajouté en fin de `constants.py` ; `WORLD_TERMS_FORBIDDEN_KEYS` importée
  du lot 025, jamais recopiée.
- Fichier de déclarations `data/resources_1400.json` : 27 gisements de D4, colonne
  `richness_class` recopiée, `historical_reason` rédigées en français.
- Module `steps/r1_resources_1400.py` : rattachement par contenance, réversibilité via
  `apply_declarations`, export déterministe.
- Contrôles `qa/checks_r1.py` (Q10 + R1-A..G) et preuves `run_proof_r1.py` /
  `test_qa_red_r1.py`.
- Crochet `pipeline.py --source resources_1400` avec réemploi de `--no-corrections`
  (passe coupée vers répertoire temporaire, sans publier dans `artifacts/`).
- Artefacts, registre, journaux v1_081 et capture générés.

## Mesures constatées (écart au planificateur consigné, pas seuil)

- `gisements_declares` = 27, `gisements_rattaches` = 27, `cellules_dotees` = 25
  (planificateur : 27 / 25 — cohérent, deux couples partagent une cellule).
- `gisements_hors_fenetre` = 0, `gisements_hors_terre` = 0.
- Distribution `par_classe_de_richesse` : majeure 13, notable 11, mineure 3.
- `par_nature` : sel 6, fer 5, argent 6, etain 2, plomb 2, charbon 2, cuivre 1,
  mercure 1, alun 1, or 1.

## Cohérence classe / raison historique

Aucune contradiction signalée entre `richness_class` et `historical_reason` : les
entrées `mineure` (Val Trompia, Schwaz, Liège) décrivent un usage local ou régional
immédiat ; les `majeure` mentionnent un rayonnement lointain ou un monopole connu.

## Capture regardée (règle 11)

Fichier `pipeline/geo/capture/v1_081_resources_window.png` :

- Les points apparaissent aux endroits attendus : étain en Cornouailles (ouest anglais),
  sel en Pologne (Wieliczka), Franche-Comté / Jura (Salins), Suède centrale (fer Norberg,
  cuivre Falun), mercure en Castille (Almadén), or en Slovaquie (Kremnica), alun en
  Anatolie (Phocée).
- Aucun point ne flotte en pleine mer.
- La classe de richesse est montrée par la **forme du marqueur** (cercle / carré /
  triangle dans la légende), pas par la taille ni l'intensité de couleur — la couleur
  encode la nature de ressource uniquement.

## Résistances

- Environnement initial sans `.venv` local dans le worktree : lien symbolique vers le
  venv du dépôt parent.
- Première passe des cas rouges R1-B/R1-F et littéraux de classes dans le module d'étape :
  corrigés avant preuve verte.

## Non fait (hors périmètre)

- Pas de commit, push, fusion, verdict, ni modification de `sim/`.
