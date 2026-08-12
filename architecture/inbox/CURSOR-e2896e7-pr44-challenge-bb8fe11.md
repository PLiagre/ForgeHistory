---
audit_id:                CURSOR-e2896e7-pr44-challenge-bb8fe11
auditor:                 cursor-cloud
target_branch:           master
target_commit:           e2896e752441b801dc97dd489def32ee02b6f57a
created_at:              2026-08-12T14:02:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #44 — « challenge : revue de l'audit CURSOR-bb8fe11-hermes-console-adr-0011 »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005 / ADR-0006).

## 0. Objet et périmètre

| | |
|---|---|
| PR | [#44](https://github.com/PLiagre/ForgeHistory/pull/44), `forge-bot/review-CURSOR-bb8fe11-hermes-console-adr-0011-31598872392` → `master` |
| Auteur | `app/github-actions` (contenu produit headless par `claude-challenger`, run 31598872392) |
| Diff net | **1 fichier, +131 / −0** : `architecture/reviews/CLAUDE-CURSOR-bb8fe11-hermes-console-adr-0011.md` |
| Commits | `1e6ee01` (la revue + une ligne de ledger) puis `e2896e7` (retrait de cette ligne, auteur `Cursor Agent <cursoragent@cursor.com>`) |
| SHA audité | `e2896e752441b801dc97dd489def32ee02b6f57a` (tête de la PR) |

**Note de fraîcheur, à lire avant tout le reste.** La PR a été **fusionnée
pendant la conduite de cet audit**, à `2026-08-12T13:54:37Z`, alors que le
présent auditeur avait été lancé à `13:49`. Les constats P0-1 et P1-1
ci-dessous ne sont donc plus des prédictions : ils sont **mesurés sur le
résultat réel de la fusion**. C'est aussi, en soi, le constat P1-1.

```
$ gh pr view 44 -R PLiagre/ForgeHistory --json state,mergedAt,mergeCommit,mergedBy
{"by":"PLiagre","merge":"417a4e1a1755c6b479a7a80d1e600ca2d9a436cd",
 "mergedAt":"2026-08-12T13:54:37Z","state":"MERGED"}
```

## 1. Portes mécaniques — classification de la CI (lentille 3)

CI du SHA audité : **verte**, sans exception.

```
$ gh pr checks 44 -R PLiagre/ForgeHistory
Reconcile local Hermes state   pass   6s
actionlint                     pass   11s      actionlint          pass   10s
check-and-automerge            pass   13s
f0-demo                        pass   12s      f0-demo             pass   11s
gitleaks                       pass   11s      gitleaks            pass   11s
invoke-cursor-auditor          pass   18s
schema                         pass   10s      schema              pass   10s
tests                          pass   25s      tests               pass   24s
cursor-scope                   skipping  0     cursor-scope        skipping  0
```

Lecture : 13 jobs verts, 2 `skipping`. Les deux `cursor-scope` sont
structurellement corrects — `.github/workflows/audit-guard.yml:30` conditionne
ce job à `startsWith(github.head_ref, 'cursor/')`, or la branche est
`forge-bot/*` (voir toutefois P2-2). `mergeStateStatus` est passé à `CLEAN`,
`reviewDecision` est vide et le nombre de relectures humaines est **0**.

**La CI verte de cette PR ne dit rien de son effet réel.** Tout ce qui compte
dans ce livrable se produit *après* la fusion, dans un workflow que la PR ne
déclenche pas encore — c'est l'objet du P0-1.

## 2. Constats

### P0-1 — La fusion a détruit la transition d'état que la PR déléguait explicitement au post-fusion

Le commit `e2896e7` retire la ligne de ledger de la PR, avec ce message :

```
challenge: la ligne de ledger sort de la PR (convention post-#46 --
AUDIT_CHALLENGED sera écrit sur master par pipeline-orchestrate)
```

L'intention est correcte et documentée (`.github/workflows/pipeline-challenge.yml:178-185`,
`harness/pipeline/orchestrator.py` § `handle_review_recorded`) : deux challenges
concurrents entreraient en conflit d'append sur le ledger partagé. Mais le
destinataire de cette délégation **échoue sur ce fichier précis**.

**Reproduit hors dépôt avant la fusion** (inbox, reviews et ledger recréés
dans `/tmp`, fichier de revue réel de la PR copié tel quel) :

