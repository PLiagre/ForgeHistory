---
audit_id:                  CURSOR-779d97c-revue-verdicts-illisibles
auditor:                   cursor-cloud
target_branch:             master
target_commit:             779d97c8fd66d16e2bad4f81ca88d968358b96d8
created_at:                2026-08-12T12:20:00Z
audit_type:                architecture-and-qa
status:                    PROPOSED
implementation_authorized: false
ci_changes_authorized:     false
code_changes_authorized:   false
---

# Audit de la PR #30 — « challenge: revue de l'audit CURSOR-73022bd-hermes-dashboard-modele-auditeur »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** :
il propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006).

## 1. Provenance et périmètre

| | |
|---|---|
| PR | <https://github.com/PLiagre/ForgeHistory/pull/30> |
| Auteur | `app/github-actions` (bot), branche `forge-bot/review-CURSOR-73022bd-hermes-dashboard-modele-auditeur-31593583378` |
| Tête de la PR | `ae66c1a3f7524fc2e5d13acdf7efcd93f7c0b211` |
| Commit de fusion audité | `779d97c8fd66d16e2bad4f81ca88d968358b96d8` |
| État | `MERGED` le 2026-08-12T12:01:47Z, fusionnée par `PLiagre` (humain, pas le bot) |
| Diff | 2 fichiers, +108 / −0 |

Contenu du diff (`git diff 779d97c^1..779d97c --stat`) :

```
 architecture/audit-ledger.jsonl                    |   1 +
 ...SOR-73022bd-hermes-dashboard-modele-auditeur.md | 107 +++++++++++++++++++++
 2 files changed, 108 insertions(+)
```

Provenance de la revue vérifiée : le `target_commit` qu'elle annonce existe et
est bien un ancêtre de `master`.

```
$ git cat-file -t 73022bdab6d2fff7c4d08812c281bcc56172dcc8
commit
$ git merge-base --is-ancestor 73022bdab6d2fff7c4d08812c281bcc56172dcc8 HEAD && echo ok
ok
```

## 2. Intention annoncée contre livrable réel (lentille 1)

L'intention est lisible, ce qui est déjà une qualité : le corps de PR dit
« Contre-audit produit headless par claude-challenger (run 31593583378). La
fusion de cette PR déclenche pipeline-orchestrate.yml (event
review_recorded). » Le contrat de rôle applicable est
`architecture/agents/claude-challenger.md` (« Preuve de fin » : un verdict
`CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER` **par point numéroté**, chacun
avec une preuve reproductible).

| Promesse | État réel | Où |
|---|---|---|
| « un verdict par point numéroté » | La table existe et est renseignée, mais elle est numérotée `P1-1 … P3-12` au lieu de `1 … N`, et chaque verdict est en gras. Aucun de ses 14 verdicts n'est lisible par le moteur de décision. | § 3, P0-1 |
| « La fusion déclenche pipeline-orchestrate.yml » | Déclenché, et **rouge** : `pipeline-orchestrate` sort en code 2 sur le commit de fusion. La boucle reste bloquée à `AUDIT_CHALLENGED`. | § 3, P0-1 ; § 6 |
| Ledger `AUDIT_CHALLENGED` écrit par le module dédié | La ligne est bien écrite par `audit_review.record_challenge`, mais elle publie `REFUTED: 2` alors que la revue conclut littéralement « **Aucun REFUTED.** » | § 3, P1-3 |

Le fond de la revue est solide (voir § 4). Ce qui échoue est le **passage de
relais** : le document est juste pour un lecteur humain et inexploitable pour
la machine qui doit le consommer. C'est précisément la classe de défaut que la
littérature 2026 sur les pipelines d'agents désigne comme le mode de panne
dominant — « les systèmes multi-agents échouent aux passages de relais plus
souvent qu'ils n'échouent au raisonnement » [S3].

## 3. Constats

### P0-1 — La revue livrée est illisible par l'étape suivante de la même boucle ; la CI est rouge et l'audit est bloqué

`harness/audit_decision.py:64-67` définit le seul format que le moteur de
décision sait lire :

```python
_POINT_VERDICT_ROW = re.compile(
    r"^\|\s*(\d+)\s*\|.*?\|\s*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\s*\|",
    re.MULTILINE,
)
```

