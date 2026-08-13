---
audit_id:                CURSOR-a7d1c57-pr76-approbation-sans-conversion
auditor:                 cursor-cloud
target_branch:           master
target_commit:           a7d1c571f0b910c3b7305c579cfb507e4cd78d7b
created_at:              2026-08-13T11:12:41Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la PR #76 — l'approbation produite par cette fusion ne mène nulle part

Critique de <https://github.com/PLiagre/ForgeHistory/pull/76> selon
`architecture/review-guidelines.md` (six lentilles, sévérités P0–P3, une
preuve citée par constat). Rôle : `architecture/agents/cursor-auditor.md`.

**Objet audité.** PR #76, « challenge: revue de l'audit
`CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion` ». Un seul fichier
ajouté, `+112/−0` :
`architecture/reviews/CLAUDE-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion.md`.
Auteur du commit : `forge-bot` ; auteur de la PR : `PLiagre` (ouverture
manuelle, voir P2-4).

**Note de fraîcheur.** `a7d1c57` est la tête de la PR ; elle a été *squashée*
dans `4313de5` sur `master`, donc `a7d1c57` n'est pas un ancêtre de
`master` mais porte exactement le même diff. Toutes les mesures ci-dessous
sont prises sur le contenu fusionné (`git show 4313de5:<chemin>`).

## 1. Résumé exécutif

Cette PR fait bien ce qu'elle annonce : elle dépose un contre-audit honnête,
dont les onze verdicts se rejouent. Sur la lentille « preuve d'exécution »,
c'est la meilleure des revues récentes : j'ai rejoué quatre de ses mesures et
les quatre tombent au caractère près (§ 5).

Le problème n'est pas dans le fichier, il est dans ce que la fusion en fait.
Vingt-quatre secondes après la fusion, la chaîne automatique a inscrit
`AUDIT_APPROVED` avec `retained_points: [1, …, 11]`, puis **s'est arrêtée
là** — et elle s'arrête là par construction, non par accident : aucun
chemin automatique n'émet l'événement `audit_approved` qui déclencherait la
conversion en brief (P1-1). Sept des treize approbations du registre sont
dans cet état. L'approbation produite par cette PR est la quatrième
approbation morte des cinq dernières minutes.

Deuxième défaut, moins visible : les onze « points retenus » sont les
**numéros de lignes du tableau de la revue**, alors que la décision et la
future graine de brief citent l'**audit**, qui numérote ses constats
`P0-1 … P3-1` et n'en a que neuf. Deux des onze points retenus ne
correspondent à aucun constat (P1-2). Et les trois seuls éléments
réellement actionnables de la revue — ses deux briefs proposés et une
question de priorité — vivent dans une section « NEEDS_OWNER » que la
machine ne lit pas : ils sont jetés, tandis que onze vérifications déjà
faites sont retenues (P1-3).

## 2. Lentille 1 — intention avant diff

Le corps de la PR annonce trois choses vérifiables.

| Affirmation du corps de PR | Vérification | Verdict |
|---|---|---|
| « Contenu : uniquement `architecture/reviews/CLAUDE-CURSOR-ab0e7f0-…md` » | `gh pr view 76 --json files` → un seul fichier, `ADDED`, `+112/−0` | exacte |
| « verdicts par point, lignes de tableau : 11 CONFIRMED » | `audit_decision.parse_point_verdicts` → 11 lignes, 11 `CONFIRMED` (§ 5.1) | exacte |
| « Après fusion, `pipeline-orchestrate` enregistre `AUDIT_CHALLENGED` puis applique la décision automatique (ADR-0006) » | registre : les deux lignes existent, horodatées `11:04:50Z` (§ 5.2) | exacte — **mais l'intention s'arrête exactement là où la chaîne s'arrête** (P1-1) |

C'est un progrès mesurable : sur la PR #62, la description annonçait
« 11 CONFIRMED, 1 PARTIAL » pour un tableau qui portait 13/4/1 (constat
P1-2 de l'audit `CURSOR-ab0e7f0`, confirmé par la revue que cette PR-ci
livre). Ici, la description et le fichier disent la même chose. Ce qui
subsiste, c'est le désaccord entre la description et le **registre**
(P2-1) — pas entre la description et le fichier.

La description est donc lisible et honnête. Sa limite est qu'elle décrit la
chaîne jusqu'à la décision automatique et s'y arrête, ce qui masque que
l'étape suivante (conversion) n'existe pas automatiquement.

## 3. Constats

### P1-1 — L'événement `audit_approved` n'est émis par aucun chemin automatique : l'approbation que cette PR produit est morte à la naissance

`harness/pipeline/auto_policy.yaml` porte la règle :

```yaml
  - id: approved_audit_convert
    event: audit_approved
    condition: always
    action: audit_convert_one_brief_per_retained_finding_if_split_needed_else_one_brief
```

