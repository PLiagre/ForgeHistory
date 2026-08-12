---
audit_id:                CURSOR-7e5244b-ledger-post-fusion-poussee-master
auditor:                 cursor-cloud
target_branch:           master
target_commit:           7e5244b26cded2905b095769deb6200f771dab46
created_at:              2026-08-12T13:48:50Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---
# Audit du merge 7e5244b — la ligne AUDIT_CHALLENGED déplacée vers une poussée directe sur `master`

Audit post-fusion du rôle `cursor-auditor`
(`architecture/agents/cursor-auditor.md`), avec `cursor-qa-scout`
(`architecture/agents/cursor-qa-scout.md`) en compagnon de session : sa veille
occupe la section « Veille externe » de ce même fichier, comme son contrat le
prévoit.

**Un audit n'instruit rien.** Ce fichier est une *entrée* pour
`claude-challenger`, puis pour le propriétaire (`architecture/README.md`,
ADR-0005 / ADR-0006). Aucun constat ci-dessous n'est un ordre, aucun brief
proposé n'est pré-autorisé, et les trois flags `*_authorized` du frontmatter
valent `false`.

## Résumé en une page

Le merge corrige un vrai blocage : la PR de contre-audit emportait
`architecture/audit-ledger.jsonl`, un chemin que l'allowlist du merge-bot n'a
jamais contenu — donc toute PR de challenge était refusée à la fusion
automatique. Le commit retire cette ligne de la PR et confie l'écriture de
`AUDIT_CHALLENGED` à `pipeline-orchestrate.yml`, après fusion, sur `master`.
L'intention est lisible, le module qui possède la transition est respecté
(`audit_review.record_challenge`, gardes incluses), et deux tests neufs
couvrent les deux formes de PR (avant / après changement).

