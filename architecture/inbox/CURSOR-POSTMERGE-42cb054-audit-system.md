---
audit_id: CURSOR-POSTMERGE-42cb054-audit-system
auditor: cursor-cloud
target_branch: master
target_commit: 42cb054256d7d74da2523690aa790b1cb407f8dd
created_at: 2026-08-05T09:40:00Z
audit_type: post-implementation-audit
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# Résumé exécutif

Audit post-implémentation **en lecture seule** du merge `42cb054` (PR #4,
*forge/cursor-audit-loop*, migration 11/11). L'architecture annoncée est
**largement présente** dans le dépôt : dossiers, modules Python, commandes
`/forge-audit-*`, workflows CI, ADR-0005, et 73 tests dédiés passent
localement. Les garde-fous des **commandes** (review, accept, convert,
archive) sont solides en usage normal. En revanche, la **machine à états
n'est pas centralisée** : le ledger accepte toute transition `AUDIT_*` sans
validation, ce qui permet de contourner Claude et le propriétaire. Plusieurs
états documentés (`STALE`, `IMPLEMENTED`, `VERIFIED`) n'ont **aucune
commande** ni automatisation. La CI est **verte et utile**, mais
`cursor-scope` ne s'exécute que sur les PR `cursor/*`, pas sur les push.

## Cinq garanties confirmées

1. **Séparation des rôles par dossier** — `architecture/inbox/`, `reviews/`,
   `decisions/`, `archive/` existent ; le README et ADR-0005 décrivent qui
   écrit quoi (`architecture/README.md`, `docs/adr/0005-cursor-as-independent-auditor.md`).
2. **Ledger append-only au niveau fichier** — `harness/audit_ledger.py`
   n'ouvre le fichier qu'en mode `"a"` ; tests `test_append_is_append_only`
   et `test_unknown_event_is_refused` passent.
3. **Impossible d'accepter sans challenge via les commandes** —
   `audit_decision.decide()` refuse si `state != AUDIT_CHALLENGED` (test
   `test_refuses_when_not_challenged`).
4. **Conversion en graine de brief, pas en instruction exécutable** —
   `audit_convert.convert()` exige `AUDIT_APPROVED`, écrit des
   `<<TODO (planificateur)>>`, et teste l'absence de fabrication de spec
   (`test_convert_seeds_brief_with_provenance`).
5. **CI déterministe ajoutée** — merge `42cb054` : workflows `harness-ci`,
   `audit-guard`, `security` tous **success** (runs GitHub
   `30993426166`, `30993425330`, `30993426149`) ; `python3 -m pytest
   harness/tests/ -q` → **182 passed, 15 skipped** localement.

## Cinq risques les plus importants

1. **P0 — Contournement de la machine à états via `audit_ledger.py append`**
   — un append manuel `AUDIT_APPROVED` sans `AUDIT_CHALLENGED` fait passer
   l'état à APPROVED ; reproduit localement (voir § Machine à états).
2. **P0 — Double conversion** — après `AUDIT_CONVERTED`, un append manuel
   `AUDIT_APPROVED` permet une seconde conversion et un second brief
   (`001-topic`, `002-topic`) ; reproduit localement.
3. **P1 — `target_commit` non vérifié contre Git** — le schéma exige 40 hex
   mais accepte `000…000` inexistant ; fraîcheur/STALE non implémentés.
4. **P1 — Garde de périmètre Cursor partielle** — `cursor-scope` (job
   `audit-guard.yml`) ne tourne que si `pull_request` **et**
   `startsWith(head_ref, 'cursor/')` ; skipped sur push merge ; contournable
   par branche non-`cursor/` ou push direct.
5. **P1 — Branche `IMPLEMENTED → VERIFIED` absente du code** — aucune
   commande `/forge-audit-*` ni hook `/forge-run` n'écrit ces événements ;
   archivage `VERIFIED` requiert append manuel du ledger.

## Verdict global

**READY_WITH_GAPS** — pilote possible pour le flux nominal
(review → accept → convert → `/forge-run`), avec discipline humaine et sans
confiance aveugle dans le ledger ; corrections P0 recommandées avant usage
à volume ou multi-opérateur.

---

# Commit et périmètre analysés