`condition: always` — donc toute approbation doit être convertie. Et
`harness/pipeline/orchestrator.py:21` documente bien la transition
(`audit_approved -> audit_convert.convert  AUDIT_CONVERTED`), câblée en
`orchestrator.py:190` (`handle_audit_approved`) et
`orchestrator.py:279` (table de dispatch).

Mais rien n'émet cet événement. `.github/workflows/pipeline-orchestrate.yml`
n'a que deux entrées (lignes 25-40) : un `push` sur `master` limité au chemin
`architecture/reviews/*.md`, et un `workflow_dispatch` manuel. Et le
résolveur du chemin automatique ne sait produire qu'un seul événement —
`harness/pipeline/trigger_resolve.py:180` :

```python
        return ResolveOutcome(event="review_recorded", payload={"audit_id": audit_id}, notices=notices)
```

`review_recorded` produit `AUDIT_CHALLENGED` puis la décision automatique, et
c'est tout. Pour que `approved_audit_convert` se déclenche, il faut qu'un
humain lance un `workflow_dispatch` avec `--event audit_approved`.

Mesure sur le registre au SHA audité (§ 5.2) : **13 `AUDIT_APPROVED`, 6
`AUDIT_CONVERTED`**. Sept approbations n'ont jamais été converties, dont les
quatre produites entre 11:00:16Z et 11:04:50Z — la dernière étant
précisément celle que cette PR déclenche
(`CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion`). Le dernier
`workflow_dispatch` de `pipeline-orchestrate` date du **2026-08-12T15:48:54Z**
(§ 5.3) : depuis, dix-sept runs, tous `push`, donc tous `review_recorded`.

Ce que cela veut dire en termes de boucle : `architecture/README.md` écrit
qu'« un audit accepté redevient un **brief normal** ». En pratique, en
`mode: full_auto`, l'acceptation est le dernier état atteint sans main
humaine. La boucle produit des approbations, pas des briefs. C'est la
définition d'un état terminal silencieux : rien n'échoue, rien n'alerte, le
travail s'accumule dans un état que personne ne surveille [S1, S3].

**Sévérité P1** (à corriger avant de continuer à empiler des approbations) et
non P0 : bloquer la fusion de cette PR-ci ne répare rien, le défaut est en
aval d'elle. Motif **non trouvé** dans `architecture/inbox/**` : les deux
seules occurrences de `audit_approved` (`CURSOR-48a5659`, lignes 153 et 517)
citent `handle_audit_approved` pour un autre propos (le push par PAT), pas
l'absence de déclencheur.

### P1-2 — Les « points retenus » sont des numéros de lignes de la revue, mais la décision et la graine de brief citent l'audit, qui numérote autrement et n'a que neuf constats

La décision écrite automatiquement,
`architecture/decisions/DECISION-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion.md` :

```yaml
decision_of: CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion
decided_by: policy:auto
verdict: APPROVED
retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11]
```

Or les constats de l'audit visé ne s'appellent pas 1 à 11. Ils s'appellent
(`grep -nE "^### P" architecture/inbox/CURSOR-ab0e7f0-…md`) : `P0-1`, `P1-1`,
`P1-2`, `P2-1`, `P2-2`, `P2-3`, `P2-4`, `P2-5`, `P3-1` — **neuf** constats.
Les nombres 1 à 11 sont les numéros de lignes du tableau de la **revue**, un
fichier différent.

Et deux de ces onze lignes ne sont pas des constats du tout. Lignes 10 et 11
du tableau de la revue vérifient, respectivement, que les rejeux du § 5(e) de
l'audit fonctionnent et que sa déclaration de non-duplication tient. Ce sont
des contrôles de la revue sur l'audit, pas des défauts à corriger. Ils entrent
pourtant dans `retained_points` comme « points retenus par le propriétaire ».

La conséquence est en aval, dans `harness/audit_convert.py:98-118`
(`brief_seed_text`) : la graine de brief écrit

```
- Audit source : `architecture/inbox/{audit_id}.md`
- Décision du propriétaire : `architecture/decisions/DECISION-{audit_id}.md`
- Points retenus : {retained_str}
```

soit, ici, « Points retenus : 1, 2, …, 11 » adossés à un fichier qui ne
contient aucun point ainsi numéroté. La revue — seule source de cette
numérotation — **n'est pas citée** dans la provenance de la graine. Le
Planificateur qui recevra ce brief devra deviner que « point 7 » désigne la
ligne 7 du tableau d'un troisième fichier. C'est exactement la traçabilité
que `CLAUDE.md` › Single Source of Instruction exige d'un brief : ici la
source unique d'instruction naîtrait avec une référence non résoluble.

