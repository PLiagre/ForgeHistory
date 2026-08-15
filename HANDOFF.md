# HANDOFF.md

> **Borné à trois sessions** par ADR-0014 amendement 001 (2026-08-15). Les
> sessions plus anciennes sont dans l'historique git ; ce qui compte pour le
> récit du projet va dans `hermes/reports/`, pas dans une strate de plus ici.

## Session la plus récente — 2026-08-15 : brief 022 livré et fusionné, brief 023 écrit, ADR-0014

**Contexte** : session interactive, le propriétaire pilotant Claude Code
directement. Le pilote ForgePilot (ADR-0013) a fait passer son deuxième lot
réel, et la session a débordé sur une question d'architecture que le
propriétaire a tranchée.

### Ce qui a été fait

1. **Brief 022 (ForgePilot : relecture par stdin, commande `iterate`)** passé
   de bout en bout dans le pilote : `plan` → `execute` (Cursor) → `publish` →
   relecture → itération → fusion. **PR #108 fusionnée par le propriétaire**
   (`6c6a807`).
   - **Amendement 001** avant toute génération : le brief annonçait `quatre`
     tests préexistants là où `test_workflow.py` en contient `six`.
   - Le défaut d'origine est **prouvé corrigé en production** sur le vrai diff
     de `1 239 157` octets qui avait cassé le lot 021 : plus grand élément
     d'argv `24` octets (contre `1 253 092` avant), et `611 570` jetons de
     prompt réellement parvenus à l'API. L'`OSError: [Errno 7]` a été reproduit
     sur le code d'avant, puis a disparu.
   - **Relecture par `forgepilot review`** — premier usage réel depuis sa
     réparation : verdict `FAIL`, huit constats, dont un (`F3`) **faux**, ayant
     lu le `site-packages` du dépôt principal au lieu de celui du worktree.
   - **Itération 2 via `forgepilot iterate`** — premier usage réel de la
     commande que ce lot ajoute. Les six points du `feedback-001` corrigés.
     `tests_rouges_avant_correction` passe de `2` à **`6` sur `6`**.
2. **Brief 023 écrit** (`harness/queue/briefs/023-forgepilot-modele-et-effort-par-role/`) :
   réglage du modèle et de l'effort **par rôle**, avec priorité au drapeau
   d'appel — la brique sans laquelle ADR-0014 est inapplicable. Contrôles :
   `no_bare_python_alias` PASS, `split-check` `SIZE_OK`, Single Source of
   Instruction passé. **Pas encore lancé.**
3. **ADR-0014 proposé** (+ `hermes/requests/DEMANDE-20260815-hermes-cerveau-du-pipeline.md`) :
   Hermes déclenche et rend compte, Claude juge, Cursor exécute. **Amendement
   001** joint, qui tranche le tableau de bord et la question des deux mémoires.
4. **Phase 0 de reprise** : worktree fusionné retiré, branche locale supprimée,
   `hermes/DASHBOARD.md` régénéré (il annonçait `full_auto` depuis le
   2026-08-14 alors qu'ADR-0013 l'avait mis en `manual`).

### La mesure qui a orienté la session

`harness/backends/ledger.py tokens` : **`68.66` USD** d'équivalent tarif API
pour ce lot, dont **`59.70` pour la seule orchestration** — `87` % du total, sur
`434` appels à `213 801` jetons de contexte moyen. Le plan a coûté `1.08`, la
relecture `1.96`. Le coût de Cursor n'est pas observable par ce registre et
n'est pas supposé nul.

**Le plafond mensuel de l'abonnement Claude a été atteint pendant la session**,
pour la troisième fois depuis le 2026-08-13. Il a tué le sous-agent Évaluateur
et une relecture de 1,2 Mo (`6.24` USD à elle seule).

### Dette assumée de cette session