| Champ | Valeur |
|---|---|
| Merge commit | `42cb054256d7d74da2523690aa790b1cb407f8dd` |
| PR | #4 `PLiagre/forge/cursor-audit-loop` |
| Commits migration | `465321b` … `f6750a7` (11 étapes) |
| Fichiers ajoutés | 33 fichiers, +3090 lignes (modules, tests, workflows, ADR, commandes) |
| Méthode | Lecture repo, exécution tests locaux, inspection workflows, runs GH Actions, tests adversariaux manuels |
| Hors périmètre respecté | Aucune modification code/tests/workflows ; pas de `/forge-run` ; pas d'Unity |

---

# Conformité au plan

| Élément | Statut | Preuve | Risque |
|---|---|---|---|
| `architecture/inbox/` | IMPLEMENTED | 2 audits réels + ce rapport ; `glob CURSOR-*.md` | Faible |
| `architecture/reviews/` | IMPLEMENTED | Dossier + `audit_review.py` scaffold/record | Moyen — review minimale acceptée |
| `architecture/decisions/` | IMPLEMENTED | Dossier + `audit_decision.py` | Faible |
| `architecture/archive/` | IMPLEMENTED | Dossier + `audit_archive.py` | Faible |
| `architecture/audit-ledger.jsonl` | IMPLEMENTED | Fichier vide créé au merge ; `audit_ledger.py` | **Élevé** — pas de FSM au append |
| `/forge-audit-list` | IMPLEMENTED | `.claude/commands/forge-audit-list.md` → `py harness/audits.py list` | Faible |
| `/forge-audit-review` | IMPLEMENTED | Command + `audit_review.py` ; 13 tests | Moyen — gate sémantique faible |
| `/forge-audit-accept` | IMPLEMENTED | Command + `audit_decision.py` ; 11 tests | Faible en usage commande |
| `/forge-audit-reject` | IMPLEMENTED | Idem accept | Faible |
| `/forge-audit-convert` | IMPLEMENTED | Command + `audit_convert.py` ; 10 tests | Moyen — double conversion |
| `/forge-audit-archive` | IMPLEMENTED | Command + `audit_archive.py` ; 8 tests | Faible |
| `/forge-audit-status` | IMPLEMENTED | Command + `audits.py status` ; 6 tests | Faible |
| Statuts `AUDIT_*` (9) | IMPLEMENTED | `VALID_EVENTS` dans `audit_ledger.py` | **Élevé** — 3 états sans commande |
| Vérification fraîcheur `target_commit` | PARTIALLY_IMPLEMENTED | Format SHA dans `audit_schema.py` ; mention README règle 4 | **Élevé** — pas de `git cat-file` |
| Conversion audit → brief | IMPLEMENTED | `audit_convert.py` ; provenance + TODO Planificateur | Moyen — points retenus non filtrés mécaniquement |
| Traçabilité audit → brief → coût | PARTIALLY_IMPLEMENTED | Ledger CONVERTED porte `briefs[]` ; cost ledger séparé (`harness/backends/ledger.py`) | **Élevé** — pas de lien automatique |
| Compatibilité `/forge-run` | IMPLEMENTED | Additif ; brief seed compatible ; ADR-0005 | Faible |
| Backend Cursor Générateur conservé | IMPLEMENTED | `harness/backends/run_cursor_generator.sh` + README ; ADR-0005 « kept but deprecated » | Faible |
| ADR et documentation | IMPLEMENTED | `docs/adr/0005-cursor-as-independent-auditor.md`, `architecture/README.md`, `CLAUDE.md` | Faible |

---

# Machine à états réelle

## Transitions codées dans les commandes

| Transition | Mécanisme | Garde |
|---|---|---|
| (aucun) → `AUDIT_PROPOSED` | Audit dans `inbox/` sans événement ledger | Défaut dans `audits.current_state()` |
| `PROPOSED` → `CHALLENGED` | `audit_review.record` | Audit inbox, state PROPOSED, review sans `<<`, ≥1 verdict token |
| `CHALLENGED` → `APPROVED` | `audit_decision.decide(APPROVED)` | state CHALLENGED, reason non vide, pas de clobber decision |
| `CHALLENGED` → `REJECTED` | `audit_decision.decide(REJECTED)` | Idem |
| `APPROVED` → `CONVERTED` | `audit_convert.convert` | state APPROVED, brief dir inexistant |
| `REJECTED` ou `VERIFIED` → `ARCHIVED` | `audit_archive.archive` | state terminal, archive inexistante |

