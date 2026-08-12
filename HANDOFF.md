# HANDOFF.md

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

## Session la plus récente — 2026-08-12 (après-midi) : ADR-0010, câblage réel, nettoyage

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
