# HANDOFF.md

## Session la plus récente — 2026-08-13 : critique du brief 011 traitée, brief 012 (le monde vit + commerce)

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