## Transitions **non** codées (append ledger brut uniquement)

| Transition documentée | Statut |
|---|---|
| `CONVERTED` → `IMPLEMENTED` | **MISSING** — aucun module |
| `IMPLEMENTED` → `VERIFIED` | **MISSING** — aucun module |
| `*` → `STALE` | **MISSING** — aucune détection automatique |
| Ré-écriture `AUDIT_PROPOSED` au dépôt d'audit | **MISSING** — pas d'événement automatique |

## Contournements observés (reproduits localement, 2026-08-05)

```text
# 1. Bypass review + owner via ledger CLI
append AUDIT_APPROVED → state AUDIT_APPROVED (sans CHALLENGED)

# 2. Double conversion
convert → CONVERTED ; append AUDIT_APPROVED ; convert → second brief 002-*

# 3. Review minimale acceptée
review body: "# x\nCONFIRMED\n" → record_challenge OK
```

## Divergence fichier vs ledger

- **Conçu** : le frontmatter `status: PROPOSED` est figé ; l'état courant vient du **dernier** événement ledger (`audits.py`, test `test_state_comes_from_ledger_not_file`).
- **Risque** : si quelqu'un modifie le frontmatter ou le corps d'un audit existant, **aucune CI** ne détecte la modification (schéma valide le contenu actuel, pas le diff « append-only inbox »).

## Transitions impossibles via commandes (OK)

- Accept sans challenge → refusé (`DecisionError`).
- Convert sans APPROVED → refusé (`ConvertError`).
- Archive en vol (PROPOSED/CHALLENGED/APPROVED/CONVERTED) → refusé (`ArchiveError`).
- Double decision file → refusé (no clobber).

## Transitions autorisées à tort (via ledger)

- Toute séquence `AUDIT_*` valide via `py harness/audit_ledger.py append`.
- Double `AUDIT_CONVERTED` possible si état repasse à APPROVED manuellement.

---

# Sécurité et autorisations

## Périmètre Cursor « inbox only »

| Contrôle | Présent | Efficacité |
|---|---|---|
| Job `cursor-scope` | Oui — `audit-guard.yml` | **Partielle** |
| Condition | `pull_request` + branche `cursor/*` | Push merge : **skipped** (run `30993425330`) |
| Filtre chemins | `grep -v '^architecture/inbox/'` | OK pour PR conformes |
| Branche non `cursor/` | Non bloquée | PR documentaire possible hors préfixe |
| CODEOWNERS | **ABSENT** | UNVERIFIABLE côté repo |
| Protection branche | API GH → **403** | UNVERIFIABLE |
| Auto-merge | Non configuré dans repo | UNVERIFIABLE |

## Workflows

| Workflow | Trigger | Permissions | Pin SHA |
|---|---|---|---|
| `audit-guard.yml` | push, PR | `contents: read` | checkout, setup-python pinned |
| `harness-ci.yml` | push, PR | `contents: read` | pinned |
| `security.yml` | push, PR | `contents: read` | pinned ; actionlint + gitleaks binaires |

## Contournements « PR documentaire uniquement »

1. **PR depuis branche non `cursor/`** — scope guard inactif ; audit + code dans la même PR possible si merge autorisé.
2. **Push direct sur branche** (si protection absente) — aucun scope guard.
3. **Modifier `.github/workflows/audit-guard.yml` dans PR non-Cursor** — aucun job ne bloque.
4. **Append ledger / reviews / decisions en local** — hors périmètre CI Cursor (attendu pour Claude/owner, mais pas signé).

## Schéma — flags d'autorisation

Les trois flags `*_authorized: false` sont **vérifiés** (`test_self_authorised_flag_rejected`, CI schema).

## Absents du schéma CI

- Existence Git du `target_commit`
- Taille maximale du fichier audit
- Déduplication FINDING IDs dans le corps
- Un seul audit par PR
- Interdiction de modifier un fichier `inbox/` existant (diff-based)
- Injection d'instruction exécutable dans le corps (non analysé)

---

# Commandes Forge