Deux exigences y sont cachées : la première cellule doit être un **entier**, et
la cellule de verdict ne doit contenir **que** le jeton. Le fichier livré viole
les deux : ses lignes commencent par `| P1-1 |`, `| P2-5 |`, `| § 2 (…) |`, et
chaque verdict est écrit `**CONFIRMED**`. Résultat mesuré :

```
$ .venv/bin/python -c "…; print(audit_decision._parse_point_verdicts(text))"
[]
```

La CI l'a confirmé toute seule, sur le commit de fusion
(run `31594525965`, job `orchestrate`) :

```
error: /home/runner/work/ForgeHistory/ForgeHistory/architecture/reviews/CLAUDE-CURSOR-73022bd-hermes-dashboard-modele-auditeur.md has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
##[error]Process completed with exit code 2.
```

Conséquence vérifiée sur l'état courant : l'audit ne progresse plus.

```
$ python3 harness/audit_ledger.py show --audit-id CURSOR-73022bd-hermes-dashboard-modele-auditeur | tail -1
{"timestamp": "2026-08-12T11:55:18Z", …, "event": "AUDIT_CHALLENGED", …}
```

Les deux écarts sont indépendants et il faut corriger les deux : renuméroter ne
suffit pas, retirer le gras ne suffit pas.

```
renuméroté P1-1 -> 1 seulement            : []
renuméroté ET gras retiré des cellules    : [(1,'CONFIRMED'), (2,'CONFIRMED'), (4,'CONFIRMED'),
                                             (5,'CONFIRMED'), (6,'CONFIRMED'), (8,'CONFIRMED'),
                                             (9,'CONFIRMED'), (10,'CONFIRMED'), (11,'CONFIRMED'),
                                             (12,'CONFIRMED')]
```

Et même avec les deux corrections, **deux points restent perdus en silence** :
`P1-3` (« **CONFIRMED**, avec une réserve sur une preuve secondaire ») et
`P2-7` (« **PARTIAL — logique du code confirmée…** ») — toute cellule nuancée
tombe hors du motif. Ce n'est pas théorique : la revue précédente
(`CLAUDE-CURSOR-cdc683f-…`), elle, a bien été décidée par la policy, mais avec
9 lignes captées sur 11. Ses lignes 3 (`REFUTED (sur l…`) et 9
(`CONFIRMED (avec réserve sur le brief 2)`) ont été écartées sans un mot, et
le `retained_points: [1, 2, 5, 8, 10, 11]` inscrit au ledger omet donc un
point réellement confirmé. Perte silencieuse, pas refus explicite — l'inverse
du fail-closed que le dépôt s'impose partout ailleurs.

### P0-2 — La porte qui garde l'écriture du ledger valide une propriété plus faible que celle dont dépend son consommateur

Cause structurelle du P0-1 : il existe **deux** définitions de « un verdict »
dans le dépôt, et c'est la plus permissive qui garde l'écriture.

| Fonction | Ce qu'elle exige | Qui s'en sert |
|---|---|---|
| `audit_review.parse_verdicts` (`harness/audit_review.py:126-133`) | le **mot** `CONFIRMED` (etc.) apparaît quelque part dans le texte | la porte `record_challenge`, qui autorise l'écriture `AUDIT_CHALLENGED` |
| `audit_decision._parse_point_verdicts` (`harness/audit_decision.py:185-191`) | une **ligne de table** `\| N \| … \| VERDICT \| …` | `decide_auto`, l'étape immédiatement suivante |