**Sévérité P1.** Motif **non trouvé** dans `architecture/inbox/**` (le seul
constat voisin, `CURSOR-9e35764` ligne 505, porte sur la numérotation
`§1`/`P1-1` acceptée par le gate de `audit_review.py`, pas sur la
non-correspondance entre `retained_points` et les constats de l'audit).

### P1-3 — La section « NEEDS_OWNER » de la revue est le seul endroit où vivent ses trois éléments actionnables, et c'est la seule partie que la machine ne lit pas

Le § 3 de la revue s'intitule « Points à porter au propriétaire
(NEEDS_OWNER) » et contient trois éléments : les deux briefs proposés par
l'audit, jugés techniquement fondés, et une question de priorité sur la file
de motifs non arbitrés. Aucune ligne du tableau ne porte le verdict
`NEEDS_OWNER` (§ 5.1 : onze lignes, onze `CONFIRMED`).

Ordre des règles dans `harness/audit_decision.py` :

```
283:    if retained:
                (→ APPROVED, règle review_has_confirmed_or_partial)
292:    if has_needs_owner:
                (→ REJECTED, règle review_needs_owner_only)
```

`retained` est non vide (onze lignes `CONFIRMED`), donc le `return` de la
ligne 283 est pris et la ligne 292 n'est jamais atteinte. Résultat
mécanique, vérifié dans le registre et la décision : `retained_points` = les
onze lignes du tableau ; les trois éléments du § 3 n'apparaissent nulle
part.

L'inversion est complète : **ce qui est retenu comme « décidé par le
propriétaire », ce sont onze vérifications déjà faites et qui ne demandent
aucune action ; ce qui est jeté, ce sont les trois seuls éléments qui
demandaient une décision.**

Cadrage adverse (lentille 4) : je ne reproche pas à la revue d'ignorer ce
défaut — elle le confirme explicitement, c'est sa ligne 8. Je lui reproche
d'avoir, en connaissant le défaut, placé ses seuls éléments actionnables
dans le canal qu'elle venait de démontrer inopérant, sans le signaler. C'est
la forme documentée de la « correction hallucinée » : une section qui a
l'apparence d'une escalade et n'en produit aucune [S4, lentille 6].

**Sévérité P1**, et **aucun brief proposé** : l'ordre des règles est déjà le
constat `P2-5` de l'audit `CURSOR-ab0e7f0`, retenu comme point 8 de la
décision du 11:04:50Z. Le re-proposer serait du bruit
(`review-guidelines.md` › « pas de rubber-stamping inverse »). Ce que
j'ajoute est l'élément neuf : le canal mort a été utilisé sciemment, et
l'inversion retenu/jeté est mesurable.

### P2-1 — Le registre inscrit 27 / 11 / 15 / 7 pour un tableau de onze lignes, dont onze `REFUTED` que la revue ne prononce jamais

Rejeu (§ 5.1) :

```
parse_verdicts (-> registre) : {'CONFIRMED': 27, 'REFUTED': 11, 'PARTIAL': 15, 'NEEDS_OWNER': 7}
parse_point_verdicts        : 11 {(1,'CONFIRMED'):1, …, (11,'CONFIRMED'):1}
```

Ligne réellement écrite dans `architecture/audit-ledger.jsonl` :

```json
{"timestamp": "2026-08-13T11:04:50Z", "audit_id": "CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion", "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion.md", "verdicts": {"CONFIRMED": 27, "REFUTED": 11, "PARTIAL": 15, "NEEDS_OWNER": 7}}
```

Soixante « verdicts » pour onze lignes, et onze `REFUTED` pour une revue
dont le § 4 dit « Une seule chose tombe, et c'est mineur » et dont aucune
ligne n'est `REFUTED`. La cause est connue : `parse_verdicts` compte les
occurrences des quatre mots dans **tout** le texte
(`harness/audit_review.py:127-134`), et cette revue cite abondamment les
verdicts de l'audit qu'elle relit.

Progression de la distorsion sur les quatre dernières revues (§ 5.2) :
16/2/6/2, 16/4/8/5, 16/5/10/4, puis 27/11/15/7. C'est la plus forte à ce
jour, et la première où les `REFUTED` fantômes deviennent le double du
nombre réel de verdicts distincts.

**Sévérité P2**, **aucun brief proposé** : motif déjà posé, avec preuve, par
`CURSOR-779d97c` constat `P1-3` (« Le champ `verdicts` du ledger n'est pas un
décompte de verdicts, et cette PR y inscrit `REFUTED: 2` sur une revue qui
conclut “Aucun REFUTED” »), non arbitré. Je n'apporte que la magnitude.

### P2-2 — Le job `schema` est vert sans avoir lu le seul fichier de la PR

`harness/audit_schema.py` ne connaît qu'un dossier :

```
26:INBOX = REPO_ROOT / "architecture" / "inbox"
92:def validate_inbox(inbox: Path = INBOX) -> dict[str, list[str]]:
98:    for path in sorted(inbox.glob("CURSOR-*.md")):
```

et `.github/workflows/audit-guard.yml` n'a que ce seul contrôle de schéma
(`run: python harness/audit_schema.py`). Le fichier livré par la PR #76 est
dans `architecture/reviews/`. Le job `schema` est donc passé au vert
(`31693857157`, `success`, 10 s) **sans ouvrir le seul fichier que la PR
ajoute**. La porte mécanique est là, elle est verte, et elle ne couvre rien
de ce qui est en revue — le pire cas pour la lentille 3, parce que le vert
donne l'impression inverse.

**Sévérité P2**, **aucun brief proposé** : déjà posé par `CURSOR-779d97c`
`P2-6` et par `CURSOR-ab0e7f0` `P2-3`, ce dernier retenu comme point 6 de la
décision du 11:04:50Z.

### P2-3 — Fusionnée 36 secondes après son ouverture ; le job « auditeur » qui devait peser n'a fait que dépêcher

Chronologie reconstituée (§ 5.4) :

| Horodatage UTC | Événement | Source |
|---|---|---|
| 11:03:50 | PR #76 ouverte | `GET /pulls/76` → `created_at` |
| 11:03:56 | `invoke-cursor-auditor` démarre (run 31693857174) | `GET /actions/runs/…/jobs` |
| 11:03:56 | `check-and-automerge` démarre (run 31693857191) | idem |
| 11:04:10 | auto-merge (squash) armé par `PLiagre` | `GET /issues/76/timeline` → `auto_squash_enabled` |
| 11:04:13 | `check-and-automerge` → `success` | jobs |
| 11:04:17 | `invoke-cursor-auditor` → `success` (21 s) | jobs |
| 11:04:26 | **fusionnée** (`4313de5`) | `GET /pulls/76` → `merged_at` |
| 11:04:50 | `AUDIT_CHALLENGED` + `AUDIT_APPROVED` au registre | ledger |

Vingt et une secondes de job auditeur : c'est la durée d'un *dispatch*, pas
d'une critique. Le `success` de `invoke-cursor-auditor` atteste que l'agent a
été lancé, pas qu'un audit existe. Cet audit-ci est écrit après la fusion —
ADR-0010 fait de Cursor le maillon *critique* de chaque PR, et le maillon
critique n'a pas pu peser.

**Sévérité P2**, **aucun brief proposé** : motif déjà posé par
`CURSOR-ab0e7f0` `P0-1` (retenu comme point 1 de la décision du 11:04:50Z) et
`CURSOR-063d7eb` `P2-6`. L'élément neuf est seulement que le motif se
reproduit sur la PR qui *transporte* sa propre confirmation.

### P2-4 — Le run qui a produit la revue est vert alors qu'il n'a pas ouvert sa PR ; 2 h 05 de latence manuelle

`GET /actions/runs/31684016021` (`pipeline-challenge`, créé 08:52:35Z) :
`conclusion: success`, ses deux jobs `invoke-claude-challenger` et
`mechanical-scaffold-smoke` en `success`. Le corps de PR reconnaît lui-même
que ce run « n'a pas pu ouvrir la PR » — donc un run vert dont le livrable
final n'existe pas. Le blocage est réel et connu (`HANDOFF.md` lignes 23, 93,
290, 438 : réglage GitHub « Allow GitHub Actions to create and approve pull
requests » inactif), mais il est signalé sans faire rougir le run.