| Commande | Module | Préconditions | Effets | Idempotence | Rollback |
|---|---|---|---|---|---|
| `/forge-audit-list` | `audits.py list` | Aucune | Lecture seule | Oui | N/A |
| `/forge-audit-status` | `audits.py status` | Audit dans inbox | Lecture seule | Oui | N/A |
| `/forge-audit-review` scaffold | `audit_review.py` | Audit inbox | Crée `reviews/CLAUDE-*.md` | Non — refuse clobber | Suppression manuelle |
| `/forge-audit-review` record | `audit_review.py` | PROPOSED + review remplie | Ledger `CHALLENGED` | Non | Pas de delete ledger |
| `/forge-audit-accept` | `audit_decision.py` | CHALLENGED | Decision file + ledger | Non — refuse clobber | Manuel |
| `/forge-audit-reject` | idem | CHALLENGED | idem | Non | Manuel |
| `/forge-audit-convert` | `audit_convert.py` | APPROVED | Brief seed + ledger CONVERTED | Non — refuse 2e convert direct | Supprimer brief + ligne ledger |
| `/forge-audit-archive` | `audit_archive.py` | REJECTED ou VERIFIED | Copie bundle + ledger ARCHIVED | Non — refuse clobber archive | Supprimer archive + ligne ledger |

**Fragilités transverses** : pas de verrou fichier ledger ; chemins relatifs corrects via `REPO_ROOT` ; erreurs → exit 2 (CLI) ; pas de rollback automatique.

---

# Conversion audit vers brief

| Exigence | Statut | Preuve |
|---|---|---|
| Seuls findings approuvés | PARTIEL | `--retain` enregistré dans decision + provenance brief ; **pas** de validation contre le corps de l'audit |
| Audit jamais exécutable directement | OK | Seed avec `<<TODO (planificateur)>>` ; test dédié |
| Provenance conservée | OK | Section Provenance dans `brief_seed_text()` |
| Critères d'acceptation | PARTIEL | Placeholders Success Conditions — Planificateur requis |
| Problèmes indépendants séparés | NON | Un seul brief par conversion ; pas de split automatique |
| NEEDS_SPLIT si trop large | NON | `budget.py split-check` non appelé à la conversion |
| Format harness | OK | `brief.md`, `eval-rubric.md`, `deliverables/` |
| `/forge-run` seule exécution | OK | Seed non passable au gate sans Planificateur |

---

# Ledger

| Propriété | Statut | Détail |
|---|---|---|
| Append-only fichier | OK | Mode `"a"` ; test byte-preserving |
| Horodatage | OK | ISO UTC `Z` |
| `audit_id`, `event`, champs libres | OK | `briefs`, `retained_points`, `verdicts`, etc. |
| Déduplication | **ABSENT** | Doublons d'événements possibles |
| Concurrence | **ABSENT** | Documenté dans `audit_ledger.py` docstring |
| Cohérence FSM | **ABSENT** | Append accepte tout `VALID_EVENTS` |
| Fichier repo | Vide à ce commit | Normal avant premier cycle réel |
| Lien cost ledger | **MANUEL** | Deux fichiers ; pas de champ `audit_id` dans cost ledger vérifié |

---

# GitHub Actions

## Synthèse par workflow (état post-merge `42cb054`)

| Workflow | Jobs | CI merge | Notes |
|---|---|---|---|
| `harness-ci` | `tests`, `f0-demo` | success | pytest complet + fake brief REJECT |
| `audit-guard` | `schema`, `cursor-scope` | success / **skipped** | scope skipped sur push |
| `security` | `actionlint`, `gitleaks` | success | dependency-review explicitement omis |

## Checklist CI annoncée