Rejoué de bout en bout, hors du dépôt (inbox, reviews et ledger recréés dans
`/tmp`, fichier de revue réel de la PR #30 copié tel quel) :

```
ETAPE 1 -- record_challenge (la porte qui garde l'ecriture du ledger)
   ACCEPTE. ligne ledger ecrite : {"event": "AUDIT_CHALLENGED", "verdicts": {"CONFIRMED": 12, "REFUTED": 2, "PARTIAL": 4, "NEEDS_OWNER": 4}}
ETAPE 2 -- decide_auto (l'etape suivante de la meme boucle)
   REFUSE -> …/CLAUDE-CURSOR-73022bd-….md has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
```

La docstring de `audit_review.py:16-22` dit pourtant l'intention juste :
« Recording it from an empty scaffold would make that assertion a lie. So
`record` proves the review is filled before it writes ». La porte prouve que
la revue est *remplie*, pas qu'elle est *consommable* — et c'est la seconde
propriété dont dépend la suite. Le format n'est écrit nulle part comme un
contrat : il n'existe que dans le gabarit `scaffold_text`
(`audit_review.py:87`, qui émet bien `| 1 | … |`) et dans une expression
régulière d'un autre module. Deux endroits qui décrivent la même forme
finissent toujours par diverger [S1], et c'est exactement la règle « une seule
source de vérité » de `CLAUDE.md` appliquée à un format d'échange. La pratique
recommandée est de valider la sortie amont **comme contrat d'entrée de
l'aval**, à la frontière, et de ne jamais lancer l'aval si elle ne s'y
conforme pas [S1, S2, S5].

Portée honnête : la conséquence n'est pas une mauvaise décision, c'est un
arrêt. `decide_auto` refuse de deviner — ce comportement-là est correct et
doit être conservé. Le défaut est que le refus arrive trop tard, après la
fusion, au lieu d'être opposé au producteur.

### P1-3 — Le champ `verdicts` du ledger n'est pas un décompte de verdicts, et cette PR y inscrit `REFUTED: 2` sur une revue qui conclut « Aucun REFUTED »

`parse_verdicts` compte les **occurrences de mots dans tout le document**. La
ligne inscrite au ledger par cette PR se reproduit exactement :

```
$ .venv/bin/python -c "…; print(audit_review.parse_verdicts(text))"
{'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}
```

Or la revue elle-même écrit, en toutes lettres : « **Aucun REFUTED.** Aucun
des constats de fond ne s'effondre à la reproduction » et « **11 sont
intégralement confirmés** ». Les deux `REFUTED` comptés sont la phrase de
garde du gabarit (« Un verdict par point : CONFIRMED / REFUTED / PARTIAL /
NEEDS_OWNER », `audit_review.py:75`) et… la phrase qui nie tout refus. Le
12ᵉ `CONFIRMED` vient de la même ligne de gabarit. Autrement dit **toute**
revue produite par ce scaffold porte, par construction, +1 sur chacun des
quatre jetons, et une revue qui *parle* d'un verdict est comptée comme si elle
en *portait* un.

Le `generator-log` du brief 006 (ligne 121) documente bien que ce champ « ne
porte que des comptes par jeton, pas quel point porte quel verdict » — cette
partie-là n'est pas un motif écarté que je réouvre. L'élément nouveau est
ailleurs : les comptes ne sont pas non plus des comptes **de verdicts**, et
ils sont publiés dans `architecture/audit-ledger.jsonl`, qui est le journal
append-only servant de trace de gouvernance de la boucle. Un chiffre faux y
est définitif. Bornage vérifié : `hermes/dashboard.py` ne lit pas ce champ
(`grep -n "verdicts" hermes/dashboard.py` → aucune occurrence), donc le
propriétaire ne le voit pas aujourd'hui dans son tableau de bord ; le préjudice
est limité au journal et à quiconque le relit.

### P1-4 — Le garde-fou de périmètre du merge-bot a rendu un verdict rouge sur un fichier que la PR ne touche pas

`.github/workflows/merge-bot.yml:38-39` calcule l'ensemble des chemins
modifiés ainsi :

```bash
git fetch --no-tags origin "$BASE_REF"
changed="$(git diff --name-only "origin/${BASE_REF}...HEAD")"
```

Cette base est **mobile**. Chronologie mesurée sur cette PR :

| Horodatage | Événement | Source |
|---|---|---|
| 11:56:02Z | run `31594081902` créé (ouverture de la PR) | `gh run view --json createdAt` |
| 12:01:42Z | run démarré (5 min 40 s d'attente en file) | `gh run view --json startedAt` |
| 12:01:47Z | PR fusionnée par `PLiagre` | `gh pr view --json mergedBy,mergedAt` |
| 12:01:58Z | l'étape de périmètre s'exécute — après la fusion | log du job |

Sortie du job :

```
warning: origin/master...HEAD: multiple merge bases, using ae66c1a3f7524fc2e5d13acdf7efcd93f7c0b211
Changed files:
hermes/DASHBOARD.md
##[error]PR touches path(s) outside .github/merge-bot.yaml's allow_paths -- refusing auto-merge:
hermes/DASHBOARD.md
```

`hermes/DASHBOARD.md` n'apparaît nulle part dans le diff de la PR #30 : entre
temps `master` avait avancé (`4a5995a hermes: tableau de bord régénéré`), et le
`...` a donc comparé la branche à un `master` plus récent. Le verdict de la
frontière d'auto-fusion **dépend de l'heure à laquelle le job passe**, pas du
contenu de la PR. Ici l'erreur est bénigne (un refus sur une PR déjà fusionnée
à la main) ; c'est la direction symétrique qui est le vrai risque, car un
chemin réellement interdit peut sortir de l'ensemble calculé pour la même
raison, et `auto_merge_denylist` contient `.github/workflows/**`.

`harness/tests/test_merge_bot_policy.py` — que l'audit précédent citait à
juste titre comme le bon exemple de test d'une frontière de workflow — teste
l'allowlist extraite du YAML, jamais le calcul du diff :
`grep -nE "origin/|BASE_REF|\.\.\." harness/tests/test_merge_bot_policy.py`
ne renvoie aucune occurrence. La partie testée est la liste ; la partie qui a
échoué est la mesure.

### P2-5 — Le smoke test « sans secret » prouve le mécanisme sur un format que le producteur réel ne produit pas

`pipeline-challenge.yml:187-223` conserve un job
`mechanical-scaffold-smoke` dont l'intention est excellente : prouver à chaque
déclenchement, sans jeton, que la moitié non-LLM du rôle fonctionne. Mais il
fabrique lui-même la ligne qu'il va faire valider (ligne 217) :

```python
text += "\n| 1 | mock point | CONFIRMED | ci smoke, no placeholder left |\n"
```

C'est la forme idéale, celle que le vrai challenger n'a pas produite. Les
fixtures des tests unitaires font le même choix — `test_audit_decision.py:205`,
`test_audit_review.py:60`, `test_full_auto_pipeline.py:65-66`,
`test_orchestrator.py:56` — toutes écrivent `| 1 | … | CONFIRMED | … |`. D'où
la situation à expliquer : la suite est verte et le livrable réel casse.

```
$ .venv/bin/python -m pytest harness/tests/ -q
309 passed, 16 skipped in 17.15s
```

Un test qui n'échoue jamais sur ce que la production produit vraiment ne
mesure pas la production ; la forme la plus forte de preuve reste un test qui
échoue sur le comportement d'avant et passe après [S4]. La revue de PR #30
elle-même est la fixture idéale pour ce test : elle est réelle, elle est
datée, et elle échoue.

### P2-6 — `architecture/reviews/**` n'a aucune validation de schéma, alors que `architecture/inbox/**` en a une

`harness/audit_schema.py` ne regarde que l'inbox (`INBOX` ligne 26,
`inbox.glob("CURSOR-*.md")` ligne 98). Côté CI, `audit-guard.yml` lance ce
validateur (ligne 25-26) puis un second job `cursor-scope` conditionné à
`startsWith(github.head_ref, 'cursor/')` (ligne 30) — donc **inactif** pour
une branche `forge-bot/*`. Vérifié sur la PR : `cursor-scope` est marqué
`skipping`, `schema` est vert. La face « produite par Cursor » du relais est
donc validée mécaniquement, et la face « produite par Claude et consommée par
la machine » ne l'est pas du tout, alors que c'est celle dont dépend la
décision automatique. C'est l'asymétrie qui a laissé passer le P0-1 : les
portes mécaniques ont bien tourné, elles ne regardaient simplement pas cet
artefact [S2].