Le problème est la **destination** choisie. `pipeline-orchestrate.yml` écrit sur
`master` par un `git push` nu : pas de groupe `concurrency`, pas de
`pull --rebase`, pas de nouvelle tentative, pas de repli en pull request. Ce
chemin a déjà perdu une écriture réelle il y a une heure (course perdue de 11
secondes contre `hermes-dashboard`, décision d'audit disparue du dépôt), et la
dernière poussée directe observée sur `master` — sur la poussée de ce merge
même — a été refusée par la protection de branche (`GH006`). Le commit déplace
donc une transition obligatoire de la machine à états vers un chemin dont la
fiabilité est, ce jour, mesurée à zéro.

| sévérité | nombre | objet |
|---|---|---|
| P0 | 1 | la nouvelle écriture de `AUDIT_CHALLENGED` dépend d'une poussée directe sur `master` qui, en l'état, est refusée par la protection de branche et perd déjà des écritures |
| P1 | 2 | la moitié « workflow » du changement n'est couverte par aucun test alors que le dépôt sait tester la forme d'un workflow ; deux contrats de rôle décrivent encore l'ancienne circulation de la ligne |
| P2 | 2 | garde de portée dissymétrique entre `pipeline-challenge.yml` et `pipeline-orchestrate.yml` ; deux lecteurs d'état concurrents sur la même décision |
| P3 | 2 | l'escalade d'échec ne journalise que dans le log du run ; `hermes-observer` s'auto-amplifie (39 runs en 5 minutes sur un runner auto-hébergé) |

## Ce que le merge change

`git diff --stat cd1dcd2..7e5244b` :

```
 .github/workflows/pipeline-challenge.yml | 14 +++++++--
 docs/rules/full-auto-pipeline.md         |  9 ++++--
 harness/pipeline/orchestrator.py         | 46 ++++++++++++++++++++++++----
 harness/tests/test_orchestrator.py       | 51 ++++++++++++++++++++++++++++++++
 4 files changed, 109 insertions(+), 11 deletions(-)
```

Quatre fichiers, +109/-11, un seul parent fusionné (`8ebe5f9`) : la taille est
très en dessous du seuil au-delà duquel une relecture honnête décroche
(`architecture/review-guidelines.md` § 5). L'intention est écrite dans le diff
lui-même, en commentaire, aux trois endroits concernés — c'est la bonne
pratique et elle est tenue.

Mécanique, en trois pièces :

1. `pipeline-challenge.yml:185` — `git checkout -- architecture/audit-ledger.jsonl || true`
   jette la ligne que le gate `record` a écrite pendant l'invocation ; la PR
   ne porte plus que `architecture/reviews`.
2. `orchestrator.py:169-171` — sur `review_recorded`, si l'état du ledger est
   `None` ou `AUDIT_PROPOSED`, l'orchestrateur écrit lui-même
   `AUDIT_CHALLENGED` via `audit_review.record_challenge` (donc via
   `audit_ledger.append_event`, jamais une ligne JSON à la main), puis décide.
3. `docs/rules/full-auto-pipeline.md:30-45` — le schéma du pipeline est mis à
   jour en conséquence.

Le raisonnement affiché (« deux challenges simultanés entreraient en conflit
d'append sur le ledger partagé ») est vérifiable et **la deuxième moitié de sa
justification est exacte** : l'allowlist du merge-bot
(`.github/workflows/merge-bot.yml:53`) autorise
`architecture/inbox/`, `architecture/reviews/` et
`harness/queue/briefs/*/feedback/` — jamais `architecture/audit-ledger.jsonl`.
Toute PR de challenge portant le ledger faisait donc échouer le job
`check-and-automerge` et devait être fusionnée à la main (les deux derniers
commits de challenge, `ae66c1a` et `8319f55`, portent bien le ledger). Ce
commit débloque réellement ce maillon.

## État du dépôt au SHA audité

- Suite de tests du harnais : **311 passés, 16 ignorés** (les cas Unity, hors
  Linux). Commande et sortie en annexe.
- 14 audits dans `architecture/inbox/` : 4 `AUDIT_PROPOSED`, 2
  `AUDIT_CHALLENGED`, 1 `AUDIT_APPROVED`, 7 `AUDIT_ARCHIVED`.
- `architecture/decisions/` contient 4 décisions. Une cinquième,
  `DECISION-CURSOR-779d97c-revue-verdicts-illisibles.md`, a été **écrite puis
  perdue** en CI (voir P0-1) : elle est absente du dépôt.
- `git status --short` : propre.

## CI du commit audité — classée

Poussée du merge sur `master` (5 workflows déclenchés) :

| workflow | run | conclusion |
|---|---|---|
| `harness-ci` | 31602793471 | success |
| `pipeline-audit` | 31602793384 | success |
| `audit-guard` | 31602793404 | success |
| `security` | 31602793402 | success |
| **`hermes-dashboard`** | **31602793423** | **failure** |
| `pipeline-failure-escalate` | 31602834190 | skipped |

**Verte sauf un job**, et l'échec n'est pas cosmétique : c'est exactement le
mécanisme dont le commit vient de rendre le ledger dépendant. Log du run
31602793423, étape « Commit and push (hermes) », `2026-08-12T13:42:35` :

```
remote: error: GH006: Protected branch update failed for refs/heads/master.
remote: - 5 of 5 required status checks are expected.
 ! [remote rejected] master -> master (protected branch hook declined)
error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
```

`pipeline-challenge` et `pipeline-orchestrate` ne se déclenchent pas sur ce
merge (leurs filtres de chemins ne matchent pas `harness/**` ni
`.github/workflows/**`) : **le nouveau chemin d'écriture n'a donc encore jamais
tourné en CI.** Le P0 ci-dessous est établi par la mécanique du workflow et par
deux runs réels du même mécanisme, pas par une exécution du chemin neuf.

---

# Constats

## P0-1 — `AUDIT_CHALLENGED` dépend maintenant d'une poussée directe sur `master` qui perd déjà des écritures et que la protection de branche refuse

**Le fait.** `pipeline-orchestrate.yml:106-118` termine par :

```
git add architecture/audit-ledger.jsonl architecture/decisions harness/queue/briefs
git commit -m "pipeline-orchestrate: ${{ steps.resolve.outputs.event }}"
git push
```

Aucun bloc `concurrency:` dans ce fichier (`rg -n "concurrency" .github/workflows/`
ne remonte que `hermes-dashboard.yml:28` et `hermes-observer.yml:25`), aucun
`git pull --rebase` avant la poussée (`hermes-dashboard.yml:102` en a un), aucune
nouvelle tentative, aucun repli par pull request, et le `checkout` (lignes
50-52) n'utilise pas `FORGE_BOT_PAT` — c'est le `GITHUB_TOKEN` par défaut, le
même que celui qui vient d'être refusé.

**Preuve 1 — la course est déjà perdue une fois, et une décision a disparu.**
Run `31597010007` (`pipeline-orchestrate`, push, 2026-08-12T12:33:26,
**failure**), log de l'étape de commit :

```
 create mode 100644 architecture/decisions/DECISION-CURSOR-779d97c-revue-verdicts-illisibles.md
To https://github.com/PLiagre/ForgeHistory
 ! [rejected]        master -> master (fetch first)
error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
hint: Updates were rejected because the remote contains work that you do not
hint: have locally. This is usually caused by another repository pushing to
hint: the same ref.
```

L'« autre dépôt qui pousse sur la même ref » est identifiable : `hermes` a
poussé `dd16d76` (« tableau de bord régénéré ») à `12:33:52`, **11 secondes
avant** la poussée de l'orchestrateur à `12:34:03`. Conséquences vérifiables
dans le dépôt au SHA audité :

- `ls architecture/decisions/` → 4 fichiers, `DECISION-CURSOR-779d97c-…md`
  **absent** ;
- `rg -n "779d97c" architecture/audit-ledger.jsonl` → **une seule ligne**,
  `AUDIT_CHALLENGED` du `12:30:17Z` ; aucun `AUDIT_APPROVED`/`AUDIT_REJECTED` ;
- `git log --all --oneline --grep="pipeline-orchestrate:"` → un seul commit
  (`9ee112d`), celui du 11:41 ; le travail du run de 12:34 n'existe nulle part.

L'audit `CURSOR-779d97c` est donc **coincé à `AUDIT_CHALLENGED`**, sa décision
calculée puis jetée. Cet incident précède le commit audité ; ce que le commit
change, c'est qu'il ajoute la ligne `AUDIT_CHALLENGED` — une transition
**obligatoire** de la FSM — à la charge de cette même poussée.

**Preuve 2 — la poussée directe est aujourd'hui refusée tout court.** Le
`GH006` cité en § CI vient d'un workflow qui pousse sur `master` avec le même
jeton et le même geste, et son `git pull --rebase` ne l'a pas sauvé (le log dit
`Current branch master is up to date` juste avant le rejet). La protection a
changé récemment : les poussées directes de `hermes` ont réussi à `12:25:27`,
`12:25:49`, `12:33:52`, `12:40:08`, `12:56:12`, `12:56:33`, et échouent à
`13:42:35`. Je n'ai pas pu lire la configuration exacte
(`gh api repos/PLiagre/ForgeHistory/branches/master/protection` → HTTP 403,
`Resource not accessible by integration`), donc je ne peux pas nommer les 5
contrôles requis ; le refus lui-même, lui, est cité mot pour mot.

**Pourquoi c'est P0 et pas P1.** Tant que la protection reste telle
qu'observée à `13:42:35`, `pipeline-orchestrate.yml` ne peut plus rien écrire
sur `master` : ni la décision, ni le brief-seed, ni — depuis ce commit — la
ligne `AUDIT_CHALLENGED`. La boucle « full-auto » s'arrête à la fusion du
contre-audit. Le commit est cohérent avec lui-même, mais il rend une
transition de la machine à états dépendante d'un chemin dont la fiabilité
mesurée ce jour est nulle. À noter : ce mode d'échec est **rattrapable par
relance** (rien n'ayant été écrit, l'état reste `AUDIT_PROPOSED` et un
`workflow_dispatch` réécrit la même ligne) — c'est la seule raison pour
laquelle il ne corrompt pas le ledger ; il l'arrête.

## P1-2 — la moitié « workflow » du changement n'est testée par rien, alors que le dépôt sait le faire

Les deux tests ajoutés (`test_orchestrator.py:104-155`) sont bons et couvrent
les deux formes de PR (avec et sans la ligne de ledger) plus le refus quand
aucune revue n'existe. Mais l'invariant que ce commit crée vit dans le YAML :
« la PR de challenge ne porte que `architecture/reviews/**` ». Rien ne le
retient. Un futur ré-ajout de `architecture/audit-ledger.jsonl` au `git add` de
`pipeline-challenge.yml:194` remettrait le refus du merge-bot sans faire rougir
un seul test. La FSM éviterait le pire — un deuxième `AUDIT_CHALLENGED` est
illégal (`audit_ledger.py:87`) et l'orchestrateur sauterait l'écriture — mais la
régression se manifesterait par un job de merge-bot rouge, sans qu'aucun test
n'ait su nommer la cause.

Ce n'est pas une exigence hors sol : le dépôt teste déjà la forme d'un
workflow. `harness/tests/test_merge_bot_policy.py:17` lit
`.github/workflows/merge-bot.yml` et vérifie son texte. Le même geste
appliqué à `pipeline-challenge.yml` (le `checkout --` du ledger présent, le
`git add` limité aux revues) coûte quelques lignes.

## P1-3 — deux contrats de rôle décrivent encore l'ancienne circulation de la ligne

`docs/rules/full-auto-pipeline.md` a été mis à jour ; les contrats de rôle non.

- `architecture/agents/claude-challenger.md:22-25` (§ Sorties) : « Ledger
  `AUDIT_CHALLENGED` (écrit exclusivement par `audit_review.record_challenge`…) »
  et `:50-52` (§ Preuve de fin) : « …le ledger contient un nouvel événement
  `AUDIT_CHALLENGED` pour cet `audit_id` ». Depuis ce commit, en CI, cette
  écriture est **systématiquement jetée** (`pipeline-challenge.yml:185`) : la
  preuve de fin du rôle n'est plus observable dans son propre passage.
- `architecture/agents/pipeline-orchestrator.md:23-29` (§ Sorties) ne mentionne
  pas qu'il possède désormais la transition `PROPOSED → CHALLENGED`. Le § Sorties
  reste formellement vrai (« appends au ledger exclusivement via
  `append_event` »), mais la table « un rôle, une écriture » de
  `architecture/README.md:27-33` devient moins lisible : l'événement dont le
  sens est « Claude a rendu son contre-audit » est maintenant écrit par la
  machine, sur un autre déclencheur, dans un autre run.

Le dépôt s'interdit la paraphrase (`CLAUDE.md` › Single Source of
Instruction) ; ici il ne s'agit pas de paraphrase mais de **contrats qui
décrivent une circulation qui n'existe plus**. C'est le genre d'écart qui
transforme, plus tard, une lecture de contrat en fausse piste.