**La PR #108 a été fusionnée sans verdict d'Évaluateur.** Le sous-agent est mort
sur le plafond avant d'écrire `verdict.md`. C'est une entorse réelle au harnais,
consignée ici et non effacée. Le travail est intégralement sur `master`
(`82a356a`, `1eade7a`), donc un Évaluateur peut juger a posteriori — et le brief
023 en dépend (son non-objectif n° 3 renvoie au « verdict de référence du lot
022 »).

L'orchestrateur ne peut pas l'écrire : il a rédigé l'amendement 001 **et** le
feedback 001 du même lot.

### Trois défauts que seule une reconstruction indépendante a attrapés

Ils motivent ADR-0014 et méritent d'être retenus :

1. Le brief annonçait `quatre` tests là où il y en a `six`.
2. La relecture automatique a conclu faux (`F3`) en lisant le mauvais
   environnement.
3. Le Générateur déclarait `2` tests rouges là où il y en avait `5`.

Aucun n'était visible sans refaire la mesure soi-même.

### Validation rejouée sur l'état final

- `git status` → propre, `master` = `origin/master` = `4b472be`.
- `cd control-plane && python3 -m unittest discover -s tests` → **12 tests, OK**.
- `.venv/bin/python -m pytest harness/tests/ -q` → **9 failed, 355 passed**
  (les neuf Unity préexistants sous WSL2, binaire absent).
- `.venv/bin/python harness/harness_audit.py` → **23/24**. Le rouge est
  `no_premature_stub_content`, hérité : l'outil croit `sim/` vide alors que F2
  l'a peuplé. **Ne jamais vider `sim/` pour le faire passer.**
- `.venv/bin/python harness/demo/fake_brief_001/run_demo.py` → **PROVEN**, le
  faux brief est rejeté.

### Prochain pas

**Phase 1, dès que le quota revient** : sous-agent `forge-evaluateur` sur le lot
022 (~`3` USD), puis `/forge-checkpoint` pour enregistrer son verdict.

**Ensuite** : lancer le brief 023 dans le pilote — il part désormais de `master`,
la PR #108 étant fusionnée.

**En attente du propriétaire** : le plafond mensuel Claude (point 2c), et
l'acceptation d'ADR-0014, qui reste `proposed`.

**Après le lot 023** : le bilan des trois lots réels qu'ADR-0013 exige avant
toute décision de VPS.

### Briefs appelés, non écrits

- `hermes/dashboard.py:205-206` affirme en dur que le tableau est régénéré à
  chaque poussée et toutes les 6 h — **faux depuis ADR-0013**
  (`workflow_dispatch` seul).
- `/forge-checkpoint` doit borner `HANDOFF.md` à trois sessions (règle appliquée
  à la main dans cette version).
- Hermes sait déclencher un lot — suppose le brief 023 livré.

---

## Session précédente — 2026-08-14 : brief 020 (provenance du littoral G3)

**Contexte** : orchestration tenue par un agent Cursor Cloud remplaçant le
CTO Claude. Trois rôles, jamais le même agent dans la même passe, modèle
Claude Opus 5 (`claude-opus-5-thinking-high`) — jamais inherit/Grok pour
un rôle du harnais. Signatures natives `forge-planificateur` /
`forge-generateur` / `forge-evaluateur`, sans suffixe. Note de
transparence Cursor Cloud. Les rôles n'ont ni committé, ni poussé, ni
créé de branche — l'orchestrateur seul dépose. Branche `forge/` (pas
`cursor/*` : le job `cursor-scope` réserve ce préfixe aux PRs
`architecture/inbox/`).