### P3-7 — Le coût réellement mesuré de cette invocation (1,594 USD) est jeté

L'étape « Post-hoc budget marking » du run `31593583378` a bien mesuré la
dépense de ce contre-audit :

```
{"cap_usd": 5.0, "over_cap": false, "prices_as_of": "2026-08-03", "step": "challenge:CURSOR-73022bd-hermes-dashboard-modele-auditeur", "timestamp": "2026-08-12T11:55:56.140193Z", "usd": 1.593695}
```

Mais l'étape de publication ne committe que deux chemins
(`pipeline-challenge.yml:178` : `git add architecture/reviews
architecture/audit-ledger.jsonl`), et le fichier reste donc sur le disque
éphémère du runner. Dans le dépôt : `wc -c harness/pipeline/ci-budget-ledger.jsonl`
→ **1 octet**. Le `precheck` mensuel de l'étape suivante lira donc toujours un
journal vide.

Ce point est **déjà** un constat confirmé de la revue même que cette PR livre
(son `P1-3`) et il est en attente d'arbitrage propriétaire ; je ne le
re-propose pas comme brief et je ne le compte pas comme un constat nouveau. Le
seul élément neuf que j'ajoute est un chiffre : 1,593695 USD réellement
mesurés puis perdus, sur cette invocation-ci, ce qui donne un ordre de
grandeur à la décision. La distinction est celle que la littérature 2026
insiste à faire : observer une dépense n'est pas la contrôler, et un journal
qui ne persiste pas ne peut fonder aucun plafond [S6, S7].