Coût en latence, mesuré : commit de la revue 08:58:57Z → ouverture de la PR
11:03:50Z = **2 h 04 min 53 s**, comblées à la main. La littérature est
explicite : ne jamais se fier au succès auto-déclaré d'un agent, vérifier
l'existence de l'artefact attendu à sa destination [S4]; et faire porter la
détection par un niveau au-dessus de l'agent, pas par l'agent lui-même [S3].

**Sévérité P2**, **aucun brief proposé** : déjà posé par `CURSOR-16ff5ac`
(« l'étape qui devait ouvrir sa PR échoue en `::warning::` au lieu d'échouer
tout court »), approuvé le 11:00:16Z et non converti — il est lui-même l'une
des sept approbations mortes du constat P1-1.

### P3-1 — `reviewed_at` est cette fois antérieur au commit : progrès réel, toujours non contraint

Frontmatter de la revue : `reviewed_at: 2026-08-13T08:57:10Z` ; date du
commit `a7d1c57` : `2026-08-13T08:58:57Z`. La relecture est donc horodatée
**1 min 47 s avant** le fichier qui la porte — plausible et cohérent. À
comparer aux 42 min *après* de la revue précédente (constat `P2-3` de
`CURSOR-ab0e7f0`). Le champ reste écrit à la main et non contraint (P2-2),
mais ici il est juste. À noter comme progrès, pas comme défaut.

### P3-2 — `reviewer: claude-code` : trois noms pour un seul acteur, et ce n'est pas le fait de cette PR