**Décision CTO.** E2 est clos sur `master` (briefs 017+018, PRs #101 /
#102 / #103 fusionnées, sans squash). Brief 019 (adjacence maritime G4)
PASS, **PR #105 fusionnée** le 2026-08-14 (sans squash) — le lot 020
était autorisé. Un seul lot : **réparer la provenance du littoral G3**
(trou mesuré et escaladé par 019, amendement 001, non-objectif 18). E1
n'est **pas** clos. Pas de villes (E3). Pas de réouverture du 007. Pas
de fusion par l'orchestrateur. Fusion **sans squash**.

### Ce qui a été fait

1. **Planificateur** (`3ac7c77`) : brief neuf
   `harness/queue/briefs/020-geo-provenance-littoral-g3/`. Mesures avant
   réécriture : écart de **sérialisation** (union des cellules vs terre
   du littoral vivant sous l'epsilon **lue**), pas de géométrie — maille
   non rejouée ; les identifiants consommés par `sim/` restent ceux du
   fichier committé ; G4 relit les champs de provenance, graphe intact.
   Sept SC, `38` compteurs. `split-check` : `SIZE_OK`.
2. **Générateur itération 1** (`a32f598`) : script neuf
   `steps/03b_align_coastline_provenance.py` (un seul champ G3 :
   `inputs.coastline_1400` calculé sur le fichier vivant). Garde
   `tests/run_proof_coastline_provenance.py` (codes 0/1/2, vue rouge hors
   dépôt). Commande d'écart : code `1` avant, code `0` après. Maille
   `4`/`4` diffs vides. Graphe G4 `16`/`16` diffs vides. `sim/` lecture
   seule (`65 passed`). Harnais `348 passed`, `16 skipped`.
3. **Évaluateur passe 1** : **PASS** (`verdict.md`). Porte ACCEPT dix
   sur dix, couple `forge-generateur<->forge-evaluateur`. Reconstruction
   hors dépôt : `38` compteurs, aucun écart. Sabotage de la déclaration :
   garde code `1` ; absence : code `2`. Mutation d'un octet du littoral :
   écart code `1`. Idempotence : seconde passe octet-identique.

**Branche / PR** : `forge/020-geo-provenance-g3-2099`, **PR #106**
brouillon. Ne pas fusionner soi-même. Fusion **sans squash**. E1 n'est
**pas** clos.

**Réserves (verdict 020, non bloquantes)** : formulation SC5
(`porcelain` vide) piège un rôle qui n'a pas le droit de committer —
défaut du brief, pas du travail ; `code_sortie_ecart_avant` dérivé du
texte committé ; `progress.jsonl` hors ensemble de balayage hex (propre
quand même) ; rejeu de la maille G3 hors dépôt non tenté (facultatif,
lot dédié possible).

**Suites (pas ce lot)** : G5 fleuves / G6 relief ; recalibrage
`SEA_ZONE_COUNT_MAX` (bornes d'intention toujours ouvertes) ; N1 du
017 ; briefs de harnais ; réparation PR #100 ; audits PROPOSED ;
clôture E1.

**Validation rejouée** :
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/020-geo-provenance-littoral-g3` → ACCEPT (dix sur dix).
- `.venv/bin/python -m pytest harness/tests/ -q` → 348 passed, 16 skipped (Unity/Linux, attendus).
- `.venv/bin/python -m pytest sim/tests/ -q` → 65 passed.

**Prochain pas** : le propriétaire fusionne **#106** (lot 020 + cette
correction de feuille de route), **sans squash**. Ensuite : G5/G6, ou
recalibrage des bornes de semis, ou brief de harnais — pas d'audit
Cursor à attendre sur #106 (ADR-0012 : audit à la clôture d'étape, E1
n'est pas close).

---

## Session encore avant — 2026-08-14 : brief 019 (adjacence maritime G4), premier lot E1

**Contexte** : orchestration tenue par un agent Cursor Cloud remplaçant le
CTO Claude. Trois rôles, jamais le même agent dans la même passe, modèle
Claude Opus 5 (`claude-opus-5-thinking-high`) — jamais inherit/Grok pour
un rôle du harnais. Branche `forge/` (pas `cursor/*` : le job
`cursor-scope` réserve ce préfixe aux PRs `architecture/inbox/`).

**Décision CTO.** E2 est clos sur `master` (briefs 017+018, PRs #101 / #102
/ #103 fusionnées, sans squash). Prochain jalon = **E1 — Fondations
monde**. E1 entier est trop gros : premier lot atomique seulement =
**G4 adjacence maritime** (zones de mer + graphe typé). Motif : G5
fleuves et G6 relief dépendent des cellules **et** de l'adjacence. G3
est livré (596 cellules) ; le lot 007b n'a jamais été exécuté. Brief
**neuf 019**, pas une réouverture du 007.

### Ce qui a été fait

1. **Planificateur** : brief + rubrique (`95215a2`). Dix SC, D1–D16,
   46+ compteurs, reconstruction contre la barre QA déjà portée.
2. **Générateur itération 1** (`5e54571`) : `steps/04_adjacency.py`,
   40 zones (5000–5039), 2085 arêtes (917 terre-terre, 437 terre-mer,
   63 mer-mer, 668 détroits), 2 liens déclarés (Zuiderzee / Lauwerszee).
   Preuve rouge d'abord, déterminisme deux passes, `pipeline.py` et
   `constants.py` intacts.
3. **Évaluateur passe 1** : **REJECT** (`3a6a397`). Porte ACCEPT (forme).
   Huit SC sur dix tiennent ; 48/48 compteurs reconstruits sans écart.
   SC7 : empreinte du littoral relu ≠ entrée déclarée par G3
   (incohérence antérieure au lot, D16 interdit de toucher G3). SC10 :
   une empreinte de parité citée par sa valeur dans le journal (règle 12).
   Les 24 zones hors bornes d'intention : constat ouvert, pas un rejet.
4. **Planificateur amendement 001** (`6654af2`) : reçoit l'escalade D2.
   SC7 à deux branches (égalité, ou 0 mesuré + constat ouvert). G3
   intouché. Réparation de provenance = brief ultérieur (non-objectif 18).
   Horodatages `Authored` d'origine conservés.
5. **Générateur itération 2** (`61b387b`) : hex retiré ; script
   `check_provenance_coastline_019.py` (codes 0/1/2, aucune valeur
   imprimée) ; waiver aligné. Artefacts G4 non régénérés.
6. **Évaluateur passe 2** : **PASS** (`1c5cd46`). Porte ACCEPT dix sur
   dix. SC7 par la **branche escalade**, jamais par égalité. SC10 : zéro
   chaîne hexadécimale dans les livrables. Les trois rôles n'ont ni
   committé, ni poussé, ni créé de branche.

**Branche / PR** : `forge/019-geo-adjacence-g4-d07d`, **PR #105**. Ne
pas fusionner soi-même. Fusion **sans squash**. E1 n'est **pas** clos.

**Réserves (verdict 019, non bloquantes)** : semis saturé sur
`SEA_ZONE_COUNT_MAX` (fenêtre ~5,1 millions de km² vs calibration
d'intention) ; journal d'adjacence porteur d'une durée d'horloge ;
manifeste qui ne décrit le fichier de divergence qu'indirectement ;
cas rouge de `Q4` trop grossier ; `MANIFEST_g4.json` propage l'empreinte
périmée que G3 déclare.

**Suites (pas ce lot)** : brief de réparation de la provenance G3
(non-objectif 18) ; G5 fleuves / G6 relief ; recalibrage éventuel des
bornes de semis ; N1 du 017 ; briefs de harnais ; réparation PR #100.

**Validation rejouée** :
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/019-geo-adjacence-g4` → ACCEPT (dix sur dix).
- `.venv/bin/python -m pytest harness/tests/ -q` → 348 passed, 16 skipped (Unity/Linux, attendus).

**Prochain pas** : le propriétaire fusionne **#105** (lot 019 + cette
correction de feuille de route), **sans squash**. Ensuite : provenance
G3, ou G5/G6, ou brief de harnais — pas d'audit Cursor à attendre sur
#105 (ADR-0012 : audit à la clôture d'étape, E1 n'est pas close).

---
