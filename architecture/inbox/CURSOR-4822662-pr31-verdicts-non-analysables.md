---
audit_id:                CURSOR-4822662-pr31-verdicts-non-analysables
auditor:                 cursor-cloud
target_branch:           forge-bot/review-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur-31594124761
target_commit:           4822662bfddf5a3aebb0f4535f9dbecac51a3ec0
created_at:              2026-08-12T13:53:49Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la PR #31 — « challenge: revue de l'audit CURSOR-65c3ac1-dashboard-hermes-modele-auditeur »

Audit en lecture seule, produit selon `architecture/review-guidelines.md`
(six lentilles, sévérités P0–P3, une preuve citée par constat). Ce document
**ne décide rien et n'autorise rien** : c'est une entrée pour la boucle
(`architecture/README.md`, ADR-0005/0006).

- PR auditée : <https://github.com/PLiagre/ForgeHistory/pull/31>
- Commit audité : `4822662bfddf5a3aebb0f4535f9dbecac51a3ec0` (tête de la
  branche, qui a déjà fusionné `master` = `7e5244b` dans elle-même — l'arbre
  de cette tête est donc l'état post-fusion).
- Auteur : `app/github-actions` / `forge-bot` (invocation headless
  `claude-challenger`, run 31594124761).

En une phrase : **la revue produite est excellente sur le fond, mais son
tableau de verdicts n'est pas lisible par la machine qui doit s'en servir.**
Fusionner cette PR en l'état laisse l'audit `CURSOR-65c3ac1` dans un
cul-de-sac : le moteur de décision automatique refuse de décider, et la
boucle « full auto » s'arrête là.

## 1. Intention avant diff (lentille 1)

L'intention est lisible et légitime : produire le contre-audit
`CLAUDE-CURSOR-65c3ac1-...` puis faire avancer l'automate d'un cran
(`AUDIT_PROPOSED` → `AUDIT_CHALLENGED`). Le rôle est bien tenu — l'acteur qui
critique (`claude-challenger`) est distinct de l'acteur qui a produit l'audit
(`cursor-cloud`), conformément à la lentille 4 « cadrage adverse ».

Mais la description de la PR affirme un effet non mesuré :

> « La fusion de cette PR déclenche pipeline-orchestrate.yml (event
> review_recorded). »

C'est vrai pour le déclenchement, et **faux pour le résultat** : l'événement
est bien résolu, puis l'orchestrateur échoue. Preuve reproduite en §6.3 et
§6.4. C'est le motif « correction hallucinée » de la lentille 6 : un succès
affirmé, jamais mesuré.

## 2. Taille et découpage (lentille 5)

Rien à reprocher ici, et il faut le dire : 2 fichiers, 120 lignes ajoutées,
0 supprimée — très en dessous du seuil d'environ 400 lignes cité par
`architecture/review-guidelines.md` ligne 33. Le lot est honnêtement
découpé.

## 3. Portes mécaniques (lentille 3) — classification de la CI

15 vérifications sur le commit audité : **14 vertes, 1 rouge**.

| workflow / job | conclusion |
|---|---|
| `merge-bot` / `check-and-automerge` | **FAILURE** |
| `harness-ci` / `tests`, `f0-demo` | success (×2 runs) |
| `audit-guard` / `schema` | success (×2 runs) |
| `audit-guard` / `cursor-scope` | skipped (branche non `cursor/*`) |
| `security` / `actionlint`, `gitleaks` | success (×2 runs) |
| `pipeline-audit` / `invoke-cursor-auditor` | success |
| `hermes-observer` / `Reconcile local Hermes state` | success |

Le point important n'est pas le job rouge (il est franc, voir F2) mais le
**silence des 14 verts** : aucune porte mécanique ne regarde le contenu de
`architecture/reviews/**`. Le job `schema` de `audit-guard` ne valide que le
frontmatter des audits de `inbox/` (`audit-guard.yml` ligne 26 →
`python harness/audit_schema.py`). C'est exactement ce que la lentille 3
demande de vérifier : les machines ont tourné, mais elles ne couvrent pas le
défaut réel.

## 4. Constats

### F1 — P0 — Le tableau de verdicts n'est pas analysable : après fusion, la décision automatique échoue et l'audit reste bloqué

Le consommateur de la revue est
`audit_decision._parse_point_verdicts` (`harness/audit_decision.py`
lignes 62-66, 185), qui exige une cellule de verdict **nue** :

```python
r"^\|\s*(\d+)\s*\|.*?\|\s*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\s*\|",
```

La revue livrée écrit ses verdicts **en gras** — `| **CONFIRMED** |` (lignes
43 à 56 du fichier ajouté). Les astérisques empêchent le motif de coller.
Résultat mesuré (§6.1) : **14 lignes numérotées présentes, 0 reconnue.**

Conséquence de bout en bout, rejouée sur l'arbre post-fusion (§6.4) :

```
etat FSM apres fusion : AUDIT_CHALLENGED
decide_auto a LEVE DecisionError: .../CLAUDE-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md
  has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
```

L'audit `CURSOR-65c3ac1` s'immobilise donc à `AUDIT_CHALLENGED` : ni
`AUDIT_APPROVED`, ni `AUDIT_REJECTED`. Le refus est propre (fail-closed,
`audit_decision.py` lignes 249-253) et bruyant (l'étape « Run orchestrator »
de `pipeline-orchestrate.yml` ligne 86-92 tourne sous `set -euo pipefail`,
sans `continue-on-error` : le run sur `master` passera au rouge). Mais
l'événement `review_recorded` est consommé une seule fois — aucun
déclencheur ne rejouera la transition. Il faut une intervention humaine,
c'est-à-dire précisément ce que le mode `full_auto` prétend supprimer
(ADR-0006).

**Ce n'est pas un cas isolé.** Corrélation mesurée sur les quatre décisions
déjà enregistrées au ledger (§6.2) : les deux audits dont la revue est
analysable ont été décidés par `policy:auto` ; les deux dont la revue ne
l'est pas ont été décidés par `owner`.

| revue | lignes numérotées | reconnues | décision |
|---|---|---|---|
| `CLAUDE-CURSOR-FIXTURE-full-auto-demo` | 1 | 1 | `policy:auto` |
| `CLAUDE-CURSOR-cdc683f-...` | 11 | 9 | `policy:auto` |
| `CLAUDE-CURSOR-e9a6f4c-...` | 16 | **0** | `owner` |
| `CLAUDE-CURSOR-5633ee7-...` | 0 | 0 | `owner` |
| **`CLAUDE-CURSOR-65c3ac1-...` (cette PR)** | **14** | **0** | *(bloquée)* |

Deux détails aggravants, tous deux vérifiés : le gabarit officiel écrit
pourtant le verdict **sans gras** (`harness/audit_review.py` ligne 87), et
**aucun test ne couvre un verdict en gras** — `rg '\*\*CONFIRMED\*\*'
harness/tests/` ne renvoie rien. La suite de 311 tests est donc structurellement
aveugle à ce défaut.

### F2 — P0 — La PR transporte `architecture/audit-ledger.jsonl`, hors allowlist : elle ne peut pas être auto-fusionnée

Le seul job rouge le dit mot pour mot (run 31603131575, job 94135226805) :

```
Changed files:
architecture/audit-ledger.jsonl
architecture/reviews/CLAUDE-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md
##[error]PR touches path(s) outside .github/merge-bot.yaml's allow_paths -- refusing auto-merge:
architecture/audit-ledger.jsonl
```

`allow_paths` (`.github/merge-bot.yaml` lignes 29-32) liste `inbox/**`,
`reviews/**` et `briefs/**/feedback/**` — jamais le ledger.

**Élément nouveau, et il faut être précis pour ne pas répéter un motif déjà
tranché** (`review-guidelines.md` : pas de rubber-stamping inverse). La cause
générique *est déjà corrigée sur `master`* : le commit `8ebe5f9` (« la ligne
AUDIT_CHALLENGED s'écrit sur master après fusion, plus dans la PR de
challenge ») retire le ledger des PR de challenge, et nomme même la PR #31
comme cas réel. Le constat qui reste n'est pas la cause, c'est le **résidu** :
ce correctif est *en avant seulement*. `8ebe5f9` est bien ancêtre de la tête
auditée (§6.5) — la PR contient donc la règle qui interdit ce qu'elle fait
encore. Elle est périmée par rapport à sa propre base et sa propre chaîne ne
peut plus la fusionner. La sortie est une action d'exploitation (régénérer
la PR sans le ledger, ou retirer ce fichier du lot), pas un nouveau brief.