## P2-4 — garde de portée dissymétrique entre les deux workflows

`pipeline-orchestrate.yml:94-104` refuse de committer si le diff sort de son
allowlist, et le dit en `::error::`. `pipeline-challenge.yml`, lui, fait
l'inverse au même endroit du cycle : il **jette en silence**. Le
`git checkout -- architecture/audit-ledger.jsonl || true` est délibéré et
documenté, mais il est suivi d'un `git add architecture/reviews` seul : toute
autre modification laissée par l'invocation headless (par exemple
`harness/pipeline/ci-budget-ledger.jsonl`, un fichier de `inbox/`, du code)
disparaît avec le runner, sans `::warning::`, sans échec. Une invocation qui
dérape hors de son périmètre est précisément ce qu'un audit doit pouvoir
constater après coup ; là, il n'en reste aucune trace.

## P2-5 — deux lecteurs d'état concurrents sur la même décision

`orchestrator.py:169` lit l'état par `audit_ledger.current_state_for`, qui rend
`None` quand l'audit n'a aucun événement. `audit_review.record_challenge:153`
le relit par `audits.current_state`, qui rend `AUDIT_PROPOSED` dans ce même cas
(`audits.py:44`, `DEFAULT_STATE`). Les deux ne s'accordent aujourd'hui que
parce que le handler énumère le couple à la main : `state in (None,
"AUDIT_PROPOSED")`.