Le frontmatter annonce `reviewer: claude-code`, le corps de PR dit
`claude-challenger`, le registre écrit `"actor": "claude"`. La valeur du
frontmatter n'est **pas** un choix de cette PR : elle est codée en dur dans
le gabarit, `harness/audit_review.py:68` (`reviewer: claude-code`). Je le
signale pour que le constat ne soit pas mal attribué à l'agent : c'est le
scaffold qu'il faudrait aligner sur `architecture/agents/claude-challenger.md`,
si quelqu'un décide que l'identité de l'acteur doit être traçable dans les
trois surfaces. Information, aucune action réclamée.

### P3-3 — Taille et découpage : rien à redire

Un fichier, `+112/−0`. Très en dessous des seuils de la lentille 5 (~5
fichiers, quelques centaines de lignes). Aucun découpage à recommander, et
la revue est lisible d'un bout à l'autre. C'est le cas facile, et il est
tenu.

### P3-4 — Le plafond budgétaire de l'invocation s'appuie sur un compteur vide

Le run de la revue a été lancé avec `--max-budget-usd 5.00` et le garde
budgétaire a rapporté `{"month_total_usd": 0.0, "status": "PROCEED"}`
(journal du run 31684016021, 08:52:53Z). Or
`harness/pipeline/ci_budget_guard.py:39` lit
`harness/pipeline/ci-budget-ledger.jsonl`, qui pèse **1 octet** (une ligne
vide) depuis le commit `cd89141`. Le cumul mensuel est donc structurellement
nul : le plafond ne peut jamais se déclencher.

C'est le point où la littérature 2026 est la plus insistante : la métrique
qui compte est le **coût par résultat abouti**, pas le prix au million de
jetons, et un plafond adossé à un compteur qui n'est jamais alimenté ne
protège rien [S5, S6]. Le rapprochement avec P1-1 est direct : sept
approbations payées et jamais converties sont exactement du coût sans
résultat abouti.

**Sévérité P3**, **aucun brief proposé** : le motif « le coût mesuré est
jeté » est déjà posé par `CURSOR-779d97c` `P3-7` et cité dans treize autres
audits de l'inbox.

## 4. Lentille 2 — preuve d'exécution : ce que la revue avance tient

Sur les onze verdicts de la revue, j'ai rejoué quatre des mesures citées, par
un chemin indépendant. Les quatre tombent.

| Mesure citée par la revue | Rejeu | Résultat |
|---|---|---|
| `pytest harness/tests/` → « 314 passed, 16 skipped » | `.venv/bin/python -m pytest harness/tests/ -q` | `314 passed, 16 skipped in 17.27s` — **identique** |
| `orchestrator.py:146` | `sed -n '146p' harness/pipeline/orchestrator.py` | verbatim identique |
| `dashboard.py:235` | `sed -n '235p' hermes/dashboard.py` | `if audit["event"] in ("AUDIT_APPROVED",):` — exact |
| registre de `a600532` = 16/2/6/2 | `grep a600532 architecture/audit-ledger.jsonl` | `{"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}` — exact |
| `audit_schema.py` → « All 30 audit(s) valid » | `.venv/bin/python harness/audit_schema.py` | « All 33 audit(s) valid » — dérive attendue (3 audits déposés depuis), pas une divergence de méthode |

La revue déclare aussi explicitement ce qu'elle **n'a pas** rejoué (« le run
GitHub réel de `sim-tests` sur la PR #62 … hors de portée sans
authentification »). Une limite déclarée vaut mieux qu'une affirmation non
mesurée : c'est la discipline attendue [S1 de `review-guidelines.md`], et
elle est tenue ici. Sur cette lentille, la PR est bonne.

## 5. Commandes rejouées et sorties

### 5.1 Les deux compteurs de verdicts sur le fichier livré

```
$ git show 4313de5:architecture/reviews/CLAUDE-CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion.md > /tmp/review76.md
$ wc -l /tmp/review76.md
112 /tmp/review76.md
$ .venv/bin/python -c "... import audit_review, audit_decision ..."
parse_verdicts (-> registre) : {'CONFIRMED': 27, 'REFUTED': 11, 'PARTIAL': 15, 'NEEDS_OWNER': 7}
parse_point_verdicts        : 11 {(1, 'CONFIRMED'): 1, (2, 'CONFIRMED'): 1, (3, 'CONFIRMED'): 1, (4, 'CONFIRMED'): 1, (5, 'CONFIRMED'): 1, (6, 'CONFIRMED'): 1, (7, 'CONFIRMED'): 1, (8, 'CONFIRMED'): 1, (9, 'CONFIRMED'): 1, (10, 'CONFIRMED'): 1, (11, 'CONFIRMED'): 1}
```

### 5.2 Inventaire des approbations et des conversions du registre

