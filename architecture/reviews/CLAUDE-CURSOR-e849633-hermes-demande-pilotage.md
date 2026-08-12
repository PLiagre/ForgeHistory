---
review_of: CURSOR-e849633-hermes-demande-pilotage
reviewer: claude-code
target_commit: e8496336391ada87719ee0fa210de4d71a8f9487
reviewed_at: 2026-08-12T15:10:00Z
---

# Contre-audit de CURSOR-e849633-hermes-demande-pilotage

Rôle : vérifier la *véracité technique* de l'audit, pas sa valeur métier.
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

**Limite d'environnement déclarée d'emblée** : cette relecture s'est faite
sans accès à `gh` authentifié (`gh auth status` échoue, aucun `GH_TOKEN`
dans l'environnement). Tout ce que l'audit source depuis l'API GitHub
(chronologie seconde par seconde de la PR, liste des `check-runs`, journal
du run `31595782109`) n'a pas pu être rejoué à l'identique. Chaque fois que
c'est le cas ci-dessous, le verdict est `PARTIAL` et précise ce qui a été
corroboré autrement (git local, contenu des fichiers) contre ce qui reste
non rejouable dans cet environnement.

## 1. Provenance (re-vérifiée)

- target_commit annoncé : `e8496336391ada87719ee0fa210de4d71a8f9487`
- Le commit existe dans l'historique de `master` :
  `git merge-base --is-ancestor e8496336391ada87719ee0fa210de4d71a8f9487 master`
  → exit 0 (ancêtre confirmé). `git log -1 e849633...` retrouve le même
  message de commit que celui cité par l'audit.
- Mesures rejouées localement (sans `gh`) :
  - `git show -s --format='%an <%ae>%n%cn <%ce>%n%B' e849633...` →
    auteur = `Cursor Agent <cursoragent@cursor.com>`, co-auteur
    `Pierre-Edouard Liagre <PLiagre@users.noreply.github.com>`.
  - `git log --all --merges --grep="#32"` → commit de fusion `e7c4895`,
    date `2026-08-12T14:18:04+02:00` = **12:18:04 UTC**, branche
    `PLiagre/forge/hermes-tableau-pilotage-c2dd`. Corrobore à 1s près
    l'heure de fusion citée par l'audit (12:18:05 UTC) et confirme que la
    branche est bien `forge/*`, pas `cursor/*`.
  - `.venv` absent de cet environnement ; suite rejouée avec
    `python3 -m pytest harness/tests/ -q` (équivalent à
    `.venv/bin/python`) → `309 passed, 16 skipped` — chiffre identique à
    celui cité par la PR et par l'audit (§ 6.3).
  - Reproduction indépendante du cas § 6.6 (H2) : voir point 3 ci-dessous.
  - Non rejouable ici : `gh pr view 32`, `gh api .../check-runs`,
    `gh run view 31595782109 --log` (pas de jeton GitHub dans ce
    bac à sable).

## 2. Verdicts point par point

| # | Point de l'audit | Verdict | Preuve / délimitation |
|---|---|---|---|
| 1 | § 4.1 — fenêtre de critique de 4 s ; l'audit arrive après la fusion ; `pipeline-audit.yml` l.43 exclut les brouillons | **PARTIAL** | `.github/workflows/pipeline-audit.yml:43` reproduit mot pour mot : `if: github.event_name != 'pull_request' || (github.event.pull_request.draft == false && !startsWith(...,'cursor/'))` — confirme que le job `invoke-cursor-auditor` est structurellement sauté sur une PR brouillon, mécanisme réel. La fusion à 12:18:04 UTC (git local) corrobore l'ordre de grandeur (quelques secondes) de la fenêtre. Le détail seconde par seconde du passage brouillon→prêt→fusion (§ 6.1, table horodatée) vient de `gh api issues/32/timeline`, non rejouable sans jeton GitHub dans cet environnement — non falsifié, juste non re-mesuré indépendamment ici. |
| 2 | § 4.2 — le diagnostic H2 est faux : l'API Cursor *est* interrogée ; le message du tableau confond « interrogée-vide » et « jamais interrogée » | **CONFIRMED** | Reproduit indépendamment : `python3 hermes/dashboard.py --agents-json {"agents":[]}` et `python3 hermes/dashboard.py` (sans `--agents-json`) produisent la **même** ligne `Non disponible dans cette génération (API Cursor non interrogée).` — `hermes/dashboard.py:274` est bien la ligne citée. `.github/workflows/hermes-dashboard.yml` contient l'étape « Collect recent Cursor Cloud agents (optional) » qui appelle `https://api.cursor.com/v1/agents?limit=10`. Contradiction interne du fichier livré également vérifiée : H1 dit la clé « déjà provisionnée côté CI », H2 dit l'API « non interrogée » — les deux ne peuvent être vrais ensemble. Seul point non rejoué ici : le journal exact du run `31595782109` (accès `gh` requis) ; non nécessaire pour confirmer le point, la reproduction locale suffit à établir que le message est ambigu par construction. |
| 3 | § 4.3 — auteur d'`hermes/**` non vérifié mécaniquement ; garde `cursor-scope` pilotée par un préfixe de branche que l'agent choisit | **CONFIRMED** | `hermes/README.md` promet `author: hermes` + commit `hermes:` comme traçabilité ; le fichier livré déclare `author: hermes` en frontmatter mais `git show -s --format='%an <%ae>'` donne `Cursor Agent <cursoragent@cursor.com>` — écart réel, vérifié localement. `harness/audit_schema.py:26` : `INBOX = REPO_ROOT / "architecture" / "inbox"` — aucune référence à `hermes/**` dans ce fichier ni ailleurs (`rg` sur les `.py` du dépôt) : aucun validateur n'existe. `.github/workflows/audit-guard.yml:30` : `if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')` — confirmé mot pour mot ; la PR #32 vient de `forge/hermes-tableau-pilotage-c2dd` (confirmé par le commit de fusion local), donc hors du filtre. Le filet de rattrapage (`merge-bot.yml` exclut `hermes/**` de son allowlist, l.50 : `architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/`) est également vérifié — c'est cohérent avec le classement `P1` et non `P0` que fait l'audit. |
| 4 | § 4.4 — H4 proposerait de confier la fusion à Hermes sans exclure ses propres PR | **CONFIRMED** | Texte du fichier livré (lignes 95-104 dans ma lecture) : les 4 garde-fous listés sont « confirmation explicite », « jeton dédié minimal », « journalisation », « `127.0.0.1` sans exposition réseau » — aucun ne mentionne l'exclusion d'une PR dont Hermes serait l'auteur. `hermes/README.md` (dernier paragraphe) confirme que la relecture humaine des PR `hermes/**` tient aujourd'hui *parce que* ces chemins sont hors allowlist merge-bot — un mécanisme que H4 ne reproduit pas pour la fusion elle-même. |
| 5 | § 4.5 — cinq arbitrages hétérogènes pour un seul champ `status` | **CONFIRMED** | `hermes/README.md:66` : `status: OPEN \| HANDED_TO_CTO \| REFLECTED_IN_ROADMAP \| CLOSED` — un seul champ, quatre valeurs, aucune granularité par décision. Le fichier livré liste bien 5 arbitrages numérotés (H1 à H5) dans sa section finale « Arbitrages demandés au propriétaire ». |
| 6 | § 4.6 — « 309 passed » exact mais n'exerce aucune ligne du livrable | **CONFIRMED** | Rejoué : `python3 -m pytest harness/tests/ -q` → `309 passed, 16 skipped` (chiffre identique). `rg -ln "hermes" harness/tests/` ne retourne que `test_hermes_dashboard.py` (teste le générateur, pas `hermes/requests/**`) — confirmé. |
| 7 | § 4.7 — deux affirmations non vérifiables/imprécises (date « shadow » 2026-08-24 ; « chaque » workflow/événement transmis par `hermes-observer`) | **CONFIRMED** | `rg -n "2026-08-24" --glob '!hermes/requests/**'` → aucun résultat ailleurs dans le dépôt, confirmé. `.github/workflows/hermes-observer.yml` : la liste `workflows:` énumère 9 noms (`audit-guard`, `harness-ci`, `merge-bot`, `pipeline-audit`, `pipeline-challenge`, `pipeline-failure-escalate`, `pipeline-forge-run`, `pipeline-orchestrate`, `security`) — n'inclut ni `hermes-dashboard` ni `hermes-observer` lui-même, confirmé. Côté PR : `types: [opened, reopened, synchronize, ready_for_review, closed]` — 5 types, ni `edited` ni événement de revue, confirmé. |
| 8 | § 4.8 — « secrets périmés » non mesurable via l'API GitHub ; le manque signalé par H3 est réel (la demande n'apparaît pas dans « Ce qui attend le propriétaire ») | **CONFIRMED** (mesurabilité) / **NEEDS_OWNER** (reformulation) | `.github/workflows/hermes-dashboard.yml` permissions : `contents: write`, `actions: read`, `pull-requests: read` — aucune portée liée aux secrets, cohérent avec le fait que l'API GitHub Actions secrets ne renvoie que `name`/`created_at`/`updated_at` (connaissance publique de l'API, non re-vérifiable par requête réseau dans ce bac à sable, mais non contestée). L'expiration « ~8 jours » du secret Codex est bien sourcée : `docs/rules/full-auto-pipeline.md:109` dit littéralement « after roughly 8 days without a run refresh », confirmé. Que la reformulation proposée (« présent/absent » + « inchangé depuis N jours ») soit la bonne mesure de substitution est un choix de conception, pas un fait — relève du propriétaire/CTO, pas de cette relecture technique. |
| 9 | § 5.2 — non-doublon avec les audits en cours (73022bd CHALLENGED, 65c3ac1 déposé/PROPOSED, cdc683f APPROVED) | **CONFIRMED** | `architecture/audit-ledger.jsonl` : `CURSOR-cdc683f-hermes-workflow-quatre-acteurs` a bien `AUDIT_CHALLENGED` puis `AUDIT_APPROVED` ; `CURSOR-73022bd-hermes-dashboard-modele-auditeur` a `AUDIT_CHALLENGED` sans événement postérieur. `CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md` existe dans `architecture/inbox/` avec `status: PROPOSED` et **aucune** entrée au ledger — c'est bien « déposé », pas « challenged », comme l'écrit l'audit. |
| 10 | § 6.2 — classification CI du commit `e849633` (13 success / 4 skipped / 1 cancelled, aucun échec) | **PARTIAL — non rejouable ici** | Nécessite `gh api .../check-runs`, indisponible sans jeton GitHub dans ce bac à sable. Cohérent par déduction avec les mécanismes vérifiés au point 3 (`cursor-scope` structurellement `skipped` pour une branche `forge/*`, `invoke-cursor-auditor` structurellement `skipped` pour un brouillon, `check-and-automerge` structurellement `skipped` pour un chemin hors allowlist) : les trois `skipped` cités s'expliquent tous par du code vérifié indépendamment, ce qui rend la classification plausible sans confirmer le compte exact des 18 checks. |

## 3. Points à porter au propriétaire (NEEDS_OWNER)

- **§ 4.8, reformulation de H3** : remplacer « secrets périmés » par
  « présent/absent » + « inchangé depuis N jours » est un choix de
  conception d'indicateur, pas une vérité technique — au CTO/propriétaire
  de trancher au moment d'écrire le brief.
- **§ 4.1 et § 4.3, propositions de traitement** (rendre les fusions non
  auditées visibles ; ancrer la garde de périmètre sur l'identité plutôt
  que sur le nom de branche) : l'audit les présente déjà comme des
  propositions non instructives, correctement. Leur adoption reste un
  arbitrage de priorité et de coût pour le propriétaire, pas une question
  technique.
- **§ 4.4** : la trajectoire H4 (ADR-0011 avant tout câblage) est déjà
  correcte dans le fichier livré ; l'exclusion des PR dont Hermes est
  l'auteur, elle-même, est un arbitrage de conception d'ADR à trancher par
  le propriétaire quand l'ADR sera écrit — pas quelque chose que cette
  relecture peut confirmer ou réfuter techniquement.

## 4. Synthèse

Ce qui tient, à l'unanimité des points techniquement vérifiables dans cet
environnement : le fichier livré existe tel que décrit, respecte le format
imposé par `hermes/README.md`, et les sept chemins qu'il cite sont réels.
Les trois `P1` de l'audit résistent tous à la contre-vérification : (a)
`pipeline-audit.yml:43` exclut bien les brouillons et la fusion a eu lieu
quelques secondes après le passage en `ready_for_review` (corroboré par le
commit de fusion local, à défaut de la chronologie seconde par seconde de
`gh api`) ; (b) le diagnostic H2 est **reproductiblement faux** — j'ai
rejoué moi-même les trois cas de `hermes/dashboard.py` et confirmé que
« interrogée mais vide » et « jamais interrogée » produisent le mot-à-mot
identique ; (c) `harness/audit_schema.py` ne couvre que
`architecture/inbox`, aucun validateur `hermes/**` n'existe, et la garde
`cursor-scope` est bien pilotée par un préfixe de branche que l'agent
producteur choisit lui-même. Les `P2`/`P3` (arbitrages multiples sous un
seul `status`, preuve orthogonale au livrable, affirmations non
vérifiables, mesurabilité des « secrets périmés ») sont également
confirmés point par point contre le contenu réel des fichiers cités.

Seule réserve, déclarée dès § 1 : les faits sourcés exclusivement par
l'API GitHub (chronologie seconde par seconde de la PR, liste complète des
18 `check-runs`, journal du run `31595782109`) n'ont pas pu être rejoués
dans ce bac à sable sans jeton GitHub authentifié. Rien de ce qui a pu être
corroboré autrement (git local, contenu des workflows, reproduction du bug
H2) ne les contredit ; ils restent `PARTIAL` par prudence méthodologique,
pas par doute sur leur véracité.

Recommandation de traitement : les trois `P1` et les propositions
associées (briefs proposés 1 et 2 en particulier — vérité de la section
« agents Cursor », validateur de frontmatter `hermes/**`) sont des candidats
solides pour devenir des briefs, sous réserve de l'arbitrage du
propriétaire sur leur priorité. Rien dans cet audit ne devait bloquer la
fusion de la PR #32 elle-même, et rien ici ne le remet en cause a
posteriori.