### F3 — P1 — Le champ `verdicts` du ledger n'est pas un décompte de points : c'est un comptage de mots

La ligne ajoutée au ledger annonce :

```json
"verdicts": {"CONFIRMED": 16, "REFUTED": 1, "PARTIAL": 4, "NEEDS_OWNER": 7}
```

Soit 28 verdicts pour un tableau de **14** lignes, dont **1 REFUTED alors
qu'aucun point n'est réfuté** (le corps de la revue le dit lui-même :
« Sur les 14 points vérifiés, 10 sont CONFIRMED »).

Ces chiffres ne sont pas inventés par l'agent : ils sont exactement ce que
produit `audit_review.parse_verdicts` (`harness/audit_review.py`
lignes 126-133), qui compte chaque occurrence du mot **partout dans le
fichier** — y compris la phrase de gabarit « Un verdict par point :
CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. » (ligne 75) et toute la prose
de synthèse. Reproduit au chiffre près en §6.1.

Le vrai problème est architectural : **deux analyseurs, deux contrats, sur le
même artefact.**

| | analyseur | comportement sur cette revue |
|---|---|---|
| Côté producteur (validation) | `parse_verdicts` — permissif | 28 « verdicts » → accepte |
| Côté consommateur (décision) | `_parse_point_verdicts` — strict | 0 ligne → refuse |