Conséquence pratique : tout autre état saute l'écriture du challenge sans le
dire. `AUDIT_STALE` est un successeur légal de `AUDIT_PROPOSED`
(`audit_ledger.py:86`) ; sur un audit devenu `STALE`, le handler passe
directement à `decide_auto`, qui échoue avec un message parlant de la
*décision* (« is AUDIT_STALE, not AUDIT_CHALLENGED ») et jamais du challenge
manquant. Le comportement est fail-closed — c'est bien — mais le diagnostic
qu'il laisse au lecteur désigne la mauvaise étape.

## P3-6 — l'escalade d'échec n'écrit que dans le log du run qui échoue

`pipeline-failure-escalate.yml` surveille bien les quatre workflows `pipeline-*`
et se déclenche sur `failure`, mais son unique action est
`orchestrator.py run --event pipeline_job_failed`, dont l'effet est
`escalate_pipeline_stuck` **journalisé** (le fichier le dit lui-même, lignes
14-18 : « Log-only… no real `gh issue create` call here »). C'est pour cela que
la perte de la décision `CURSOR-779d97c` à 12:34 n'a alerté personne. Constat
informatif, non causé par ce commit, mais il conditionne la gravité du P0 :
l'arrêt de la boucle est silencieux par construction.

## P3-7 — `hermes-observer` s'auto-amplifie