## 4. Ce que la PR tient bien (à ne pas défaire en corrigeant)

Ces points sont crédités avec preuve, parce qu'ils forment la partie de la
discipline qu'il faut préserver :

- **Taille du diff (lentille 5)** : 2 fichiers, 108 lignes ajoutées, aucune
  suppression. Très en dessous du seuil au-delà duquel une relecture honnête
  décroche (~5 fichiers / quelques centaines de lignes).
- **Séparation des rôles (lentille 4)** : la revue est produite par un acteur
  distinct de l'auteur de l'audit, et elle ne touche ni code, ni test, ni
  workflow — conforme aux « Interdits » de
  `architecture/agents/claude-challenger.md`.
- **Honnêteté sur ses propres limites** : faute de `GH_TOKEN`, la revue marque
  `PARTIAL` / `NEEDS_OWNER` au lieu de supposer (« je m'abstiens plutôt que de
  supposer », § 4 de la revue). Elle signale aussi une preuve mal transcrite
  dans l'audit qu'elle relit (`P1-3`, « Preuve 2 ») tout en montrant que la
  conclusion tient par une autre voie. C'est l'inverse de la correction
  hallucinée.
- **Reconstruction indépendante** : son `P1-2` est ré-établi à partir des
  horodatages `git log` locaux plutôt que recopié de l'audit. C'est la bonne
  méthode.

## 5. Commandes rejouées

Environnement : VM Linux Cursor Cloud, `master` à `779d97c`, `.venv/bin/python`
(3.13) pour pytest, `python3` pour les scripts stdlib.

**5.1 — Les deux analyseurs sur le fichier livré**

```
$ .venv/bin/python - <<'PY' … PY
A) audit_review.parse_verdicts (ce qui part au ledger) :
   {'CONFIRMED': 12, 'REFUTED': 2, 'PARTIAL': 4, 'NEEDS_OWNER': 4}
B) audit_decision._parse_point_verdicts (ce que lit --policy auto) :
   []
C) meme mesure sur la revue precedente (approuvee par la policy) :
   parse_verdicts        : {'CONFIRMED': 10, 'REFUTED': 3, 'PARTIAL': 3, 'NEEDS_OWNER': 4}
   _parse_point_verdicts : [(1,'CONFIRMED'), (2,'PARTIAL'), (4,'REFUTED'), (5,'PARTIAL'),
                            (6,'NEEDS_OWNER'), (7,'NEEDS_OWNER'), (8,'CONFIRMED'),
                            (10,'CONFIRMED'), (11,'CONFIRMED')]
```

(La ligne A reproduit exactement le champ `verdicts` que cette PR inscrit au
ledger. La ligne C montre les points 3 et 9 absents : perte silencieuse
antérieure, même mécanisme.)

**5.2 — Reproduction de bout en bout, hors du dépôt** (`/tmp`, inbox + ledger
recréés, fichier de revue réel copié) : sortie citée intégralement au P0-2.

**5.3 — Suites de tests et gate**

```
$ .venv/bin/python -m pytest harness/tests/test_audit_review.py harness/tests/test_audit_decision.py harness/tests/test_merge_bot_policy.py -q
34 passed in 1.10s

$ .venv/bin/python -m pytest harness/tests/ -q
309 passed, 16 skipped in 17.15s
```

Les 16 `skip` sont les cas `test_run_unity.py` qui exigent Unity/PowerShell —
attendu sur Linux, pas un échec.

**5.4 — État de la boucle et journal des coûts**

```
$ python3 harness/audit_ledger.py show --audit-id CURSOR-73022bd-hermes-dashboard-modele-auditeur | tail -1
{"timestamp": "2026-08-12T11:55:18Z", "audit_id": "CURSOR-73022bd-hermes-dashboard-modele-auditeur", "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-73022bd-hermes-dashboard-modele-auditeur.md", "verdicts": {"CONFIRMED": 12, "REFUTED": 2, "PARTIAL": 4, "NEEDS_OWNER": 4}}

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl
```

