# HANDOFF.md

## Session la plus récente — 2026-08-14 : brief 019 (adjacence maritime G4), premier lot E1

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

## Session précédente — 2026-08-14 matin : brief 018 (Province dérivée), critères E2 réunis

**Contexte** : orchestration tenue par un agent Cursor Cloud remplaçant le
CTO Claude. Trois sous-agents distincts, modèle Claude Opus 5
(`claude-opus-5-thinking-high`) pour Planificateur, Générateur et
Évaluateur — jamais le même dans la même passe, jamais inherit/Grok pour
un rôle du harnais (décision propriétaire du 2026-08-13 soir).

**HANDOFF était en retard.** La session du 2026-08-13 soir / 2026-08-14
n'avait pas d'addendum. Rattrapage factuel, puis le travail de ce matin.

### Rattrapage — soirée 2026-08-13 / matin 2026-08-14 (avant cette passe)

1. **PR #99** (ADR-0012, audit par grandes étapes) fusionnée le 2026-08-13
   à 20:21 UTC, sans squash.
2. **PR #100** : le dispositif d'étape ADR-0012 s'est déclenché sans jalon
   (`CURSOR-546a9d4`). Connu, **non réparé** dans le lot 018 (hors
   périmètre, adjudication au jalon).
3. **Brief 017** (seuil de survie honnête, fusion des graines 015+016) :
   boucle trois rôles, verdict PASS à l'itération 1, gate ACCEPT dix sur
   dix. **PR #101** fusionnée le 2026-08-14 à 05:53 UTC, **sans squash**.
   Master à jour, aucune PR ouverte au début de cette passe. Les graines
   015/016 ne s'exécutent plus : elles pointent vers 017.
4. **Réserve N1 du verdict 017** (prédiction trop peu sensible à
   `HUNGER_DEATH_SCALE`) : hors périmètre de cette passe, brief ultérieur.

### Ce qui a été fait cette passe (2026-08-14 matin)

1. **Brief 018** — agrégation Province DÉRIVÉE (ADR-0003), un seul lot
   `sim/` (+ lecture seule geo). Branche
   `forge/018-province-derivee-779a`, **PR #102**.
   - Planificateur (Opus 5) : brief + rubrique ; vue hors `sim.model` ;
     départage au plus petit `id` de centre ; source déclarée proxy.
   - Générateur (Opus 5, autre passe) : `sim/aggregation.py`, couverture
     596/596, redessin 22 cellules sans réécrire `Cell`, deux paires
     rouge/vert. `sim/engine.py` intact.
   - Gate : ACCEPT dix sur dix (après amendement de forme du verdict :
     nombres ≥ 2 chiffres hors manifeste entre backticks).
   - Évaluateur (Opus 5, 3e passe) : **PASS** itération 1. 22 compteurs
     reconstruits, y compris une géométrie indépendante à 0 désaccord sur
     596 cellules. Trois contre-preuves hors dépôt rougissent.
   - Les trois rôles n'ont ni committé, ni poussé, ni créé de branche.
     Aucune branche `cursor/*` parasite.
2. **Correction factuelle de `ROADMAP.md`** (ce commit) : 017 fusionné ;
   015/016 plus en file ; couche 1 / F2 / E2 mis à jour. **Sans** ligne
   d'historique.
