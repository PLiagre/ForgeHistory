---
audit_id:                CURSOR-2a4f808-decision-auto-ledger
auditor:                 cursor-cloud
target_branch:           master
target_commit:           2a4f808f6be589d584ef7c2de75bb325e79ab4f4
created_at:              2026-08-12T14:53:50Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit du merge 2a4f808 — verdicts Markdown tolérés et écriture du ledger sérialisée

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

Le merge répare la première exécution réelle de la boucle : trois contre-audits
fusionnés à 13:54 UTC n'ont produit aucune ligne `AUDIT_CHALLENGED`. Deux causes
étaient visées, deux correctifs sont livrés.

Ce qui est solide, et qu'il faut porter au crédit du commit :

- le parseur de verdicts est **exposé une seule fois** (`parse_point_verdicts`,
  public) et réutilisé par `audit_review.record_challenge` : un seul parseur,
  un seul contrat, plus de second endroit qui pourrait diverger du premier ;
- le refus au moment du `record` est la bonne direction — mieux vaut refuser
  une revue illisible tôt que la voir bloquer la boucle plus tard ;
- trois tests neufs, dont un test « anti-chasse-aux-mots-clés » qui prouve que
  la tolérance ne dégénère pas en recherche de mot-clé n'importe où ;
- la suite complète est verte (314 réussis, 16 ignorés) et le workflow reste
  couvert par sa denylist de fusion automatique.

Ce que l'audit conteste, avec preuve rejouée à chaque fois :

1. **Le `git push` de l'orchestrateur est aujourd'hui refusé par la protection
   de branche de `master`** (`GH006 ... 5 of 5 required status checks are
   expected`). Le `git pull --rebase` ajouté par ce commit corrige une course
   *fast-forward* qui n'est plus le verrou : la même poussée directe est
   rejetée pour une autre raison, observée sur ce commit même.
2. **Le groupe `concurrency` livré annule les runs en attente.** Par défaut
   (`queue: single`), GitHub garde **un seul** run en attente et annule le
   précédent. Le scénario exact que le commit décrit — trois fusions coup sur
   coup — perd toujours l'exécution du milieu, désormais en silence (annulée)
   au lieu de bruyamment (push rejeté). Le dépôt en fournit déjà la preuve.
3. **Le parseur n'est pas une lecture de la colonne « Verdict »** malgré ce
   qu'affirment son commentaire et son docstring : il retient la **première
   cellule qui commence par un mot de verdict**, quelle que soit sa colonne. Un
   verdict raturé, une colonne Verdict vide ou une ligne de gabarit non remplie
   produisent donc un verdict exploitable.
4. **La cellule composite fait disparaître `NEEDS_OWNER`**, et le résultat
   dépend de l'ordre des mots dans la phrase du challenger. La règle
   `review_needs_owner_only` (« pas de propriétaire en full_auto ») est
   neutralisée dès que le challenger écrit `CONFIRMED` avant `NEEDS_OWNER` — et
   un test livré par ce commit fige ce comportement comme correct.

Aucun de ces points ne remet en cause l'intention du commit ; tous portent sur
l'écart entre ce que le code **affirme garantir** et ce qu'il garantit
réellement, dans une boucle où une ligne mal lue devient une décision
`APPROVED` sans humain.

## Périmètre audité