**5.5 — Bornage de deux affirmations** (pour ne pas surestimer la portée)

```
$ grep -n "verdicts" hermes/dashboard.py
  aucune occurrence
$ grep -nE "origin/|merge base|BASE_REF|\.\.\." harness/tests/test_merge_bot_policy.py
  aucune occurrence (l'allowlist est testee, pas le calcul du diff)
```

## 6. CI du commit audité — classification

**Sur le commit de fusion `779d97c` (push vers `master`) : ROUGE.** Un job en
échec, cinq verts.

| Workflow | Conclusion |
|---|---|
| `pipeline-orchestrate` | **failure** (job `orchestrate`, exit 2 — cause citée au P0-1) |
| `harness-ci` | success |
| `audit-guard` | success |
| `pipeline-audit` | success |
| `security` | success |
| `hermes-dashboard` | success |
| `hermes-observer` (×5, `workflow_run`) | success |
| `pipeline-failure-escalate` | 1 × success, 1 × skipped |

**Sur la tête de la PR `ae66c1a` (événement `pull_request`) : ROUGE également.**

| Check | Conclusion |
|---|---|
| `check-and-automerge` (merge-bot) | **fail** — faux positif sur `hermes/DASHBOARD.md`, cf. P1-4 |
| `harness-ci` / `tests`, `f0-demo` | pass |
| `audit-guard` / `schema` | pass |
| `audit-guard` / `cursor-scope` | **skipping** (branche `forge-bot/*`, cf. P2-6) |
| `pipeline-audit` / `invoke-cursor-auditor` | pass |
| `security` / `actionlint`, `gitleaks` | pass |
| `hermes-observer` / `Reconcile local Hermes state` | pass |

Fait à porter au propriétaire sans le qualifier ici : la PR a été fusionnée à
12:01:47Z par `PLiagre` (humain) pendant que son propre `check-and-automerge`
était en file d'attente ; il a conclu à l'échec 11 secondes après la fusion.
`auto_policy.yaml` interdit en `full_auto` « merge vers master si un workflow
requis est rouge » — la fusion n'a pas été faite par le bot, donc la règle
n'est pas mécaniquement contournée, mais l'ordre des événements prive la PR de
la garantie que cette règle est censée offrir.

## 7. Risques par sévérité

| Sévérité | Constat | Risque si rien ne change |
|---|---|---|
| **P0-1** | Revue illisible par `decide_auto` ; `pipeline-orchestrate` rouge | La boucle full-auto s'arrête à chaque contre-audit dont la mise en forme dérive ; l'audit `CURSOR-73022bd` reste bloqué en `AUDIT_CHALLENGED` |
| **P0-2** | Deux analyseurs de verdicts ; la porte valide le plus faible | Le défaut se reproduit à chaque revue, et les cellules nuancées sont perdues en silence au lieu d'être refusées |
| **P1-3** | `verdicts` du ledger = comptage de mots (`REFUTED: 2` vs « Aucun REFUTED ») | Le journal append-only de gouvernance porte des chiffres faux, définitivement |
| **P1-4** | Périmètre du merge-bot calculé sur une base mobile | Le verdict de la frontière d'auto-fusion dépend de l'heure d'exécution ; un chemin du denylist peut sortir de l'ensemble mesuré |
| **P2-5** | Fixtures et smoke test au seul format idéal | 309 tests verts continueront de coexister avec un livrable cassé |
| **P2-6** | Aucun schéma sur `architecture/reviews/**` | Le défaut ne peut être attrapé qu'après la fusion, jamais avant |
| **P3-7** | 1,594 USD mesurés puis jetés (déjà confirmé par la revue elle-même) | Le plafond mensuel reste inatteignable ; aucun brief proposé ici |

## 8. Briefs atomiques proposés (3, plafond du contrat respecté)

Propositions, pas instructions — la conversion reste à la décision de la
boucle.

