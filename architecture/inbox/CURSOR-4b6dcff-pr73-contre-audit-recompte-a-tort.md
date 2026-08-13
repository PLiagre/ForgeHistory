---
audit_id:                CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort
auditor:                 cursor-cloud
target_branch:           master
target_commit:           4b6dcff50276b8f4884430ff899f8969eeca6ac2
created_at:              2026-08-13T11:20:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la pull request #73 — « contre-audit de CURSOR-4c45718 »

Critique conduite selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cet audit **ne prescrit
rien** : il propose, la décision reste à la boucle
(`architecture/README.md`, ADR-0005/0006).

## 0. Identité de l'objet audité

PR [#73](https://github.com/PLiagre/ForgeHistory/pull/73) — « challenge :
revue de l'audit CURSOR-4c45718-pr65-ledger-recupere-a-la-main ».

| | |
|---|---|
| Auteur | `PLiagre` (PR ouverte à la main, contenu produit par `claude-challenger` au run 31684301091) |
| Diff | 1 fichier, +99 / −0 : `architecture/reviews/CLAUDE-CURSOR-4c45718-pr65-ledger-recupere-a-la-main.md` |
| Tête de branche | `ae26aaecb208dde87bd9c6cce3425fe106c064d2` (branche `forge-bot/review-…-31684301091`, supprimée depuis) |
| Fusion | squash, 2026-08-13T11:01:37Z → commit `4b6dcff50276b8f4884430ff899f8969eeca6ac2` |

La tête de branche n'a **pas** survécu à la fusion (squash), donc le
`target_commit` de cet audit est le commit de fusion, seul SHA de cet état
présent dans l'historique de `master` — règle d'intégrité 4 du
`architecture/README.md`.

```
$ git merge-base --is-ancestor ae26aae… origin/master ; echo $?   →  1   (non)
$ git merge-base --is-ancestor 4b6dcff… origin/master ; echo $?   →  0   (oui)
$ git log -1 --format='parents=%P%n subj=%s' 4b6dcff
parents=ae76a15873a2f769e7ccbf073f5cf4f3c37e658e
 subj=challenge: revue CLAUDE-CURSOR-4c45718-pr65-ledger-recupere-a-la-main (claude-challenger headless, run 31684301091) (#73)
```

## 1. Classification de la CI du commit audité

Toutes les portes mécaniques sont **vertes**. Aucune n'est rouge.

| Job | Résultat |
|---|---|
`actionlint`, `gitleaks`, `schema`, `tests`, `sim-tests`, `f0-demo`, `check-and-automerge`, `invoke-cursor-auditor` | `pass` (deux fois chacun : une passe sur le push de branche, une sur la PR) |
| `cursor-scope` | `skipping` (branche `forge-bot/*`, pas `cursor/*` — `.github/workflows/audit-guard.yml:30`) |
| `Reconcile local Hermes state` | `pending` au moment de la fusion |

Sortie citée : `gh pr checks 73`. La fusion a donc eu lieu avec une
vérification encore en attente, ce que la règle « pas de fusion si un
workflow requis est rouge » (`harness/pipeline/auto_policy.yaml`, section
« Interdit en full_auto ») n'interdit pas — *pending* n'est pas *rouge*.
Signalé pour mémoire, pas comme violation.

## 2. Lentille 1 — intention avant diff

L'intention est **lisible et honnête**, ce qui est rare et mérite d'être
dit : la description de PR nomme le workflow d'origine, le run, le blocage
GitHub qui a empêché l'ouverture automatique, le chemin unique touché, et la
suite attendue (`pipeline-orchestrate` → `AUDIT_CHALLENGED` puis décision
automatique). Le diff fait exactement cela : un document, dans
`architecture/reviews/`, rien d'autre.

Le fond du document tient aussi. La revue rejoue les mesures de l'audit
qu'elle conteste, corrige deux chiffres faux (8 occurrences et non 6 ; 722
lignes copiées et non 704), et distingue proprement « je confirme » de « je
n'ai pas pu vérifier ». C'est le comportement attendu d'un contre-audit.

Les constats ci-dessous ne portent donc presque pas sur le texte de la
revue, mais sur **ce que la machine en fait**.