| Check | En CI ? |
|---|---|
| `pytest harness/tests/` | Oui |
| Gate mécanique (demo F0 REJECT) | Oui (`f0-demo`) |
| Gate ACCEPT contrôle honnête | Non en CI (documenté : mtime) ; couvert par `test_verdict_audit.py` |
| Validation schéma audit | Oui (`schema` job) |
| Validation diff Cursor scope | **PR cursor/* seulement** |
| single-source-of-instruction | Oui (pytest) |
| actionlint | Oui |
| gitleaks | Oui |
| dependency-review | Non (repo sans manifests versionnés) |

## Classification CI globale

**CI_GREEN_VERIFIED** pour ce qui est **effectivement exécuté** sur le merge
`42cb054` (182 tests, F0, schema, actionlint, gitleaks).

**CI_GREEN_INCOMPLETE** pour les garanties **annoncées mais non branchées** :
scope Cursor sur push, fraîcheur commit, FSM ledger, branche protection
(UNVERIFIABLE).

---

# Couverture QA

## Cartographie (73 tests `test_audit*`)

| Garantie | Test(s) | CI | Type | Mock | Risque résiduel |
|---|---|---|---|---|---|
| Ledger append-only | `test_audit_ledger.py` | Oui | Positif | Non | FSM bypass |
| État depuis ledger | `test_audits.py` | Oui | Positif | Non | — |
| Schema flags false | `test_audit_schema.py` | Oui | Pos+Nég | Non | SHA inexistant |
| Review gate | `test_audit_review.py` | Oui | Négatif | Non | Review triviale |
| Owner gate | `test_audit_decision.py` | Oui | Négatif | Non | Ledger bypass |
| Convert seed | `test_audit_convert.py` | Oui | Pos+Nég | monkeypatch clobber | Double convert |
| Archive bundle | `test_audit_archive.py` | Oui | Positif | Non | VERIFIED manuel |
| Audits réels inbox | `test_real_cursor_audits_pass` | Oui | Positif | Non | — |
| Scope CI Cursor | — | — | — | — | **Non testé** |
| target_commit exists | — | — | — | — | **Non testé** |
| E2E boucle complète | — | — | — | — | **Non testé** |

Exécution locale : `python3 -m pytest harness/tests/test_audit*.py -q` →
**73 passed** ; suite complète **182 passed, 15 skipped**.

---

# Scénarios adversariaux

Cinq scénarios où le système **semble correct** mais reste contournable ou
incomplet :

1. **Ledger append silencieux** — un opérateur exécute
   `py harness/audit_ledger.py append --audit-id X --event AUDIT_APPROVED`
   ; `/forge-audit-list` affiche APPROVED sans review Claude.
2. **PR « documentaire » hors `cursor/`** — branche `docs/audit-foo` modifie
   `harness/` + `inbox/` ; CI schema passe ; scope guard **jamais exécuté**.
3. **Review token-only** — fichier `# x\nCONFIRMED\n` satisfait
   `record_challenge` ; le propriétaire croit à un challenge substantiel.
4. **Audit sur commit fantôme** — frontmatter SHA valide mais inexistant ;
   schema OK ; fraîcheur/STALE jamais déclenchés.
5. **Double brief pour un audit** — après conversion légitime, re-append
   APPROVED + second `convert` ; deux briefs, un seul audit inbox.

---

# Tests manquants

## P0

| ID | Type | Risque couvert |
|---|---|---|
| T-P0-1 | ADVERSARIAL + UNIT | `audit_ledger.append` refuse transitions invalides (ex. APPROVED sans CHALLENGED) |
| T-P0-2 | INTEGRATION | Double conversion impossible même après re-append APPROVED |

## P1

| ID | Type | Risque couvert |
|---|---|---|
| T-P1-1 | CONTRACT | `audit_schema` rejette SHA absent de `git rev-parse` |
| T-P1-2 | SECURITY | Test CI (fixture workflow) : PR `cursor/*` avec fichier hors inbox → fail |
| T-P1-3 | INTEGRATION | Commande ou hook post-`/forge-run` PASS → `AUDIT_IMPLEMENTED` |

## P2

| ID | Type | Risque couvert |
|---|---|---|
| T-P2-1 | ADVERSARIAL | Diff PR détecte modification d'un `inbox/CURSOR-*.md` existant |
| T-P2-2 | CONTRACT | Review gate exige ≥N lignes de preuve ou table remplie |
| T-P2-3 | RECOVERY | Concurrence : deux append ledger simultanés |

## P3

| ID | Type | Risque couvert |
|---|---|---|
| T-P3-1 | END_TO_END | Fixture : inbox → review → accept → convert → archive REJECTED |
| T-P3-2 | INTEGRATION | Lien automatique audit_id dans cost ledger à la conversion |

---

# Test de bout en bout

| Étape | Couverture |
|---|---|
| 1. Cursor crée audit valide | **Partiel** — 2 audits réels ; schema CI |
| 2. PR documentaire passe CI | **Partiel** — PR #4 non `cursor/*` ; schema oui, scope N/A |
| 3. Claude review | **Non exécuté** en prod (audits encore PROPOSED) |
| 4. Owner approve | **Tests unitaires** seulement |
| 5. Findings → briefs | **Tests unitaires** seulement |
| 6. Briefs `/forge-run` | **Hors scope** — compatibilité structurelle OK |
| 7. Ledger lie audit/brief/coût | **Partiel** — brief path oui ; coût non |
| 8. Archive | **Tests unitaires** REJECTED/VERIFIED |

**Classification : E2E_PARTIAL**

---

# Verdict

**READY_WITH_GAPS**

Le merge `42cb054` livre une boucle d'audit **utilisable en pilote contrôlé**
: structure, commandes, tests et CI de base sont réels et verts. Les écarts
prioritaires concernent l'**intégrité de la machine à états** (ledger non
gouverné), la **fraîcheur du commit**, et l'**étanchéité CI du périmètre
Cursor**. Les états `IMPLEMENTED`, `VERIFIED`, `STALE` restent
**documentaires** jusqu'à implémentation dédiée.

---

# Briefs proposés à Claude

Maximum trois briefs atomiques pour problèmes P0/P1 confirmés.

## Brief A — Gouvernance FSM du audit-ledger (P0)

- **Finding source** : § Machine à états — contournement append ; T-P0-1
- **Objectif** : Centraliser les transitions autorisées ; refuser tout append
  incohérent avec l'état courant.
- **Périmètre** : `harness/audit_ledger.py` ; appels depuis
  `audit_review/decision/convert/archive` ; tests adversariaux.
- **Hors périmètre** : UI ; migration historique ledger vide.
- **Fichiers** : `harness/audit_ledger.py`, `harness/audit_*.py`,
  `harness/tests/test_audit_ledger.py`, nouveau `test_audit_fsm.py`
- **Tests** : append APPROVED sans CHALLENGED → refuse ; séquence valide → OK
- **Critères d'acceptation** : 100 % transitions invalides reproductibles
  ci-dessus échouent ; suite audit tests verte ; pas de régression CLI
- **Rollback** : Revert commit ; ledger reste append-only fichier
- **Budget** : ~80 appels outils

## Brief B — Double conversion et re-approbation (P0)

- **Finding source** : § Scénario adversarial #5 ; T-P0-2
- **Objectif** : Un audit APPROVED ne peut produire qu'**un** brief ; état
  CONVERTED terminal pour conversion.
- **Périmètre** : `audit_convert.py`, garde FSM (Brief A prérequis ou inclus)
- **Hors périmètre** : Multi-brief volontaire (futur ADR)
- **Fichiers** : `harness/audit_convert.py`, `harness/tests/test_audit_convert.py`
- **Tests** : re-append APPROVED post-CONVERTED → convert refuse
- **Critères d'acceptation** : test rouge→vert ; un seul chemin `briefs[]` par audit
- **Rollback** : Revert ; briefs déjà créés restent (provenance manuelle)
- **Budget** : ~60 appels

## Brief C — Fraîcheur target_commit + garde scope CI (P1)

- **Finding source** : § Sécurité ; T-P1-1, T-P1-2
- **Objectif** : (1) Schema rejette SHA absent ; option STALE si ancestor
  derrière `target_branch`. (2) Test workflow : PR cursor avec path hors inbox
  échoue.
- **Périmètre** : `audit_schema.py`, `audit-guard.yml`, tests
- **Hors périmètre** : Branch protection GitHub (owner)
- **Fichiers** : `harness/audit_schema.py`, `harness/tests/test_audit_schema.py`,
  `.github/workflows/audit-guard.yml`, test fixture shell
- **Tests** : SHA `000…0` → FAIL ; fixture diff cursor PR → FAIL
- **Critères d'acceptation** : schema exit 1 sur SHA invalide ; doc README
  règle 4 alignée avec code
- **Rollback** : Revert ; audits existants re-validés manuellement
- **Budget** : ~100 appels

---

*Audit réalisé en lecture seule par Cursor Cloud, post-merge PR #4. Aucun
fichier de production, test ou workflow modifié hors ce rapport documentaire.*