1. **Un seul analyseur de verdicts, et le refus opposé au producteur.**
   Faire de `_parse_point_verdicts` l'unique lecteur de verdicts, l'utiliser
   dans `record_challenge`, et refuser d'inscrire `AUDIT_CHALLENGED` quand la
   revue ne contient aucune ligne que `decide_auto` saurait lire. Élargir le
   motif pour accepter le gras et une cellule nuancée, ou, à l'inverse, exiger
   la forme stricte et la refuser explicitement — l'un ou l'autre, mais une
   seule définition. Le test rouge/vert existe déjà tout fait : le fichier de
   revue de la PR #30 comme fixture (rouge avant, vert après). Couvre P0-1 et
   P0-2 ; recoupe P1-3 si le champ `verdicts` devient le décompte des lignes
   captées au lieu d'un comptage de mots.

2. **Une porte de schéma sur `architecture/reviews/**`, avant la fusion.**
   Symétrique de `audit_schema.py` côté inbox : frontmatter attendu, et au
   moins une ligne de verdict lisible par le consommateur. Rejouer le
   `mechanical-scaffold-smoke` sur une fixture au format réellement produit
   par le challenger, et non sur une ligne fabriquée par le test lui-même.
   Couvre P2-5 et P2-6.

3. **Rendre le périmètre du merge-bot indépendant du moment d'exécution.**
   Figer la base du diff (SHA de base de la PR, ou la liste de fichiers
   fournie par l'API GitHub) au lieu de `origin/${BASE_REF}...HEAD`, et
   ajouter au `test_merge_bot_policy.py` un cas rouge sur le scénario
   « `master` a avancé pendant l'attente ». Couvre P1-4. À noter pour
   l'arbitrage : ce brief touche `.github/workflows/**`, qui figure dans
   `auto_merge_denylist` — il ne peut donc pas passer par l'auto-fusion.

## 9. Sources externes

Recherche du 2026-08-12 sur « autonomous AI dev pipeline », « agent
orchestration CI » et « token budget LLM agents ». URL + date de consultation.

| # | source | consulté le |
|---|---|---|
| S1 | Antigravity Lab — *Designing Schema Evolution So Sub-Agent Handoffs Never Break* — <https://antigravitylab.net/en/articles/agents/antigravity-subagent-handoff-schema-evolution-design> | 2026-08-12 |
| S2 | tianpan.co — *Contract Testing for AI Pipelines: Schema-Validated Handoffs Between AI Components* (2026-04-20) — <https://tianpan.co/blog/2026-04-20-contract-testing-ai-pipelines> | 2026-08-12 |
| S3 | Geodocs.dev — *Agent Handoff Protocol Documentation Spec for Multi-Agent AI Systems* — <https://geodocs.dev/ai-agents/agent-handoff-protocol-spec> | 2026-08-12 |
| S4 | SitePoint — *The Model Handshake: Chaining AI Agents for Complex Refactors* — <https://www.sitepoint.com/the-model-handshake-chaining-ai-agents-complex-refactors/> | 2026-08-12 |
| S5 | DEV / az365ai — *AgentOps on Microsoft Foundry: CI/CD Reference Architecture (2026)* — <https://dev.to/az365ai/agentops-on-microsoft-foundry-a-practitioner-decode-of-the-new-cicd-reference-architecture-2026-1fnn> | 2026-08-12 |
| S6 | Braintrust — *How to track LLM costs (2026): per-user, per-feature, per-agent-run attribution* — <https://www.braintrust.dev/articles/how-to-track-llm-costs-2026> | 2026-08-12 |
| S7 | Jatin Bansal — *Agent Budgets and Runaway Prevention* — <https://jatinbansal.com/ai-engineering/agent-budgets-and-runaway-prevention/> | 2026-08-12 |

Ce que ces sources apportent, en une phrase : le format d'un livrable qui
passe d'un agent à l'autre doit être un **contrat unique, partagé, validé à la
frontière de réception**, sinon la panne est silencieuse [S1, S2, S3, S4] ; la
porte de qualité doit tourner **avant** la fusion, sur l'artefact réel et non
sur une fixture inventée [S5] ; et observer une dépense n'est pas la contrôler
— un journal de coût qui ne persiste pas ne fonde aucun plafond [S6, S7].

## 10. Ce que cet audit ne fait pas

Il ne décide rien, n'autorise aucune implémentation, ne modifie aucun audit
existant, et ne touche aucun chemin hors de `architecture/inbox/`. Les trois
flags `*_authorized` de son frontmatter sont à `false`. Les points ci-dessus
sont soumis au contre-audit puis à la décision de la boucle.
