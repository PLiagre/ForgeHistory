---
audit_id:                CURSOR-063d7eb-pr35-challenge-perte-decision
auditor:                 cursor-cloud
target_branch:           master
target_commit:           063d7ebaba561eef452c579879be013831bc80b4
created_at:              2026-08-12T12:45:00Z
audit_type:              pr-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #35 — « challenge: revue de l'audit CURSOR-779d97c-revue-verdicts-illisibles »

Critique de <https://github.com/PLiagre/ForgeHistory/pull/35> selon
`architecture/review-guidelines.md` (six lentilles, sévérités P0–P3, une
preuve citée par constat). Ce fichier **n'instruit rien** : il propose, la
décision reste à la boucle (`architecture/README.md`, ADR-0005/0006).

## 1. Provenance et périmètre

| Élément | Valeur mesurée |
|---|---|
| PR | #35, `forge-bot/review-CURSOR-779d97c-revue-verdicts-illisibles-31596321701` → `master` |
| Auteur | `app/github-actions` (bot), produite par le run `31596321701` de `pipeline-challenge.yml` |
| Fusionnée par | `PLiagre` (humain), le 2026-08-12T12:33:24Z |
| Commit de fusion audité | `063d7ebaba561eef452c579879be013831bc80b4` |
| Taille du diff | 2 fichiers, +117 / −0 |
| Fichiers touchés | `architecture/audit-ledger.jsonl` (+1 ligne), `architecture/reviews/CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md` (nouveau, 116 lignes) |

Vérifications de provenance rejouées :

```
$ git rev-parse HEAD
063d7ebaba561eef452c579879be013831bc80b4
$ gh pr diff 35 --name-only
architecture/audit-ledger.jsonl
architecture/reviews/CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md
$ gh pr view 35 --json mergedAt,mergedBy -q '.mergedAt, .mergedBy.login'
2026-08-12T12:33:24Z
PLiagre
```