## 3. Constats

### F1 — `P1` — le ledger recompte faux le document qui certifie ce bug

La ligne `AUDIT_CHALLENGED` écrite pour cette revue enregistre des
verdicts qui ne sont pas ceux du document.

`architecture/audit-ledger.jsonl`, ligne 50 :

```json
{"timestamp": "2026-08-13T11:01:51Z", "audit_id": "CURSOR-4c45718-pr65-ledger-recupere-a-la-main",
 "event": "AUDIT_CHALLENGED", "actor": "claude",
 "verdicts": {"CONFIRMED": 16, "REFUTED": 4, "PARTIAL": 8, "NEEDS_OWNER": 5}}
```

Comptage réel de la colonne « Verdict » du tableau du document, rejoué :

```
$ .venv/bin/python …  # parse_verdicts (ce que le ledger enregistre)
{'CONFIRMED': 16, 'REFUTED': 4, 'PARTIAL': 8, 'NEEDS_OWNER': 5}

$ .venv/bin/python …  # colonne verdict du tableau, ligne par ligne
lignes de tableau: 16
[('1','PARTIAL'), ('2','CONFIRMED'), ('3','PARTIAL'), ('4','CONFIRMED'), ('5','CONFIRMED'),
 ('6','CONFIRMED'), ('7','PARTIAL'), ('8','CONFIRMED'), ('9','CONFIRMED'), ('10','CONFIRMED'),
 ('§4.1','PARTIAL'), ('§4.2','CONFIRMED'), ('§4.3','CONFIRMED'), ('§4.4','CONFIRMED'),
 ('§2','NEEDS_OWNER'), ('§7 environnement','CONFIRMED')]
{'PARTIAL': 4, 'CONFIRMED': 11, 'NEEDS_OWNER': 1}
```

Le document ne réfute **rien** (0 `REFUTED`), le ledger en compte 4. Cause :
`harness/audit_review.py:127-134`, `parse_verdicts` compte les occurrences du
mot sur tout le texte — légende, prose et synthèse comprises — pas la colonne
du tableau.

Ce qui rend ce constat nouveau et non un doublon : **c'est exactement le
défaut que ce document certifie**. Son point 2 est libellé « La PR réécrit une
ligne de ledger avec des comptages faux (`REFUTED: 4` alors que 0 point n'est
réfuté) » et il est marqué `CONFIRMED`. Il a été retenu par la décision
automatique dix secondes plus tard (ligne 51 du ledger,
`retained_points: [1..10]`). Le défaut confirmé s'est donc reproduit sur
l'acte même qui le confirme, et rien ne l'a vu. Je ne re-découvre pas le bug —
je mesure sa récurrence après acceptation, ce qui est l'élément nouveau exigé
par `review-guidelines.md` (« pas de rubber-stamping inverse »).

### F2 — `P1` — six lignes de verdict sur seize sont invisibles à la décision

Le tableau porte 16 verdicts ; la décision n'en voit que 10. Les six lignes
identifiées `§4.1`, `§4.2`, `§4.3`, `§4.4`, `§2`, `§7 environnement` sont
silencieusement écartées, parce que l'expression régulière exige un numéro en
première cellule — `harness/audit_decision.py:75-78` :

```python
_POINT_VERDICT_ROW = re.compile(
    r"^\|\s*(\d+)\s*\|.*?\|[\s*_`~]*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\b[^|]*\|",
    re.MULTILINE,
)
```

Rejeu :

```
$ .venv/bin/python …  # audit_decision.parse_point_verdicts
[(1,'PARTIAL'),(2,'CONFIRMED'),(3,'PARTIAL'),(4,'CONFIRMED'),(5,'CONFIRMED'),(6,'CONFIRMED'),
 (7,'PARTIAL'),(8,'CONFIRMED'),(9,'CONFIRMED'),(10,'CONFIRMED')]