| Élément | Valeur |
|---|---|
| Merge audité | `2a4f808f6be589d584ef7c2de75bb325e79ab4f4` (PR [#52](https://github.com/PLiagre/ForgeHistory/pull/52), fusionnée le 2026-08-12 à 14:47:43 UTC) |
| Premier parent | `bad1ffbe52996f4af4aaa805098254fe3abee6bc` |
| Diff | 5 fichiers, +128 / −5 |
| Fichiers | `.github/workflows/pipeline-orchestrate.yml`, `harness/audit_decision.py`, `harness/audit_review.py`, `harness/tests/test_audit_decision.py`, `harness/tests/test_audit_review.py` |

Commande rejouée :

```
$ git show --stat --format='%H%n%P' 2a4f808f6be589d584ef7c2de75bb325e79ab4f4
2a4f808f6be589d584ef7c2de75bb325e79ab4f4
bad1ffbe52996f4af4aaa805098254fe3abee6bc d7c503275ac6e8665b4b876a17e3182da0ea26c7

 .github/workflows/pipeline-orchestrate.yml | 16 ++++++++++
 harness/audit_decision.py                  | 25 ++++++++++++----
 harness/audit_review.py                    | 15 ++++++++++
 harness/tests/test_audit_decision.py       | 48 ++++++++++++++++++++++++++++++
 harness/tests/test_audit_review.py         | 29 ++++++++++++++++++
 5 files changed, 128 insertions(+), 5 deletions(-)
```

## État de la CI sur le commit audité

Classification des exécutions déclenchées par la poussée de `2a4f808` sur
`master` (`gh run list --commit 2a4f808f6be589d584ef7c2de75bb325e79ab4f4`) :

| Workflow | Run | Conclusion |
|---|---|---|
| `harness-ci` | 31608759559 | ✅ succès |
| `pipeline-audit` | 31608759562 | ✅ succès |
| `audit-guard` | 31608759571 | ✅ succès |
| `security` | 31608759551 | ✅ succès |
| `hermes-dashboard` | 31608759556 | ❌ **échec** |
| `pipeline-failure-escalate` | 31608798448 | ⏭️ ignoré (skipped) |
| `hermes-observer` (×5) | 31608777689 … 31608806531 | ✅ succès |

**CI globalement verte sur les gardes de qualité, rouge sur `hermes-dashboard`.**
Cet échec n'est pas un détail cosmétique : il documente précisément la panne
décrite en P0-1 ci-dessous.

Suite de tests rejouée localement au commit audité :

```
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 16.86s
```

(Les 16 ignorés sont les cas `test_run_unity.py`, qui exigent Unity et
PowerShell — comportement attendu sur Linux, documenté dans `AGENTS.md`.)

## Risques par sévérité

### P0-1 — La poussée directe sur `master` est refusée par la protection de branche ; le rebase ne peut pas la débloquer

`pipeline-orchestrate.yml` termine par `git pull --rebase origin master` puis
`git push` : une **poussée directe sur `master`**, avec le `GITHUB_TOKEN` du
run. Or `master` refuse désormais ces poussées. Preuve, tirée du run déclenché
par le commit audité lui-même :

```
$ gh run view 31608759556 --log-failed
... remote: error: GH006: Protected branch update failed for refs/heads/master.
... remote: - 5 of 5 required status checks are expected.
... ! [remote rejected] master -> master (protected branch hook declined)
... error: failed to push some refs to 'https://github.com/PLiagre/ForgeHistory'
```

Ce n'est pas un incident isolé. `hermes-dashboard.yml` — le workflow que le
commentaire du commit cite explicitement comme modèle (« même schéma que
`hermes-dashboard.yml` ») — échoue à **chaque** exécution depuis 13:47 UTC :

```
$ gh run list --workflow hermes-dashboard.yml --limit 14
failure 31608759556  2026-08-12T14:47:47Z
failure 31604845295  2026-08-12T14:05:01Z
failure 31604701402  2026-08-12T14:03:28Z
failure 31604699496  2026-08-12T14:03:26Z
failure 31604232593  2026-08-12T13:58:17Z
failure 31603929705  2026-08-12T13:54:54Z
cancelled 31603909696 2026-08-12T13:54:39Z
failure 31603893487  2026-08-12T13:54:29Z
cancelled 31603872453 2026-08-12T13:54:14Z
failure 31603848737  2026-08-12T13:53:59Z
...
```

Et la dernière poussée directe de bot réellement arrivée sur `master` date de
12:56:33 UTC :

```
$ git log --format='%h %an %ad' --date=iso -40 | grep hermes
3807764 hermes 2026-08-12 12:56:33 +0000
ad5ac91 hermes 2026-08-12 12:56:12 +0000
8c3f0c5 hermes 2026-08-12 12:40:08 +0000
```

Conséquence : le correctif « rebase avant push » traite une course
*fast-forward* (`! [rejected] ... (fetch first)`, symptôme de 13:54) qui n'est
plus le verrou. Depuis ~13:47, la poussée est refusée par le crochet de
protection, que le rebase soit fait ou non. Tant que ce point tient, la boucle
full-auto **ne peut plus persister aucun état** : ni `AUDIT_CHALLENGED`, ni
décision, ni graine de brief.

*Limite de la preuve, à dire honnêtement* : aucune exécution de
`pipeline-orchestrate` n'a atteint l'étape de poussée depuis la fusion (la
seule tentative post-fusion a échoué plus tôt, voir P2-6). Le constat repose
donc sur l'identité de mécanisme (même cible `master`, même `GITHUB_TOKEN`,
même `git push`) et sur huit refus consécutifs observés sur ce mécanisme, dont
un sur le commit audité. Il ne repose pas sur la lecture des règles de
protection : l'API est inaccessible à ce jeton
(`gh api repos/PLiagre/ForgeHistory/branches/master/protection` →
`403 Resource not accessible by integration`).

### P0-2 — Le groupe `concurrency` livré annule les runs en attente : la ligne du milieu est toujours perdue

Le commit ajoute :

```yaml
concurrency:
  group: pipeline-orchestrate-master
  cancel-in-progress: false
```

`cancel-in-progress: false` protège le run **en cours**. Il ne dit rien de la
**file d'attente**. La documentation GitHub est explicite :

> « When a concurrent job or workflow is queued, if another job or workflow
> using the same concurrency group in the repository is in progress, the queued
> job or workflow will be `pending`. **By default, any existing `pending` job or
> workflow in the same concurrency group will be canceled** and the new queued
> job or workflow will take its place. »
> — GitHub Docs, *Workflow syntax › `concurrency`*, consulté le 2026-08-12.

Le paramètre qui gouverne la file est `queue` (`single` par défaut, `max` pour
autoriser jusqu'à 100 runs en attente). Il n'est pas positionné ici.

Déroulé du scénario que le commit dit corriger (trois fusions coup sur coup) :
le run A démarre, le run B passe en attente, le run C arrive → **B est annulé**
et sa ligne de ledger disparaît. Symptôme différent, perte identique — et cette
fois silencieuse, puisqu'un run annulé n'a pas de journal d'erreur.

Ce n'est pas une déduction théorique : `hermes-dashboard.yml` porte déjà
exactement ce même motif (`group: hermes-dashboard`, `cancel-in-progress:
false`) et le dépôt en montre la conséquence, au moment même de la rafale de
13:54 :

```
$ gh run view 31603872453 --json conclusion,createdAt,updatedAt,jobs \
    --jq '{conclusion,createdAt,updatedAt,steps:[.jobs[].steps[]|{name,conclusion}]}'
{"conclusion":"cancelled","createdAt":"2026-08-12T13:54:14Z","steps":[],"updatedAt":"2026-08-12T13:54:30Z"}

$ gh run view 31603909696 ... (idem)
{"conclusion":"cancelled","createdAt":"2026-08-12T13:54:39Z","steps":[],"updatedAt":"2026-08-12T13:54:55Z"}
```

Lecture : les deux runs sont annulés **sans avoir exécuté une seule étape**
(`steps: []`), à la seconde près où le run suivant du même groupe est créé
(13:54:29 → annulation à 13:54:30 ; 13:54:54 → annulation à 13:54:55). C'est
la signature exacte du remplacement d'un run en attente.

### P1-3 — Le parseur retient la première cellule qui *commence* par un verdict, pas la colonne « Verdict »

Le commentaire du commit affirme : « this stays a parse of the verdict column,
not a keyword hunt ». Le motif livré est :

```python
r"^\|\s*(\d+)\s*\|.*?\|[\s*_`~]*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\b[^|]*\|"
```

`.*?\|` est paresseux et **non ancré sur une position de colonne** : il avance
jusqu'à la première cellule dont le contenu s'ouvre par un mot de verdict,
quelle que soit sa place dans la ligne. Sonde rejouée sur le code du commit
audité :

```
$ .venv/bin/python -c "import sys; sys.path.insert(0,'harness'); import audit_decision as ad; ..."

A. cellule Verdict VIDE, la preuve commence par un verdict
   | 1 | point non tranche |  | CONFIRMED par ailleurs dans le run 42 |
   -> [(1, 'CONFIRMED')]

D. verdict raturé puis corrigé
   | 2 | point corrige | ~~REFUTED~~ CONFIRMED | preuve |
   -> [(2, 'REFUTED')]

E. ligne de gabarit non remplie
   | 1 | <<point>> | CONFIRMED/REFUTED/PARTIAL/NEEDS_OWNER | <<preuve>> |
   -> [(1, 'CONFIRMED')]
```

Trois façons de fabriquer un verdict que le challenger n'a pas rendu :

- **A** : la colonne Verdict est vide, le parseur lit la colonne Preuve. Le
  garde « une revue sans verdict n'est pas une revue » est contourné par une
  simple phrase de preuve qui commence par le mot.
- **D** : le caractère `~` figure dans la classe de décoration tolérée, donc un
  verdict **barré** (rature Markdown, forme naturelle d'une correction) est lu
  *à la place* du verdict corrigé qui le suit.
- **E** : la ligne d'exemple d'un gabarit, qui énumère les quatre jetons,
  devient `CONFIRMED`.

Le test « anti-chasse-aux-mots-clés » livré par le commit ne couvre que le cas
où le mot est **au milieu** d'une phrase ; il ne couvre aucun des trois
ci-dessus. La garantie annoncée dans le commentaire est donc plus forte que la
garantie réelle — dans un chemin où une ligne lue devient un `AUDIT_APPROVED`
sans humain (`audit_decision.decide_auto`).

### P1-4 — La cellule composite efface `NEEDS_OWNER`, et le résultat dépend de l'ordre des mots

`auto_policy.yaml` contient une règle dont l'objet est précisément de **ne pas
décider sans le propriétaire** : `review_needs_owner_only` → `REJECTED`,
« policy: no owner in full_auto ». La tolérance « le jeton de tête gagne »
la neutralise dès que le challenger écrit `CONFIRMED` en premier. Sonde
rejouée :

```
B. | 8 | secrets non mesurables | **CONFIRMED** (mesurabilite) / **NEEDS_OWNER** (reformulation) | preuve |
   -> [(8, 'CONFIRMED')]      => point RETENU, décision APPROVED

C. | 9 | idem, ordre inverse  | **NEEDS_OWNER** (reformulation) / **CONFIRMED** (mesurabilite) | preuve |
   -> [(9, 'NEEDS_OWNER')]    => point NON retenu
```

Même contenu sémantique, deux décisions opposées selon l'ordre de rédaction.
Deux conséquences distinctes :

1. le `NEEDS_OWNER` explicite du challenger — sa demande d'arbitrage humain —
   disparaît sans trace du ledger dans le cas B ;
2. le verdict machine devient sensible à un choix de style du rédacteur, ce que
   `docs/rules/hard-won-rules.md` proscrit pour tout compteur mécanique.

Le point mérite d'être souligné : le commit livre un test
(`test_decide_auto_accepts_markdown_decorated_verdicts`) qui **fige ce
comportement comme attendu** (`retained_points == [1, 2, 3, 8]`, le point 8
étant justement la cellule composite). Un futur correctif devra donc d'abord
défaire un test vert — c'est le mécanisme classique par lequel un assouplissement
provisoire devient une règle permanente.

### P2-5 — Le refus au `record` ne bloque pas la publication de la PR de contre-audit

L'intention du garde est juste : « refusing here, at record time, puts the
error in front of the actor (Claude) who can still rewrite the table ». Le
chemin réel a deux appels de `record_challenge`, et seul le premier est devant
Claude :

- **pendant l'invocation headless**, `/forge-audit-review`
  (`.claude/commands/forge-audit-review.md`, étape « `py
  harness/audit_review.py record --audit-id $ARGUMENTS` ») — c'est bien là que
  le nouveau garde peut aider ;
- **après fusion**, `pipeline-orchestrate.yml` →
  `orchestrator.py` (`review_recorded`) → `audit_review.record_challenge`
  (`.github/workflows/pipeline-challenge.yml`, lignes 178-185 : la ligne écrite
  en session est volontairement jetée, « la ligne authentique est ré-écrite sur
  master, après fusion »).

Or l'étape qui publie la revue en PR ne vérifie **pas** que le `record` en
session a réussi ; elle ne teste que la présence d'un fichier modifié :

```yaml
if [ -z "$(git status --porcelain -- architecture/reviews)" ]; then
  echo "::warning::the invocation left no review -- nothing to publish"; exit 0
fi
```

Une revue refusée par le nouveau garde est donc quand même publiée, fusionnée,
et rebloque la boucle après fusion — au même endroit qu'avant, à une étape près
(`record_challenge` au lieu de `decide_auto`), avec en prime un audit qui reste
`PROPOSED` au lieu de `CHALLENGED`. Le bénéfice réel du garde dépend
entièrement de la bonne volonté de l'agent en session ; rien de mécanique ne
l'impose.

### P2-6 — Le chemin de reprise manuel accepte un `audit_id` malformé et a échoué à son premier usage

La PR #52 documente une reprise manuelle (« Reste à faire après fusion,
2 minutes, propriétaire »). Elle a été tentée à 14:49 UTC, deux minutes après
la fusion, et a échoué :

```
$ gh run view 31608933878 --log-failed
python harness/pipeline/orchestrator.py run --event "review_recorded" \
  --payload '{"audit_id": "audit_id=CURSOR-e849633-hermes-demande-pilotage"}'
error: no audit 'audit_id=CURSOR-e849633-hermes-demande-pilotage' in inbox
##[error]Process completed with exit code 2.
```

Le garde a bien échoué proprement (fail-closed, message clair) — c'est à
porter au crédit du code. Mais l'étape `Resolve event + payload` a transmis
telle quelle une valeur `clé=valeur` collée par erreur dans le champ, sans la
normaliser ni la confronter à l'inbox, alors qu'elle lit déjà le ledger à cette
étape. Résultat : le seul chemin de reprise offert pour les trois audits bloqués
n'a rien réparé, et les trois lignes `AUDIT_CHALLENGED` manquent toujours
(`architecture/audit-ledger.jsonl` ne contient aucune entrée pour
`CURSOR-e849633-…`, `CURSOR-0269d8e-…`, `CURSOR-bb8fe11-…`).

### P3-7 — `pull --rebase` sans re-tentative ni repli, et une prémisse trop large

Le commentaire justifie l'absence de conflit ainsi : « Les seuls autres
écrivains du ledger sont les runs de CE workflow ». C'est exact **pour le
fichier ledger**. Mais le rebase et la poussée, eux, sont exposés à tous les
autres écrivains de `master` (`hermes-dashboard.yml`, le merge-bot, les
fusions du propriétaire). Entre le `git pull --rebase` et le `git push`, une
autre poussée peut encore gagner la course : il n'y a ni boucle de
re-tentative, ni repli en pull request. Le point avait déjà été soulevé par
l'audit `CURSOR-7e5244b-ledger-post-fusion-poussee-master` (« pas de nouvelle
tentative, pas de repli en pull request ») ; ce commit en traite deux volets
sur quatre.

### P3-8 — Couplage de `audit_review` vers `audit_decision`

`audit_review.py` importe désormais `audit_decision` pour réutiliser le
parseur. L'objectif — un seul parseur — est le bon. L'effet de bord est que le
module qui **enregistre** dépend du module qui **décide**, et que
`audit_review` charge maintenant, par transitivité, `pipeline.policy_loader` et
`auto_policy.yaml`. Un module neutre (par exemple `harness/review_table.py`)
porterait le contrat partagé sans cette dépendance directionnelle. Aucun défaut
observable aujourd'hui ; c'est une dette de structure, à arbitrer, pas un bug.

## Briefs proposés (3 maximum — aucun n'est pré-autorisé)

> Rappel de contrat : ces propositions sont des **entrées** pour
> `claude-challenger` puis pour le propriétaire. Rien ici n'autorise une
> implémentation ; seule une conversion explicite en brief sous
> `harness/queue/briefs/` fait autorité (`CLAUDE.md` › Single Source of
> Instruction).

### Brief proposé n°1 — Rendre la persistance du ledger insensible à la protection de branche et à l'annulation de runs

Couvre P0-1, P0-2, P3-7. Le sujet est *comment* l'état de la boucle atteint
`master`, pas *quoi* décider. Pistes à instruire, sans en présumer l'issue :
poussée par pull request `forge-bot/*` (chemin déjà allowlisté par le
merge-bot) plutôt que poussée directe ; ou `queue: max` sur le groupe
`concurrency` accompagné d'une re-tentative bornée ; ou combinaison des deux.
La condition de succès mesurable évidente : trois fusions déclenchées en moins
de 60 secondes produisent trois lignes de ledger, prouvé par un rejeu.

### Brief proposé n°2 — Faire du parseur de verdicts une vraie lecture de colonne

Couvre P1-3, P1-4. Trois questions à trancher, qui sont des choix de produit
autant que de code : la position de la colonne « Verdict » doit-elle être
déterminée par l'en-tête du tableau plutôt que par « la première cellule qui
commence par un jeton » ? une cellule contenant **deux** jetons doit-elle être
refusée (ambiguë) plutôt que résolue par l'ordre des mots ? un verdict raturé
(`~~…~~`) doit-il être ignoré ? Le test qui fige aujourd'hui la résolution
« jeton de tête » devra être révisé en conséquence, ce qui suppose une décision
explicite et non un simple correctif.

### Brief proposé n°3 — Fermer le contre-audit avant la PR, et fiabiliser la reprise manuelle

Couvre P2-5, P2-6. Deux volets : (a) `pipeline-challenge.yml` ne publie une
revue en PR que si `audit_review.py record` a réussi en session — sinon la
tolérance nouvellement ajoutée reste facultative pour l'agent qui l'ignore ;
(b) l'étape `Resolve event + payload` normalise et valide l'`audit_id` reçu en
`workflow_dispatch` contre l'inbox, pour que le chemin de reprise documenté ne
puisse plus échouer sur un collage `clé=valeur`.

## Veille externe (`cursor-qa-scout`)

Section append-only du compagnon de session, conforme à
`architecture/agents/cursor-qa-scout.md`. Aucune recommandation ci-dessous
n'est un ordre : c'est une comparaison entre l'état du dépôt et l'état de
l'art, destinée à `cursor-auditor` puis à `claude-challenger`.

### Axe 1 — Sérialisation : file d'attente de fusion (merge queue) vs groupe `concurrency`

L'état de l'art distingue nettement deux mécanismes que ce commit confond
partiellement. Le **groupe `concurrency`** sérialise l'exécution d'un workflow ;
sa file par défaut ne retient qu'un seul run en attente et annule le précédent
(GitHub Docs, *Workflow syntax › `concurrency`*, consulté le 2026-08-12). La
**file d'attente de fusion** sérialise les fusions elles-mêmes dans une branche
protégée, en garantissant l'ordre premier-entré/premier-sorti et la validation
des vérifications requises sur l'état futur de la branche (GitHub Docs,
*Managing a merge queue*, consulté le 2026-08-12).

Comparaison avec le dépôt : `pipeline-orchestrate.yml` a besoin des deux
propriétés (un seul écrivain à la fois **et** aucune perte en file d'attente),
et n'obtient aujourd'hui que la première. Le paramètre `queue: max` documenté
par GitHub (jusqu'à 100 runs en attente) est l'écart le plus court entre le
livré et l'état de l'art ; une file de fusion adresserait en plus le refus de
poussée directe décrit en P0-1.

### Axe 2 — Boucles agentiques : le coupe-circuit doit être structurel, pas déclaratif

Les guides 2026 convergent : dans une boucle agentique, les garde-fous
(plafonds d'étapes, délais, plafonds de dépense, remontée à l'humain) doivent
être **imposés par la couche d'orchestration**, jamais laissés à l'agent
lui-même — implémentés par application, ils dérivent en un trimestre (TechTIQ,
*AI Orchestration: Enterprise Architecture Guide (2026)*, consulté le
2026-08-12). Le même point revient sous la forme « circuit breakers » sur le
nombre de tentatives et la profondeur d'appel d'outils (Zylos Research,
2026-06-30, consulté le 2026-08-12).

Comparaison avec le dépôt : le harnais applique déjà ce principe côté coût
(`--max-budget-usd 5`, `ci_budget_guard.py`, `harness/budget.py`, plafonds
100/130/160). Le constat P2-5 est le cas symétrique côté **qualité** : le garde
ajouté par ce commit vit *dans* l'appel de l'agent, et la couche
d'orchestration (le workflow) ne vérifie pas qu'il a été respecté avant
d'ouvrir la PR. C'est exactement le motif que ces sources déconseillent.

### Axe 3 — Plafonds de coût : attribuer, puis bloquer

L'état de l'art pour les agents en CI/CD recommande d'étiqueter chaque appel
(équipe, dépôt, pipeline, étape) et d'appliquer des seuils gradués — alerte,
mode dégradé, blocage dur (TrueFoundry, *The agentic token explosion in CI/CD*,
consulté le 2026-08-12) — et rappelle que le coût dominant vient des
re-tentatives et du contexte renvoyé, pas du prix au jeton (Cockroach Labs,
*Managing agentic AI costs at scale*, consulté le 2026-08-12).

Comparaison avec le dépôt : `ci_budget_guard.py` couvre le plafond mensuel et
le marquage post-hoc ; il n'y a pas d'étiquetage par audit permettant de
répondre à « combien a coûté la boucle sur l'audit X, re-tentatives comprises ».
Le sujet est réel mais **hors périmètre de ce commit** — il n'est donc pas
proposé en brief ici, et est simplement consigné pour un cycle ultérieur.

### Non-duplication avec les briefs ouverts

Briefs vérifiés un par un (`harness/queue/briefs/**/brief.md`) : 001
(clé primaire spatiale), 002 (littoral géo), 003 (portage Unity), 004 (polish
visuel), 005 (refonte carte), 006 (pipeline full-auto), 007 (cellules géo),
008-contexte-opus5, 008-full-auto-automation-gaps, 009-full-auto-agent-invocation,
010-repartition-roles-full-auto.

**Aucun doublon** : aucun de ces briefs ne traite du mode de persistance du
ledger sur une branche protégée, de la sémantique de la file d'attente d'un
groupe `concurrency`, ni de la grammaire du tableau de verdicts. Le brief 006
définit la *politique* de décision (les trois règles), pas l'analyse
syntaxique de la revue ; le brief 009 câble l'*invocation* du challenger, pas
la validation de son livrable avant publication.

Recoupement partiel signalé, non dupliqué : l'audit
`architecture/inbox/CURSOR-7e5244b-ledger-post-fusion-poussee-master.md`
(statut `PROPOSED`) avait déjà relevé sur cette même poussée « pas de groupe
`concurrency`, pas de `pull --rebase`, pas de nouvelle tentative, pas de repli
en pull request ». Le commit audité en traite les deux premiers volets. Le
présent audit ne les re-soulève donc pas : il documente ce qui reste (P3-7) et
surtout ce qui est **nouveau** — l'annulation en file d'attente (P0-2) et le
refus par la protection de branche (P0-1), apparu après 13:47 UTC, donc
postérieurement à cet audit-là.

## Sources externes

| # | Source | URL | Publication | Consultée le |
|---|---|---|---|---|
| 1 | GitHub Docs — *Workflow syntax for GitHub Actions*, section `concurrency` (`queue: single` / `queue: max`) | https://docs.github.com/en/actions/reference/workflows-and-actions/workflow-syntax#concurrency | non datée (doc vivante) | 2026-08-12 |
| 2 | GitHub Docs — *Control workflow concurrency* | https://docs.github.com/en/actions/how-tos/write-workflows/choose-when-workflows-run/control-workflow-concurrency | non datée (doc vivante) | 2026-08-12 |
| 3 | GitHub Docs — *Managing a merge queue* | https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/configuring-pull-request-merges/managing-a-merge-queue | non datée (doc vivante) | 2026-08-12 |
| 4 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* | https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/ | 2026-06-30 | 2026-08-12 |
| 5 | TechTIQ — *AI Orchestration: Enterprise Architecture Guide (2026)* | https://techtiq.com/blog/ai-orchestration/ | 2026 | 2026-08-12 |
| 6 | TrueFoundry — *The Agentic Token Explosion in CI/CD* | https://www.truefoundry.com/blog/the-agentic-token-explosion-in-ci-cd | non datée sur la page | 2026-08-12 |
| 7 | Cockroach Labs — *Managing Agentic AI Costs at Scale* | https://www.cockroachlabs.com/blog/agentic-ai-costs-at-scale/ | non datée sur la page | 2026-08-12 |

Les dates de publication non affichées sont signalées comme telles plutôt
qu'estimées : une date inventée serait pire qu'une date absente.

## Commandes rejouées (récapitulatif)

Toutes les sorties citées dans cet audit proviennent des commandes suivantes,
exécutées sur un checkout de `2a4f808f6be589d584ef7c2de75bb325e79ab4f4` :

```bash
git show --stat --format='%H%n%P' 2a4f808f6be589d584ef7c2de75bb325e79ab4f4
git diff bad1ffbe52996f4af4aaa805098254fe3abee6bc..2a4f808f6be589d584ef7c2de75bb325e79ab4f4
gh run list --commit 2a4f808f6be589d584ef7c2de75bb325e79ab4f4 --limit 30
gh run view 31608759556 --log-failed          # hermes-dashboard, GH006
gh run view 31608933878 --log-failed          # reprise manuelle, audit_id malformé
gh run list --workflow hermes-dashboard.yml --limit 14
gh run view 31603872453 --json conclusion,createdAt,updatedAt,jobs
gh api repos/PLiagre/ForgeHistory/branches/master/protection   # 403, consigné tel quel
git log --format='%h %an %ad' --date=iso -40
.venv/bin/python -m pytest harness/tests/ -q  # 314 passed, 16 skipped
.venv/bin/python  <sonde parse_point_verdicts, 6 cas>          # P1-3 / P1-4
```

## Budget de l'audit

30 appels d'outils environ pour l'auditeur et son compagnon de veille, sous le
plafond contractuel (≤ 60 pour `cursor-auditor`, ≤ 25 pour `cursor-qa-scout`).
Aucune scission en deux passes n'a été nécessaire.