Environnement de cet audit : `gh` **authentifié** (contrairement à
l'environnement de la revue auditée, cf. P1-4 plus bas), ce qui permet de
lever les deux points que la revue avait dû laisser en `NEEDS_OWNER`.

## 2. Classification CI du commit audité

Sur le commit de fusion `063d7eb` (six workflows déclenchés par `push`) :

| Conclusion | Workflow | Run |
|---|---|---|
| success | `harness-ci` | 31597009979 |
| success | `security` | 31597009961 |
| success | `pipeline-audit` | 31597009996 |
| success | `audit-guard` (job `schema` ✔, job `cursor-scope` **skipped**) | 31597009970 |
| success | `hermes-dashboard` | 31597009982 |
| **failure** | **`pipeline-orchestrate`** | **31597010007** |

Sur la tête de PR `8319f556` (événement `pull_request`) :

| Conclusion | Workflow | Run |
|---|---|---|
| success | `harness-ci`, `security`, `pipeline-audit`, `audit-guard`, `hermes-observer` | 31596787180 / 31596787076 / 31596787281 / 31596787249 / 31597013736 |
| **failure** | **`merge-bot`** (job `check-and-automerge`) | **31596787272** |

**Verdict CI : rouge des deux côtés.** La PR a été fusionnée alors que sa
seule porte mécanique bloquante (`merge-bot`) était rouge, et la fusion a
produit un second échec (`pipeline-orchestrate`). Les deux échecs sont
analysés en P0-1 et P1-4.

## 3. Constats

### P0-1 — La décision produite par cette fusion a été calculée, committée, puis perdue : la boucle reste bloquée en `AUDIT_CHALLENGED`

C'est le constat le plus grave de cet audit, et il est **nouveau** : il ne
figure pas dans l'audit `CURSOR-779d97c` que cette PR relit.

La fusion de la PR #35 a bien déclenché `pipeline-orchestrate.yml`
(run 31597010007). Le calcul a **réussi** : la policy a produit le fichier
de décision et la ligne de ledger. C'est le `git push` final qui a échoué.
Extrait du log du run, étape « Commit ledger/decision/brief-seed update » :

```
2026-08-12T12:34:03.3327561Z [master f62195f] pipeline-orchestrate: review_recorded
2026-08-12T12:34:03.3328510Z  2 files changed, 19 insertions(+)
2026-08-12T12:34:03.3329804Z  create mode 100644 architecture/decisions/DECISION-CURSOR-779d97c-revue-verdicts-illisibles.md
2026-08-12T12:34:03.6816279Z To https://github.com/PLiagre/ForgeHistory
2026-08-12T12:34:03.6817690Z  ! [rejected]        master -> master (fetch first)
2026-08-12T12:34:03.6818679Z error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
2026-08-12T12:34:03.6854388Z ##[error]Process completed with exit code 1
```

Le travail est effectivement perdu, pas seulement retardé :

```
$ git cat-file -t f62195f
fatal: Not a valid object name f62195f
$ git ls-tree -r --name-only origin/master architecture/decisions/ | grep -i 779d97c
(aucune sortie — ABSENTE de origin/master)
$ git show origin/master:architecture/audit-ledger.jsonl | grep 779d97c
{"timestamp": "2026-08-12T12:30:17Z", "audit_id": "CURSOR-779d97c-revue-verdicts-illisibles", "event": "AUDIT_CHALLENGED", ...}
```

Le seul événement de `CURSOR-779d97c` sur `master` reste `AUDIT_CHALLENGED`.
La transition `AUDIT_APPROVED` a existé une fraction de seconde dans le
runner, puis a disparu. L'audit est donc **immobilisé au milieu de son
cycle de vie** (`architecture/README.md` § « Cycle de vie d'un audit »).

**Cause mécanique — deux écrivains concurrents sur `master`, aucun
sérialisé, aucun réessai.** Chronologie mesurée :

| Heure (UTC) | Événement | Preuve |
|---|---|---|
| 12:33:24Z | PR #35 fusionnée → `063d7eb` | `gh pr view 35 --json mergedAt` |
| 12:33:26Z | `pipeline-orchestrate` démarre (event `push`) | `gh api .../runs/31597010007 -q .created_at` |
| 12:33:52Z | `hermes-dashboard` pousse `dd16d76` « hermes: tableau de bord régénéré » sur `master` | `git log --format='%h %cI %s' origin/master` |
| 12:34:03Z | `pipeline-orchestrate` committe `f62195f` puis se fait rejeter | log ci-dessus |

Deux workflows écrivent sur `master` :
`.github/workflows/hermes-dashboard.yml:103` (`git push origin master`) et
`.github/workflows/pipeline-orchestrate.yml:117` (`git push`). Le premier
possède un garde de concurrence, le second **aucun** :

```
$ for f in pipeline-orchestrate hermes-dashboard pipeline-challenge merge-bot; do
    echo "--- $f ---"; grep -n -A3 "^concurrency:" .github/workflows/$f.yml || echo "AUCUN bloc concurrency"; done
--- pipeline-orchestrate ---
AUCUN bloc concurrency
--- hermes-dashboard ---
28:concurrency:
29-  group: hermes-dashboard
30-  cancel-in-progress: false
--- pipeline-challenge ---
AUCUN bloc concurrency
--- merge-bot ---
AUCUN bloc concurrency
```

Et même si `pipeline-orchestrate` en avait un, un groupe `hermes-dashboard`
et un groupe `pipeline-orchestrate` sont **deux files distinctes** : elles ne
se sérialisent pas entre elles. La ligne 117 est un `git push` nu — pas de
`git pull --rebase`, pas de boucle de réessai, pas de compare-and-swap.
C'est exactement le mode de défaillance que la littérature 2026 sur les
dépôts partagés entre humains, agents et bots décrit comme non négociable :
un seul committeur, ou un réessai avec re-parentage sur la nouvelle tête
[S6, S7, S8].

**Portée exacte, sans exagération.** L'échec **a été signalé** : le workflow
est rouge et l'escalade a bien tourné (run 31597071997 de
`pipeline-failure-escalate`, conclusion `success` à 12:34:09Z, soit 3 s après
la fin du run en échec) — cette escalade est volontairement « log-only » par
son propre en-tête (`.github/workflows/pipeline-failure-escalate.yml`
lignes 14-19). Ce qui manque n'est donc pas l'alerte, c'est la **reprise** :

1. rien ne distingue « le calcul a échoué » de « le calcul a réussi et son
   résultat a été jeté » — deux incidents de gravité très différente
   produisent le même signal rouge ;
2. le déclencheur du workflow est `push`, donc un simple *re-run* du run
   31597010007 ne rejouerait pas la transition manquante ; il faut une
   intervention manuelle non documentée pour débloquer `CURSOR-779d97c`.

C'est la définition du « silent data gap » que les postmortems de pipelines
autonomes 2026 placent en tête des défaillances : une étape se termine, la
suivante hérite d'un état incomplet sans le savoir [S9, S10].

### P1-2 — La revue livrée par cette PR reproduit dans son propre artefact les deux défauts qu'elle confirme

Cadrage : l'audit `CURSOR-779d97c` diagnostique en P0-1 la perte silencieuse
des lignes de verdict non conformes, et en P1-3 le fait que le champ
`verdicts` du ledger compte des **mots** et non des verdicts. La revue
livrée ici les marque tous deux `CONFIRMED` (ses points 3, 5 et 8). Je ne
réémets pas ces constats — ce serait du bruit
(`review-guidelines.md` § « Forme imposée des constats »). L'**élément
nouveau** est que l'artefact que cette PR fusionne en est lui-même victime,
mesurablement, et plus gravement que le cas qu'il décrit.

**(a) Le ledger publie 72 verdicts pour 20 rendus, dont 25 qui n'existent
pas.** Ligne ajoutée par cette PR (`architecture/audit-ledger.jsonl`,
dernière ligne) :

```json
{"timestamp": "2026-08-12T12:30:17Z", "audit_id": "CURSOR-779d97c-revue-verdicts-illisibles",
 "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md",
 "verdicts": {"CONFIRMED": 34, "REFUTED": 15, "PARTIAL": 10, "NEEDS_OWNER": 13}}
```

Décompte réel des 20 lignes du tableau de la revue :

```
$ python3 -c "..."   # comptage des cellules de verdict du fichier livré
nb lignes numerotees: 20
  14  'CONFIRMED'
   1  'CONFIRMED (paraphrase fidèle)'
   2  'NEEDS_OWNER'
   (+ 3 lignes de tableau échappées citées dans le texte des preuves)
```

La revue ne rend **aucun** `REFUTED` et **aucun** `PARTIAL`. Le ledger en
publie respectivement 15 et 10. Le cas décrit par l'audit audité était
`REFUTED: 2` contre « Aucun REFUTED » ; ici l'écart est passé à 15 contre 0,
parce que la revue cite abondamment ces jetons dans ses cellules de preuve.
Le journal de la boucle affirme donc que Claude a réfuté 15 points de
l'audit alors qu'il n'en a réfuté aucun — l'inverse exact du contenu.

**(b) Une confirmation sur vingt n'atteindra jamais la décision.** Rejeu des
deux fonctions concurrentes sur le fichier livré :

```
$ python3 -c "import sys; sys.path.insert(0,'harness'); import audit_review, audit_decision; ..."
=== parse_verdicts (audit_review, mots) ===
{'CONFIRMED': 34, 'REFUTED': 15, 'PARTIAL': 10, 'NEEDS_OWNER': 13}
=== _parse_point_verdicts (audit_decision, lignes strictes) ===
19 [(1,'CONFIRMED'), ..., (9,'CONFIRMED'), (11,'CONFIRMED'), ...]
```

Le point **10** est absent : sa cellule vaut `CONFIRMED (paraphrase fidèle)`,
et le motif strict de `harness/audit_decision.py` refuse toute cellule
comportant autre chose que le jeton. Preuve de la conséquence réelle, rejeu
de la policy hors du dépôt (copie du fichier réel dans `/tmp/dec`, aucun
écrit dans le dépôt) :

```
$ cd /tmp/dec && python3 /workspace/harness/audit_decision.py auto \
    --audit-id CURSOR-779d97c-revue-verdicts-illisibles --inbox ... --decisions ... --ledger ... \
    --policy-path /workspace/harness/pipeline/auto_policy.yaml
recorded AUDIT_APPROVED for CURSOR-779d97c-revue-verdicts-illisibles (reason: policy: ...)

$ tail -1 /tmp/dec/architecture/audit-ledger.jsonl
... "retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 14, 15, 16, 17, 19, 20]}
```

`retained_points` compte 17 entrées ; les points 13 et 18 sont légitimement
exclus (`NEEDS_OWNER`), mais le point **10 est perdu alors qu'il est
`CONFIRMED`**. La revue qui confirme la perte silencieuse est donc elle-même
amputée d'une de ses confirmations par cette même perte. C'est le motif
« correction hallucinée » de la lentille 6 sous sa forme la plus nette : le
diagnostic est juste, l'artefact qui le porte est cassé par le défaut
diagnostiqué, et rien dans la PR ne le signale.

**Aucun brief proposé pour ce constat** : le brief 1 déjà proposé par
`CURSOR-779d97c` couvre la cause. Ce point apporte une mesure fraîche qui
en relève la priorité, pas une demande nouvelle.

### P1-3 — Toute PR de revue produite par `pipeline-challenge.yml` est structurellement inéligible à l'auto-merge : le workflow committe un fichier absent de `allow_paths`

Constat nouveau, indépendant du faux positif de base mobile (P1-4).

`pipeline-challenge.yml` intitule son étape de publication « Publish the
review as a pull request (**reviews/** is merge-bot allowlisted**) » (l. 163)
puis committe deux chemins (l. 178) :

```
178:          git add architecture/reviews architecture/audit-ledger.jsonl
```

Or l'allowlist normative ne contient pas le ledger. `.github/merge-bot.yaml` :

```yaml
allow_paths:
  - "architecture/inbox/**"
  - "architecture/reviews/**"
  - "harness/queue/briefs/**/feedback/**"
```

et `.github/workflows/merge-bot.yml:51` code en dur les trois mêmes préfixes :

```bash
offending="$(printf '%s\n' "$changed" | grep -vE '^(architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/)' || true)"
```

`architecture/audit-ledger.jsonl` est donc « offending » par construction
dans **chaque** PR de revue. Conséquence : le `mode: full_auto` ne peut pas
refermer sa propre boucle sans main humaine — ce qui s'est produit ici
(fusion par `PLiagre`). Le commentaire de l'étape 163 affirme une
éligibilité que la ligne 178 du même fichier détruit : c'est une
affirmation non mesurée dans le code lui-même, le motif que la lentille 2
demande de traquer.

Nuance importante pour ne pas surévaluer : sur **ce** run, ce n'est pas
cette cause qui a fait rougir `merge-bot` — le faux positif de base mobile
(P1-4) a masqué le vrai blocage en produisant une liste de fichiers
entièrement différente. Les deux causes sont indépendantes ; corriger la
base mobile laisserait le blocage du ledger intact.

### P1-4 — Le faux positif du merge-bot est reproduit ici avec sa chronologie complète : les deux points `NEEDS_OWNER` de la revue sont levés

L'audit `CURSOR-779d97c` décrit ce mécanisme en P1-4 ; la revue l'a
`CONFIRMED` sur le code (son point 11) mais a dû classer `NEEDS_OWNER` la
chronologie (son point 13) et la classification CI (son point 18), faute
d'accès `gh`. Ayant cet accès, je livre les mesures manquantes — c'est un
apport de preuve, pas un constat neuf.

Chronologie mesurée du run `merge-bot` 31596787272 :

```
$ gh run view 31596787272 --json createdAt,startedAt,updatedAt
created=2026-08-12T12:30:41Z started=2026-08-12T12:33:20Z updated=2026-08-12T12:33:37Z
$ gh pr view 35 --json mergedAt -q .mergedAt
2026-08-12T12:33:24Z
```

Le run est resté **2 min 39 s en file**, a démarré à 12:33:20Z, et la PR a
été fusionnée à 12:33:24Z — **pendant** son exécution. Son étape de
périmètre a donc tourné à 12:33:34Z contre un `master` qui contenait déjà la
fusion. Sortie de cette étape :

```
warning: origin/master...HEAD: multiple merge bases, using 8319f5566cfc6cf624fc3dfe5c7c4b1d5dfc676e
Changed files:
ROADMAP.md
docs/adr/0011-hermes-console-du-proprietaire.md
hermes/DASHBOARD.md
hermes/README.md
hermes/requests/DEMANDE-20260812-hermes-tableau-de-bord-pilotage.md
##[error]PR touches path(s) outside .github/merge-bot.yaml's allow_paths -- refusing auto-merge
```

**Aucun de ces cinq fichiers n'est touché par la PR #35** (son diff réel
est de 2 fichiers, § 1). Ils proviennent de la fusion de la PR #34 et des
régénérations de tableau de bord arrivées sur `master` entre-temps
(`0269d8e`, `c80f0a4`, `1074d95`). Le verdict rouge porte donc sur du
travail que l'auteur de la PR n'a pas produit — la base `origin/${BASE_REF}...HEAD`
de `.github/workflows/merge-bot.yml:39` est mobile, exactement comme décrit.

**Classification CI** demandée par la revue (son point 18) : voir le § 2 de
cet audit — 5 verts + `merge-bot` rouge sur la tête, 5 verts +
`pipeline-orchestrate` rouge sur la fusion, `cursor-scope` `skipped` (branche
`forge-bot/*`, mécanisme déjà connu et confirmé).

**Aucun brief proposé** : le brief 3 de `CURSOR-779d97c` couvre déjà cette
cause. Ces mesures visent à retirer aux points 13 et 18 leur statut
`NEEDS_OWNER`.

### P2-5 — `reviewed_at` est un champ écrit à la main, et sa valeur ici est postérieure à la fusion de la revue

Le gabarit laisse ce champ au modèle (`harness/audit_review.py:69`) :

```
69:reviewed_at: <<TODO: ISO 8601 UTC, ex. 2026-08-05T10:00:00Z>>
```

Valeur livrée et réalité mesurée :

```
$ grep -n reviewed_at architecture/reviews/CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md
5:reviewed_at: 2026-08-12T12:35:00Z
ledger (AUDIT_CHALLENGED) : 2026-08-12T12:30:17Z
commit de la revue        : 2026-08-12T12:30:35+00:00
fusion de la PR #35       : 2026-08-12T12:33:24Z
```

La revue se déclare écrite **4 min 25 s après avoir été committée** et
**1 min 36 s après avoir été fusionnée** — un horodatage impossible. La
seconde ronde (`:00`) confirme la saisie manuelle, motif qu'on retrouve dans
d'autres revues (`10:15:00Z`, `08:52:00Z`) là où les valeurs générées par
machine portent des secondes réelles (`20:11:47Z`, `11:53:47Z`, `21:28:53Z`).

Portée : faible en conséquence immédiate (aucun code ne lit ce champ), réelle
en principe, dans un dossier dont la fonction est la traçabilité auditable.
Rien ne le valide — `harness/audit_schema.py` ne couvre que `inbox/`
(asymétrie déjà constatée en P2-6 de l'audit audité, que je ne réémets pas).

**Cause structurelle jumelle, mesurée ici.** L'étape qui invoque le
challenger ne lui donne pas d'accès GitHub, alors que l'étape suivante du
même fichier en a un :

```
136:      - name: Invoke claude-challenger headless (/forge-audit-review)
138:        env:
139:          CLAUDE_CODE_OAUTH_TOKEN: ...
140:          ANTHROPIC_API_KEY: ...
141:          AUDIT_ID: ...
                                    <-- pas de GH_TOKEN
163:      - name: Publish the review as a pull request ...
166:          GH_TOKEN: ${{ secrets.GITHUB_TOKEN }}
```

L'honnêteté de la revue sur ses limites (§ 1 : « pas de `GH_TOKEN`/`gh auth`
disponible ici non plus ») est donc exacte et **structurellement causée par
une omission d'une ligne**. C'est la cause des deux seuls `NEEDS_OWNER` de
la revue.

### P2-6 — La porte mécanique a rendu son verdict 10 secondes après la fusion qu'elle était censée autoriser

Fusion à 12:33:24Z ; étape de périmètre de `merge-bot` à 12:33:34Z (§ P1-4).
La porte n'a donc pas été « contournée » : elle a été **doublée par la
course**. Dans cet ordre, `merge-bot` ne peut structurellement plus rien
empêcher — il devient un rapport post-mortem.

Je ne rouvre pas l'absence de protection de branche : elle est déjà consignée
comme dérogation documentée dans `.github/merge-bot.yaml` (l'API renvoie 403,
fonctionnalité indisponible sur le plan du dépôt). L'élément nouveau est
uniquement l'**ordonnancement** : une porte dont le run attend 2 min 39 s en
file alors que la fusion peut survenir à tout instant ne protège rien, même
correcte. La lentille 3 dit de ne dépenser du jugement que sur ce que les
machines ne voient pas ; ici la machine a vu, mais trop tard, et pour la
mauvaise raison.

### P3-7 — Le coût réel de production de cette revue (1,560825 USD) est également jeté ; le cumul non journalisé atteint ≈ 3,15 USD

Déjà confirmé comme P3-7 par l'audit audité ; je n'apporte que la mesure du
run de cette PR. Log du run `31596321701`, étape « Post-hoc budget marking » :

```
{"cap_usd": 5.0, "over_cap": false, "prices_as_of": "2026-08-03",
 "step": "challenge:CURSOR-779d97c-revue-verdicts-illisibles",
 "timestamp": "2026-08-12T12:30:35.414371Z", "usd": 1.560825}
```

Le fichier destinataire n'a jamais reçu ces lignes :

```
$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
```

`pipeline-challenge.yml:178` ne committe que `architecture/reviews` et
`architecture/audit-ledger.jsonl`. Avec le run précédent (1,593695 USD, cité
par l'audit audité), ce sont ≈ 3,15 USD dépensés et mesurés dont il ne reste
aucune trace versionnée. La mesure est faite au bon endroit — c'est le
« ledger de run » que la littérature FinOps agents 2026 recommande [S11] —
mais elle est écrite dans un fichier qui n'est jamais persisté, ce qui la
rend équivalente à une absence de mesure : la détection arrive par la
facture, jamais par la trace [S12].

**Aucun brief proposé** : motif déjà tranché par la revue antérieure ; je
n'y touche pas.

### P3-8 — L'intention de la PR n'est pas lisible sans ouvrir le fichier

Description intégrale de la PR #35 :

> Contre-audit produit headless par claude-challenger (run 31596321701).
> La fusion de cette PR déclenche pipeline-orchestrate.yml (event review_recorded).

Elle dit **qui** a produit et **ce que la fusion déclenche**, jamais **ce
que la revue conclut** : ni le nombre de points, ni la répartition des
verdicts, ni le fait qu'aucun point n'est réfuté. Un relecteur doit ouvrir
116 lignes pour savoir si la PR mérite d'être fusionnée — alors que le
gabarit produit déjà ces chiffres et que le workflow les inscrit au ledger
(§ P1-2 (a)). La lentille 1 rend une PR critiquable pour cela même. Ironie
mesurable : la seule affirmation d'effet de la description
(« la fusion déclenche `pipeline-orchestrate` ») s'est vérifiée, et son
résultat a été perdu (P0-1) — la description promet un déclenchement, pas un
aboutissement, et personne n'a vérifié l'aboutissement.

## 4. Ce que la PR tient bien

Cadrage adverse ne veut pas dire procès. Tout ce que la revue affirme et que
j'ai pu rejouer se reproduit **exactement** :

| Affirmation de la revue | Rejeu indépendant | Résultat |
|---|---|---|
| Suite de tests : `309 passed, 16 skipped` | `.venv/bin/python -m pytest harness/tests/ -q` | `309 passed, 16 skipped in 18.01s` — identique |
| `parse_verdicts` sur la revue PR #30 → `12/2/4/4`, identique au ledger | rejeu sur `CLAUDE-CURSOR-73022bd-….md` | `{'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}` — identique |
| Ce même fichier est illisible par `_parse_point_verdicts` | rejeu | `0 points` — confirme le blocage décrit |
| Revue `cdc683f` : 9 lignes captées sur 11 | rejeu | `9 points` — identique |
| Couverture : un verdict par point de l'audit | `grep -nE "^#+ (P[0-3]-|§)"` sur l'audit → 7 constats (P0-1, P0-2, P1-3, P1-4, P2-5, P2-6, P3-7) | les 7 reçoivent un verdict, aucune lacune |

Trois qualités méritent d'être nommées :

1. **Taille exemplaire.** 2 fichiers, +117/−0 : très en dessous du seuil de
   ~400 lignes au-delà duquel une relecture honnête décroche [S1, S2]. Aucun
   `NEEDS_SPLIT` à signaler (lentille 5).
2. **Périmètre respecté.** La revue n'écrit que dans
   `architecture/reviews/**` + le ledger, conformément aux Interdits de
   `architecture/agents/claude-challenger.md` : aucun code, aucun test,
   aucun workflow.
3. **Limites déclarées plutôt que devinées.** Les deux points hors de portée
   sont marqués `NEEDS_OWNER` avec le motif exact, au lieu d'être affirmés.
   C'est précisément la discipline que la lentille 2 exige, et c'est ce qui
   m'a permis de compléter le travail plutôt que de le refaire.

## 5. Risques par sévérité

| Sévérité | Constat | Portée |
|---|---|---|
| **P0** | P0-1 — décision calculée puis perdue par une course de `git push`, sans reprise ; `CURSOR-779d97c` bloqué en `AUDIT_CHALLENGED` | La boucle d'audit ne peut plus refermer un cycle ; toute fusion concurrente rejoue le défaut |
| **P1** | P1-2 — l'artefact fusionné publie 15 `REFUTED` inexistants et perdra son point 10 | Journal de la boucle faux ; une confirmation n'atteint pas la décision |
| **P1** | P1-3 — `pipeline-challenge.yml` committe un chemin hors `allow_paths` : aucune PR de revue n'est auto-mergeable | `mode: full_auto` exige une main humaine à chaque tour |
| **P1** | P1-4 — faux positif de base mobile reproduit ; chronologie et classification CI établies | Lève les 2 `NEEDS_OWNER` de la revue |
| **P2** | P2-5 — `reviewed_at` saisi à la main, valeur postérieure à la fusion ; challenger privé de `GH_TOKEN` | Traçabilité non vérifiable ; cause des `NEEDS_OWNER` |
| **P2** | P2-6 — la porte `merge-bot` rend son verdict 10 s après la fusion | La porte est devenue consultative de fait |
| **P3** | P3-7 — 1,560825 USD mesurés et jetés (≈ 3,15 USD cumulés) | Coût de la boucle non journalisé |
| **P3** | P3-8 — intention de la PR illisible sans ouvrir le fichier | Coût de relecture inutilement élevé |

## 6. Briefs atomiques proposés (3 — plafond du contrat)

Rappel : un audit **ne pré-autorise rien**. Ces trois propositions n'ont
valeur d'instruction qu'après conversion explicite en brief par le
propriétaire (`CLAUDE.md` › Single Source of Instruction).

**Brief 1 — Rendre les écritures de la boucle sur `master` non perdables (P0-1).**
Portée : `.github/workflows/pipeline-orchestrate.yml` (+ tout autre workflow
qui pousse sur `master`). Objet : sérialiser les écrivains dans une file
commune **et** rendre le `git push` réessayable après re-parentage sur la
nouvelle tête, de sorte qu'une transition calculée ne puisse plus être
jetée ; distinguer dans le signal d'échec « calcul impossible » de
« résultat calculé puis perdu ». Preuve rouge attendue avant correctif : un
test qui simule une tête distante ayant avancé entre le `commit` et le
`push` et qui échoue aujourd'hui. Note de périmètre : touche
`.github/workflows/**`, donc `auto_merge_denylist`
(`harness/pipeline/config.yaml`) — jamais auto-mergeable, arbitrage
propriétaire requis.

**Brief 2 — Aligner ce que `pipeline-challenge.yml` committe avec `allow_paths` (P1-3).**
Portée : `.github/workflows/pipeline-challenge.yml`, `.github/merge-bot.yaml`,
`.github/workflows/merge-bot.yml` — deux options à trancher par le
propriétaire (étendre l'allowlist au ledger, ou sortir le ledger de la PR de
revue), pas par l'agent. Preuve rouge attendue : un test qui confronte la
liste des chemins réellement committés par le workflow à l'allowlist et
échoue aujourd'hui. Même note de périmètre `denylist` que le brief 1.

**Brief 3 — Supprimer les deux causes structurelles de non-vérifiabilité d'une revue (P2-5).**
Portée : `harness/audit_review.py` (horodater `reviewed_at` par la machine au
moment de l'enregistrement, au lieu d'un `<<TODO>>` confié au modèle) et
`.github/workflows/pipeline-challenge.yml` (fournir `GH_TOKEN` à l'étape
d'invocation du challenger, comme l'étape de publication l'a déjà, afin que
les points de chronologie CI cessent de tomber en `NEEDS_OWNER` par défaut
d'outil). Preuve rouge attendue : un test qui refuse un `reviewed_at`
antérieur au commit ou postérieur à l'enregistrement.

## 7. Sources externes

Sources datées, consultées le **2026-08-12**. S1–S5 sont le référentiel de
`architecture/review-guidelines.md` (lentilles) ; S6–S12 sont les sources
propres à cet audit, sur les trois axes exigés par le contrat.

| # | source | axe | consulté le |
|---|---|---|---|
| S1 | The New Stack — *Move code review before the code* — <https://thenewstack.io/move-code-review-upstream/> | lentilles 1, 3, 5 | 2026-08-12 |
| S2 | Augment Code — *Reviewing AI-Generated Code: A Verification Discipline for the Loop* — <https://www.augmentcode.com/guides/reviewing-ai-generated-code> | lentilles 2, 3 | 2026-08-12 |
| S3 | aiarch.dev — *Reviewing AI-Written Code: A Diff Discipline Workflow* — <https://aiarch.dev/workflows/ai-assisted-review> | lentilles 2, 4 | 2026-08-12 |
| S4 | AnAr Solutions — *The Five Lenses of AI Code Review* — <https://anarsolutions.com/ai-code-review-framework/> | forme des constats | 2026-08-12 |
| S5 | danicat.dev — *How to Do Code Reviews in the Agentic Era* (publié 2026-03-03) — <https://danicat.dev/posts/20260303-code-reviews-in-2026/> | lentille 6 | 2026-08-12 |
| S6 | Munder Difflin — *The Single-Committer Pattern: Multi-Agent Git Without Corruption* — <https://munderdiffl.in/blog/single-committer-git-pattern/> | agent orchestration CI (écrivain unique, réessai avec backoff) | 2026-08-12 |
| S7 | WOWHOW — *Single-Push Discipline: Multi-Agent Git Workflow 2026* — <https://wowhow.cloud/blogs/single-push-deterministic-multi-agent-git-workflow-2026> | agent orchestration CI (une seule poussée, intégrateur désigné ; décrit la double-exécution CI et le déploiement silencieusement périmé) | 2026-08-12 |
| S8 | Kody Wildfeuer — *When Humans, Agents, and Bots All Push to Main: Rules for Sharing a Repo* (publié 2026-04-20) — <https://kody-w.github.io/2026/04/20/agents-and-bots-share-a-repo/> | agent orchestration CI (règles de cohabitation humain/bot sur une même branche) | 2026-08-12 |
| S9 | Ralph Workflow — *12 Multi-Agent Bugs in One Night — Claude Code #54393 Postmortem* — <https://ralphworkflow.com/blog/claude-code-multi-agent-overnight-postmortem> | autonomous AI dev pipeline (abandon silencieux ; passation par le dépôt, pas par la mémoire) | 2026-08-12 |
| S10 | devops.com — *CI/CD Was Built for Deterministic Software; Agents Just Broke the Model* — <https://devops.com/ci-cd-was-built-for-deterministic-software-agents-just-broke-the-model/> | autonomous AI dev pipeline (le pipeline doit produire la preuve que l'agent a fait ce qu'il devait, et laisser une trace exploitable) | 2026-08-12 |
| S11 | Braintrust — *How to track LLM costs (2026): per-user, per-feature, per-agent-run attribution* — <https://www.braintrust.dev/articles/how-to-track-llm-costs-2026> | token budget LLM agents (attribution par run ; l'analyse post-hoc de logs perd le contexte que l'application avait déjà) | 2026-08-12 |
| S12 | AgentBudget — *Real-Time Cost Enforcement for AI Agents*, livre blanc v1 — <https://agentbudget.dev/agentbudget_whitepaper_v1.pdf> | token budget LLM agents (ledger de session persisté, plafond appliqué avant l'appel) | 2026-08-12 |

## 8. Commandes rejouées dans cet audit

Toutes exécutées au commit `063d7eb`, en lecture seule sur le dépôt (le seul
écrit a eu lieu dans `/tmp/dec`, hors du dépôt).

```
git rev-parse HEAD
gh pr view 35 --json ...  /  gh pr diff 35 --name-only
gh run list --commit 063d7eba... / --commit 8319f556...
gh run view 31597010007 --log-failed        # perte de la décision (P0-1)
gh run view 31596787272 --log-failed        # faux positif merge-bot (P1-4)
gh run view 31596321701 --log | grep -iE "usd|cost|budget"   # 1,560825 USD (P3-7)
gh run list --workflow pipeline-failure-escalate --limit 8    # escalade bien déclenchée
git cat-file -t f62195f                     # → fatal: Not a valid object name
git ls-tree -r --name-only origin/master architecture/decisions/ | grep -i 779d97c
git show origin/master:architecture/audit-ledger.jsonl | grep 779d97c
grep -n -A3 "^concurrency:" .github/workflows/{pipeline-orchestrate,hermes-dashboard,pipeline-challenge,merge-bot}.yml
grep -rn "git push" .github/workflows/*.yml
wc -c harness/pipeline/ci-budget-ledger.jsonl
.venv/bin/python -m pytest harness/tests/ -q          # 309 passed, 16 skipped
python3  # audit_review.parse_verdicts / audit_decision._parse_point_verdicts sur 3 revues
cd /tmp/dec && python3 /workspace/harness/audit_decision.py auto --audit-id CURSOR-779d97c-... 
```

Budget d'appels de cet audit : 32 appels outils, plafond contractuel 60
(`architecture/agents/cursor-auditor.md` § « Budget max appels »).