```
$ git show origin/master:architecture/audit-ledger.jsonl > /tmp/ledger.jsonl
$ .venv/bin/python -c "... Counter(e['event'] for e in ev) ..."
total événements: 55
Counter({'AUDIT_CHALLENGED': 16, 'AUDIT_APPROVED': 13, 'AUDIT_ARCHIVED': 8,
         'AUDIT_CONVERTED': 6, 'AUDIT_IMPLEMENTED': 4, 'AUDIT_VERIFIED': 4,
         'AUDIT_STALE': 4})

AUDIT_APPROVED: 13 | AUDIT_CONVERTED: 6
  2026-08-05T21:28:53Z  CURSOR-FIXTURE-full-auto-demo                       converti=True   pts=[1]
  2026-08-08T20:32:21Z  CURSOR-5633ee7-automation-completeness              converti=True   pts=[1, 2, 3, 4]
  2026-08-11T06:53:23Z  CURSOR-e9a6f4c-codex-passation-full-auto            converti=True   pts=[3, 6, 7, 8, 10, 11, 12, 15, 16]
  2026-08-12T11:41:27Z  CURSOR-cdc683f-hermes-workflow-quatre-acteurs       converti=False  pts=[1, 2, 5, 8, 10, 11]
  2026-08-12T15:32:27Z  CURSOR-e849633-hermes-demande-pilotage              converti=False  pts=[1..10]
  2026-08-12T15:49:09Z  CURSOR-0269d8e-hermes-console-droit-executer        converti=False  pts=[1..12]
  2026-08-13T06:25:04Z  CURSOR-3b47ffe-pr57-monde-sans-faim                 converti=True   pts=[1..12]
  2026-08-13T08:35:12Z  CURSOR-a600532-fusion-sans-contre-audit             converti=True   pts=[1..16, 18]
  2026-08-13T08:40:11Z  CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois    converti=True   pts=[1..10]
  2026-08-13T11:00:16Z  CURSOR-16ff5ac-contre-audit-perdu-a-la-publication  converti=False  pts=[1..7]
  2026-08-13T11:01:51Z  CURSOR-4c45718-pr65-ledger-recupere-a-la-main       converti=False  pts=[1..10]
  2026-08-13T11:03:12Z  CURSOR-9e35764-pr63-contre-audit-jamais-enregistre  converti=False  pts=[1..10]
  2026-08-13T11:04:50Z  CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion    converti=False  pts=[1..11]
```

Les quatre dernières lignes sont les quatre approbations de la fenêtre
11:00–11:05 ; aucune n'est convertie. La dernière est le produit de cette PR.

### 5.3 Aucun `workflow_dispatch` depuis la veille

```
$ gh api "repos/PLiagre/ForgeHistory/actions/workflows/pipeline-orchestrate.yml/runs?per_page=8"
2026-08-13T11:04:29Z push            completed/success 31693902912
2026-08-13T11:02:54Z push            completed/success 31693786429
2026-08-13T11:01:39Z push            completed/success 31693694291
2026-08-13T11:00:04Z push            completed/success 31693570402
2026-08-13T08:35:11Z push            completed/failure 31682710982
2026-08-13T08:35:01Z push            completed/success 31682696284
2026-08-13T08:28:19Z push            completed/failure 31682196140
2026-08-12T15:48:54Z workflow_dispatch completed/success 31614340200
```

Tous les runs `push` passent par `resolve_push`, qui ne sait émettre que
`review_recorded` (`trigger_resolve.py:180`). Le seul chemin capable
d'émettre `audit_approved` est le `workflow_dispatch`, dont la dernière
occurrence date du 2026-08-12.

### 5.4 Chronologie de la PR #76

```
$ gh api repos/PLiagre/ForgeHistory/pulls/76 --jq '{created_at,merged_at,merge_commit_sha,merged_by:.merged_by.login,head:.head.sha}'
{"created_at":"2026-08-13T11:03:50Z","merged_at":"2026-08-13T11:04:26Z",
 "merge_commit_sha":"4313de56feeccd925ced6d94562e778726d6d9d5",
 "merged_by":"PLiagre","head":"a7d1c571f0b910c3b7305c579cfb507e4cd78d7b"}

$ gh api repos/PLiagre/ForgeHistory/issues/76/timeline
2026-08-13T11:04:10Z auto_squash_enabled PLiagre
2026-08-13T11:04:26Z merged             PLiagre

$ gh api repos/PLiagre/ForgeHistory/actions/runs/31693857174/jobs
invoke-cursor-auditor 2026-08-13T11:03:56Z -> 2026-08-13T11:04:17Z success
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31693857191/jobs
check-and-automerge   2026-08-13T11:03:56Z -> 2026-08-13T11:04:13Z success
```

### 5.5 Portée de la PR et de ses gardes

```
$ gh pr view 76 --json files,additions,deletions,changedFiles
1 fichier : architecture/reviews/CLAUDE-CURSOR-ab0e7f0-…md  (+112/−0, ADDED)

$ grep -n "offending=" .github/workflows/merge-bot.yml
50: offending="$(printf '%s\n' "$changed" | grep -vE '^(architecture/inbox/|architecture/reviews/|harness/queue/briefs/.*/feedback/)' || true)"

$ sed -n '30p' .github/workflows/audit-guard.yml
    if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')
```

