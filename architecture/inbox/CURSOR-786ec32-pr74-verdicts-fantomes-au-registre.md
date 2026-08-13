---
audit_id:                CURSOR-786ec32-pr74-verdicts-fantomes-au-registre
auditor:                 cursor-cloud
target_branch:           master
target_commit:           786ec32f520adbc0361914dff4e7b7314232973b
created_at:              2026-08-13T11:08:26Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #74 — le registre publie des verdicts qui n'existent pas

Objet audité : [PR #74](https://github.com/PLiagre/ForgeHistory/pull/74)
« challenge: revue de l'audit CURSOR-9e35764-pr63-contre-audit-jamais-enregistre ».
Commit de fusion `786ec32f520adbc0361914dff4e7b7314232973b`, branche source
`forge-bot/review-CURSOR-9e35764-pr63-contre-audit-jamais-enregistre-31683996328`,
fusionnée sur `master` le 2026-08-13 à 11:02:52Z.

Méthode : les six lentilles de
[`architecture/review-guidelines.md`](../review-guidelines.md). Chaque constat
porte une sévérité P0–P3 et cite sa preuve (fichier + lignes, ou commande
rejouée avec sa sortie collée). Cet audit **ne prescrit rien** : il propose,
la décision reste à la boucle (`architecture/README.md`, ADR-0005/0006).

## 0. Résumé

La PR ajoute **un seul fichier de 109 lignes**,
`architecture/reviews/CLAUDE-CURSOR-9e35764-pr63-contre-audit-jamais-enregistre.md`,
le contre-audit produit par `claude-challenger`. Le contenu de ce contre-audit
est honnête et sérieux : il rejoue ses mesures au lieu de recopier celles de
l'audit, et il dit explicitement où il n'a **pas** pu rejouer (logs de run en
`HTTP 403`). C'est exactement la discipline « preuve d'exécution, pas
affirmation » que le guide demande.

Le problème n'est pas dans le texte. Il est dans ce que la machine en a fait.
Ce document confirme, à son point 4, que le registre compte les verdicts avec
un parseur de mots au lieu d'un parseur de lignes de tableau. **Quatre-vingts
secondes après sa fusion, ce même défaut l'a frappé lui-même** : le registre
de `master` a publié `16 CONFIRMED / 5 REFUTED / 10 PARTIAL` pour un document
qui contient 7 CONFIRMED, 3 PARTIAL et **zéro** REFUTED. La description de la
PR annonce, elle, « 7 CONFIRMED, 3 PARTIAL » — le chiffre humain et le chiffre
machine sont en désaccord, sur `master`, dans un journal déclaré append-only.

Trois autres constats accompagnent celui-là : la revue fusionnée affirme deux
faits qui étaient déjà faux au moment de la fusion, la suite de tests ne peut
structurellement pas voir le défaut de comptage, et la porte de fusion à quatre
preuves n'a de nouveau pas été appliquée.

## 1. Intention avant diff — l'intention est lisible et le diff y répond

La description de la PR annonce précisément trois choses : (a) le contenu se
limite à un fichier sous `architecture/reviews/`, (b) ce fichier contient
« 7 CONFIRMED, 3 PARTIAL », (c) après fusion, `pipeline-orchestrate` enregistre
`AUDIT_CHALLENGED` puis applique la décision automatique.

Les points (a) et (b) sont vérifiés et exacts :

```
$ gh pr view 74 --json files -q '.files[] | "\(.additions)+ \(.deletions)- \(.path)"'
109+ 0- architecture/reviews/CLAUDE-CURSOR-9e35764-pr63-contre-audit-jamais-enregistre.md
```

Comptage réel des lignes de verdict du tableau (parseur du dépôt, voir § 2) :
7 `CONFIRMED` (points 1, 3, 4, 6, 8, 9, 10) et 3 `PARTIAL` (points 2, 5, 7).
La description dit vrai.

Le point (c) s'est produit, mais avec un contenu faux — c'est l'objet du
constat P0-1.

**Verdict de la lentille : conforme.** L'intention est écrite, vérifiable, et
le diff y correspond. C'est à porter au crédit de la PR.

## 2. P0-1 — Le registre a publié 5 REFUTED pour une revue qui n'en réfute aucun

**Sévérité : P0** (aurait dû bloquer la fusion).

### Le fait

La ligne 52 du registre `architecture/audit-ledger.jsonl` sur `master`, écrite
à 11:03:12Z, soit vingt secondes après la fusion :

```
$ git show origin/master:architecture/audit-ledger.jsonl | grep -n "9e35764" | head -1
52:{"timestamp": "2026-08-13T11:03:12Z", "audit_id": "CURSOR-9e35764-pr63-contre-audit-jamais-enregistre",
   "event": "AUDIT_CHALLENGED", "actor": "claude",
   "review": "architecture/reviews/CLAUDE-CURSOR-9e35764-pr63-contre-audit-jamais-enregistre.md",
   "verdicts": {"CONFIRMED": 16, "REFUTED": 5, "PARTIAL": 10, "NEEDS_OWNER": 4}}
```

Le document référencé par cette ligne ne contient **aucun** REFUTED. Il le dit
même en toutes lettres à sa § 4 (ligne 92 du fichier) : « Deux nuances, aucune
ne renverse un constat ». Le registre publie donc cinq réfutations imaginaires
et attribue à Claude un jugement qu'il n'a pas rendu.

### La cause, rejouée

```
$ .venv/bin/python -c "... parse_verdicts / parse_point_verdicts sur le fichier fusionné"
parse_verdicts (-> champ 'verdicts' du registre) : {'CONFIRMED': 16, 'REFUTED': 5, 'PARTIAL': 10, 'NEEDS_OWNER': 4}
parse_point_verdicts (tableau reel)              : {'CONFIRMED': 7, 'PARTIAL': 3}
```

Deux parseurs coexistent dans le dépôt et ne mesurent pas la même chose :

- `harness/audit_review.py` lignes 127-134 — `parse_verdicts()` compte chaque
  jeton comme **un mot, n'importe où dans le texte** (`re.findall(rf"\b{token}\b", text)`).
- `harness/audit_decision.py` ligne 196-206 — `parse_point_verdicts()` ne lit
  que les **lignes de tableau** `| N | ... | VERDICT | ... |`.

C'est le premier, le parseur de mots, dont le résultat part au registre :
`harness/audit_review.py` ligne 174 (`verdicts = parse_verdicts(text)`) puis
ligne 203 (`verdicts=verdicts`). Le second n'est utilisé, ligne 187, que comme
garde binaire « y a-t-il au moins une ligne lisible ? ».

### L'élément nouveau, qui justifie P0 et non un simple rappel

Ce mécanisme est déjà connu : l'audit `CURSOR-9e35764` l'a signalé en P1-1, la
revue de la PR #74 l'a marqué CONFIRMED (point 4), et la décision automatique
`architecture/decisions/DECISION-CURSOR-9e35764-...md` l'a retenu. Le
re-signaler à l'identique serait du bruit. Trois choses sont nouvelles :

1. **Il ne s'agit plus d'une prédiction.** L'audit écrivait « le registre
   *aurait* publié 14/4 » à propos d'un événement jamais enregistré. Ici,
   l'événement est enregistré, sur `master`, dans un journal que
   `architecture/README.md` (ligne 33) désigne comme ce qui « referme la boucle
   audit ↔ brief ». Le faux chiffre est devenu la source de vérité de la boucle.

2. **`inbox/` et le registre sont append-only** (`architecture/README.md`
   règle d'intégrité 3). La ligne 52 ne peut donc pas être corrigée par
   réécriture : toute correction devra prendre la forme d'un événement
   supplémentaire, ce qui suppose un mécanisme qui n'existe pas aujourd'hui.
   Chaque fusion supplémentaire aggrave la dette.

3. **Le plancher de faux verdicts vient du gabarit produit par l'outil
   lui-même.** Mesure inédite, rejouée sur la sortie de `scaffold_text()`,
   c'est-à-dire sur une revue **entièrement vide, sans un seul verdict rendu** :

```
[1] Gabarit genere par scaffold_text() (revue VIDE, 0 verdict reel)
  parse_verdicts (-> champ 'verdicts' du registre) : {'CONFIRMED': 2, 'REFUTED': 2, 'PARTIAL': 2, 'NEEDS_OWNER': 3}
  parse_point_verdicts (tableau reel)              : {}
```

La ligne de légende « Un verdict par point : CONFIRMED / REFUTED / PARTIAL /
NEEDS_OWNER. » (`harness/audit_review.py` ligne 76, dans le gabarit) et la
ligne d'exemple du tableau (ligne 88) injectent elles-mêmes les jetons que le
registre comptera ensuite. Autrement dit : **l'outil pollue sa propre mesure**,
et aucune revue produite par le gabarit ne peut publier un compte juste. Ce
n'est pas un cas limite, c'est le cas nominal.

### Contradiction interne de l'enregistrement

La même ligne 52 annonce 31 verdicts (16+5+10) pour un audit qui compte 10
points, et la ligne 53 qui la suit retient `retained_points: [1..10]` — parce
que la décision, elle, utilise le bon parseur. Le registre se contredit donc à
deux lignes d'intervalle : il publie des chiffres issus d'un parseur et des
points issus de l'autre.

Enfin, le commentaire de `harness/audit_decision.py` lignes 203-205 affirme :
« one parser, one contract, no second place that could disagree with the
first ». La ligne 52 du registre est la démonstration que cette affirmation est
fausse. C'est un commentaire qui décrit une propriété que le code n'a pas —
le motif « correction hallucinée » de la lentille 6 [S5, S3].

## 3. P1-1 — La revue fusionnée affirme deux faits qui étaient faux à la fusion

**Sévérité : P1.**

Le frontmatter du fichier porte `reviewed_at: 2026-08-13T08:56:47Z`. La PR a
été ouverte à 11:02:25Z et fusionnée à 11:02:52Z :

```
reviewed_at (frontmatter) : 2026-08-13T08:56:47Z
PR ouverte                : 2026-08-13T11:02:25Z
PR fusionnee              : 2026-08-13T11:02:52Z
revue -> fusion           : 2:06:05
```

Deux heures et six minutes séparent la mesure de sa publication. Rien, dans
cet intervalle, ne re-vérifie quoi que ce soit. Deux affirmations centrales du
document sont devenues fausses pendant ce délai :

1. Le document dit (§ 2, point 2, et § 4) que la PR #65 est « **encore
   ouverte, non fusionnée** au moment de cette revue ». Or :

```
$ gh pr view 65 --json number,state,mergedAt -q '.'
{"mergedAt":"2026-08-13T10:47:51Z","number":65,"state":"MERGED", ...}
```

   #65 a été fusionnée 14 minutes et 34 secondes **avant** l'ouverture de la
   PR #74.

2. Le document dit, dans la même ligne de tableau, « donc le registre de
   `master` est **toujours vide** de `a4de4bb` aujourd'hui ». Or le registre
   de `master` en contient trois lignes, entrées par le commit `4c45718` que
   la fusion de #65 a portées sur `master` :

```
$ git show origin/master:architecture/audit-ledger.jsonl | grep -c "a4de4bb"
3
$ git log --format='%h %cI %s' -S'"audit_id": "CURSOR-a4de4bb-...", "event": "AUDIT_CHALLENGED"' origin/master -- architecture/audit-ledger.jsonl | tail -1
4c45718 2026-08-13T08:41:03+00:00 boucle d'audit : décision a4de4bb rejouée, clôture 3b47ffe, graines 013/014
```

Ce n'est pas une faute de `claude-challenger` : à 08:56:47Z, ses deux
affirmations étaient exactes. C'est une faute de la **chaîne**, qui fusionne
une preuve sans vérifier qu'elle est encore valable. La conséquence est
concrète : la décision automatique de 11:03:12Z a retenu le point 2 en
s'appuyant sur une nuance (« la perte reste ouverte ») déjà résolue depuis
quinze minutes.

C'est précisément le motif que la littérature 2026 appelle *freshness binding*
— une preuve doit être liée par empreinte à l'état de l'arbre au moment où la
porte l'admet, et rejetée sinon [S7, S8] — et que les outils de forensique de
revue nomment `stale_approval` : « l'approbation couvre du code qui a changé
depuis la revue » [S9].

## 4. P1-2 — La suite de tests ne peut pas voir le défaut de comptage

**Sévérité : P1.**

La suite est verte, et cette vert-là ne prouve rien sur ce défaut :

```
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 17.41s
```

La raison est structurelle. Le seul test qui vérifie le champ publié au
registre est `harness/tests/test_audit_review.py` ligne 190 :

```python
assert record["verdicts"] == {"CONFIRMED": 1, "PARTIAL": 1}
```

Il s'appuie sur la fixture `FILLED_REVIEW` (lignes 48-62 du même fichier), qui
**ne contient pas** la ligne de légende du gabarit et où chaque jeton apparaît
exactement une fois. Rejeu des deux parseurs sur cette fixture :

```
[2] Fixture FILLED_REVIEW du test test_audit_review.py:48-62
  parse_verdicts (-> champ 'verdicts' du registre) : {'CONFIRMED': 1, 'PARTIAL': 1}
  parse_point_verdicts (tableau reel)              : {'CONFIRMED': 1, 'PARTIAL': 1}
```

Les deux parseurs coïncident. L'assertion passe donc **avec l'un comme avec
l'autre** : elle est incapable, par construction, de détecter lequel des deux
est branché. La fixture est le seul document du dépôt où cette coïncidence se
produit — et elle n'est pas représentative de ce que l'outil génère lui-même
(comparer avec le rejeu `[1]` de la § 2 : `{'CONFIRMED': 2, 'REFUTED': 2,
'PARTIAL': 2, 'NEEDS_OWNER': 3}` sur un gabarit vide).

C'est le motif « porte de test affaiblie / fixture non représentative » de la
lentille 6 [S5, S3] : le test décrit un monde plus simple que celui que le code
produit, et il donne un signal vert qui ne couvre pas le chemin réel. Un
correctif du P0-1 sans test rouge d'abord sur un document issu de
`scaffold_text()` serait, selon la lentille 2, une prétention et non une
démonstration.

## 5. P1-3 — La porte à quatre preuves reste inapplicable, pas seulement inappliquée

**Sévérité : P1.**

La décision propriétaire du 2026-08-11
(`architecture/decisions/DECISION-CURSOR-e9a6f4c-codex-passation-full-auto.md`,
§ 1) exige quatre preuves avant auto-fusion : « CI verte, gate mécanique
ACCEPT, verdict d'un Évaluateur dont l'acteur diffère du producteur, et audit
Cursor déposé sur la pull request », et précise qu'« aucune étape de cette
porte ne peut être rendue facultative sans une nouvelle décision écrite ».

Mesures sur la PR #74 :

- **Vingt-sept secondes** entre ouverture (11:02:25Z) et fusion (11:02:52Z).
- **CI non verte** au moment de la fusion :

```
$ gh api repos/PLiagre/ForgeHistory/commits/ea978f94.../status -q '.state'
pending
$ gh api repos/PLiagre/ForgeHistory/commits/ea978f94.../check-runs
Reconcile local Hermes state   completed  cancelled  ... 11:02:55Z
Reconcile local Hermes state   queued     null       11:02:56Z   (toujours en file)
```

  Le job `Reconcile local Hermes state` a été annulé à 11:02:55Z **après** la
  fusion, puis remis en file à 11:02:56Z où il est resté. Le statut combiné du
  commit reste `pending` à ce jour.
- **Aucun audit Cursor déposé.** `invoke-cursor-auditor` conclut `success` à
  11:02:48Z, quatre secondes avant la fusion — mais ce job ne fait que
  *lancer* l'agent auditeur. L'audit lui-même est le document que vous lisez,
  écrit après. La porte compte comme preuve un job de démarrage, pas un
  livrable.

Le motif « fusion sans les quatre preuves » est déjà connu, confirmé et retenu
(P0-2 de `CURSOR-9e35764`, point 3 CONFIRMED de la revue fusionnée ici). Je ne
le re-signale pas pour lui-même. **L'élément nouveau est de nature
différente** : cette occurrence montre que la quatrième preuve n'est pas
seulement non vérifiée, elle est **inatteignable dans l'ordonnancement
actuel**. `pipeline-audit.yml` déclenche l'auditeur sur l'événement
`pull_request` ; l'auditeur met de l'ordre de la minute à l'heure à produire
son fichier ; `merge-bot.yml` fusionne dans la même fenêtre d'événement. Aucun
réglage de secret ni de permission ne peut réconcilier ces deux échéances —
c'est un problème de séquencement, pas de configuration. La garde de
`merge-bot.yml` ligne 50 ne compare d'ailleurs que des **chemins de fichiers** :

```
offending="$(printf '%s\n' "$changed" | grep -vE '^(architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/)' || true)"
```

puis ligne 71, `gh pr merge --auto --squash`. Aucune des quatre preuves n'y
apparaît.

## 6. P2-1 — La publication automatique est en panne ; la sérialisation est tenue à la main

**Sévérité : P2.**

La description de la PR l'écrit sans détour, et c'est à son honneur :

> « Le workflow a poussé la branche `forge-bot/*` mais n'a pas pu ouvrir la
> PR […] : PR ouverte à la main par l'orchestrateur »

> « troisième des quatre PRs de revues en attente, ouverte après la fin
> complète du run `pipeline-orchestrate` de la PR #73 (sérialisation contre le
> conflit de rebase du ledger) »

Deux interventions humaines sont donc nécessaires par revue : ouvrir la PR, et
espacer les fusions. Les horodatages confirment le cadencement manuel — trois
runs `pipeline-orchestrate` séparés de 95 s et 75 s, chacun attendant le
précédent :

```
$ gh api .../workflows/pipeline-orchestrate.yml/runs
31693786429  786ec32f  push  success  2026-08-13T11:02:54Z
31693694291  4b6dcff5  push  success  2026-08-13T11:01:39Z
31693570402  74e03492  push  success  2026-08-13T11:00:04Z
31682710982  9e35764e  push  failure  2026-08-13T08:35:11Z
```

Le run en échec de 08:35 est celui-là même que la revue documente. Le
commentaire de `.github/workflows/pipeline-orchestrate.yml` lignes 136-138
affirme que le groupe `concurrency` (lignes 51-53,
`group: pipeline-orchestrate-master`, `cancel-in-progress: false`) empêche le
rebase de conflicter sur le registre. L'échec de 08:35 le dément : le groupe
sérialise les *exécutions*, pas les *bases* — deux runs déclenchés par `push`
partent chacun de l'arbre figé à leur propre SHA.

Conséquence pour la gouvernance : ADR-0006 décrit un mode `full_auto` sans
humain dans la boucle. En l'état, la boucle tient debout parce qu'un humain
ouvre les PR et cadence les fusions. C'est une dette de fiabilité, pas une
propriété du système. La littérature d'orchestration 2026 range exactement ce
cas parmi les échecs connus : un agent qui bute sur la CI « doit rapporter et
s'arrêter », et la coordination doit passer par des fichiers d'état ou des
webhooks plutôt que par la discipline de l'opérateur [S4, S3].

## 7. P2-2 — Aucune validation de schéma ne couvre `architecture/reviews/**`

**Sévérité : P2.**

Le job `schema` de `.github/workflows/audit-guard.yml` (lignes 18-26) a conclu
`success` sur cette PR. Ce vert ne dit rien du fichier ajouté :

```
$ grep -n "INBOX\|glob" harness/audit_schema.py
26:INBOX = REPO_ROOT / "architecture" / "inbox"
92:def validate_inbox(inbox: Path = INBOX) -> dict[str, list[str]]:
98:    for path in sorted(inbox.glob("CURSOR-*.md")):
```

Le validateur ne regarde que `architecture/inbox/CURSOR-*.md`. La PR #74
n'ajoute rien dans `inbox/` : elle ajoute un fichier dans `reviews/`. Le job a
donc validé **zéro fichier du diff** et affiché un vert. C'est exactement ce
que la lentille 3 interdit de prendre pour une preuve : une porte mécanique
verte n'a de valeur que si elle a effectivement examiné ce qui change.

Le déséquilibre est net. Un audit dans `inbox/` doit satisfaire dix champs
obligatoires, trois flags à `false`, un SHA de 40 hexadécimaux et un `audit_id`
égal au nom de fichier (`harness/audit_schema.py` lignes 31-88). Une revue dans
`reviews/` n'a aucune contrainte vérifiée avant fusion, alors que
`merge-bot.yml` ligne 50 lui accorde le même droit d'auto-fusion. La seule
vérification de contenu, `record_challenge`, s'exécute **après** la fusion — et
c'est elle qui a écrit le faux compte du P0-1.

## 8. P3-1 — Sept lignes de tableau dépassent 1 000 caractères

**Sévérité : P3** (information).

```
$ awk '{ if (length($0) > max) { max = length($0); n = NR } } END { print n, max }' <fichier>
ligne la plus longue : n° 40 (1870 caracteres)
lignes > 1000 caracteres : 7
```

Chaque verdict tient sur une seule ligne physique de plusieurs milliers de
caractères. Conséquences pratiques : le diff Git est illisible, et un
commentaire de revue ligne à ligne ne peut désigner qu'un bloc entier. Le fond
n'est pas en cause — la densité de preuve est au contraire une qualité — mais
la forme empêche la relecture assistée. À traiter comme confort, pas comme
défaut.

## 9. Lentille 5 — Taille et découpage : rien à signaler

Un fichier, 109 lignes ajoutées, zéro suppression. On est très en dessous du
seuil d'environ cinq fichiers ou quelques centaines de lignes au-delà duquel
une relecture honnête décroche [S1, S2]. **Aucun `NEEDS_SPLIT` n'est
justifié.** La PR a la bonne taille ; ses problèmes sont ailleurs.

## 10. Tableau des sévérités

| # | Sévérité | Constat | Preuve principale |
|---|---|---|---|
| P0-1 | **P0** | Le registre publie 16/5/10/4 pour une revue à 7 CONFIRMED / 3 PARTIAL / 0 REFUTED ; un gabarit vide publie déjà 2/2/2/3 | ledger `master` ligne 52 ; rejeu des deux parseurs ; `audit_review.py` 127-134, 174, 203 |
| P1-1 | **P1** | La revue fusionnée affirme « #65 non fusionnée » et « registre vide de a4de4bb », faux depuis 14 min à l'ouverture de la PR | `gh pr view 65` → `mergedAt 10:47:51Z` ; `grep -c a4de4bb` → 3 |
| P1-2 | **P1** | La fixture de test est le seul document où les deux parseurs coïncident : l'assertion passe avec l'un comme avec l'autre | `test_audit_review.py` 48-62 et 190 ; rejeu `[2]` |
| P1-3 | **P1** | Fusion en 27 s, statut combiné `pending`, audit Cursor inexistant ; la 4ᵉ preuve est inatteignable dans l'ordonnancement actuel | `status` → `pending` ; check-runs 11:02:48-56Z ; `merge-bot.yml` 50 et 71 |
| P2-1 | **P2** | PR ouverte à la main, fusions cadencées à la main ; le `concurrency` ne protège pas du conflit de rebase | description de la PR ; runs 3169357/3169369/3169378 vs 31682710982 ; `pipeline-orchestrate.yml` 51-53, 136-138 |
| P2-2 | **P2** | `schema` est vert sans avoir validé un seul fichier du diff ; `reviews/**` n'a aucun schéma imposé | `audit_schema.py` 26, 92, 98 ; `audit-guard.yml` 18-26 |
| P3-1 | **P3** | Sept lignes de tableau > 1 000 caractères (max 1 870) | mesure `awk` collée § 8 |

## 11. Classification de la CI du commit audité

**Rouge au sens de la porte, verte au sens des jobs de test.**

| Job | Conclusion | Horodatage |
|---|---|---|
| `tests`, `sim-tests`, `f0-demo`, `gitleaks`, `actionlint`, `schema`, `check-and-automerge` | `success` | 11:02:39Z – 11:02:50Z |
| `invoke-cursor-auditor` | `success` | 11:02:48Z (lance l'agent, ne dépose rien) |
| `cursor-scope` | `skipped` | 11:02:27Z |
| `Reconcile local Hermes state` | `cancelled` puis `queued` | 11:02:55Z / 11:02:56Z |
| **statut combiné du commit** | **`pending`** | encore `pending` aujourd'hui |

Le run `pipeline-orchestrate` post-fusion (`31693786429`, sha `786ec32f`)
conclut `success` — il a bien écrit au registre, avec le contenu erroné du
P0-1. Les portes locales rejouées ici sont vertes : `314 passed, 16 skipped`
pour `harness/tests/`, et `python3 harness/harness_audit.py` → `SCORE: 20/24`,
chiffre identique à celui cité par la revue auditée (le `FAIL`
`no_premature_stub_content` est la fausse alerte connue documentée dans
`AGENTS.md`).

## 12. Briefs atomiques proposés (proposition, pas instruction)

Trois au maximum, conformément au contrat. Ce sont des **propositions** : seul
le propriétaire, ou le policy engine, peut les convertir en briefs, et le
brief resterait alors la source unique d'instruction (`CLAUDE.md`).

1. **Un seul parseur de verdicts, et un test rouge d'abord.** Faire écrire au
   registre le comptage issu de `parse_point_verdicts` (lignes de tableau),
   supprimer le second comptage ou le renommer sans ambiguïté. Le test rouge
   doit s'appuyer sur un document produit par `scaffold_text()` — pas sur une
   fixture allégée — pour que la légende du gabarit soit dans le champ de
   mesure. Question à trancher par le propriétaire, hors compétence d'un
   audit : que faire des lignes déjà écrites dans un journal append-only.

2. **Garde de fraîcheur avant enregistrement d'une revue.** Refuser
   `AUDIT_CHALLENGED` lorsque l'état sur lequel la revue s'appuie a bougé
   depuis son `reviewed_at` (au minimum : `target_commit` plus à la pointe, ou
   registre modifié depuis). Le vocabulaire existe déjà côté audits —
   `AUDIT_STALE` est une transition légale depuis tous les états
   (`harness/audit_ledger.py` lignes 85-93) — mais rien ne la calcule. C'est
   la réponse directe au P1-1 [S7, S8].

3. **Étendre la validation de schéma à `architecture/reviews/**`.** Champs
   obligatoires du frontmatter (`review_of`, `reviewer`, `target_commit` en 40
   hex, `reviewed_at` ISO 8601), présence d'au moins une ligne
   `| N | ... | VERDICT | ... |`, et cohérence entre le compte annoncé et le
   tableau. Fait tomber le P2-2, et déplace la détection du P0-1 avant la
   fusion plutôt qu'après.

## 13. Ce que cet audit ne prétend pas

- Il **n'autorise aucune implémentation** : les trois flags du frontmatter sont
  à `false`. Il ne dit pas « doit être corrigé », il dit « voici ce que j'ai
  mesuré ».
- Il **ne juge pas la qualité intellectuelle** du contre-audit de Claude, qui
  est bonne. Les constats P1-1 et P0-1 portent sur la chaîne qui transporte et
  enregistre ce document, pas sur son auteur.
- Il **n'a pas rejoué les logs bruts** des runs GitHub Actions
  (`.../actions/jobs/<id>/logs`), non consultés ici. Toutes les conclusions
  sur la CI reposent sur l'API `check-runs`, `status` et `runs`, dont les
  sorties sont collées ci-dessus.
- Il **ne rouvre pas** les motifs déjà tranchés par une décision enregistrée.
  Quand un motif connu réapparaît (P1-3), l'élément nouveau est nommé
  explicitement et c'est lui seul qui porte le constat.

## Sources externes

Recherche web effectuée le 2026-08-13 sur « autonomous AI dev pipeline »,
« agent orchestration CI » et « token budget LLM agents », conformément à la
preuve de fin du contrat `architecture/agents/cursor-auditor.md`.

| # | Source | Thème | Consulté le |
|---|---|---|---|
| S1 | Growin — *AI Agents in Software Development: A 2026 CTO Guide* — <https://www.growin.com/blog/ai-agents-in-software-development-26/> | autonomous AI dev pipeline ; vérifiabilité comme condition d'extension de l'autonomie | 2026-08-13 |
| S2 | n1n.ai — *Building a Fully Autonomous AI SDLC Pipeline with Multi-Agent Systems* (2026-03-14) — <https://explore.n1n.ai/blog/autonomous-ai-sdlc-pipeline-multi-agent-2026-03-14> | orchestration déterministe ; agents fonctions pures de l'état ; E/S centralisées et atomiques | 2026-08-13 |
| S3 | Augment Code — *CI/CD for AI Agents: How to Integrate Agent Orchestration into Your Pipeline* — <https://www.augmentcode.com/guides/cicd-ai-agents-pipeline-integration> | agent orchestration CI ; porte de vérification bloquante avant fusion ; dérive spec/diff | 2026-08-13 |
| S4 | TruLayer — *Orchestration patterns for agentic dev* — <https://trulayer.ai/blog/orchestration-patterns-for-agentic-dev/> | agent orchestration CI ; « rapporter et s'arrêter » sur échec CI ; coordination par fichiers d'état, pas par discipline humaine | 2026-08-13 |
| S5 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers With Code Examples* (2026) — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | token budget LLM agents ; plafonds par requête/session/clé, hors du code de l'agent | 2026-08-13 |
| S6 | RockB — *Agent Cost Circuit Breaker Pattern Guide* (2026) — <https://baeseokjae.github.io/posts/agent-cost-circuit-breaker-pattern-guide-2026/> | token budget LLM agents ; l'application du budget doit vivre hors de l'agent, au plan de gouvernance | 2026-08-13 |
| S7 | arXiv 2607.14890 — *Proof-or-Stop: Don't Trust the Agent, Trust the Evidence* — <https://arxiv.org/html/2607.14890v1> | preuve périmée rejetée par liaison de fraîcheur (`materialHash`/`headHash`) ; fonde le P1-1 | 2026-08-13 |
| S8 | agentpatterns.ai — *Evidence-Gated Lifecycle Control* — <https://agentpatterns.ai/verification/evidence-gated-lifecycle-control/> | « la revendication ne fait pas avancer l'état, la preuve le fait » ; fonde le P1-1 et le brief 2 | 2026-08-13 |
| S9 | unpingable/dossier — détection des cicatrices de revue (`stale_approval`, `review_theater`, `self_merge`) — <https://github.com/unpingable/dossier> | approbation périmée et fusion en quelques secondes comme motifs mesurables ; fonde P1-1 et P1-3 | 2026-08-13 |

## Commandes rejouées

Toutes les sorties citées dans cet audit proviennent des commandes ci-dessous,
exécutées sur ce dépôt au commit audité.

```bash
gh pr view 74 --json number,title,state,additions,deletions,changedFiles,createdAt
gh pr view 74 --json mergeCommit,mergedAt,mergedBy
gh pr view 74 --json files -q '.files[] | "\(.additions)+ \(.deletions)- \(.path)"'
gh pr view 65 --json number,state,mergedAt
gh api repos/PLiagre/ForgeHistory/commits/ea978f941fed385cc7309c8da195c5b8970bb633/check-runs
gh api repos/PLiagre/ForgeHistory/commits/ea978f941fed385cc7309c8da195c5b8970bb633/status -q '.state'
gh api "repos/PLiagre/ForgeHistory/actions/workflows/pipeline-orchestrate.yml/runs?per_page=6"
git show origin/master:architecture/audit-ledger.jsonl | grep -n "9e35764"
git show origin/master:architecture/audit-ledger.jsonl | grep -c "a4de4bb"
git merge-base --is-ancestor 786ec32f520adbc0361914dff4e7b7314232973b origin/master
.venv/bin/python -m pytest harness/tests/ -q
python3 harness/harness_audit.py
# rejeu des deux parseurs sur le gabarit, la fixture de test et le fichier fusionné
.venv/bin/python -c "from harness.audit_review import parse_verdicts, scaffold_text; \
                     from harness.audit_decision import parse_point_verdicts; ..."
```