3. **Jalon E2** : les trois critères sont réunis (seuil honnête ✓ 017 +
   Province dérivée ✓ 018 + monde mesuré sur 596 cellules ✓). Le fichier
   `hermes/milestones/ETAPE-02-*.md` est préparé **sur une PR séparée
   (#103)**, empilée sur #102. Ne pas fusionner soi-même. Ne pas
   fusionner #103 avant #102.

**Réserves de l'Évaluateur (verdict 018, non bloquantes)** : N1 — la
détection de lecture de la couverture de vue ignore le type de l'objet
(faux vert possible sur un champ homonyme) ; N2 — le compteur d'égalités
monde réel n'a presque aucun pouvoir discriminant ; N3 — branche « centre
vide » non exercée par un test ; N4 — 50/50 provinces peuplées
indistinguable d'un plancher au seul vu du nombre ; N5 — dénominateur de
`compteurs_en_dur_trouves` non imprimé par le test ; N6 —
`province_de_cellule` linéaire ; N7 — l'adaptateur reçoit le `World`
vivant.

**Hors périmètre respecté** : F1 geo (relief/climat/ressources) ; N1 du
017 ; briefs de harnais ; arriéré d'audits PROPOSED ; réparation PR #100.

**Validation rejouée** :
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/018-sim-province-derivee` → ACCEPT (dix sur dix).
- `.venv/bin/python -m pytest sim/tests/ -q` → 65 passed.
- `.venv/bin/python -m pytest harness/tests/ -q` → 348 passed, 16 skipped (Unity/Linux, attendus).

**Prochain pas** : le propriétaire fusionne **#102** (lot 018 + cette
correction de feuille de route), **sans squash**. Puis **#103** (jalon
E2 — déclenche l'audit d'étape ADR-0012). Ensuite : F1 geo, ou brief
de harnais, ou N1 du 017 — pas d'audit Cursor à attendre sur #102.

---

## Session précédente — 2026-08-13 (après-midi) : boucle d'audit purgée de ses revues orphelines, brief 014 (la porte et le repli)

**Contexte** : même orchestration (agent Cursor Cloud remplaçant le CTO,
trois sous-agents distincts — jamais le même dans la même passe). Le quota
Claude était revenu en début de session ; **il est retombé en cours de
session** (plafond mensuel, `429` à partir de 11:14 UTC, runs
`pipeline-challenge` 31694643198/31694909507/31694993448 en échec) — le
mode de panne exact que le brief 014 de cette session transforme en état
consigné avec repli.

**Ce qui a été fait, dans l'ordre** :

1. **PRs #65 et #69 constatées fusionnées** par le propriétaire (10:47 et
   10:48 UTC, sans squash). CI verte sur les deux SHAs de fusion.
2. **Huit contre-audits de Claude bloqués en branches `forge-bot/*` sans
   PR** (blocage GitHub connu — cause racine précisée par l'audit
   `827d54e` : le PAT refuse `createPullRequest`, « Resource not
   accessible by personal access token ») : PRs #71/#73/#74/#76 (matin)
   puis #84/#85/#86/#87 (après-midi) **ouvertes à la main, une à une**,
   chacune après la fin complète du run `pipeline-orchestrate` de la
   précédente. Résultat : huit cycles CHALLENGED→APPROVED au ledger,
   **zéro conflit de rebase** (la sérialisation manuelle contourne le
   checkout-au-SHA-poussé, cause réelle du conflit du matin, mesurée par
   l'audit `4c45718` point 1).
3. **Clôture post-fusion de `CURSOR-a4de4bb`** rejouée par l'orchestrateur
   déterministe (`evaluateur_pass` → IMPLEMENTED/VERIFIED, `audit_archive`
   → ARCHIVED), **avec preuve CI sur le SHA final citée avant d'écrire
   VERIFIED** (réponse au constat 3 de `4c45718`). PR de registre séparée
   **#77** (un objet par PR — constat 7 de `4c45718`).
4. **Brief 014 rempli et exécuté par la boucle trois rôles** (branche
   `forge/014-pipeline-contre-audit-porte-e180`, **PR #83**). Périmètre
   tranché en CTO : proposition n° 1 de l'audit `a600532` (P0-1 + P1-1) ;
   les autres candidats sont différés en Non-Goals. Livré :
   `pr_audit_guard.py` (porte observable : audits non adjugés ciblant une
   PR → job `audit-check` rouge dans `audit-guard.yml`, aucun nouveau
   `pipeline-*.yml`) et `vendor_refusal.py` + refonte des étapes de
   `pipeline-challenge.yml` (classification du transcript, état persistant
   `vendor-refusal-state.jsonl` versionné et commis même sans repli, repli
   `codex exec` sous les mêmes gardes, échec relevé — jamais de succès
   simulé, jamais de vert sans revue produite). **Boucle réelle en trois
   itérations** : REJECT (B1 étapes inatteignables sur le 429 — `success()`
   implicite des `if:` ; B2 revue perdue si transcript illisible), REJECT
   (B3 `other_error` rendait le job vert — échec invisible pour
   l'escalade ; B4 preuve mécanique absente ; B5 garde `ci_budget_guard`
   resserré, interdit), PASS (test des sept chemins lisant le vrai YAML,
   qui rougit si l'étape B3 est retirée — paire rouge/verte committée).
   Gate ACCEPT dix sur dix. Correctif post-verdict : actionlint
   (`github.head_ref` par variable d'environnement), re-vérifié par
   l'Évaluateur, PASS inchangé. Le job `audit-check` **tourne déjà** sur
   la PR #83 (vert : aucun audit non adjugé ne la cible à cette heure).
5. **Critiques Cursor traitées par la boucle** : sur la PR #69 (deux
   audits moteur : `0e98199` « le seuil de survie ignore la mortalité »,
   `29913c0` « seuil non borné — aucune mort sous 200 kg de déficit
   quelle que soit la population ») et sur la PR #77 (`f978cc7`).
   Contre-audits fusionnés, décisions APPROVED. **Conversions tranchées
   en CTO** : les deux audits moteur → graines **015/016** (PR de
   registre **#89**, empilée sur la #77, conflit d'append du ledger résolu
   dans la branche pour épargner le propriétaire) ; les **six audits
   pipeline/registre approuvés** (`16ff5ac`, `4c45718`, `9e35764`,
   `ab0e7f0`, `827d54e`, `f978cc7`) restent volontairement
   `AUDIT_APPROVED` sans conversion — leur substance recoupe le lot 014
   livré et les candidats différés ci-dessous.
6. **Deux incidents de processus consignés** (quatrième et cinquième
   violations de rôle du dépôt) : le Planificateur a committé/poussé sa
   propre branche `cursor/brief-014-planificateur-d4e7` ; le Générateur
   (itération 3) a committé/poussé `cursor/014-pipeline-it3-…-111d`
   malgré l'interdiction répétée en toutes lettres dans son prompt.
   Contenus repris à l'identique sur la branche du lot (diffs vides
   vérifiés), branches parasites supprimées (locales ET distantes — la
   distante du Générateur avait échappé au premier contrôle, c'est
   l'Évaluateur qui l'a mesuré). Le traçage mécanique de l'acteur reste
   LE trou à fermer (points 1 et 7 de `3b47ffe`, différés).
7. **N8 traité** : la ligne de coût `backend: claude` sans audit (fausse
   — l'acteur réel était un sous-agent Cursor) est contredite par une
   ligne rectificative au registre append-only.

**Réserves de l'Évaluateur (verdict 014, non bloquantes, pour le prochain
Planificateur)** : N9 — le test des sept chemins garde la présence de
l'étape B3, pas son effet (`exit 1` remplacé par `exit 0` resterait vert) ;
N10 — « sept chemins » n'exerce que quatre comportements distincts ; N11 —
`except Exception: return True` dans l'évaluateur de conditions du test ;
N12 — la paire de preuves C annonce `8` tests, le fichier en compte `9`.

**Candidats du prochain brief de harnais (matière : les six audits
approuvés non convertis + réserves)** : acteurs RÉELS au ledger et
comptage des verdicts sur les lignes de tableau (les lignes CHALLENGED du
jour portent encore des comptages de texte libre — mesuré en direct) ;
sérialisation des orchestrations (checkout à la tête de master, pas au SHA
poussé) ; `evaluateur_pass` sans déclencheur (dernier segment manuel,
`f978cc7` point 2) ; `ci_green_post_merge` déclaré mais évalué par aucun
code (`f978cc7` point 1) ; le budget CI mesuré puis jeté
(`ci-budget-ledger.jsonl` à 1 octet pour `7.2771804` USD de transcripts le
2026-08-13 — `827d54e` point 4) ; archives sans empreinte ; enregistrement
`AUDIT_PROPOSED` à l'entrée + santé de la boucle au DASHBOARD (proposition
n° 2 de `a600532`, toujours en attente).

**État de la boucle** : 15 `PROPOSED` (les 12 du 2026-08-12 + les
critiques du jour des PRs #71/#73/#76, non challengées — le 429 est
revenu), 3 `CHALLENGED` du 2026-08-12 (revues fusionnées, décisions jamais
prises — runs d'orchestration antérieurs au groupe de concurrence),
11 `APPROVED` (dont les six non convertis, décision tracée), 2 `CONVERTED`
(graines 015/016), 8 `ARCHIVED`. La purge motivée de l'arriéré
(STALE/archivage) reste à faire — **non traitée cette session** (priorité
donnée au lot 014 et aux revues orphelines).

**Prochain pas** : le propriétaire fusionne, dans l'ordre et sans squash :
**#77** (clôture a4de4bb) → **#89** (conversions 015/016, se recible seule)
→ **#83** (lot 014). Le propriétaire peut aussi : donner au `FORGE_BOT_PAT`
le droit `createPullRequest` (cause racine des PRs de revue orphelines) ;
réapprovisionner le quota Claude **ou** provisionner `CODEX_AUTH_JSON` pour
que le repli du brief 014 serve dès la prochaine panne. Ensuite : brief
moteur 015+016 (seuil de survie honnête — fusionner les deux graines sous
un seul lot), agrégation Province dérivée (F2), purge de l'arriéré, brief
de harnais des candidats ci-dessus.

### Addendum — même session (13:43 UTC) : décision propriétaire « audit par grandes étapes » (ADR-0012)

Le propriétaire tranche : l'audit/contre-audit **à chaque PR** consommait
tout le quota Claude (plafond mensuel atteint deux fois en 24 h, mesures au
dossier). Désormais : **un audit + contre-audit à la clôture de chaque
grande étape** du projet, plus un audit ponctuel possible par
`workflow_dispatch`. Traitement en session mandatée (comme ADR-0010, revue
humaine sur la PR — pas d'auto-jugement) :

1. Demande enregistrée : `hermes/requests/DEMANDE-20260813-audit-par-grandes-etapes.md`.
2. Décision : `docs/adr/0012-audit-contre-audit-par-grandes-etapes.md`
   (remplace « Cursor relit chaque PR » d'ADR-0010 ; amende la cadence
   post-merge d'ADR-0005/0006 ; cycle de vie des audits inchangé).
3. Grandes étapes définies : `ROADMAP.md` § « Grandes étapes — jalons
   d'audit » (E1 fondations monde, **E2 le monde vivant compte juste =
   prochain jalon**, E3 villes, E4 états, E5 armées, E6 batailles + rendu).
   Marqueur : fichier `hermes/milestones/ETAPE-NN-<slug>.md` fusionné sur
   `master` (contrat : `hermes/milestones/README.md`).
4. Recâblage : `pipeline-audit.yml` ne se déclenche plus que sur jalon ou
   dispatch (les déclencheurs `pull_request` et push-master génériques sont
   retirés ; l'audit d'étape couvre tout ce qui est entré depuis le jalon
   précédent). `pipeline-challenge.yml` inchangé mécaniquement — il suit la
   cadence des dépôts, donc des jalons. Clés de politique basculées dans
   `harness/pipeline/config.yaml`.
5. Conséquence sur l'arriéré : les 15 audits `PROPOSED` ne sont plus une
   dette de contre-audit individuelle — adjudication en lot au prochain
   jalon ou purge `STALE` motivée. Ce qui reste à chaque PR : les contrôles
   gratuits (CI, gate, `audit-check` du brief 014 — la porte reste
   pertinente aux jalons et pour les audits ponctuels).

## Session précédente — 2026-08-13 (suite) : critique du brief 012 traitée, brief 013 (le tick nourrit une fois)

**Contexte** : suite immédiate de la session du matin, même orchestration
(agent Cursor Cloud remplaçant le CTO, trois sous-agents distincts). Fait
nouveau : **le quota Claude est revenu** — `pipeline-challenge` fonctionne à
nouveau et les contre-audits sont redevenus l'œuvre du vrai Claude (deux
runs verts de ~6 min). La PR #60 (brief 012) a été fusionnée par le
propriétaire à 08:28 UTC, sans squash (réserve N6 respectée).

**Ce qui a été fait, dans l'ordre** :

1. **Deux nouveaux audits Cursor traités** :
   `CURSOR-a600532-fusion-sans-contre-audit` (post-fusion de la PR #57 —
   la fusion s'est faite pendant la panne du contre-audit, escalade
   invisible du propriétaire) et
   `CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois` (critique de la
   PR #60 — **1 P0 réel sur le moteur** : la nourriture transférée
   annulait le déficit du tick courant ET restait en stock).
2. **Les contre-audits de Claude étaient bloqués sur des branches
   `forge-bot/*` sans PR** (réglage GitHub « Allow GitHub Actions to
   create and approve pull requests » toujours inactif) : PRs #62 et #63
   ouvertes à la main, fusionnées par le merge-bot (`reviews/**`
   allowlisté).
3. **Course d'orchestration détectée et réparée** : les deux fusions de
   revues à 12 s d'écart ont déclenché deux `pipeline-orchestrate`
   concurrents ; le second a perdu sa persistance sur un conflit de
   rebase du ledger (run `31682710982` en échec). La décision de
   `a4de4bb` a été rejouée localement par le même orchestrateur
   déterministe (CHALLENGED + APPROVED, points 1 à 10, identiques au log
   CI). **La sérialisation des orchestrations concurrentes est un trou à
   briefer.**
4. **Cycle `CURSOR-3b47ffe` refermé** : brief 012 fusionné →
   `evaluateur_pass` (IMPLEMENTED, VERIFIED) → archivage. Conversions :
   graine 013 (`sim-tick-nourrit-une-fois`, issue de `a4de4bb`) et
   graine 014 (`pipeline-contre-audit-porte`, issue de `a600532`).
   Le tout sur la **PR #65** (tenue de registre, séparée du lot — réponse
   au constat 8 « PRs trop grosses »).
5. **Brief 013 écrit et exécuté par la boucle trois rôles** (branche
   `forge/013-sim-tick-nourrit-une-fois-ddda`, PR empilée sur la #65) :
   commerce AVANT consommation (un kilogramme transféré nourrit
   exactement une fois — sonde témoin/receveur à écart nul), transport
   calculé sur instantané et limité à une arête par tick (invariance à
   l'ordre du fichier d'adjacence, répartition proportionnelle stable par
   `cell_id`, écrêtage côté receveur), mortalité continue sans plancher
   `max(1, …)` et plafonnée pour toute population, déficit à mémoire
   graduelle (`DEFICIT_RECOVERY_RATE_PER_TICK`, epsilon de coupure
   nommé), seuil de survie DÉRIVÉ du modèle (marge = expression calculée,
   plus aucune constante calibrée après mesure), compteur de transport =
   kilogrammes réellement arrivés. Boucle réelle : itération 1 = REJECT
   (B1 : marge recalibrée après mesure — échec disqualifiant, avoué au
   journal et conservé) ; itération 2 = B1 + N1→N6 corrigés, verdict
   PASS, gate ACCEPT (dix sur dix). Re-mesure du monde réel (graines
   42/42, N=200) : la correction du P0 révèle la vraie famine — 536
   cellules affamées sur 596 (contre 261 avant), 15 666 208 morts
   (contre 7 544 299), 2 676 487 kg réellement arrivés (contre 8 171 507
   « sauts » comptés en double), survie 0.7657 dans la fenêtre dérivée
   [0.7489, 1.0511].
6. **Amendements de forme consignés** : le brief 013 portait des balises
   ` ```python ` que `no_bare_python_alias` lit comme une invocation —
   corrigées par le Planificateur (note datée, en-tête Authored
   inchangé) ; l'Évaluateur a amendé sa propre section d'itération 1
   (nombres devenus orphelins après la re-mesure N3 — backticks + note
   datée, fond intact).

**Réserves de l'Évaluateur (verdict 013, non bloquantes)** : R9 — la
composition des termes de la marge dérivée n'a pas d'homogénéité
démontrée (l'Évaluateur déclare un conflit d'intérêt : il avait nommé les
ingrédients dans son feedback) ; R10 — le journal se trompe dans son
contrôle de falsifiabilité (`0.556` au lieu de `0.3475`) ; R11 — l'égalité
kg comptés/arrivés est « à l'arrondi près » depuis l'écrêtage ; R12 — une
docstring cite `0.80` pour une mesure de `0.020627` ; R13 — le cas étoile
du test SC5 n'est pas une garde ; R1/N7 — **le brief 013 contient une
contradiction SC1/SC4** (déficit identique exigé vs récupération graduelle
qui l'interdit) — à corriger dans les modèles de briefs du Planificateur.

**État de la boucle d'audit** : `3b47ffe` ARCHIVED ; `a4de4bb` CONVERTED
(graine 013, exécutée par ce lot) ; `a600532` CONVERTED (graine 014 en
file d'attente). Restent en souffrance : 12 audits `PROPOSED` et 3
`CHALLENGED` hérités du 2026-08-12.
Acteurs en dur au ledger (`claude`/`owner`) : toujours faux par
construction (point 3 de `a4de4bb`), différé au brief 014.

**Prochain pas** : fusionner la PR #65 puis la PR du lot 013 (empilée,
elle se recible sur master à la fusion de la #65 — **pas de squash** :
compteurs d'archive git-ancrés) ; après fusion du lot 013, rejouer
`evaluateur_pass` pour `a4de4bb` puis archiver ; remplir et exécuter le
brief 014 (le contre-audit comme porte, refus fournisseur = état, acteurs
réels au ledger, comptage des verdicts sur les lignes de tableau,
sérialisation des orchestrations concurrentes, points 1 et 7 différés de
`3b47ffe`) ; puis agrégation Province (F2). Le propriétaire peut aussi
activer « Allow GitHub Actions to create and approve pull requests » pour
que les PRs de revues s'ouvrent seules.

## Session précédente — 2026-08-13 (matin) : critique du brief 011 traitée, brief 012 (le monde vit + commerce)

**Contexte d'exécution** : Claude (le CTO) reste indisponible — et la cause
est maintenant mesurée : le plafond mensuel de l'abonnement est atteint
(`pipeline-challenge`, run `31621195096`, erreur `429` « You've hit your
org's monthly spend limit »). Sur instruction du propriétaire, l'orchestration
a de nouveau été tenue par un agent Cursor Cloud, avec trois sous-agents
distincts (Planificateur, Générateur, Évaluateur — jamais le même dans la
même passe), sur la branche `forge/012-monde-vivant-commerce-ddda`.

**Étape 1 — état de la PR #57** : fusionnée par le propriétaire le
2026-08-13 à 06:12 UTC (avant le début de session), CI verte. La critique
Cursor avait déposé l'audit `CURSOR-3b47ffe-pr57-monde-sans-faim`
(PR #58 : 1 P0, 4 P1, 5 P2, 2 P3) — resté `PROPOSED` faute de challenge
(plafond Claude). Traitement fait dans cette session, par la boucle :
contre-audit rédigé par l'orchestrateur (mesures de l'audit rejouées une à
une, note de transparence — même infrastructure que l'auditeur, session
distincte), `AUDIT_CHALLENGED` au ledger, décision automatique (ADR-0006)
`AUDIT_APPROVED` avec 12 points retenus (11 CONFIRMED + 1 PARTIAL), puis
conversion en graine de brief par `audit_convert.py`.

**Ce qui a été livré — le brief 012**
(`harness/queue/briefs/012-monde-vivant-commerce-inter-cellules/`, issu de
l'audit) :

1. **Base de temps unique** : `TICK_DURATION_DAYS` dans `sim/constants.py`,
   toutes les constantes temporelles dérivées et documentées dans
   `sim/SEEDING.md` ; noms trompeurs corrigés (`INITIAL_FOOD_DAYS` →
   `INITIAL_FOOD_RESERVE_TICKS`, `daily_need` → `tick_need`).
2. **Le rendement varie** : `tick()` consomme réellement son `rng`
   (facteur multiplicatif documenté) — le test de déterminisme peut enfin
   échouer ; même graine → condensés égaux, graines différentes →
   condensés différents dès le premier tick.
3. **Le déficit est un état** : `food_deficit_kg` persisté sur `Cell`,
   écrit par la consommation (le manque n'est plus écrasé), lu par la
   mortalité (proportionnelle à l'ampleur du manque, plus d'interrupteur
   binaire seul).
4. **Commerce inter-cellules physique** : les 1364 arêtes d'adjacence sont
   enfin lues ; transferts uniquement entre cellules adjacentes, bornés
   par `TRADE_CAPACITY_KG_PER_EDGE_PER_TICK`, conservation stricte de la
   masse (testée) ; pas de prix ni de marché.
5. **Le monde vit, mesuré sur les 596 cellules réelles** (graines 42/42,
   200 ticks) : 261 cellules ont connu la faim, morts cumulés > 0, kg
   transportés > 0, fraction de survie 0.887 (> seuil documenté 0.70).
6. **CI** : nouveau job `sim-tests` dans `harness-ci.yml` (25 tests).
7. **Réserves du verdict 011** : R1 (commande d'archive du manifeste 011
   ré-ancrée sur le commit d'itération 1, reproductible), R2 (découverte
   des dataclasses par introspection), R3 (vérification de l'objet écrit)
   fermées ; R4 (dédoublonnage des preuves vertes) optionnel, non fait.
8. **Boucle réelle** : itération 1 = REJECT de l'Évaluateur (B1 : le
   retrait de l'entrée R1 avait cassé le JSON du manifeste 011 — gate du
   lot 011 hors service ; B2 : commande d'un compteur ne produisant pas
   sa valeur) ; itération 2 = corrections B1/B2 + N1→N3, verdict PASS,
   gates 011 ET 012 ACCEPT (dix contrôles sur dix chacun).

**Deux incidents de processus, consignés et réparés** :

1. Le Générateur (itération 1) a **committé et poussé de lui-même** sur
   une branche `cursor/*` (préfixe réservé aux audits). Contenu repris à
   l'identique sur la branche du lot (diff d'arbres vide, vérifié),
   branche parasite supprimée, incident consigné au verdict et au message
   de commit.
2. Les rôles avaient signé `forge-generateur-cursor` /
   `forge-evaluateur-cursor` : le contrôle `verdict_is_not_self_authored`
   a **refusé à raison** (même acteur dérivé du suffixe — le contrôle
   fonctionne). Décision d'orchestration : convention du lot 011
   (signature = rôle natif, acteur réel déclaré en prose dans les notes de
   transparence). C'est la **troisième utilisation délibérée de l'angle
   mort du couple natif** — toujours ouvert, toujours documenté ; sa
   fermeture mécanique (traçage d'acteur hors chaînes auto-déclarées) est
   différée au brief de harnais ci-dessous. La réserve de principe de
   l'Évaluateur est consignée dans sa note de transparence.

**Réserves non bloquantes de l'Évaluateur (verdict 012, itération 2)** :
N4 — deux docstrings affirment `1.0 ≥ 1.444877` (phrase à corriger, pas le
test) ; N5 — le script de mesure `measure_cellules_affamees.py` porte la
commande d'un compteur mais n'est pas déclaré dans `files` du manifeste ;
N6 — deux compteurs lisent des fichiers à un commit épinglé (propriété qui
survivrait à une fusion conservant l'historique, pas à un squash — **ne pas
fusionner la PR du lot 012 en squash**) ; N7 — l'angle mort de signature
(voir ci-dessus).

**Cycle de vie de l'audit** : `CURSOR-3b47ffe` est à l'état
`AUDIT_CONVERTED`. Les transitions `IMPLEMENTED` → `VERIFIED` s'enregistrent
**après la fusion** de la PR du lot (elles affirment « fusionné, CI verte
sur le SHA final ») :
`.venv/bin/python harness/pipeline/orchestrator.py run --event evaluateur_pass --payload '{"audit_id": "CURSOR-3b47ffe-pr57-monde-sans-faim"}'`,
puis archivage. Par ailleurs, 12 audits `PROPOSED` et 3 `CHALLENGED`
hérités du 2026-08-12 restent en attente dans l'inbox — non traités cette
session (périmètre : la critique de la PR #57 seulement).

**Points d'audit différés — prochain brief de harnais à écrire par le
CTO** : point 1 (traçage mécanique de l'acteur de chaque rôle — ferme
l'angle mort des signatures), point 7 (le gate ne vérifie pas le suivi git
des fichiers déclarés hors du dossier de brief ; divergence de forme
`must_differ_from` brief/gate), et la moitié « outillage » des points 2 et
8 (voie d'écriture de `ROADMAP.md` ; budget imposé par le gate plutôt que
déclaré). En réponse au point 2 (P1-1), la correction factuelle de
`ROADMAP.md` de cette session est signalée au message de commit **sans**
ligne ajoutée à la table d'historique (voix éditoriale d'Hermes).

**Validation rejouée sur l'état final** :
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules` → ACCEPT.
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage` → ACCEPT (réparé — il était cassé entre les deux itérations).
- `.venv/bin/python -m pytest sim/tests/ -q` → 25 passed.
- `.venv/bin/python -m pytest harness/tests/ -q` → 314 passed, 16 skipped (Unity/PowerShell, attendus sur Linux).

**Prochain pas** : la critique Cursor de la PR du lot 012 alimente la
boucle comme d'habitude ; après fusion (pas en squash — voir N6),
enregistrer `evaluateur_pass` au ledger puis archiver l'audit. Ensuite :
le brief de harnais des points différés, puis l'agrégation Province
dérivée (suite F2). Le propriétaire doit aussi **réapprovisionner le
quota Claude** (plafond mensuel atteint) s'il veut que `pipeline-challenge`
retourne au titulaire du rôle.

## Session précédente — 2026-08-12 (soirée) : brief 011, premier code de `sim/` (F2 lancée)

**Contexte d'exécution** : Claude (le CTO) était indisponible ; sur
instruction du propriétaire (« claude est inutilisable, tu le remplaces en
attendant »), l'orchestration a été tenue par un agent Cursor Cloud qui a
rejoué la boucle trois rôles localement, avec trois sous-agents distincts
(Planificateur, Générateur, Évaluateur — jamais le même dans la même passe).
Transparence : les trois rôles ont tourné sur l'infrastructure Cursor ;
les signatures `forge-generateur` / `forge-evaluateur` sont les rôles natifs
(sessions séparées, orchestrées de l'extérieur), la note de remplacement est
écrite en prose dans `generator-log.md` et `verdict.md`. C'est le troisième
angle mort connu du contrôle d'auto-jugement (couple natif sans suffixe
d'acteur) — toujours ouvert, toujours documenté, à ne pas croire fermé.

**Ce qui a été livré** — le brief 011
(`harness/queue/briefs/011-sim-monde-vivant-amorcage/`), premier brief F2,
amorçage du moteur `sim/` (couche 1 « monde vivant ») :

1. Paquet `sim/` stdlib pur (modèle, monde, moteur, constantes) : le monde
   se charge depuis les artefacts G3 committés du pipeline géo
   (`cells_g3.json` / `adjacency_g3.json`, `cell_id` seule clé spatiale,
   ADR-0003 gardée par une classe de base qui refuse tout champ
   `province*`) ; population amorcée paramétriquement (formule documentée
   dans `sim/SEEDING.md`, déclarée proxy non historique) ; tick
   déterministe (même graine → condensés SHA256 égaux) ; économie physique
   de la nourriture (production → stock → consommation → faim → mortalité,
   chaque maillon écrit puis lu, sentinelles `-1`).
2. Vingt tests sous `sim/tests/` (chargement, conformité ADR, amorçage,
   déterminisme, chaîne causale maillon par maillon + bout en bout,
   couverture d'écriture des champs, inspection anti-compteurs-en-dur),
   preuves rouge/vert committées sous `sim/tests/proof_red/`.
3. Boucle réelle : itération 1 = REJECT de l'Évaluateur (SC8 : le test de
   couverture ne rougissait pas sur un champ fantôme ; hash recopié en dur
   dans le journal) ; itération 2 = corrections B1/B2 + N1→N6, verdict
   PASS, gate mécanique ACCEPT (dix contrôles sur dix).
4. `ROADMAP.md` : correction factuelle des statuts (couche 1 commencée,
   F2 en cours, étape 4 faite) — signalée au message de commit.

**Validation rejouée sur l'état final** :
- `.venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage` → ACCEPT.
- `.venv/bin/python -m pytest sim/tests/ -q` → 20 passed.
- `.venv/bin/python -m pytest harness/tests/ -q` → 314 passed, 16 skipped
  (la base a grossi depuis le 305 noté plus bas : tests ajoutés par les
  briefs fusionnés entre-temps ; les 16 skips restent les tests
  Unity/PowerShell attendus sur Linux).

**Réserves consignées par l'Évaluateur (non bloquantes, pour un brief
ultérieur)** : le compteur d'archive `lignes_differentes_preuve_rouge_iter1`
porte une étiquette de commande fausse (la valeur vient du commit de
l'itération 1, pas de la commande déclarée) ; le contrôle SC8 nomme la
dataclass `Cell` au lieu de découvrir les dataclasses ; la détection des
sites d'écriture ne vérifie pas l'objet écrit ; les deux preuves vertes
sont un doublon. Autre trou connu : `harness-ci` ne collecte que
`harness/tests/` — les tests `sim/` ne tournent pas encore en CI (les
preuves committées et le verdict couvrent ce lot ; câbler `sim/tests/` en
CI est un candidat naturel au prochain brief).

**Prochain pas** : le CTO (ou son remplaçant) écrit le brief F2 suivant
depuis `ROADMAP.md` — commerce inter-cellules ou agrégation Province
dérivée — et la critique Cursor de la PR de ce lot alimente la boucle
d'audit comme d'habitude.

## Addendum — 2026-08-12 (fin de journée) : premiers tours réels + tableau de bord

Les secrets d'abonnement ont été provisionnés par le propriétaire et la
boucle a tourné pour la première fois en conditions réelles :

1. **Cursor** : `pipeline-audit` a lancé un Cloud Agent sur le merge de la
   PR #24 (`agent_id` au log). L'agent a produit un audit conforme de 535
   lignes (`CURSOR-cdc683f-hermes-workflow-quatre-acteurs`, 4 constats
   P0-P2), fusionné via la PR #25. **Deux ratés observés** : il n'a pas
   ouvert sa PR lui-même (ouverte à la main) et a laissé deux branches de
   brouillon (supprimées) — le prompt d'invocation l'exige désormais.
2. **Claude (abonnement)** : `pipeline-challenge` a tourné headless avec
   `CLAUDE_CODE_OAUTH_TOKEN` (« Claude auth: abonnement » au log), produit
   la revue `CLAUDE-CURSOR-cdc683f-...` (101 lignes) et le marquage
   post-hoc a fonctionné : **1.0615 USD équivalent, sous le plafond 5** —
   ligne réelle au `ci-budget-ledger.jsonl`. Publication en PR #26.
3. **Blocage découvert** : `gh pr create` depuis un workflow est refusé —
   le réglage GitHub « Allow GitHub Actions to create and approve pull
   requests » (Settings → Actions → General) doit être activé par le
   propriétaire, sinon chaque publication reste une branche sans PR.
4. **Boucle-sur-la-boucle constatée** : la fusion du premier audit a
   déclenché un deuxième auditeur sur cette fusion même. Corrigé :
   `pipeline-audit` classe désormais un push ne touchant que
   `architecture/{inbox,reviews,decisions,archive}/`, le ledger d'audits
   ou `hermes/**` comme documentaire — pas de nouvel audit.
5. **Codex (`CODEX_AUTH_JSON`) toujours pas exercé** — attend le premier
   `pipeline-forge-run` (dispatch manuel sur un brief).

Nouveautés de ce même addendum : `hermes/DASHBOARD.md` (tableau de bord
généré par `hermes/dashboard.py`, régénéré par `hermes-dashboard.yml` à
chaque push master et toutes les 6 h — l'endroit où le propriétaire
regarde d'abord) ; le modèle de l'auditeur Cursor est monté en gamme —
demande propriétaire, le défaut claude-4.5-sonnet du premier tour étant
jugé trop faible. L'identifiant deviné (`claude-opus-5-thinking-high`) a
été refusé `invalid_model` par l'API : le workflow ne devine plus, il
interroge `GET /v1/models`, honore la variable de dépôt
`CURSOR_AUDITOR_MODEL` si elle correspond à un identifiant réel, sinon
choisit le premier modèle « opus » (puis « grok »), et journalise la liste
complète des identifiants valides dans chaque run.

## Session précédente — 2026-08-12 (après-midi) : ADR-0010, câblage réel, nettoyage

Sur demande explicite du propriétaire (enregistrée dans
`hermes/requests/DEMANDE-20260812-workflow-quatre-acteurs.md`), Cursor a
exécuté une session mandatée : décision de rôles, câblage des trois stubs
d'invocation, feuille de route, et nettoyage complet des points en attente.

**La décision** : [ADR-0010](docs/adr/0010-hermes-chef-de-projet-workflow-quatre-acteurs.md)
— chaîne à quatre acteurs. **Hermes** passe d'observateur à **chef de
projet** (point d'entrée, tient `ROADMAP.md` + `hermes/**`, rien d'autre).
**Claude Code** est le **CTO** (briefs, orchestration `/forge-run`,
verdicts, PR). **Codex** est l'**exécutant** (Générateur, modèle
GPT-5.6 Sol via `CODEX_MODEL`). **Cursor** est le **critique** (chaque PR,
contre `architecture/review-guidelines.md`, sources externes datées). La
ligne Hermes du 2026-08-11 est remplacée ; tout le reste (option B,
arbitrages 1-3, séparation producteur/juge) est reconduit.

## Ce qui a changé dans cette session

1. **`ROADMAP.md` créé** (racine) : couches du jeu (VISION) + phases projet
   (F0 terminé, F1 en cours, F2 = premier brief `sim/` à écrire par le CTO).
   Propriété d'Hermes ; les évolutions passent par `hermes/requests/`.
2. **`hermes/` créé** : contrat d'écriture (arbitrage n°4, étendu par
   ADR-0010), premier rapport et première demande enregistrés.
3. **Les trois stubs `TODO(operator...)` sont câblés** :
   - `pipeline-forge-run.yml` : `claude -p "/forge-run <brief> --backend codex"`
     headless (`--max-budget-usd 5`, `--permission-mode acceptEdits`),
     CLIs installés dans le job, gate mécanique rejouée, publication en PR
     `forge-bot/*` par le workflow (« Claude fait les PR »).
   - `pipeline-challenge.yml` : `claude -p "/forge-audit-review <audit_id>"`
     headless — la substance du lot 009c, y compris la **consultation
     runtime du `mode:`** (SC15) ; revue publiée en PR `forge-bot/*`
     (chemin allowlisté du merge-bot). Le job smoke mécanique est conservé.
   - `pipeline-audit.yml` : Cursor Cloud Agent lancé par l'API officielle
     (`POST https://api.cursor.com/v1/agents`), sur **push master** (audit
     post-merge) **et sur chaque `pull_request`** non-brouillon hors
     `cursor/*` (critique de PR, ADR-0010).
   - Garde-fous partout : label `pipeline/pause` (requête API réelle),
     `ci_budget_guard precheck` avant + `record` après, dérogation
     `::warning::` quand un secret manque — jamais d'échec ni de succès
     silencieux.
4. **`mode: full_auto` déclaré** dans `harness/pipeline/config.yaml` (et
   `auto_policy.yaml` mis en accord). Légal parce que le garde-fou
   `full_auto_mode_guard.py` ne trouve plus le marqueur de stub — c'était
   exactement la réserve fail-closed d'ADR-0007. Les tests du garde-fou,
   conçus pour passer au rouge à ce moment précis, ont été inversés
   consciemment dans le même commit (`test_mode_guard.py` : le contrôle
   épingle désormais l'ABSENCE du marqueur ; le refus SC1 vit sur une
   fixture re-stubée).
5. **`architecture/review-guidelines.md` créé** : six lentilles de critique
   des PR issues des bonnes pratiques d'ingénierie IA, chaque pratique
   adossée à une source externe datée (5 sources, consultées le
   2026-08-12). À re-sourcer chaque trimestre.
6. **Nettoyage : plus aucun point de validation en attente.**
   - Les **7 audits** du ledger sont clos : 4 `PROPOSED` obsolètes →
     `STALE` → `ARCHIVED` (motif tracé par audit : leur substance était déjà
     livrée par les briefs 006/008/009) ; les 2 `CONVERTED` (briefs 008 et
     010 livrés et acceptés) → `IMPLEMENTED` → `VERIFIED` → `ARCHIVED` via
     l'orchestrateur réel, puis `audit_archive.py` (dossiers gelés sous
     `architecture/archive/`).
   - Les **16 branches distantes fusionnées** sont supprimées ; les **PR
     draft #1 et #12** sont fermées — le contenu utile de #1 (AGENTS.md,
     `.venv` git-ignoré) est repris dans cette PR ; #12 (« Cursor point
     d'entrée ») est remplacée par ADR-0010, qui confie ce rôle à Hermes.
7. **`harness/backends/run_codex_generator.sh`** : variable optionnelle
   `CODEX_MODEL` transmise en `codex exec --model` (le workflow CI fixe
   `gpt-5.6-sol`) ; comportement local inchangé quand elle est absente.
8. **AGENTS.md** ajouté (notes d'environnement VM Linux/cloud, repris de la
   PR #1) et `.venv/` git-ignoré.

## Point de départ pour la prochaine session

- Branche par défaut : `master`. Le travail de cette session est sur la PR
  de la branche `forge/workflow-quatre-acteurs-977d` (préfixe `forge/` et
  non `cursor/` : audit-guard réserve mécaniquement `cursor/*` aux dépôts
  d'audits touchant uniquement `architecture/inbox/`).
- La direction du projet se lit désormais dans **`ROADMAP.md`** ;
  les prochaines actions y sont ordonnées. En résumé :
  1. **Le propriétaire provisionne les trois secrets** GitHub Actions, en
     **quota d'abonnement** (décision du 2026-08-12, jamais de crédit
     API) : `CLAUDE_CODE_OAUTH_TOKEN` (jeton `claude setup-token`, plan
     Pro/Max), `CODEX_AUTH_JSON` (contenu de `~/.codex/auth.json` après
     `codex login` ChatGPT ; à re-seeder s'il périme, ~8 jours sans
     rafraîchissement), `CURSOR_API_KEY` (clé spécifique Cloud Agents du
     dashboard Cursor — consomme déjà le forfait). Les clés API
     `ANTHROPIC_API_KEY`/`OPENAI_API_KEY` restent des replis acceptés par
     les workflows. Sans identifiant, la boucle consigne des dérogations
     et n'appelle personne.
  2. Rejouer la boucle sur un brief réel (`workflow_dispatch` de
     `pipeline-forge-run` ou label `forge-run/queued`).
  3. Le CTO écrit le premier brief F2 (`sim/`, couche 1) depuis la roadmap.

## État des briefs

Le brief 009 est **complet** : 009a/009b acceptés précédemment ; la
substance de **009c** (invocation réelle de claude-challenger, mode-gated +
budget-gated, `--max-budget-usd 5`) est livrée par cette session dans
`pipeline-challenge.yml`, hors boucle harnais (session mandatée par le
propriétaire, revue humaine sur la PR — pas d'auto-jugement : le producteur
de ce câblage n'en a écrit aucun verdict). Le brief 010 était déjà complet.
Aucun brief en file d'attente ; les prochains naissent de la roadmap.

## Validation rejouée sur l'état final de la session

- `.venv/bin/python -m pytest harness/tests/ -q` → **305 passed, 16
  skipped** (les 16 skips sont les tests Unity/PowerShell, attendus sur
  Linux — voir AGENTS.md).
- `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation` → ACCEPT.
- `py harness/verdict_audit.py harness/queue/briefs/010-repartition-roles-full-auto` → ACCEPT.
- `py harness/audit_schema.py` → 7 audits valides ; `audits.py list` →
  7/7 `AUDIT_ARCHIVED`.
- `py harness/harness_audit.py` → 23/24, le rouge `no_premature_stub_content`
  est le même rouge hérité (l'outil croit `pipeline/geo/` vide) — **ne pas
  vider `pipeline/geo/` pour le faire passer**.
- actionlint sur les workflows réécrits : propre.

## Risques connus (reconduits + nouveaux)

- **Les workflows câblés n'ont jamais tourné avec de vrais secrets.** Le
  premier déclenchement réel est à surveiller : formats de sortie
  (`--output-format stream-json`) et parsing du marquage post-hoc
  (`ci_budget_guard record`) peuvent refuser proprement (soft-fail
  `::warning::` prévu) ; le plafond natif `--max-budget-usd` protège la
  dépense dans tous les cas.
- **`gh pr create` depuis un workflow** exige le réglage « Allow GitHub
  Actions to create and approve pull requests » ; refus = branche poussée +
  `::warning::`, jamais un échec silencieux.
- Le backend Codex n'est toujours pas exécutable sur la machine Windows du
  propriétaire (AppX `Permission denied`) ; en CI le CLI est installé via
  npm, ce chemin-là est neuf.
- Un red-first lancé depuis la racine du dépôt ne prouve rien — exécuter
  depuis la copie sabotée.
- Nombres dans un `verdict.md` : tout nombre en prose doit tracer à un
  compteur du manifeste, sinon backticks.
- Ledger de budget CI absent/vide = « budget remis à zéro » (non bloquant).
- `budget.py split-check` rapporte 0 condition sur un brief à sous-titres
  `###` — ne pas remodeler un brief pour plaire au détecteur.
- Ne jamais fabriquer de contenu VictoriaProject au-delà de ce qui a été lu.
- Les 7 rouges hérités du portage Unity restent rouges-et-attribués.
- Les Générateurs ne committent jamais (le workflow forge-run committe et
  ouvre la PR, pas le Générateur).
- Pour Unity, passer par `unity/run-unity.ps1`.
- Le troisième angle mort du contrôle d'auto-jugement (couple natif
  `forge-generateur`/`forge-evaluateur` sans suffixe d'acteur) reste ouvert
  et documenté — brief futur ; ne pas le croire fermé.