```

Conséquence précise : le **seul** `NEEDS_OWNER` du document est la ligne `§2`,
donc il est perdu. La règle `review_needs_owner_only` de
`harness/pipeline/auto_policy.yaml` ne peut jamais le voir. La section « 3.
Points à porter au propriétaire » de la revue est structurellement
inatteignable : elle s'adresse à un destinataire que le parseur a supprimé
avant la décision. La revue écrit « le propriétaire doit trancher une
priorité » ; la machine a tranché à 11:01:51Z, dans la même seconde que
l'enregistrement du contre-audit.

### F3 — `P1` — `PARTIAL` est aplati en `APPROVED`, avec ses réserves perdues

La décision automatique retient 10 points sur 10 sans conserver quel verdict
portait chacun. `architecture/decisions/DECISION-CURSOR-4c45718-pr65-ledger-recupere-a-la-main.md` :

```yaml
decided_by: policy:auto
verdict: APPROVED
retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10]
```

`retained_points` est une liste d'entiers (`harness/audit_decision.py:186-187`,
`fields["retained_points"] = retained`). Rien ne distingue le point 2
(`CONFIRMED`, chiffres exacts) du point 7 (`PARTIAL`, dont la revue démontre
que l'arithmétique est fausse de 18 lignes dans les deux sens).

Or la revue avertit explicitement, dans sa recommandation finale : ces
imprécisions « ne doivent pas être recopiées telles quelles dans un brief si
le brief cite ces chiffres comme preuve ». La règle suivante de la politique
est `approved_audit_convert` → « un brief par constat retenu ». L'avertissement
ne dispose d'aucun véhicule pour arriver jusque-là : ni le ledger, ni le
fichier de décision, ni `retained_points` ne le transportent. Un contre-audit
qui distingue quatre verdicts alimente une décision qui n'en connaît qu'un.

C'est le motif d'échec que la littérature 2026 nomme explicitement : un agent
qui « fournit une appréciation positive tout en énumérant des problèmes »
(S3), d'où la recommandation d'une porte findings→décision qui classe chaque
constat en bloquant / non bloquant avant d'approuver.

### F4 — `P1` — le maillon déclaré « critique » ne peut pas bloquer

`architecture/agents/cursor-auditor.md` § Déclencheur affirme : « sur chaque
`pull_request` non-brouillon (ADR-0010 : Cursor est le maillon **critique** de
chaque PR) ». Mécaniquement, ce maillon ne peut rien retenir. Chronologie
mesurée sur cette PR même :

```
11:00:59Z  PR #73 ouverte
11:01:11Z  check-and-automerge : « All changed paths are within the allowlist and none are denied. »
11:01:12Z  gh pr merge --auto --squash "73"          ← fusion armée
11:01:21Z  invoke-cursor-auditor : {"agent_id":"bc-29800d6d-…","status":"RUNNING"}
11:01:23Z  invoke-cursor-auditor : conclusion=success  ← vert 2 s après le lancement
11:01:36Z  fusion squash dans master (4b6dcff)
11:01:51Z  AUDIT_CHALLENGED puis AUDIT_APPROVED (même seconde)
```

Sources : `gh run view --job 94426357954 --log`, `gh run view --job
94426358115 --log`, `gh api …/runs/31693648147/jobs`. Le job auditeur passe au
vert dès que l'appel API retourne `RUNNING` : il rend compte du *lancement*,
jamais du *résultat*. La fusion automatique était armée **9 secondes avant**
que l'auditeur ne démarre, et le merge est tombé **13 secondes après**. Aucun
audit ne peut exister dans cette fenêtre.

Ce n'est pas une objection de principe au *fire-and-forget* : c'est une
contradiction entre ce que le contrat déclare (maillon critique) et ce que la
CI garantit (rien). Les deux lectures possibles sont légitimes — attendre le
résultat, ou cesser de le qualifier de critique — mais l'état actuel affirme
l'une et implémente l'autre. Le cadre outillé existe côté GitHub
(`wait-for-completion`, S4 ; `needs:` et environnements à approbation, S5) et
la pratique 2026 documente le hook qui interroge la CI en temps réel et
refuse de déclarer l'achèvement avant que tout soit vert (S2).

### F5 — `P2` — le contre-audit tourne sans accès à l'API GitHub

La revue signale elle-même : « Environnement sans accès à l'API GitHub
(`gh auth status` : pas de token) — je n'ai donc pas pu rejouer les commandes
`gh api repos/.../actions/runs/...` ni `gh pr checks 65` ».

C'est vérifiable dans le workflow. `.github/workflows/pipeline-challenge.yml`,
étape « Invoke claude-challenger headless » (lignes 145-158) : le bloc `env:`
ne contient que `CLAUDE_CODE_OAUTH_TOKEN`, `ANTHROPIC_API_KEY`, `AUDIT_ID`.
Les étapes voisines, elles, reçoivent bien un jeton (`GH_TOKEN` ligne 61 pour
le kill-switch, ligne 175 pour la publication). L'agent qui juge est le seul
acteur du workflow privé d'API.

Coût mesurable sur ce document : 5 des 16 lignes de verdict portent une
réserve « je n'ai pas pu revérifier » attribuée à cette seule absence, dont la
ligne `§2` classée `NEEDS_OWNER` — et F2 montre que ce `NEEDS_OWNER` n'a
aucun destinataire. Un tiers du contre-audit est donc indécidable pour un
motif d'environnement, pas de compétence. Deux lignes d'`env:` supprimeraient
la cause.

### F6 — `P2` — la description de PR se trompe sur son propre contenu

La description annonce : « verdicts par point, **lignes de tableau : 7
CONFIRMED, 3 PARTIAL** ». Le document en contient 16, dont 11 `CONFIRMED`,
4 `PARTIAL`, 1 `NEEDS_OWNER` (rejeu en F1). Le chiffre annoncé n'est juste que
pour les 10 lignes numérotées, pas pour les « lignes de tableau » qu'il
nomme — et il **omet entièrement** le `NEEDS_OWNER`, seule catégorie à
laquelle la politique réserve une règle dédiée.

Ce n'est pas une invention : c'est un sous-comptage exact, mal étiqueté. La
convention, elle, est établie et tenue ailleurs — la description de la PR #71
annonce « 5 CONFIRMED, 2 PARTIAL » et son document en porte exactement 7 :

```
CLAUDE-CURSOR-16ff5ac-…md   lignes de tableau: 7  {'CONFIRMED': 5, 'PARTIAL': 2}
CLAUDE-CURSOR-4c45718-…md   lignes de tableau: 16 {'CONFIRMED': 11, 'PARTIAL': 4, 'NEEDS_OWNER': 1}
```

Le fait notable est que la description et la machine se trompent du **même**
dénominateur : les deux ignorent les six lignes `§`. La description de PR
reproduit l'angle mort du parseur (F2) au lieu de le révéler. C'est
précisément ce que la lentille 2 cherche : une affirmation chiffrée qu'aucune
mesure n'adosse.

### F7 — `P2` — aucune porte de schéma sur `architecture/reviews/**`

`architecture/inbox/**` est validé mécaniquement à chaque PR
(`.github/workflows/audit-guard.yml:25-26` → `python harness/audit_schema.py`),
et ce validateur ne regarde que l'inbox :

```
harness/audit_schema.py:26   INBOX = REPO_ROOT / "architecture" / "inbox"
harness/audit_schema.py:92   def validate_inbox(inbox: Path = INBOX)
harness/audit_schema.py:98   for path in sorted(inbox.glob("CURSOR-*.md")):
```

L'asymétrie est exactement inversée par rapport aux enjeux : l'artefact le
plus contrôlé (`inbox/`) ne décide de rien, et l'artefact qui **déclenche
mécaniquement** `AUDIT_APPROVED` avec ses `retained_points` (`reviews/`) n'a
aucune porte. Sa seule vérification est négative, à l'exécution :
`harness/audit_review.py:174-179` refuse une revue sans **aucun** mot de
verdict. Un document dont les verdicts sont mal comptés (F1), dont un tiers
des lignes est illisible par le décideur (F2) et dont les réserves sont
perdues (F3) passe sans un `warning`. La recommandation constante des sources
est l'inverse : porte déterministe d'abord, jugement de l'agent ensuite
(S1, S3, S6).

### F8 — `P2` — le coût de l'invocation est écrit puis jeté

Le point 10 de l'audit relu constate que `harness/pipeline/ci-budget-ledger.jsonl`
est vide, et la revue le confirme. Après l'invocation payante qui a produit ce
document de 16 KB, il l'est toujours :

```
$ wc -c -l harness/pipeline/ci-budget-ledger.jsonl
1 1 harness/pipeline/ci-budget-ledger.jsonl      # une ligne vide, 1 octet
```

L'élément nouveau est le mécanisme, lisible dans le workflow. L'étape
« Post-hoc budget marking » a bien réussi au run 31684301091 (`gh api
…/runs/31684301091/jobs` : `conclusion: success`) et écrit sa ligne via
`ci_budget_guard.py` → `DEFAULT_LEDGER_PATH = harness/pipeline/ci-budget-ledger.jsonl`
(`harness/pipeline/ci_budget_guard.py:39`). Mais l'étape suivante ne commite
que la revue : `.github/workflows/pipeline-challenge.yml:195`, `git add
architecture/reviews`. La ligne de coût meurt avec le runner, à chaque
invocation, depuis toujours. Le compteur mensuel de `precheck` lit donc un
ledger structurellement vide : le plafond ne peut pas se déclencher.

Les sources 2026 sur le budget d'agents insistent sur ce point : l'exécution
doit réserver puis **capturer** le coût réel dans un registre durable, sinon
le plafond est décoratif (S6, S7). Ici la capture existe et n'est pas
persistée — le cas le plus coûteux, puisqu'il donne l'apparence d'un
compteur.

### F9 — `P3` — une affirmation de la revue est devenue fausse à la fusion

La revue écrit en § 1 : « Ce commit n'est **pas** fusionné dans `master` (ni
local ni `origin/master` après fetch) ». C'était vrai à `reviewed_at:
2026-08-13T09:01:51Z`. La PR #65 a été fusionnée à 10:47:51Z ; le document a
été publié à 11:00:59Z et fusionné à 11:01:37Z, soit 14 minutes après que son
affirmation soit devenue fausse :

```
$ git merge-base --is-ancestor 4c4571892476603e41740f3d3ef52ca527ba5358 origin/master
→ ANCESTOR: yes
```

Aucune conclusion de la revue ne change. Mais l'état `AUDIT_STALE` existe
précisément pour cela : il est déclaré dans la machine à états
(`harness/audit_ledger.py:51` et transitions lignes 85-93) et atteignable
depuis tous les états. Rien ne le calcule — aucune occurrence hors
`audit_ledger.py` et son test (`harness/tests/test_audit_fsm.py:110`). La
péremption est modélisée, jamais mesurée.

### F10 — `P3` — taille conforme, lisibilité en trompe-l'œil

Sur la lentille 5, cette PR est **exemplaire** : un fichier, un objet, 99
lignes — très loin du seuil de ~400 lignes au-delà duquel la relecture
humaine s'effondre (S3 recommande d'ailleurs un avertissement non bloquant à
ce seuil, pas un blocage). À comparer aux 11 fichiers / 843 lignes de la PR
qu'elle relit.

Nuance : le décompte de lignes est ici un mauvais indicateur. 99 lignes
pèsent 15 908 octets et la ligne la plus longue fait 1 390 caractères — le
tableau met un raisonnement entier par ligne. Rien à corriger dans ce diff ;
juste une mise en garde si un seuil mécanique devait un jour se fonder sur le
nombre de lignes.

## 4. Ce qui tient — vérifié, pas concédé

- **Le chemin est propre.** Un seul fichier, dans l'allowlist du merge-bot,
  vérifié à l'exécution : « Changed files: architecture/reviews/CLAUDE-CURSOR-4c45718-….md
  / All changed paths are within the allowlist and none are denied. »
  (`gh run view --job 94426357954 --log`).
- **La séparation des acteurs est respectée** (lentille 4) : l'audit vient de
  `cursor-cloud`, le contre-audit de `claude-challenger`, et le document ne
  s'attribue aucune autorité d'exécution — il le vérifie même explicitement
  dans sa section 3.
- **Les corrections chiffrées du contre-audit sont justes.** J'ai rejoué les
  deux plus significatives : le ledger au commit relu contient bien 8 lignes
  `AUDIT_IMPLEMENTED`/`AUDIT_VERIFIED` (et non 6), et les trois fichiers
  archivés totalisent bien 722 lignes (et non 704). Le contre-audit corrige
  l'audit dans le bon sens.
- **La revue nomme ses limites** au lieu de les masquer. C'est la qualité qui
  rend F5 réparable : sans cet aveu, l'absence d'API serait invisible.

## 5. Limites de cet audit

- Je n'ai pas relu les transcriptions d'invocation des runs 31684301091 et
  31693648147 : elles ne sont pas conservées comme artefacts. Les
  chronologies de F4 et F8 reposent donc sur les logs de jobs et l'API
  Actions, pas sur le détail interne des agents.
- Le coût réel en jetons de l'invocation qui a produit ce document est
  **inconnaissable** depuis le dépôt, ce qui est le constat F8 lui-même.
- F1 et F8 recoupent des motifs déjà retenus par la décision automatique sur
  l'audit `CURSOR-4c45718` (`retained_points: [1..10]`, ledger ligne 51). Je
  ne les représente pas comme des découvertes : l'élément nouveau est leur
  **récurrence mesurée après acceptation**, et pour F8 le mécanisme précis
  (ligne de coût écrite puis non commitée) que l'audit amont n'identifiait
  pas.
- Aucun `P0` n'est émis. Un `P0` bloque la fusion ; celle-ci a eu lieu
  (squash `4b6dcff`) et le diff lui-même — un document de 99 lignes dans un
  chemin allowlisté — ne justifierait pas un blocage. Les quatre `P1`
  portent sur la machinerie qui consomme ce document, pas sur son contenu.

## 6. Propositions de briefs atomiques (3, plafond du contrat)

Propositions soumises à l'arbitrage de la boucle. Aucune n'est autorisée par
cet audit.

1. **Un seul analyseur de verdicts, indexé sur la colonne du tableau.**
   Remplacer le comptage de mots de `parse_verdicts` et l'exigence de numéro
   de `_POINT_VERDICT_ROW` par un analyseur unique qui lit la colonne
   « Verdict » et accepte tout identifiant de point (`1`, `§4.1`, `§7 …`).
   Test rouge exigé : le document réel de cette PR doit rendre
   `{CONFIRMED: 11, PARTIAL: 4, NEEDS_OWNER: 1}` et 16 points, là où le code
   actuel rend `{16, 4, 8, 5}` et 10 points. Couvre F1, F2, et retire à F6 sa
   cause commune.
2. **Le verdict par point survit jusqu'à la décision, et rien n'entre dans
   `reviews/**` sans porte.** Faire porter à la ligne de ledger et au fichier
   de décision le verdict de chaque point retenu (au minimum
   `CONFIRMED` vs `PARTIAL`), pour que la conversion en brief sache quels
   chiffres ne sont pas réutilisables comme preuve ; et étendre la porte de
   schéma CI à `architecture/reviews/**` comme elle l'est déjà à
   `architecture/inbox/**`. Couvre F3 et F7.
3. **Trancher le statut du maillon auditeur.** Soit `invoke-cursor-auditor`
   attend l'audit et conditionne l'armement de `gh pr merge --auto`, soit
   ADR-0010 et `architecture/agents/cursor-auditor.md` cessent de le qualifier
   de « critique » et le décrivent comme post-fusion. La contradiction entre
   les deux est le défaut ; le choix appartient au propriétaire. Couvre F4.

Non convertis, délibérément, pour tenir le plafond de trois : **F5** (deux
lignes d'`env:` dans `pipeline-challenge.yml`) et **F8** (commiter la ligne de
coût), tous deux de très faible surface et candidats naturels à accompagner la
proposition 3 si la boucle en décide ainsi. **F9** et **F10** sont
informatifs.

## 7. Commandes rejouées

Environnement : VM Linux Cursor Cloud, dépôt à `origin/master`
(`8cb2610ce1d914aac93dda43d9d048403b7a6f6f` au moment du rejeu),
`.venv/bin/python` 3.13, `gh` authentifié en lecture. Dans les extraits,
`REVIEW` désigne
`architecture/reviews/CLAUDE-CURSOR-4c45718-pr65-ledger-recupere-a-la-main.md`,
le fichier unique ajouté par la PR #73.

```
$ git merge-base --is-ancestor ae26aaecb208dde87bd9c6cce3425fe106c064d2 origin/master
ae26aae ancestor of master: NO
$ git merge-base --is-ancestor 4b6dcff50276b8f4884430ff899f8969eeca6ac2 origin/master
4b6dcff ancestor of master: yes
$ git merge-base --is-ancestor 4c4571892476603e41740f3d3ef52ca527ba5358 origin/master
ANCESTOR: yes

$ grep -n "4c45718" architecture/audit-ledger.jsonl | tail -2
50:{… "event": "AUDIT_CHALLENGED", "actor": "claude", "verdicts": {"CONFIRMED": 16, "REFUTED": 4, "PARTIAL": 8, "NEEDS_OWNER": 5}}
51:{… "event": "AUDIT_APPROVED", "actor": "policy:auto", "retained_points": [1,2,3,4,5,6,7,8,9,10]}

$ .venv/bin/python -c "import sys; sys.path.insert(0,'harness'); import audit_review, pathlib;
  print(audit_review.parse_verdicts(pathlib.Path(REVIEW).read_text(encoding='utf-8')))"
{'CONFIRMED': 16, 'REFUTED': 4, 'PARTIAL': 8, 'NEEDS_OWNER': 5}

$ .venv/bin/python -c "import sys; sys.path.insert(0,'harness'); import audit_decision, pathlib;
  print(audit_decision.parse_point_verdicts(pathlib.Path(REVIEW).read_text(encoding='utf-8')))"
[(1,'PARTIAL'),(2,'CONFIRMED'),(3,'PARTIAL'),(4,'CONFIRMED'),(5,'CONFIRMED'),(6,'CONFIRMED'),
 (7,'PARTIAL'),(8,'CONFIRMED'),(9,'CONFIRMED'),(10,'CONFIRMED')]

$ # comptage de la colonne « Verdict », ligne de tableau par ligne de tableau
CLAUDE-CURSOR-16ff5ac-…md   lignes de tableau: 7  {'CONFIRMED': 5, 'PARTIAL': 2}
                            lignes fichier: 102  octets: 14951  ligne la plus longue: 2095
CLAUDE-CURSOR-4c45718-…md   lignes de tableau: 16 {'CONFIRMED': 11, 'PARTIAL': 4, 'NEEDS_OWNER': 1}
                            lignes fichier: 99   octets: 15908  ligne la plus longue: 1390

$ wc -c -l harness/pipeline/ci-budget-ledger.jsonl
1 1 harness/pipeline/ci-budget-ledger.jsonl

$ # les deux corrections chiffrées du contre-audit, revérifiées (§ 4)
$ git show 4c45718:architecture/audit-ledger.jsonl | grep -cE '"event": "(AUDIT_IMPLEMENTED|AUDIT_VERIFIED)"'
8                                    # le contre-audit dit 8, l'audit disait 6 → contre-audit juste
$ git diff-tree --no-commit-id --name-only -r 4c45718 -- architecture/archive/ \
    | while read f; do echo "$(git show 4c45718:$f | wc -l)  $f"; done
88   architecture/archive/CURSOR-3b47ffe-…/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md
616  architecture/archive/CURSOR-3b47ffe-…/CURSOR-3b47ffe-pr57-monde-sans-faim.md
18   architecture/archive/CURSOR-3b47ffe-…/DECISION-CURSOR-3b47ffe-pr57-monde-sans-faim.md
TOTAL: 722                           # le contre-audit dit 722, l'audit disait 704 → contre-audit juste
$ git show --shortstat 4c45718 | tail -1
 11 files changed, 843 insertions(+)

$ python3 harness/audit_schema.py     # porte de schéma de l'inbox, cet audit inclus
OK   CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort.md
All 35 audit(s) valid.               # exit=0

$ grep -rn "AUDIT_STALE" --include=*.py --include=*.yml . | grep -v "^./.git"
./harness/audit_ledger.py:51,70,85,86,87,88,90,91,93     (déclaration + transitions)
./harness/tests/test_audit_fsm.py:110                     (son test)
    → aucun producteur : rien ne calcule la péremption

$ gh api repos/PLiagre/ForgeHistory/actions/runs/31693648147/jobs
{"name":"invoke-cursor-auditor","started_at":"2026-08-13T11:01:04Z","completed_at":"2026-08-13T11:01:23Z","conclusion":"success"}
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31693648136/jobs
{"name":"check-and-automerge","started_at":"2026-08-13T11:01:04Z","completed_at":"2026-08-13T11:01:16Z","conclusion":"success"}

$ gh run view --job 94426358115 --log   # invoke-cursor-auditor
11:01:21Z {"agent_id":"bc-29800d6d-1989-46c0-bace-1749a6886905","status":"RUNNING"}
11:01:21Z cursor-auditor launched -- its audit will arrive as a cursor/* PR touching architecture/inbox/** only.

$ gh run view --job 94426357954 --log   # check-and-automerge
11:01:12Z Changed files: architecture/reviews/CLAUDE-CURSOR-4c45718-pr65-ledger-recupere-a-la-main.md
11:01:12Z All changed paths are within the allowlist and none are denied.
11:01:12Z gh pr merge --auto --squash "73"
```

## 8. Sources externes

Consultées le **2026-08-13** depuis cette VM.

| # | Source | URL | Date de la source | Sert à |
|---|---|---|---|---|
| S1 | Daniel Vaughan — *OpenCodeReview and the Determinism Dividend* | <https://codex.danielvaughan.com/2026/08/11/opencodereview-deterministic-code-review-agent-codex-cli-rule-dispatch-grounded-review-independent-reflection/> | 2026-08-11 | dispatch déterministe avant jugement d'agent ; réflecteur indépendant qui filtre les constats contredits par le diff → F7 |
| S2 | kane.mx — *From Solo AI Engineer to Autonomous Dev Team* | <https://kane.mx/posts/2026/autonomous-dev-team-openclaw/> | 2026 | hook `verify-completion` qui interroge la CI en temps réel et interdit de déclarer l'achèvement avant que tout soit vert ; portes que les agents ne peuvent contourner → F4, F5 |
| S3 | tanhdev — *AI Code Review Pipeline: Zero-Trust, Multi-Agent & Mutation Testing* (part 4) | <https://tanhdev.com/series/ai-code-review-vibe-coding/part-4-review-pipeline-multi-agent/> | 2026 | taxonomie P0 bloquant / P1 exigeant résolution / P2 non bloquant, à appliquer par programme et non à la discrétion du relecteur ; avertissement au-delà de ~400 lignes → F3, F10 |
| S4 | GitHub Marketplace — *Workflow Dispatch and wait* (`wait-for-completion`) | <https://github.com/marketplace/actions/workflow-dispatch-and-wait> | consultée 2026-08-13 | le chaînage attendant réellement le résultat existe et est outillé → F4 |
| S5 | GitHub Docs — *Trigger a workflow* | <https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/trigger-a-workflow> | consultée 2026-08-13 | `needs:` et environnements à approbation ; pourquoi une PR ouverte au `GITHUB_TOKEN` ne déclenche pas les workflows (contexte du contournement PAT décrit dans `pipeline-challenge.yml:43-49`) → F4 |
| S6 | AgentBudget — *Real-Time Cost Enforcement for AI Agents* (livre blanc v1) | <https://agentbudget.dev/agentbudget_whitepaper_v1.pdf> | 2026 | application en deux phases (estimation avant appel, réconciliation après) ; l'observabilité post-hoc ne prévient aucun dépassement → F8 |
| S7 | UsageBox — *The LLM Gateway Is Your Cheapest Cost Lever* | <https://usagebox.com/articles/llm-gateway-cost-control-token-quotas-2026> | 2026 | distinction point d'application / point de comptabilité : « un journal de requêtes n'est pas un registre de facturation » → F8 |

Les six lentilles et les sévérités P0–P3 viennent de
`architecture/review-guidelines.md` (sources S1–S5 de ce fichier, consultées
le 2026-08-12) ; les sept sources ci-dessus sont propres à cet audit.