La portée de la PR est bien dans l'allowlist du merge-bot. Le garde de
portée par rôle (`cursor-scope`) ne s'applique qu'aux branches `cursor/*` et
est donc resté `skipping` ici — asymétrie déjà posée par `CURSOR-4c45718`
(P1) et `CURSOR-779d97c`, non arbitrée ; je ne la réémets pas.

## 6. Classification de la CI du commit audité

Runs sur `a7d1c571f0b910c3b7305c579cfb507e4cd78d7b` :

| Workflow | Événement | État | Run |
|---|---|---|---|
| `harness-ci` | pull_request | success | 31693857156 |
| `audit-guard` | pull_request | success (job `cursor-scope` : `skipping`) | 31693857157 |
| `security` | pull_request | success | 31693857126 |
| `merge-bot` | pull_request | success | 31693857191 |
| `pipeline-audit` | pull_request | success | 31693857174 |
| `hermes-observer` | pull_request_target | **cancelled** | 31693857203 |
| `hermes-observer` | pull_request_target | **queued** (encore en file à 11:12:41Z, soit 8 min après la fusion) | 31693903133 |
| `harness-ci` / `audit-guard` / `security` | push | success | 31684503194 / 31684503192 / 31684503261 |

**Classification : verte sur toutes les portes bloquantes** (tests, schéma,
scan de secrets, allowlist de fusion). Deux réserves, aucune bloquante :
`hermes-observer` a un run annulé et un run toujours en file huit minutes
après la fusion — donc la PR a fusionné sans que cette voie ait rendu de
verdict ; et le vert du job `schema` ne dit rien du fichier livré (P2-2).

## 7. Risques par sévérité

| Sévérité | Risque | Constat |
|---|---|---|
| **P1** | La boucle produit des approbations qui ne deviennent jamais des briefs ; 7 des 13 approbations du registre sont mortes, dont 4 minées dans les 5 dernières minutes. Le travail d'audit est payé, approuvé, puis perdu. | P1-1 |
| **P1** | La future graine de brief citera des « points retenus » numérotés selon un fichier qu'elle ne cite pas, et dont 2 des 11 ne sont pas des constats. La source unique d'instruction naîtrait non résoluble. | P1-2 |
| **P1** | Les seuls éléments actionnables d'un contre-audit passent par un canal (« NEEDS_OWNER » en prose) que la décision automatique ne lit pas ; ce qui est retenu, ce sont les vérifications déjà faites. | P1-3 |
| **P2** | Le champ `verdicts` du registre est faux et sa distorsion croît (27/11/15/7 pour 11 lignes) ; toute lecture ultérieure du registre — tableau de bord compris — s'appuie sur un décompte inventé. | P2-1 |
| **P2** | Une porte mécanique verte (`schema`) ne couvre pas le fichier en revue : le vert induit en erreur. | P2-2 |
| **P2** | Le maillon critique d'ADR-0010 rend son avis après la fusion (36 s d'ouverture à fusion ; 21 s de job = un dispatch). | P2-3 |
| **P2** | Un run vert dont le livrable n'existe pas, comblé par 2 h 05 de travail manuel non tracé dans la CI. | P2-4 |
| **P3** | `reviewed_at` juste mais non contraint ; identité de l'acteur écrite sous trois noms ; plafond budgétaire adossé à un compteur vide. | P3-1, P3-2, P3-4 |

## 8. Briefs atomiques proposés (2 — sous le plafond de 3)

Ces deux propositions sont des **entrées**, pas des instructions. Aucun
travail n'est autorisé par ce fichier ; la décision appartient à la boucle
(`architecture/README.md`, ADR-0005/0006).

1. **Fermer la boucle après l'approbation.** Rendre `audit_approved`
   atteignable sans main humaine, ou rendre l'arrêt visible. Deux formes
   possibles, à trancher par la boucle : soit le chemin automatique
   enchaîne la conversion après avoir écrit `AUDIT_APPROVED`, soit une
   vérification périodique compare le nombre d'`AUDIT_APPROVED` au nombre
   d'`AUDIT_CONVERTED` et échoue quand l'écart persiste — la littérature
   appelle cela vérifier l'artefact à destination plutôt que le succès
   auto-déclaré de l'étape [S1, S4]. Périmètre probable :
   `harness/pipeline/trigger_resolve.py`,
   `.github/workflows/pipeline-orchestrate.yml`. Compteur naturel : nombre
   d'approbations non converties de plus de N minutes (attendu : 0 ;
   mesuré aujourd'hui : 7). Note de périmètre : toucher
   `.github/workflows/**` place ce brief hors de l'allowlist du merge-bot —
   arbitrage propriétaire requis par construction.

