---
audit_id:                CURSOR-9626e9b-pr85-p0-perdu-a-la-decision
auditor:                 cursor-cloud
target_branch:           master
target_commit:           9626e9bf0aa2ffa3a05cac4329ac951db8f89479
created_at:              2026-08-13T13:20:00Z
audit_type:              pr-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #85 — « challenge: revue de l'audit CURSOR-827d54e-contre-audit-paye-jamais-publie »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006).

**Résumé en une phrase.** Le contenu de la revue est exact — j'ai rejoué chacune
de ses mesures locales et toutes tombent au chiffre près — mais la machine qui
la consomme n'a lu que **5 de ses 15 verdicts**, et les 10 verdicts perdus
contiennent **le seul P0** et **les deux P1** du constat 2 : la décision
automatique a donc approuvé l'audit en ne retenant que ses constats les moins
graves.

## 0. Identité de l'objet audité

| | |
|---|---|
| PR | [#85](https://github.com/PLiagre/ForgeHistory/pull/85), `forge-bot/review-CURSOR-827d54e-contre-audit-paye-jamais-publie-31693684053` → `master` |
| Tête de la PR | `dd608f1f1c7ddadc1a6a327ed99cc7c413ab5506` (auteur du commit : `forge-bot`) |
| Commit de fusion audité | `9626e9bf0aa2ffa3a05cac4329ac951db8f89479` (un seul parent : `4ceadec`) |
| Diff | 1 fichier, +112 / −0 : `architecture/reviews/CLAUDE-CURSOR-827d54e-contre-audit-paye-jamais-publie.md` (nouveau) |
| Ouverte / fusionnée | 2026-08-13T12:51:04Z / 2026-08-13T12:51:32Z (**28 s**), fusionnée par `PLiagre` |
| Run producteur | `pipeline-challenge` [31693684053](https://github.com/PLiagre/ForgeHistory/actions/runs/31693684053), 11:01:30Z → 11:10:11Z, `success` |

## 1. Classification de la CI du commit audité

**Verte**, sur les deux événements (`pull_request` sur `dd608f1`, puis `push`
sur `master`). `gh pr checks 85` rejoué :

```
actionlint pass | f0-demo pass | gitleaks pass | schema pass | tests pass
sim-tests pass | check-and-automerge pass | invoke-cursor-auditor pass
cursor-scope skipping        (le job ne s'exécute que si head_ref commence par cursor/)
Reconcile local Hermes State  pending  (run 31702047161, jamais démarré)
```

Deux réserves, sans conséquence sur le verdict de couleur :

- `cursor-scope` est **ignoré** ici : `audit-guard.yml:30` conditionne le job à
  `startsWith(github.head_ref, 'cursor/')`, et la branche est `forge-bot/*`.
  La porte de portée existe donc pour les PR d'auditeur, pas pour les PR de
  revue.
- Le job `Reconcile local Hermes state` est `pending` et le restera : il vise un
  runner auto-hébergé (`hermes-observer.yml:32`, `runs-on: [self-hosted,
  Windows, X64, hermes-observer]`) hors ligne. Il n'a bloqué ni la fusion ni le
  reste.

## 2. Lentille 1 — intention avant diff

L'intention est lisible dans la description de la PR : publier le contre-audit
de `CURSOR-827d54e`, un seul fichier sous `architecture/reviews/**`, chemin
allowlisté du merge-bot, la fusion déclenchant `pipeline-orchestrate`. Le diff y
répond : un seul fichier, exactement à ce chemin. Le contrat de rôle est tenu
sur la forme (Claude écrit dans `reviews/`, personne d'autre).

C'est sur le **contenu annoncé** que l'intention se détache du diff — voir F3.

## 3. Constats

### F1 — `P0` — La décision automatique n'a lu que 5 des 15 verdicts, et les trois plus graves sont parmi les 10 perdus

Le tableau de la revue porte **15 lignes de verdict** (11 `CONFIRMED`,
4 `PARTIAL`). La politique automatique n'en voit que **5**. Rejeu, avec le code
du dépôt, au commit de fusion :

```
$ python3 -c "import sys; sys.path.insert(0,'harness'); import audit_decision as d; \
    print(d.parse_point_verdicts(open('architecture/reviews/CLAUDE-CURSOR-827d54e-contre-audit-paye-jamais-publie.md').read()))"
[(3, 'CONFIRMED'), (4, 'PARTIAL'), (5, 'CONFIRMED'), (6, 'PARTIAL'), (7, 'CONFIRMED')]

$ # identifiants réels de la première cellule des 15 lignes de verdict
['1a', '1b', '1c', '2a', '2b', '3', '4', '5', '6', '7',
 '4b (§4 « ce qui tient »)', '4c', '4d', '4e', '4f']
```

Cause : `harness/audit_decision.py:75-78` exige un entier nu en première
cellule.

```python
_POINT_VERDICT_ROW = re.compile(
    r"^\|\s*(\d+)\s*\|.*?\|[\s*_`~]*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\b[^|]*\|",
    re.MULTILINE,
)
```

Ce que le registre a écrit ensuite (ligne 59 de `architecture/audit-ledger.jsonl`
sur `master`, et le fichier de décision correspondant) :

```
{"timestamp": "2026-08-13T12:51:53Z", "audit_id": "CURSOR-827d54e-contre-audit-paye-jamais-publie",
 "event": "AUDIT_APPROVED", "actor": "policy:auto", ...,
 "retained_points": [3, 4, 5, 6, 7]}
```

Le sous-ensemble retenu est **exactement** l'ensemble des lignes dont
l'identifiant est un entier nu. La sévérité n'entre nulle part dans le calcul.
Conséquence, en comparant les verdicts de la revue aux points qu'elle juge :

| | retenus par la politique | écartés en silence |
|---|---|---|
| Constats de l'audit | 3 (`P1`), 4 (`P2`), 5 (`P2`), 6 (`P3`), 7 (`P3`) | 1a **(le seul `P0`)**, 1b, 1c, 2a (`P1`), 2b (`P1`) |
| « Ce qui tient » | — | 4b, 4c, 4d, 4e, 4f |

La perte n'est pas cosmétique, parce que `retained_points` est la seule entrée
de la conversion en brief : `harness/audit_convert.py:94` va chercher
`event.get("retained_points")`. Par le chemin automatique, le `P0` de cet audit
**ne peut pas** devenir un brief : il n'existe plus dans le registre.

Détail qui referme la boucle : le point `2a` — écarté — est précisément le
diagnostic du sur-comptage de `parse_verdicts`, celui qui a corrompu la ligne
`AUDIT_CHALLENGED` de cette PR même (voir F2). La boucle a donc perdu son propre
rapport de bug.

Mesure sur tout le corpus, pour situer : sur les 19 revues de
`architecture/reviews/`, **51 lignes de verdict sur 233 sont invisibles** à la
politique. Trois revues sont à 0 lu (`bb8fe11` 16/16 perdues, `73022bd` 14/14,
`5633ee7` 5/5) — cas déjà connu et bruyant, il bloque la boucle. Deux revues
sont dans le cas **silencieux**, plus dangereux, parce qu'elles ont été
approuvées sur une base tronquée : `4c45718` (6 perdues sur 16) et celle-ci
(10 sur 15).

Ce qui rend ce constat non-doublon. La cécité du parseur est déjà posée par
`CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort` (F2, `P1`, encore
`AUDIT_PROPOSED`), sous l'angle « le seul `NEEDS_OWNER` du document est perdu ».
Je ne re-facture pas le mécanisme. L'élément nouveau est la **nature de la
perte** ici : ce n'est plus un destinataire manquant, c'est une **inversion de
sévérité** — la machine a retenu 1 `P1`, 2 `P2` et 2 `P3`, et jeté 1 `P0` et
2 `P1`. Et la conséquence est cette fois chiffrable en aval : la conversion ne
verra jamais le `P0`.

La PR est fusionnée depuis 12:51:32Z ; ce `P0` ne peut donc plus bloquer *cette*
fusion. Il se lit : aucune revue de plus ne devrait être enregistrée dans cet
état.

### F2 — `P1` — La garde censée protéger cette lecture se satisfait d'une seule ligne, et le code affirme un invariant qu'il n'a pas

`harness/audit_review.py:180-193` refuse une revue illisible. Le commentaire dit
pourquoi, et il dit vrai sur le cas qui l'a motivé :

```python
    # Same parse as audit_decision.decide_auto: the AUDIT_CHALLENGED event
    # promises "the auto-decision can read this review". The first real
    # headless challenge that broke this promise (CLAUDE-CURSOR-bb8fe11-...,
    # 2026-08-12) numbered its rows `§1` / `P1-1` instead of `| 1 |` and
    # stalled the loop AFTER merge, where nobody could fix it -- refusing
    # here, at record time, puts the error in front of the actor (Claude)
    # who can still rewrite the table.
    if not audit_decision.parse_point_verdicts(text):
```

La condition est `if not …` : elle ne se déclenche qu'à **zéro** ligne lue.
À 5 lignes lues sur 15, elle passe. La promesse inscrite dans le commentaire
(« la décision automatique peut lire cette revue ») est donc tenue au sens
« au moins une ligne », pas au sens « la revue ». Rien, à aucun moment, ne
compare *ce qui est écrit* à *ce qui est lu* — et c'est ce qui manquait pour
attraper F1.

Même écart entre le texte et le fait dans la docstring de
`audit_decision.py:203-205` : « **one parser, one contract, no second place that
could disagree with the first** ». Il y a deux parseurs, et ils divergent sur ce
document précis. Trois nombres pour un seul fichier :

| source | ce qu'elle dit du fichier | preuve |
|---|---|---|
| `parse_verdicts` → ligne 58 du registre | `CONFIRMED 18, REFUTED 4, PARTIAL 10, NEEDS_OWNER 5` | rejeu ci-dessous ; ledger `master` ligne 58 |
| Le fichier lui-même (lignes de tableau) | 11 `CONFIRMED`, 4 `PARTIAL`, 0 `REFUTED`, 0 `NEEDS_OWNER` | comptage cellule par cellule, § 7 |
| `parse_point_verdicts` → la décision | 3 `CONFIRMED`, 2 `PARTIAL` | rejeu en F1 |

La revue ne réfute **rien** et n'adresse **rien** au propriétaire dans son
tableau ; le registre lui attribue 4 `REFUTED` et 5 `NEEDS_OWNER`. Le
sur-comptage lui-même est déjà posé deux fois
(`CURSOR-786ec32-…` P0-1, `CURSOR-4b6dcff-…` F1) et je ne le re-facture pas.
L'élément nouveau est la **contradiction entre l'invariant écrit dans le code et
l'artefact produit** : un commentaire qui certifie une propriété que le dépôt
démentait au moment où il a été écrit est exactement la « correction hallucinée »
que `review-guidelines.md` demande de chercher en priorité (lentille 6) — une
affirmation de justesse non mesurée. [S6, S8]

Précision d'ordonnancement, qui explique pourquoi personne n'a pu corriger : la
garde tourne bien **pendant** l'invocation, avant la PR — mais la ligne qu'elle
écrit est ensuite **jetée**, et la ligne authentique est réécrite après la
fusion (`pipeline-challenge.yml:178-185`) :

```
# La ligne AUDIT_CHALLENGED écrite localement par le gate `record`
# pendant l'invocation ne part PAS dans la PR : […]
# La ligne authentique est ré-écrite sur master, après fusion, par
# pipeline-orchestrate.yml (orchestrator.py -> audit_review.record_challenge,
# mêmes gardes).
git checkout -- architecture/audit-ledger.jsonl || true
```

« Mêmes gardes » est exact — c'est bien le problème : la garde post-fusion est
aussi permissive que celle d'avant, et à ce moment le run du challenger est
terminé depuis 1 h 41. Le seul acteur qui pouvait réécrire le tableau n'existe
plus.

### F3 — `P1` — La description de la PR décrit un document qui n'existe pas, et reproduit exactement l'angle mort du parseur

La description annonce : « verdicts par point, **lignes de tableau : 3
CONFIRMED, 2 PARTIAL** ». Le fichier porte 15 lignes de verdict, dont 11
`CONFIRMED` et 4 `PARTIAL` (§ 7).

Ce n'est pas une invention : « 3 CONFIRMED, 2 PARTIAL » est **mot pour mot** ce
que `parse_point_verdicts` renvoie (F1). La seule prose lisible par un humain
avant la fusion décrit donc le document tel que la machine aveugle le voit, en
l'appelant « lignes de tableau ». Un relecteur qui fait confiance à la
description croit qu'il y a 5 verdicts : il ne peut pas voir qu'il en manque 10.
La description, qui est le dernier endroit où l'écart aurait pu être remarqué,
le camoufle.

Un cas voisin (`CURSOR-4b6dcff-…` F6, `P2`, sur la PR #73) relevait déjà un
sous-comptage mal étiqueté. L'élément nouveau ici : la **coïncidence exacte**
entre le chiffre annoncé et la sortie du parseur défaillant. Ce n'est pas une
approximation de rédaction, c'est la sortie du défaut recopiée en français, ce
qui la rend indétectable à la relecture. Je la classe un cran au-dessus pour
cette raison.

### F4 — `P2` — Le contre-audit de cette PR a coûté 2,93 $ et n'a laissé aucune ligne de dépense

La revue elle-même pose ce défaut (son point 4, `PARTIAL`, retenu par la
décision) mais dit ne pas avoir pu vérifier le montant faute de `GH_TOKEN`. Je
fournis la mesure qui lui manquait, pour son propre run :

```
$ gh run view 31693684053 --log | grep -oE '"total_cost_usd":[0-9.]+|num_turns":[0-9]+'
num_turns":67
"total_cost_usd":2.9291979000000006

$ git show origin/master:harness/pipeline/ci-budget-ledger.jsonl | wc -c
1
```

Après la fusion de la PR #85 et le passage de `pipeline-orchestrate`, le
registre de dépenses fait toujours **1 octet**. Mécanisme confirmé par lecture :
l'étape de publication ne stage que `architecture/reviews`
(`pipeline-challenge.yml:194`), la mesure de coût reste dans le workspace du
runner et meurt avec lui. L'élément nouveau n'est pas le mécanisme mais le
montant : l'acte même de relire l'audit « contre-audit payé jamais publié » a
coûté 2,93 $ et n'a rien laissé au registre. La pratique 2026 sur ce point est
l'inverse — la dépense d'un agent se pose en porte de CI, pas sur la facture du
mois. [S9, S10]

### F5 — `P3` — Trois motifs déjà posés, cités et non re-facturés

`review-guidelines.md` interdit de rejouer un motif déjà enregistré sans élément
nouveau. Je les mentionne pour la traçabilité, sans les compter comme constats :

| motif | mesure sur cette PR | déjà posé par |
|---|---|---|
| Le « maillon critique » d'ADR-0010 se prononce après la fusion | 28 s entre ouverture et fusion ; `invoke-cursor-auditor` = 19 s (un dispatch) ; cet audit est déposé ≈ 30 min après la fusion | `CURSOR-ab0e7f0-…` P0-1 (6 s), `CURSOR-a7d1c57-…` P2-3 (36 s) |
| `gh pr create` refusé, branche poussée, PR ouverte à la main | branche poussée à 11:10Z, PR ouverte à 12:51:04Z → **1 h 41** de latence, sans échec de job | `CURSOR-827d54e-…` (l'audit relu ici), `HANDOFF.md` ×3 |
| Aucun schéma ne valide `architecture/reviews/**` | le job `schema` est vert sans avoir lu le seul fichier du diff (`audit_schema.py:26, 98` ne parcourt que `inbox/CURSOR-*.md`) | `CURSOR-786ec32-…` P2-2 |

Pour le deuxième : la revue soutient au point 1c que la relecture « est
retrouvée à chaque fois observée, avec un délai, pas silencieusement absorbée ».
Sa propre publication confirme la thèse **et** en donne le prix : 1 h 41
d'attente d'un geste humain, sur une chaîne annoncée sans humain dans la boucle.

## 4. Ce qui tient — vérifié, pas concédé

J'ai cherché à faire tomber les mesures de la revue (lentille 4 : « trouve où
cette affirmation est fausse »). **Aucune n'est tombée.** Rejeu dans un
git-worktree posé sur le commit qu'elle audite (`827d54e`) :

| affirmation de la revue | rejeu | résultat |
|---|---|---|
| Point 3 : 6 marqueurs `TODO (planificateur)` dans les briefs 013 et 014 | `grep -c` sur les deux `brief.md` | `6` et `6` — conforme |
| Point 3 : le gate rend `REJECT` sur 014 | `python3 harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte` | `VERDICT: REJECT` — conforme |
| Point 7 : `harness_audit.py` rend 20/24 avec 2 `FAIL` | `python3 harness/harness_audit.py` | `SCORE: 20/24`, `FAIL fake_honest_demo_pair`, `FAIL no_premature_stub_content` — conforme |
| Point 4c : `314 passed, 16 skipped` et `25 passed` | `pytest harness/tests/ -q` puis `pytest sim/tests/ -q` | `314 passed, 16 skipped` ; `25 passed` — conforme |
| Point 1 : PR #65 `additions=843`, `files=11`, fusionnée 10:47:51Z | `gh pr view 65` | identique |
| Point 1c : #71 fusionnée 11:00:01Z et #73 à 11:01:37Z, **avant** l'audit (11:05:00Z) | `gh pr view 71 / 73` | identique — la chronologie qu'elle avance est juste |

C'est le point le plus important de cet audit : **le document est bon, c'est sa
consommation qui est fausse**. La revue a payé 2,93 $ et 67 tours pour produire
15 verdicts exacts et une nuance argumentée sur le point 1 ; la machine en a
gardé 5, choisis par la forme d'une cellule de tableau. Le défaut n'est pas dans
le jugement de l'agent, il est dans le tuyau qui le transporte — et c'est
exactement le mode de défaillance que la littérature 2026 attribue au fait de
lire une sortie d'agent avec une expression régulière au lieu d'un schéma
imposé. [S6, S7, S8]

## 5. Lentilles 3 et 5 — portes mécaniques, taille

- **Lentille 3 (portes mécaniques d'abord).** Les portes ont tourné et sont
  vertes (§ 1), mais aucune ne regarde le fichier du diff : `schema` ne lit que
  `inbox/`, `cursor-scope` est désactivé sur une branche `forge-bot/*`, `tests`
  et `sim-tests` ne connaissent pas `reviews/`. Le seul lecteur mécanique du
  fichier est la garde de F2, et elle passe à 5/15. Le jugement humain n'a donc
  rien à quoi s'adosser ici — la porte manque, elle n'est pas juste faible.
- **Lentille 5 (taille et découpage).** 1 fichier, +112 lignes : sous le seuil.
  Rien à signaler. Réserve de lisibilité : 5 lignes du tableau dépassent
  1 000 caractères (maximum mesuré : 1 307 ; `awk 'length($0)>1000'`), ce qui
  rend le document difficile à relire ligne par ligne sans outil — motif déjà
  signalé ailleurs (`CURSOR-786ec32-…` P3-1), non re-facturé.

## 6. Tableau des sévérités

| id | sévérité | constat | preuve |
|---|---|---|---|
| F1 | **P0** | 10 des 15 verdicts perdus à la décision ; retenus = 1 `P1` + 2 `P2` + 2 `P3`, écartés = 1 `P0` + 2 `P1` ; `retained_points` est la seule entrée de la conversion | `audit_decision.py:75-78, 196-206` ; rejeu `parse_point_verdicts` ; ledger `master` ligne 59 ; `audit_convert.py:94` |
| F2 | **P1** | La garde ne se déclenche qu'à zéro ligne lue (`if not …`, ligne 187) ; la docstring affirme « one parser, one contract » alors que les deux parseurs divergent (18/4/10/5 vs 11/4 vs 3/2) | `audit_review.py:174, 180-193` ; `audit_decision.py:203-205` ; ledger ligne 58 ; `pipeline-challenge.yml:178-185` |
| F3 | **P1** | La description annonce « 3 CONFIRMED, 2 PARTIAL » pour un document qui en porte 15 — chiffre identique à la sortie du parseur défaillant | description de la PR #85 ; comptage § 7 ; rejeu F1 |
| F4 | **P2** | Run de contre-audit à 2,93 $ / 67 tours ; `ci-budget-ledger.jsonl` toujours à 1 octet après fusion | `gh run view 31693684053 --log` ; `git show origin/master:…` ; `pipeline-challenge.yml:194` |
| F5 | **P3** | Trois motifs déjà enregistrés, mesurés à nouveau ici (28 s de fusion ; 1 h 41 de latence de publication ; `reviews/**` sans schéma) — cités, non re-facturés | § 3 F5 |

## 7. Commandes rejouées — sortie collée

Toutes les commandes ci-dessous ont été exécutées en lecture seule, dans des
git-worktrees posés sur les commits concernés (`9626e9b` pour le commit audité,
`827d54e` pour les affirmations de la revue), depuis un clone à jour de
`master`.

```
$ git log --format='%H %P' -1 9626e9bf0aa2ffa3a05cac4329ac951db8f89479
9626e9bf0aa2ffa3a05cac4329ac951db8f89479 4ceadec8cd10f09aad68336a20bc2520c90db98f

$ # comptage réel des verdicts du fichier de revue, cellule par cellule
lignes de tableau brutes: 16   (dont 1 ligne d'en-tête « Verdict »)
verdicts par ligne de tableau -> {'CONFIRMED': 11, 'PARTIAL': 4}

$ # ce que le registre enregistre (audit_review.parse_verdicts, texte entier)
{'CONFIRMED': 18, 'REFUTED': 4, 'PARTIAL': 10, 'NEEDS_OWNER': 5}

$ # ce que la décision lit (audit_decision.parse_point_verdicts, lignes strictes)
[(3, 'CONFIRMED'), (4, 'PARTIAL'), (5, 'CONFIRMED'), (6, 'PARTIAL'), (7, 'CONFIRMED')]

$ # perte de lignes de verdict sur tout le corpus des revues
revue                                                          lignes  vues perdues
CLAUDE-CURSOR-4c45718-pr65-ledger-recupere-a-la-main.md            16    10       6
CLAUDE-CURSOR-5633ee7-automation-completeness.md                    5     0       5
CLAUDE-CURSOR-73022bd-hermes-dashboard-modele-auditeur.md          14     0      14
CLAUDE-CURSOR-827d54e-contre-audit-paye-jamais-publie.md           15     5      10
CLAUDE-CURSOR-bb8fe11-hermes-console-adr-0011.md                   16     0      16
TOTAL (19 revues)                                                 233   182      51

$ grep -n '827d54e' <(git show origin/master:architecture/audit-ledger.jsonl)
58:{… "event": "AUDIT_CHALLENGED", "actor": "claude", "verdicts": {"CONFIRMED": 18, "REFUTED": 4, "PARTIAL": 10, "NEEDS_OWNER": 5}}
59:{… "event": "AUDIT_APPROVED", "actor": "policy:auto", …, "retained_points": [3, 4, 5, 6, 7]}

$ git show origin/master:architecture/decisions/DECISION-CURSOR-827d54e-contre-audit-paye-jamais-publie.md
verdict: APPROVED
retained_points: [3, 4, 5, 6, 7]

$ # dans le worktree posé sur 827d54e — rejeu des affirmations de la revue
$ grep -c "TODO (planificateur)" harness/queue/briefs/013-*/brief.md harness/queue/briefs/014-*/brief.md
013-sim-tick-nourrit-une-fois/brief.md:6
014-pipeline-contre-audit-porte/brief.md:6
$ python3 harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
VERDICT: REJECT
$ python3 harness/harness_audit.py
SCORE: 20/24
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: …
$ pytest harness/tests/ -q
314 passed, 16 skipped in 17.03s
$ pytest sim/tests/ -q
25 passed in 0.90s

$ gh pr view 65 / 71 / 73   (vérification de la chronologie du point 1c)
PR #65 additions=843 files=11 merged=2026-08-13T10:47:51Z
PR #71 merged=2026-08-13T11:00:01Z
PR #73 merged=2026-08-13T11:01:37Z

$ gh run view 31693684053 --log | grep -oE '"total_cost_usd":[0-9.]+|num_turns":[0-9]+'
num_turns":67
"total_cost_usd":2.9291979000000006

$ git show origin/master:harness/pipeline/ci-budget-ledger.jsonl | wc -c
1
```

## 8. Propositions de briefs atomiques — 3 au plafond du contrat (propositions, pas instructions)

Aucune de ces trois lignes n'autorise quoi que ce soit ; le propriétaire ou le
policy engine décide, et la source unique d'instruction resterait le brief
éventuellement ouvert (`CLAUDE.md` › Single Source of Instruction).

1. **Lecture totale ou refus.** À l'enregistrement d'une revue, comparer le
   nombre de lignes de tableau portant un mot-verdict au nombre de lignes que
   `parse_point_verdicts` capte, et refuser dès qu'il y a écart — au lieu de se
   contenter d'« au moins une ligne » (F1, F2). Preuve rouge disponible d'emblée :
   la revue de cette PR (5/15) et celle de `4c45718` (10/16) doivent échouer, les
   13 autres passer. À arbitrer avec le brief `014-pipeline-contre-audit-porte`,
   déjà ouvert sur un sujet voisin, plutôt qu'en doublon.
2. **Un seul comptage, et qu'il soit celui du tableau.** Faire écrire au registre
   les verdicts issus des lignes du tableau, et non du texte entier, de sorte que
   `verdicts`, `retained_points` et le fichier disent le même chiffre (F2). La
   forme la plus robuste, si l'on veut supprimer le parseur plutôt que le
   réparer : demander au challenger une sortie structurée validée par schéma —
   c'est disponible dans l'outillage déjà utilisé [S7] et c'est la pratique
   recommandée contre exactement ce défaut [S6, S8].
3. **La dépense dans le même commit que la revue.** Ajouter la ligne de coût du
   run au commit publié, ou la porter par un chemin allowlisté, pour que
   `ci-budget-ledger.jsonl` cesse de faire 1 octet après une dépense mesurée
   (F4) [S9, S10].

## 9. Ce que cet audit ne prétend pas

- Je ne prétends pas que la revue soit fausse : je n'ai trouvé **aucune** mesure
  locale inexacte, et je le documente au § 4 plutôt que de le concéder du bout
  des lèvres.
- Je ne re-facture pas le sur-comptage de `parse_verdicts`, ni la fusion en
  quelques secondes, ni l'absence de schéma sur `reviews/**` : trois audits
  ouverts les portent déjà, je les cite (§ 3 F5, § 5).
- Je n'ai pas accès aux réglages du dépôt, donc je ne tranche pas la cause du
  refus de `gh pr create` (PAT contre réglage « Allow GitHub Actions to create
  and approve pull requests ») — la même limite que celle que la revue reconnaît
  au point 1b.
- Le montant de 2,93 $ vient des logs du run `31693684053` ; il couvre ce run et
  lui seul, pas le total de la session.
- Les états de runs GitHub Actions (`pending` du job Hermes) sont des instantanés
  non rejouables après coup, comme la revue le note elle-même pour son point 6.

## 10. Sources externes

| # | source | consulté le |
|---|---|---|
| S1–S5 | les cinq sources de `architecture/review-guidelines.md` (référentiel des six lentilles), reprises telles quelles | 2026-08-12 |
| S6 | Layra — *Structured Output Generation — AI & LLM Architecture Pattern Guide* : « stop parsing JSON with regex » ; contraindre la sortie par schéma au lieu de la parser — <https://layra4.dev/pattern/structured-output> | 2026-08-13 |
| S7 | Anthropic — *Claude Agent SDK · Structured outputs* : sortie validée contre un JSON Schema, ré-invite automatique en cas d'écart, erreur explicite si la validation échoue — <https://code.claude.com/docs/en/agent-sdk/structured-outputs> | 2026-08-13 |
| S8 | A. Aaliyan — *Agentic Instincts: Coding Agents and their love for Pattern Matching* : « the agent has quietly become a brittle parser with an LLM attached to it » ; décisions sémantiques par sortie structurée, validation déterministe ensuite — <https://aaliyan1230.substack.com/p/agentic-instincts-coding-agents-and> | 2026-08-13 |
| S9 | DEV Community — *Gate your AI agents' token cost in CI — before the bill, not after* : poser le coût jetons/dollars en porte de CI, `mode: warn` puis blocage — <https://dev.to/wartzarbee/gate-your-ai-agents-token-cost-in-ci-before-the-bill-not-after-67g> | 2026-08-13 |
| S10 | Augment Code — *From Assisted to Autonomous: How Far Can the Engineering Loop Close?* (état juillet 2026) : la frontière reste la fusion ; porte d'approbation explicite avant merge, traces rejouables et auditables — <https://www.augmentcode.com/guides/autonomous-engineering-loop> | 2026-08-13 |