La garde fail-closed de `record_challenge` (lignes 173-178 : « a challenge
with no verdict is not a challenge ») est donc **placée au mauvais bout** :
elle laisse passer une revue que le consommateur ne peut pas utiliser. C'est
le motif documenté du parseur qui renvoie une liste vide et transforme une
panne en faux vert [S1], et la raison pour laquelle la littérature 2026
recommande une validation **de contrat**, pas une validation de syntaxe
tolérante [S4, S5]. Effet immédiat : le journal machine (lu par le tableau de
bord et par tout humain qui audite la boucle) est faux, alors même que la
décision, elle, est protégée.

### F4 — P1 — Aucune porte mécanique ne valide `architecture/reviews/**`

`audit_schema.py` ne valide que `inbox/` : champs requis, trois flags
`*_authorized` à `false`, SHA 40 hex, `status: PROPOSED`, `audit_id` égal au
nom de fichier (`harness/audit_schema.py` lignes 31-86). Rien d'équivalent
n'existe pour une revue : ni frontmatter, ni tableau analysable. C'est la
raison pour laquelle F1 et F3 traversent 14 vérifications vertes sans être
vus. La lentille 3 est claire : ce qu'une porte mécanique peut couvrir ne
doit pas dépendre du jugement d'un relecteur. Ici, la porte manque.

### F5 — P2 — Le journal « append-only » n'est plus en ordre chronologique

La ligne est **insérée au milieu** du fichier, pas ajoutée à la fin. Extrait
du diff (`architecture/audit-ledger.jsonl`) :

```
 {"timestamp": "2026-08-12T11:41:27Z", ... AUDIT_APPROVED ...}
+{"timestamp": "2026-08-12T12:01:05Z", ... AUDIT_CHALLENGED ...}
 {"timestamp": "2026-08-12T11:55:18Z", ... AUDIT_CHALLENGED ...}
 {"timestamp": "2026-08-12T12:30:17Z", ... AUDIT_CHALLENGED ...}
```

`12:01:05` précède `11:55:18`. C'est un artefact mécanique de la fusion de
`master` dans la branche, pas une faute de rédaction — mais le résultat est
qu'un fichier présenté comme append-only (`architecture/README.md`, règle
d'intégrité 3) ne s'append plus. Aucune garde ne l'attrape : `rg 'sorted|
chronolog|monoton' harness/tests/test_audit_ledger.py` ne renvoie aucune
assertion d'ordre. Impact aujourd'hui limité (l'état FSM se reconstruit par
`audit_id`, et cet audit n'a qu'une ligne — voir §6.4 qui rend bien
`AUDIT_CHALLENGED`), mais la lisibilité du journal et toute future lecture
par intervalle de temps sont atteintes. La littérature sur les boucles
autonomes insiste précisément sur l'intégrité du journal comme condition de
reprise correcte après incident [S3].

### F6 — P2 — Le challenger est structurellement aveugle à l'état de la CI, et ce trou coûte de l'information réelle

La revue classe son point 10 (« CI verte, 7/7 sur le commit audité ») en
`NEEDS_OWNER` en expliquant honnêtement : « pas d'authentification `gh` dans
cet environnement ». **J'ai cherché à réfuter cette excuse ; elle est
exacte** : l'étape « Invoke claude-challenger headless »
(`pipeline-challenge.yml` ligne 144) ne reçoit que
`CLAUDE_CODE_OAUTH_TOKEN`, `ANTHROPIC_API_KEY` et `AUDIT_ID` — aucun
`GH_TOKEN`, alors que d'autres étapes du même workflow en reçoivent un
(lignes 60 et 174). Le challenger ne peut donc pas voir la CI.