`gh run list --workflow hermes-observer.yml --limit 40`, regroupé à la minute :
`13:41` → 12 runs, `13:42` → 10, `13:45` → 1, `13:46` → 9, `13:47` → 8. Soit 40
runs en 5 minutes, chacun de 2 à 3 minutes, sur un runner **auto-hébergé
Windows** unique (`runs-on: [self-hosted, Windows, X64, hermes-observer]`). Le
déclencheur couvre les 9 workflows du dépôt en `workflow_run: completed`, plus
`pull_request_target`. Hors sujet du diff audité, mais c'est le poste de coût
CI le plus visible du dépôt au SHA audité, et le `concurrency` en place
(`group: …${{ github.event.workflow_run.id }}`) ne dédoublonne rien puisque la
clé change à chaque run observé.

---

# Veille externe — `cursor-qa-scout`

Comparaison repo ↔ état de l'art sur deux des trois axes cités par le brief
006 (files d'attente de fusion GitHub Actions ; plafonds de coût). Chaque
source porte URL + date de consultation.

## Axe « merge queues / écriture d'un bot sur une branche protégée »

L'état de l'art est net et il pointe exactement le P0-1 : un job CI **n'écrit
pas directement sur une branche protégée** ; il ouvre une PR, ou il produit
son artefact dans la PR d'origine. Les deux incidents publics ci-dessous sont
le même message d'erreur que le run 31602793423, dans un autre dépôt, avec la
même conclusion — « la correction est structurelle : ouvrir une PR au lieu de
pousser sur `main` ».

| # | source | consulté le |
|---|---|---|
| S1 | GitHub Docs — *Managing a merge queue* — <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue> | 2026-08-12 |
| S2 | ksail (devantler-tech) — *CI Failure Investigation — Run #8440 : Auto-Commit Push Rejected by Branch Protection*, issue #3467 — <https://github.com/devantler-tech/ksail/issues/3467> | 2026-08-12 |
| S3 | ksail (devantler-tech) — *Run #11528 — Auto-Commit Push Blocked by Branch Protection*, issue #4178 (`GH013`, « Changes must be made through a pull request ») — <https://github.com/devantler-tech/ksail/issues/4178> | 2026-08-12 |

Deux nuances utiles au propriétaire, tirées de S1 : une file d'attente de
fusion exige que **tous** les workflows requis écoutent aussi l'événement
`merge_group`, sinon la file se bloque indéfiniment ; et elle est incompatible
avec une règle de protection dont le motif de branche contient `*`. Ce n'est
donc pas un réglage gratuit — c'est une entrée de décision, pas une
recommandation exécutable.

## Axe « orchestration déterministe d'agents »

| # | source | consulté le |
|---|---|---|
| S4 | Praetorian — *Deterministic AI Orchestration: A Platform Architecture for Autonomous Development* — <https://www.praetorian.com/blog/deterministic-ai-orchestration-a-platform-architecture-for-autonomous-development/> | 2026-08-12 |
| S5 | Salesforce Engineering — *Building Enterprise AI Agents That Are Both Autonomous and Reliable* (« guided determinism ») — <https://engineering.salesforce.com/building-enterprise-ai-agents-that-are-both-autonomous-and-reliable/> | 2026-08-12 |

Sur cet axe, **le dépôt est en avance** : la couche d'orchestration est du code
déterministe (`orchestrator.py` + `auto_policy.yaml` versionnée), le LLM ne
décide rien dans le workflow, et la trace est immuable
(`architecture/audit-ledger.jsonl`). S4 et S5 recommandent tous deux
exactement ce partage. La faiblesse constatée ici n'est pas dans le
raisonnement de la boucle, elle est dans sa **couche de persistance** : les
deux sources insistent sur une piste d'audit immuable et fiable, et c'est
précisément l'écriture de cette piste qui échoue sans que personne soit averti
(P0-1 + P3-6).

## Axe « plafonds de coût / budget de jetons »

| # | source | consulté le |
|---|---|---|
| S6 | *AI Agent Token Budget Enforcement [2026]* — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026-08-12 |
| S7 | *How to Enforce a Token Budget on an AI Agent (Not Just Measure It)* — <https://dreaming.press/posts/how-to-enforce-a-token-budget-on-an-ai-agent.html> | 2026-08-12 |

La distinction que ces deux sources martèlent — « un compteur n'est pas un
frein » — s'applique au dépôt, mais elle est **déjà couverte par une revue
existante** (`CLAUDE-CURSOR-779d97c…`, point 16 : le coût mesuré est jeté parce
que `ci-budget-ledger.jsonl` n'est jamais committé). Je ne rouvre donc pas ce
point : ce serait du bruit (`architecture/review-guidelines.md` § « pas de
rubber-stamping inverse »). Il est cité ici seulement parce qu'il renforce le
P0-1 : le fichier de budget perdu et la décision perdue ont la même cause
mécanique, une écriture qui ne survit pas au runner.

## Doublons avec les briefs ouverts

Briefs vérifiés (sans `deliverables/verdict.md`, donc ouverts) :
`008-full-auto-automation-gaps`, `008-contexte-opus5-right-sizing`,
`009-full-auto-agent-invocation`, `010-repartition-roles-full-auto`.

`rg -ln "git push|concurrency|branch protection|protected branch|GH006|fetch first" harness/queue/briefs/*/brief.md`
→ **aucune correspondance**. **Aucun doublon avec un brief ouvert** pour les
constats P0-1, P1-2, P1-3, P2-4, P2-5. Le P3-6 (escalade log-only) est un
**Non-Goal explicite** du brief 008 (`pipeline-failure-escalate.yml:14-18`) :
il est cité comme contexte, pas proposé comme travail. Le P3-7
(`hermes-observer`) n'est couvert par aucun brief.

---

# Briefs proposés (≤ 3, aucun pré-autorisé)

Propositions, pas instructions. Seul le propriétaire peut convertir un point
retenu en brief, et le brief devient alors la source unique d'instruction.

## Proposition A — rendre durable l'écriture du pipeline sur `master` (couvre P0-1)

Objet : que la ligne de ledger, la décision et le brief-seed survivent à une
poussée concurrente **et** à la protection de branche. Deux options à
arbitrer, pas une solution imposée : (a) l'orchestrateur ouvre une PR
`forge-bot/*` (le ledger devrait alors entrer dans l'allowlist du merge-bot —
ce qui rouvre la question de conflit que ce commit voulait fermer), ou (b) il
garde la poussée directe mais gagne un groupe `concurrency`, un
`pull --rebase` et une relance bornée, ce qui ne suffira pas tant que la
protection refuse la poussée. Inclure la reprise de l'état perdu : audit
`CURSOR-779d97c` coincé à `AUDIT_CHALLENGED`, décision absente. Preuve de fin
attendue : un run vert qui écrit sur `master` pendant qu'un autre workflow
pousse, et un test qui échoue si la poussée redevient nue.

## Proposition B — épingler la forme des workflows et réaligner les contrats (couvre P1-2, P1-3)

Objet : un test qui lit `pipeline-challenge.yml` et échoue si la PR de
challenge peut de nouveau emporter `architecture/audit-ledger.jsonl` (idiome
déjà présent : `test_merge_bot_policy.py:17`), plus la mise à jour de
`architecture/agents/claude-challenger.md` (§ Sorties, § Preuve de fin) et de
`architecture/agents/pipeline-orchestrator.md` (§ Sorties) pour décrire qui
écrit `AUDIT_CHALLENGED`, où, et à quel déclencheur.

## Proposition C — symétriser les gardes de portée et unifier le lecteur d'état (couvre P2-4, P2-5)

Objet : `pipeline-challenge.yml` refuse (ou au minimum signale en
`::warning::`) tout diff hors `architecture/reviews/**` au lieu de le jeter en
silence, sur le modèle de `pipeline-orchestrate.yml:94-104` ; et une seule
fonction répond à « dans quel état est cet audit », de sorte que le couple
`(None, "AUDIT_PROPOSED")` n'ait plus à être énuméré à la main dans
`orchestrator.py`.

---

# Annexe — commandes rejouées, sorties collées

```
$ git diff --stat cd1dcd210441d220168cbaacf620bf90288f3e55..7e5244b26cded2905b095769deb6200f771dab46
 .github/workflows/pipeline-challenge.yml | 14 +++++++--
 docs/rules/full-auto-pipeline.md         |  9 ++++--
 harness/pipeline/orchestrator.py         | 46 ++++++++++++++++++++++++----
 harness/tests/test_orchestrator.py       | 51 ++++++++++++++++++++++++++++++++
 4 files changed, 109 insertions(+), 11 deletions(-)

$ git rev-list --parents -n 1 7e5244b2...
7e5244b26cded2905b095769deb6200f771dab46 cd1dcd210441d220168cbaacf620bf90288f3e55 8ebe5f998ec010ec124bd9d876deb8ba829a825a

$ .venv/bin/python -m pytest harness/tests/ -q
311 passed, 16 skipped in 17.01s

$ python3 harness/audits.py list
14 audit(s) in architecture/inbox/
[AUDIT_PROPOSED]  (4) ... [AUDIT_CHALLENGED]  (2) ... [AUDIT_APPROVED]  (1) ... [AUDIT_ARCHIVED]  (7)

$ ls architecture/decisions/
DECISION-CURSOR-5633ee7-automation-completeness.md
DECISION-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md
DECISION-CURSOR-FIXTURE-full-auto-demo.md
        (DECISION-CURSOR-779d97c-revue-verdicts-illisibles.md absent -- cf. P0-1)

$ rg -n "779d97c" architecture/audit-ledger.jsonl
30:{"timestamp": "2026-08-12T12:30:17Z", "audit_id": "CURSOR-779d97c-revue-verdicts-illisibles", "event": "AUDIT_CHALLENGED", ...}

$ git log --all --oneline --grep="pipeline-orchestrate:"
9ee112d pipeline-orchestrate: review_recorded

$ rg -n "concurrency|git pull --rebase" .github/workflows/
.github/workflows/hermes-dashboard.yml:28:concurrency:
.github/workflows/hermes-dashboard.yml:102:          git pull --rebase origin master
.github/workflows/hermes-observer.yml:25:concurrency:

$ rg -n "git push" .github/workflows/
.github/workflows/hermes-dashboard.yml:103:          git push origin master
.github/workflows/pipeline-orchestrate.yml:117:            git push
.github/workflows/pipeline-forge-run.yml:237:          git push -u origin "$branch"
.github/workflows/pipeline-challenge.yml:196:          git push -u origin "$branch"

$ gh run list --commit 7e5244b26cded2905b095769deb6200f771dab46
success  harness-ci | success pipeline-audit | success security | success audit-guard
failure  hermes-dashboard (31602793423) | skipped pipeline-failure-escalate

$ gh run view 31602793423 --log-failed
remote: error: GH006: Protected branch update failed for refs/heads/master.
remote: - 5 of 5 required status checks are expected.
 ! [remote rejected] master -> master (protected branch hook declined)

$ gh run view 31597010007 --log-failed
 create mode 100644 architecture/decisions/DECISION-CURSOR-779d97c-revue-verdicts-illisibles.md
 ! [rejected]        master -> master (fetch first)
hint: ... This is usually caused by another repository pushing to the same ref.

$ git log --format='%h | %ad | %an | %s' --date=iso -6 -- hermes/DASHBOARD.md
3807764 | 2026-08-12 12:56:33 +0000 | hermes | hermes: tableau de bord régénéré
ad5ac91 | 2026-08-12 12:56:12 +0000 | hermes | hermes: tableau de bord régénéré
8c3f0c5 | 2026-08-12 12:40:08 +0000 | hermes | hermes: tableau de bord régénéré
dd16d76 | 2026-08-12 12:33:52 +0000 | hermes | hermes: tableau de bord régénéré
1074d95 | 2026-08-12 12:25:49 +0000 | hermes | hermes: tableau de bord régénéré
c80f0a4 | 2026-08-12 12:25:27 +0000 | hermes | hermes: tableau de bord régénéré

$ gh api repos/PLiagre/ForgeHistory/branches/master/protection
HTTP 403 -- Resource not accessible by integration (configuration non lisible ici)

$ rg -ln "git push|concurrency|branch protection|protected branch|GH006|fetch first" harness/queue/briefs/*/brief.md
(aucune correspondance)
```

# Limites de cet audit

- Le nouveau chemin d'écriture (`review_recorded` → `record_challenge` sur
  `master`) **n'a pas tourné en CI** depuis la fusion : le P0-1 est établi par
  la forme du workflow et par deux runs réels du même geste
  (`hermes-dashboard` 31602793423, `pipeline-orchestrate` 31597010007), pas par
  une exécution du chemin neuf.
- La configuration de protection de `master` n'est pas lisible avec le jeton
  disponible ici (HTTP 403) : je cite le refus, pas la règle. Le nombre « 5
  contrôles requis » vient du message d'erreur, pas de l'API.
- Le budget d'appels du contrat (`≤ 60`) est respecté : cet audit a consommé
  environ 30 appels outils, veille externe incluse.