```
ETAPE 0 -- trigger_resolve (push master, 1 fichier reviews/ modifie)
   event='review_recorded' payload={'audit_id': 'CURSOR-bb8fe11-hermes-console-adr-0011'}
ETAPE 1 -- audit_review.record_challenge (porte d'ecriture du ledger)
   ACCEPTE. ligne ledger : {"event": "AUDIT_CHALLENGED", "actor": "claude",
     "verdicts": {"CONFIRMED": 15, "REFUTED": 2, "PARTIAL": 5, "NEEDS_OWNER": 6}}
ETAPE 2 -- audit_decision.decide_auto (etape suivante du meme job)
   REFUSE -> DecisionError: …/CLAUDE-CURSOR-bb8fe11-….md has no
     '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
```

**Confirmé par la CI réelle**, run `31603909788` (`pipeline-orchestrate`, push
du merge de #44, 13:54:40, **failure**) :

```
$ gh run view 31603909788 -R PLiagre/ForgeHistory --log-failed
error: …/architecture/reviews/CLAUDE-CURSOR-bb8fe11-hermes-console-adr-0011.md
  has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
##[error]Process completed with exit code 2.
```

Cause : `harness/audit_decision.py:185-191` exige des lignes
`| N | … | VERDICT | … |` avec `int(n)`. Les points de cette revue sont
numérotés `§1`, `P1-1`, `P2-3`, `§9` — jamais un entier. Aucune ligne n'est
donc parsable.

**Conséquence, et c'est le cœur du P0.** L'étape 1 du job *avait* écrit la
ligne `AUDIT_CHALLENGED` dans l'espace de travail du runner ; l'étape 2 échoue
au même processus, et les étapes suivantes sont sautées :

```
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31603909788/jobs --jq '…'
4 success   Resolve event + payload
5 failure   Run orchestrator
6 skipped   Refuse to push outside the ledger/decision/brief allowlist
7 skipped   Commit ledger/decision/brief-seed update
```

La ligne n'est donc **jamais commitée**. Mesuré sur `master` après la fusion :

```
$ git show origin/master:architecture/audit-ledger.jsonl | grep -c bb8fe11
0
$ git show origin/master:architecture/audit-ledger.jsonl | tail -1
{"timestamp": "2026-08-12T12:30:17Z", "audit_id": "CURSOR-779d97c-…", "event": "AUDIT_CHALLENGED", …}
```

Le ledger s'arrête à **12:30:17**, alors que trois PR de revue (#42, #43, #44)
ont été fusionnées à **13:54**. Les trois runs `pipeline-orchestrate`
correspondants sont en échec (`31603872434`, `31603893491`, `31603909788`).

L'audit `CURSOR-bb8fe11` est donc, sur `master`, revenu à l'état implicite
`AUDIT_PROPOSED` (`harness/audits.py:94-99` : « la dernière entrée du ledger,
ou PROPOSED si aucune ») **alors que son contre-audit est bel et bien présent
dans `architecture/reviews/`**. Le dossier et le journal se contredisent.

Et la boucle ne peut pas se rattraper seule : `pipeline-challenge.yml` ne se
déclenche que sur un push touchant `architecture/inbox/*.md` (`:22-26`) ; même
relancé, `audit_review.write_scaffold` refuse d'écraser une revue existante
(`harness/audit_review.py:117-120`) et l'étape de publication sort en
avertissement quand `reviews/` est inchangé (`pipeline-challenge.yml:186-189`).
Seul un `workflow_dispatch` manuel peut réparer.

Enfin, la perte est **silencieuse** : `pipeline-failure-escalate.yml:15-17`
indique explicitement qu'aucun `gh issue create` n'est fait, et la seule issue
ouverte du dépôt date du 2026-08-06 (`gh issue list` → `7 OPEN … 2026-08-06`).
Le seul témoin est un run rouge dans l'onglet Actions.

> Lentille 2 (preuve d'exécution, pas d'affirmation) et lentille 6 (correction
> hallucinée) : le message de commit `e2896e7` affirme un comportement
> post-fusion — « sera écrit sur master par pipeline-orchestrate » — qui n'a
> jamais été mesuré sur ce fichier, et qui est faux. C'est exactement la forme
> « succès affirmé, non mesuré ».

### P1-1 — L'auto-fusion a précédé de 5 minutes l'audit Cursor qu'ADR-0010 rend obligatoire sur chaque PR

Chronologie mesurée :

| horodatage | événement | preuve |
|---|---|---|
| 13:48:41 | commit `e2896e7` poussé | `git show -s --format=%cI e2896e7` |
| 13:49:07 | job `invoke-cursor-auditor` **termine** (18 s) | run `31603382077` |
| 13:54:37 | PR **fusionnée**, 0 relecture humaine | `gh pr view 44 --json mergedAt,reviews` |
| 14:02 | le présent audit existe | frontmatter `created_at` de ce fichier |

Le job qui « fait l'audit Cursor » ne fait que **poster une requête et rendre
la main** (`.github/workflows/pipeline-audit.yml:185-196`) :

```yaml
response="$(curl -sS --fail-with-body --request POST \
  --url https://api.cursor.com/v1/agents …)"
…
echo "cursor-auditor launched -- its audit will arrive as a cursor/* PR touching architecture/inbox/** only."
```

Pendant ce temps, `merge-bot.yml:66-72` lance `gh pr merge --auto --squash`,
qui fusionne dès que les vérifications requises passent — c'est-à-dire dès que
le job de *lancement* est vert, jamais quand l'audit existe.

Le prédicat 4 de `docs/rules/conditional-merge-gate.md:46-50` (« exactement un
fichier `architecture/inbox/**` portant `target_commit: <SHA de tête>` ») est
donc **structurellement insatisfiable** pour toute PR auto-fusionnée : l'audit
arrive après. Le présent document en est la démonstration : il porte
`target_commit: e2896e7…` et il est écrit après la fusion de ce même SHA.

*Antériorité assumée* : `CURSOR-bb8fe11` (P1-1) avait déjà relevé une fusion
sans lecture des quatre preuves. Élément nouveau ici : ce n'est plus un
manquement d'exécution mais une **impossibilité de conception** entre deux
workflows du dépôt, mesurée à la seconde. Je propose donc de traiter les deux
ensemble (voir § 5), pas d'ouvrir un doublon.

### P1-2 — Le contre-audit a été produit sans jeton GitHub, alors que le workflow en fournit un à l'étape d'à côté

La revue elle-même le dit (`architecture/reviews/CLAUDE-CURSOR-bb8fe11-…md:30-34`) :

> **Environnement de cette revue : pas de `GH_TOKEN`/`gh auth` disponible** …
> Tout ce qui dépend d'un appel `gh` en direct … n'a pas pu être rejoué tel quel ici

Ce n'est pas une fatalité, c'est une ligne d'environnement manquante. Dans
`pipeline-challenge.yml`, l'étape d'invocation (`:144-157`) ne reçoit que
`CLAUDE_CODE_OAUTH_TOKEN` et `ANTHROPIC_API_KEY` ; l'étape de publication
juste en dessous (`:171-176`) reçoit bien
`GH_TOKEN: ${{ secrets.FORGE_BOT_PAT || secrets.GITHUB_TOKEN }}`. Le jeton
existe dans le job, mais pas dans l'étape qui en a besoin.

Coût mesuré sur ce livrable : sur 16 lignes de verdict, **4** (§1, P2-3, §6,
§9) retombent en `PARTIAL`/`NEEDS_OWNER` pour ce seul motif — dont la
classification CI, qui est précisément ce qu'un contre-audit devrait vérifier
en premier (lentille 3).

*Antériorité vérifiée* : `CURSOR-779d97c:328-333` mentionne cette absence de
`GH_TOKEN`, mais **pour la porter au crédit** de la revue (« honnêteté sur ses
propres limites »). Personne n'a encore signalé que le workflow pouvait la
supprimer. Constat neuf.

### P2-1 — Zéro REFUTED sur 16 points, et au moins une case « CONFIRMED » contient un aveu de non-vérification

Distribution réelle, extraite de la colonne verdict du tableau du fichier :

```
16 lignes de verdict : 14 CONFIRMED (dont 4 hybrides), 1 PARTIAL (§1),
1 NEEDS_OWNER (§9), 0 REFUTED.
```

Comparaison avec les quatre contre-audits précédents inscrits au ledger
(`architecture/audit-ledger.jsonl`) : `cdc683f` → 3 REFUTED, `73022bd` → 2,
`65c3ac1` → 1, `779d97c` → 15. Celui-ci est le **premier à zéro**.

Ce n'est pas une faute en soi — un audit peut être juste. Mais la lentille 4
(cadrage adverse) demande de chercher où l'affirmation est fausse, et une case
au moins confirme sans mesurer
(`CLAUDE-CURSOR-bb8fe11-…md:53`, ligne §6, verdict **CONFIRMED**) :

> Point 5 : … `HANDOFF.md` (non relu ligne à ligne ici, hors périmètre de cette
> PR) est cité comme preuve externe — **plausible, non contesté**.

« Plausible, non contesté » est l'absence de verdict, pas un verdict. Sous la
forme imposée (`review-guidelines.md:50-56`, « un constat sans preuve citable
ne doit pas être émis »), cette sous-partie appelait `NEEDS_OWNER`, comme le
§9 voisin l'a fait correctement.

### P2-2 — Un commit signé « Cursor Agent » modifie un chemin hors `inbox/` sans que la garde de périmètre s'exécute

```
$ git show --stat --format='%an <%ae> | %s' e2896e7
Cursor Agent <cursoragent@cursor.com> | challenge: la ligne de ledger sort de la PR …
 architecture/audit-ledger.jsonl | 1 -
```

Le fichier touché (`architecture/audit-ledger.jsonl`) est hors
`architecture/inbox/**`, qui est le seul chemin qu'un livrable Cursor peut
toucher (`architecture/agents/cursor-auditor.md` § Interdits). Et la garde
prévue pour ça, `audit-guard / cursor-scope`, **n'a pas tourné** — `gh pr
checks 44` la donne deux fois `skipping`, parce qu'`audit-guard.yml:30` teste
un préfixe de branche (`cursor/`) et que la branche est `forge-bot/*`.

Deux lectures possibles, et je ne tranche pas : soit le propriétaire s'est
servi de Cursor comme d'un éditeur (ce qui est son droit, et le
co-`Author` `PLiagre@users.noreply.github.com` va dans ce sens), soit un rôle
auditeur a écrit hors de son périmètre. Le point est que **le dépôt ne peut
pas faire la différence** : rien ne distingue mécaniquement « Cursor
auditeur » de « le propriétaire via Cursor ».

*Antériorité assumée* : `CURSOR-bb8fe11` (P2-3) portait déjà « la garde teste
un préfixe, pas une identité », et proposait un brief pour ça. Élément
nouveau : **première occurrence mesurée** où un commit d'auteur Cursor modifie
réellement un chemin non-`inbox` avec la garde inactive. Je ne propose pas de
brief : c'est un renfort de preuve pour celui de `CURSOR-bb8fe11`.

### P2-3 — La ligne de ledger que ce livrable produira reste fausse (déjà porté, non tranché)

La simulation du P0-1 montre le champ que `record_challenge` inscrirait :
`{"CONFIRMED": 15, "REFUTED": 2, "PARTIAL": 5, "NEEDS_OWNER": 6}`. Or la revue
n'a **aucun** verdict REFUTED ; les deux occurrences comptées sont la phrase
de gabarit (`:11`, « Un verdict par point : CONFIRMED / REFUTED / … ») et la
phrase qui nie tout refus (`:114`, « **Aucun REFUTED.** ») :

```
$ grep -n "REFUTED" CLAUDE-CURSOR-bb8fe11-….md
11:Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.
114:**Aucun REFUTED.** Le seul point que je ne peux ni confirmer ni infirmer par
```

**Ce constat est déjà porté**, à l'identique, par `CURSOR-779d97c` (P1-3, « le
champ `verdicts` du ledger n'est pas un décompte de verdicts »), audit encore
au stade `AUDIT_CHALLENGED` — donc ni retenu ni écarté. Conformément à
`review-guidelines.md:57-59`, je ne le ré-émets pas comme constat neuf et **je
ne propose aucun brief** pour lui. Le seul élément à verser au dossier : c'est
la **deuxième occurrence mesurée**, et cette fois les deux moitiés du défaut
(comptage faux côté `parse_verdicts`, illisibilité côté `_parse_point_verdicts`)
se déclenchent sur **le même fichier** — ce qui fait passer le sujet d'anomalie
ponctuelle à régularité.

### P3-1 — La reproduction phare de la revue n'est épinglée à aucun SHA

La revue écrit (`:20-23`) que `pytest` a été rejoué « directement sur le HEAD
actuel » et donne `309 passed, 16 skipped`, « identique au caractère près » à
la sortie de l'audit relu — laquelle avait été mesurée sur `bb8fe11`.

Vérifié des deux côtés :

```
$ git worktree add /tmp/wt-bb8fe11 bb8fe11b860f8383e5178994f35ca116f89da2fd
$ (cd /tmp/wt-bb8fe11 && …/python -m pytest harness/tests/ -q | tail -1)
309 passed, 16 skipped in 17.39s

$ .venv/bin/python -m pytest harness/tests/ -q | tail -1     # master, 13:52
311 passed, 16 skipped in 17.28s
```

Le chiffre est donc **exact au commit audité** — je ne conteste pas le fond.
Mais il a déjà dérivé de 2 en moins de deux heures, et « le HEAD actuel » ne
nomme pas de SHA : la preuve la plus forte de la revue n'est pas rejouable par
un tiers dans six mois. Nommer le SHA coûte une commande.

### P3-2 — Le corps de la PR est le gabarit générique et ne dit rien de son propre contenu

Corps intégral de la PR :

> Contre-audit produit headless par claude-challenger (run 31598872392).
> La fusion de cette PR déclenche pipeline-orchestrate.yml (event review_recorded).

C'est le texte figé de `pipeline-challenge.yml:200`. Il ne mentionne ni les
conclusions de la revue, ni le second commit et son changement de convention,
ni le fait que la complétude du livrable dépend d'un job externe post-fusion.
La lentille 1 (intention avant diff) demande de commencer par la spec : ici,
un relecteur ne peut pas savoir ce qu'il fusionne sans ouvrir le fichier. Et
la seule phrase du corps qui affirme quelque chose de vérifiable — « la fusion
déclenche pipeline-orchestrate » — est celle qui s'est révélée fausse dans ses
effets (P0-1).

## 3. Ce que cette PR fait bien

Sans complaisance, sept points tiennent à la vérification :

1. **Taille exemplaire** : 1 fichier, +131 lignes — très en deçà du seuil de
   ~5 fichiers / quelques centaines de lignes de `review-guidelines.md:38-41`.
   La lentille 5 (taille et découpage) n'a rien à redire.
2. **Périmètre propre** : le diff net ne touche que `architecture/reviews/**`,
   exactement ce que l'allowlist du merge-bot autorise (`merge-bot.yml:50`),
   et rien de la denylist (`:43`).
3. **Séparation des rôles tenue** : le producteur de l'audit (Cursor) et son
   contre-auditeur (Claude) sont bien deux acteurs distincts, comme l'exige
   `review-guidelines.md:34-37`.
4. **Reproduction indépendante réelle** : la revue ne relit pas, elle rejoue —
   et souvent par une voie *différente* de celle de l'audit (chronologie
   reconstruite à partir des horodatages `git` faute de `gh`). C'est la bonne
   discipline.
5. **Honnêteté sur ses limites** : 4 points marqués `PARTIAL`/`NEEDS_OWNER`
   plutôt que supposés. L'inverse de la correction hallucinée.
6. **Auto-correction utile** : la revue relève trois décalages de numéros de
   ligne dans l'audit relu (`:112` vs `:116`, `:45/:47` vs `:41/:43`) tout en
   montrant que le texte cité existe verbatim — elle corrige la preuve sans
   abattre le constat.
7. **Le second commit va dans le bon sens** : appliquer la convention post-#46
   plutôt que laisser un conflit d'append programmé sur le ledger est le bon
   réflexe. C'est le destinataire de la délégation qui est cassé, pas
   l'intention.

## 4. Risques par sévérité

| Sévérité | Constat | Preuve principale |
|---|---|---|
| **P0** | P0-1 — la transition `AUDIT_CHALLENGED` est perdue silencieusement à la fusion ; ledger et dossier `reviews/` se contredisent sur `master` | run `31603909788` en échec, étape 7 `skipped`, `grep -c bb8fe11` du ledger `master` → `0` |
| **P1** | P1-1 — l'auto-fusion précède l'audit Cursor ; le prédicat 4 de la porte de fusion est insatisfiable par construction | `mergedAt 13:54:37` vs job de lancement terminé `13:49:07` ; `pipeline-audit.yml:185-196` |
| **P1** | P1-2 — le contre-audit headless n'a pas de `GH_TOKEN` alors que l'étape voisine du même job en a un | `pipeline-challenge.yml:144-157` vs `:171-176` ; revue `:30-34` |
| **P2** | P2-1 — 0 REFUTED sur 16 points, une case `CONFIRMED` contenant « plausible, non contesté » | revue `:53` ; ledger, 4 revues antérieures à 15/3/2/1 REFUTED |
| **P2** | P2-2 — commit d'auteur Cursor hors `inbox/`, garde `cursor-scope` inactive | `git show e2896e7` ; `gh pr checks 44` → `cursor-scope skipping` ; `audit-guard.yml:30` |
| **P2** | P2-3 — champ `verdicts` du ledger faux (`REFUTED: 2` pour « Aucun REFUTED ») — **déjà porté par `CURSOR-779d97c`, aucun brief proposé ici** | revue `:11` et `:114` ; simulation § P0-1 |
| **P3** | P3-1 — reproduction `pytest` non épinglée à un SHA (309 à `bb8fe11`, 311 à `master`) | les deux sorties `pytest` ci-dessus |
| **P3** | P3-2 — corps de PR générique, sa seule affirmation vérifiable est fausse | corps de la PR = `pipeline-challenge.yml:200` |

## 5. Briefs atomiques proposés (3 — aucun n'est une instruction)

**Brief A — rendre la transition post-fusion atomique et bruyante.**
Périmètre : `harness/pipeline/orchestrator.py`, `pipeline-orchestrate.yml`.
Problème mesuré : un `decide_auto` qui refuse annule aussi l'écriture d'un
`AUDIT_CHALLENGED` déjà validé par sa propre porte, et l'échec ne produit
aucun signal hors du log de run. Deux effets à séparer : *enregistrer que la
revue existe* et *décider quoi en faire*. Test rouge disponible immédiatement :
le fichier réel de la PR #44, qui reproduit l'échec en trois lignes de Python
(§ P0-1).

**Brief B — refermer la course entre l'auto-fusion et l'audit asynchrone.**
Périmètre : `pipeline-audit.yml`, `merge-bot.yml`,
`docs/rules/conditional-merge-gate.md`. Problème mesuré : le job qui *lance*
l'auditeur devient vert en 18 s ; l'auto-fusion part 5 minutes avant que
l'audit existe. **À fusionner avec le brief 1 déjà proposé par
`CURSOR-bb8fe11`** (« porte de fusion lisible ») plutôt qu'ouvert en doublon —
c'est le même prédicat 4, vu côté machine au lieu de côté Hermes.

**Brief C — donner au contre-audit headless de quoi vérifier la CI.**
Périmètre : `pipeline-challenge.yml`, étape « Invoke claude-challenger ».
Problème mesuré : 4 des 16 verdicts de cette revue sont dégradés faute d'un
jeton présent trois étapes plus bas dans le même job. Le contrat
`claude-challenger` demande de vérifier la véracité technique ; la
classification CI en fait partie.

*Aucun brief pour P2-2* (renfort de `CURSOR-bb8fe11` P2-3) ni pour **P2-3**
(déjà porté par `CURSOR-779d97c` P1-3, non tranché).

## 6. Commandes rejouées

Toutes les commandes citées ont été exécutées le 2026-08-12 sur
`master` = `8d9d8f2` (et sur les SHA nommés). Les sorties sont collées
intégralement aux sections concernées ; les principales :

```
git fetch origin pull/44/head:pr44 ; git log --oneline -2 pr44
gh pr view 44 -R PLiagre/ForgeHistory --json state,mergedAt,mergeCommit,mergedBy
gh pr checks 44 -R PLiagre/ForgeHistory
gh run list -R PLiagre/ForgeHistory --workflow=pipeline-orchestrate.yml --limit 12
gh run view 31603909788 -R PLiagre/ForgeHistory --log-failed
gh api repos/PLiagre/ForgeHistory/actions/runs/31603909788/jobs --jq '.jobs[].steps[]…'
git show origin/master:architecture/audit-ledger.jsonl | grep -c bb8fe11        # -> 0
.venv/bin/python -m pytest harness/tests/ -q                                    # 311 passed, 16 skipped
(cd /tmp/wt-bb8fe11 && …/python -m pytest harness/tests/ -q)                    # 309 passed, 16 skipped
python3 -c "audit_review.parse_verdicts(review)"                                # CONFIRMED 15, REFUTED 2, PARTIAL 5, NEEDS_OWNER 6
python3 -c "audit_decision.decide_auto(…)"                                      # DecisionError, no '| N | … |' rows
```

Historique `pipeline-orchestrate` (contexte du P0-1) : sur les 10 derniers
runs, **9 sont en échec** ; le seul succès est le merge de la PR #26
(`CURSOR-cdc683f`), dont la revue numérotait ses points `1`, `2`, `3`…

## 7. Sources externes

| # | source | ce qu'elle apporte | consulté le |
|---|---|---|---|
| S1 | Microsoft Open Source Blog — *Conductor: Deterministic orchestration for multi-agent AI workflows* (2026-05-14) — <https://opensource.microsoft.com/blog/2026/05/14/conductor-deterministic-orchestration-for-multi-agent-ai-workflows/> | La topologie d'un pipeline d'agents doit être **déclarée, pas découverte à l'exécution** ; c'est ce qui rend l'enchaînement auditable. Appui du P0-1 : ici, la transition d'état dépend d'un enchaînement implicite entre deux workflows, et se perd quand l'un échoue. | 2026-08-12 |
| S2 | *Stop building agents like prompts. Build them like state machines.* — <https://blogs.subhanshumg.com/stop-building-agents-like-prompts-build-them-like-state-machines> | « Les échecs sont rejoués de façon déterministe, pas retentés au hasard » ; les effets de bord doivent être **idempotents et à clé d'idempotence**, l'état survivant au crash du worker. Appui direct du brief A : l'écriture du ledger et la décision sont deux effets qui ne devraient pas partager le même sort. | 2026-08-12 |
| S3 | AWS — *Agent orchestration* (série AI Agent Learning) — <https://aws.amazon.com/marketplace/build-learn/ai-agent-learning-series/agent-orchestration> | L'exécution durable exige que **chaque transition d'état soit persistée** et fournisse la piste d'audit ; à défaut, une étape interrompue laisse le système dans un état non observable. Appui du P0-1 (étape 7 `skipped` = transition perdue). | 2026-08-12 |
| S4 | Microsoft Community Hub — *CI/CD for AI Agents on Microsoft Foundry* — <https://techcommunity.microsoft.com/blog/educatordeveloperblog/cicd-for-ai-agents-on-microsoft-foundry/4522218> | Les portes d'évaluation se placent **pré-fusion (CI)**, pas après ; une porte qui s'exécute après la promotion n'est pas une porte. Appui du P1-1 (auditeur lancé en fire-and-forget, fusion 5 min avant l'audit). | 2026-08-12 |
| S5 | Growin — *AI Agents in Software Development: A 2026 CTO Guide* — <https://www.growin.com/blog/ai-agents-in-software-development-26/> | L'orchestration CI/CD par agent n'est fiable que si le pipeline sous-jacent est robuste (« failure modes définis, journalisation structurée ») ; les couches agentiques greffées sur une infra fragile héritent de sa fragilité. Appui du P0-1 et du P3-2. | 2026-08-12 |
| S6 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30) — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | Plafonds durs **par agent et par workflow** appliqués à la couche d'orchestration, avec blocage ou escalade humaine à l'épuisement. Contexte pour le plafond `--max-budget-usd 5.00` de `pipeline-challenge.yml:155` : il est du bon type, appliqué à l'invocation. | 2026-08-12 |
| S7 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers* (2026) — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | Le budget par session est « le contrôle le plus efficace contre les boucles emballées », et il doit être **impossible à contourner par l'agent** — donc posé hors de son code. Le `ci_budget_guard.py precheck`/`record` du dépôt suit ce motif ; rien à redire sur ce point dans cette PR. | 2026-08-12 |

## 8. Ce que cet audit n'est pas

Un audit `PROPOSED`. Aucun des trois drapeaux d'autorisation n'est levé ; rien
ici ne doit être implémenté du seul fait d'y figurer. La suite appartient au
contre-audit de Claude, puis au propriétaire
(`architecture/README.md` § Cycle de vie).