Le coût est mesurable. J'ai rejoué la vérification qu'il ne pouvait pas
faire (§6.6) : **7 runs sur `65c3ac1`, tous `success`** — l'affirmation de
l'audit était vraie, et ce point pouvait être `CONFIRMED`. Or en mode
`full_auto`, un `NEEDS_OWNER` sans `CONFIRMED`/`PARTIAL` déclenche la règle
`review_needs_owner_only` → `AUDIT_REJECTED` « policy: no owner in
full_auto » (`harness/pipeline/auto_policy.yaml` lignes 42-45). Un trou
d'outillage peut donc se transformer en rejet automatique. C'est le point
soulevé par [S2] : sur les modes d'échec latents, ce qui compte est la
détection et l'attribution, pas la relance à l'aveugle.

### F7 — P3 — Horodatage de complaisance dans le frontmatter de la revue

`reviewed_at: 2026-08-12T00:00:00Z` (ligne 5 du fichier ajouté) — minuit
pile, alors que la ligne de ledger écrite dans le même geste porte
`2026-08-12T12:01:05Z` et que le commit est daté `12:01:29Z`. Valeur de
remplissage, non fausse au jour près mais non informative. Aucune garde ne
la contrôle (voir F4).

## 5. Hypothèses testées et écartées

Le cadrage adverse (lentille 4) impose de dire aussi ce qui ne tient pas.

- **« La PR va créer une ligne de ledger en double, puisque `master`
  ré-écrit maintenant `AUDIT_CHALLENGED` après fusion. » — Écarté.**
  `handle_review_recorded` n'écrit la transition que si l'état est `None` ou
  `AUDIT_PROPOSED` (`harness/pipeline/orchestrator.py`, garde ajoutée par
  `8ebe5f9`). Après fusion l'état est déjà `AUDIT_CHALLENGED` (§6.4) : rien
  n'est ré-ajouté. La garde fait son travail.
- **« La PR casse la suite de tests. » — Écarté.** Le job `tests` est vert.
  Mon propre rejeu sur l'arbre extrait montre `1 failed, 310 passed, 16
  skipped`, mais l'unique échec
  (`test_verdict_audit_actor_identity.py::test_unseen_actor_name_...`) est un
  artefact de ma méthode : `git archive` produit un arbre sans `.git`, et le
  test appelle `git grep` (`returncode 128 = not a git repository`). Ce n'est
  pas une régression de la PR.
- **« Le challenger a affaibli sa vérification pour se simplifier la
  tâche. » — Écarté** (voir F6 : la limite d'accès est réelle et documentée
  dans le workflow).
- **« Le lot est trop gros / mal découpé. » — Écarté** (§2).

## 6. Commandes rejouées

### 6.1 Les deux analyseurs sur le même fichier

```
$ .venv/bin/python  # (harness/ dans sys.path, revue de la PR #31)
parse_verdicts (ce qui part dans le ledger) : {'CONFIRMED': 16, 'REFUTED': 1, 'PARTIAL': 4, 'NEEDS_OWNER': 7}
_parse_point_verdicts (ce qui decide APPROVED/REJECTED) : []
nb lignes de tableau reconnues : 0
nb lignes '| N |' presentes dans le fichier : 14
```

### 6.2 Taux d'analyse de toutes les revues existantes

```
CLAUDE-CURSOR-5633ee7-automation-completeness.md             lignes_num=  0 reconnues=  0
CLAUDE-CURSOR-73022bd-hermes-dashboard-modele-auditeur.md    lignes_num=  0 reconnues=  0
CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md           lignes_num= 20 reconnues= 19
CLAUDE-CURSOR-FIXTURE-full-auto-demo.md                      lignes_num=  1 reconnues=  1
CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md      lignes_num= 11 reconnues=  9
CLAUDE-CURSOR-e9a6f4c-codex-passation-full-auto.md           lignes_num= 16 reconnues=  0
```

Et l'acteur de chaque décision enregistrée :

```
CURSOR-FIXTURE-full-auto-demo                    AUDIT_APPROVED  actor=policy:auto
CURSOR-5633ee7-automation-completeness           AUDIT_APPROVED  actor=owner
CURSOR-e9a6f4c-codex-passation-full-auto         AUDIT_APPROVED  actor=owner
CURSOR-cdc683f-hermes-workflow-quatre-acteurs    AUDIT_APPROVED  actor=policy:auto
```

### 6.3 L'événement post-fusion est bien résolu

```
$ printf 'architecture/reviews/CLAUDE-CURSOR-65c3ac1-...md\n' \
  | python harness/pipeline/trigger_resolve.py resolve --ledger architecture/audit-ledger.jsonl ...