2. **Rendre `retained_points` traçable.** Faire porter à la ligne
   `AUDIT_APPROVED` et à la décision l'origine des numéros (quel fichier,
   quelle numérotation), et faire citer la revue dans la provenance de la
   graine de brief. Périmètre probable :
   `harness/audit_decision.py`, `harness/audit_convert.py`. Compteur
   naturel : pour chaque point retenu, existe-t-il dans le fichier cité un
   constat portant ce numéro (attendu : oui pour tous ; mesuré sur
   `ab0e7f0` : 0 sur 11).

Je ne propose **rien** sur les motifs P2-1 à P2-4 et P3-4 : chacun est déjà
posé avec preuve par un audit de l'inbox et n'est pas arbitré (`CURSOR-779d97c`
P1-3 et P2-6, `CURSOR-ab0e7f0` P0-1 / P2-3 / P2-5, `CURSOR-16ff5ac`,
`CURSOR-4c45718`, `CURSOR-063d7eb` P1-2 et P2-6). Les réémettre serait du
bruit, pas de la critique.

## 9. Déclaration de non-duplication

Vérifications faites avant émission (sorties en § 5) :

- `grep -rn "audit_approved" architecture/inbox/*.md` → 2 occurrences, toutes
  deux dans `CURSOR-48a5659` (lignes 153 et 517), citant
  `handle_audit_approved` à propos du push par PAT. **Le motif P1-1
  (événement jamais émis) est neuf.**
- `grep -rn "numérotation|indices du tableau" architecture/inbox/*.md` → une
  seule occurrence proche, `CURSOR-9e35764:505`, sur la numérotation acceptée
  par le gate de `audit_review.py`. **Le motif P1-2 est neuf.**
- `grep -rln "parse_verdicts" architecture/inbox/` → 10 fichiers ; motif P2-1
  saturé, **non réémis**.
- `grep -rln "ci_budget_guard|month_total_usd" architecture/inbox/*.md` → 14
  fichiers ; motif P3-4 saturé, **non réémis**.
- `ls architecture/decisions/ | wc -l` → 12 décisions, toutes `policy:auto`.
  Aucune décision enregistrée n'écarte l'un des motifs ci-dessus : je ne
  contredis donc aucune décision existante (`review-guidelines.md` › pas de
  rubber-stamping inverse).

## 10. Sources externes

| # | source | consulté le |
|---|---|---|
| S1 | Ultrathink Field Notes — *Your Agent Tasks Are Failing Silently — Here's How We Catch Them* (détection par absence de signal ; alerter sur ce qui n'arrive pas) — <https://ultrathink.art/blog/agent-tasks-failing-silently> | 2026-08-13 |
| S2 | Mindra — *Always-On Intelligence: Building Event-Driven AI Agent Pipelines with Triggers, Schedules, and Queues* (files de rebut, idempotence, événements silencieusement non traités) — <https://mindra.co/blog/event-driven-ai-agent-orchestration-triggers-schedules-queues> | 2026-08-13 |
| S3 | Auto-Claude, issue #509 — *Task execution never starts after approval — state machine stuck in `human_review`* (cas documenté d'un état d'approbation sans transition sortante ; même forme que P1-1) — <https://github.com/AndyMik90/Auto-Claude/issues/509> | 2026-08-13 |
| S4 | zemna.net — *Your AI Agent Pipeline Has No Zombie Detection — Here's How to Add It* (« ne jamais faire confiance au succès auto-déclaré : vérifier que l'artefact existe, est récent et structurellement valide ») — <https://zemna.net/blog/your-ai-agent-pipeline-has-no-zombie-detection-heres-how-to-add-it/> | 2026-08-13 |
| S5 | MightyBot — *The Token Economics of AI Agents in 2026: What a Decision Actually Costs* (étude mesurée de juillet 2026 : 1,34 $ la passe unique, 4,64 $ l'agent à contexte croissant, 10–15 $ les configurations à vote) — <https://mightybot.ai/blog/token-economics-of-ai-agents-2026/> | 2026-08-13 |
| S6 | Zylos Research — *Token Budget Management and Cost Control for Autonomous AI Agents* (2026-06-30 ; « coût par résultat abouti » comme métrique, budgets par tâche avec seuils d'escalade) — <https://zylos.ai/research/2026-06-30-token-budget-management-cost-control-autonomous-agents/> | 2026-08-13 |

## 11. Ce que cet audit ne fait pas

Il ne décide rien et n'autorise rien. Les trois flags `*_authorized` du
frontmatter sont à `false`. Il ne touche aucun fichier hors
`architecture/inbox/**`. Il ne modifie ni ne supprime aucun audit existant.
Les deux briefs du § 8 sont des propositions à arbitrer par la boucle ; tant
qu'un brief n'existe pas sous `harness/queue/briefs/`, rien de ce document
n'est une instruction (`CLAUDE.md` › Single Source of Instruction).