event=review_recorded
payload={"audit_id": "CURSOR-65c3ac1-dashboard-hermes-modele-auditeur"}
```

### 6.4 …et l'orchestrateur échoue ensuite

```
etat FSM apres fusion : AUDIT_CHALLENGED
decide_auto a LEVE DecisionError: /tmp/repo31/architecture/reviews/CLAUDE-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md
  has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
```

### 6.5 La PR contient déjà la règle qu'elle enfreint

```
$ git merge-base --is-ancestor 8ebe5f9 pr31 && echo OUI
OUI
$ git rev-parse pr31^2
7e5244b26cded2905b095769deb6200f771dab46
```

### 6.6 La vérification CI que le challenger ne pouvait pas faire

```
$ gh run list --commit 65c3ac1c85c24cc61265c7f9ec4989cc67a0b4f9
hermes-observer   success   |   hermes-observer   success
security          success   |   pipeline-audit     success
hermes-dashboard  success   |   audit-guard        success
harness-ci        success
=> 7 runs, 7 success
```

## 7. Propositions (entrées pour la boucle — aucune n'est une instruction)

Trois pistes au maximum, comme l'exige le contrat. Aucune n'autorise quoi
que ce soit : seul le propriétaire, ou le policy engine, peut les convertir
en brief, et le brief resterait alors la source unique d'instruction.

1. **Un seul analyseur de verdicts, et une porte mécanique sur
   `architecture/reviews/**`** (couvre F1, F3, F4). Faire lire la même
   fonction au producteur et au consommateur, et refuser une revue dont le
   tableau ne s'analyse pas — au moment où elle est écrite, pas après
   fusion. Un test qui échoue sur `| **CONFIRMED** |` et passe sur
   `| CONFIRMED |` serait la démonstration attendue par la lentille 2.
2. **Rendre l'ordre du ledger vérifiable** (couvre F5) : une garde
   d'ordre chronologique croissant, pour qu'un journal dit append-only le
   soit mécaniquement.
3. **Donner au challenger la lecture de l'état CI** (couvre F6) : un jeton
   en lecture seule, ou l'état de la CI injecté dans son contexte, pour que
   les points « CI » cessent d'être renvoyés au propriétaire par défaut.

F2 ne demande pas de brief : la cause est déjà corrigée par `8ebe5f9`, il
reste une action d'exploitation sur cette PR précise.

## 8. Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | DEV Community — *Empirical Failure Modes in Autonomous Agent Operations* (144 cycles ; « empty list `[]` … a fatal method call error into a plausible false-green result ») — <https://dev.to/adevbelgium/empirical-failure-modes-in-autonomous-agent-operations-25k4> | 2026-08-12 |
| S2 | arXiv 2608.05263 — *OrchestraBench: Evaluating Multi-Agent Orchestration Failure Modes, Recovery, and Decomposition Quality* (« detection/attribution, not blind retry, is the necessary containment mechanism ») — <https://arxiv.org/abs/2608.05263v1> | 2026-08-12 |
| S3 | Heemeng Foo — *AI Dev Summit 2026: The Model Wasn't the Only Problem* (juin 2026 ; l'« intent ledger » et la continuité hors du processus modèle) — <https://heemeng.medium.com/ai-dev-summit-2026-the-model-wasnt-the-only-problem-4f10db15f596> | 2026-08-12 |
| S4 | Tian Pan — *JSON Mode Won't Save You: Structured Output Failures in Production LLM Systems* (2026-04-09 ; validation en couches, la conformité de forme ne garantit pas l'usage) — <https://tianpan.co/blog/2026-04-09-structured-output-failures-production-llm> | 2026-08-12 |
| S5 | Collin Wilkins — *LLM Structured Outputs: Schema Validation for Real Pipelines (2026)* — <https://collinwilkins.com/articles/structured-output> | 2026-08-12 |
| S6 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers* (2026 ; « buggy agent code can skip its own budget check » — argument d'enforcement externe à l'agent, cohérent avec `ci_budget_guard.py precheck`/`record`) — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026-08-12 |

## 9. Portée

Audit en lecture seule. Aucun chemin hors `architecture/inbox/**` n'est
touché par la PR qui le dépose. Les trois flags `*_authorized` sont à
`false` : ce document ne s'attribue aucune autorité d'exécution.
