---
audit_id:                CURSOR-16ff5ac-contre-audit-perdu-a-la-publication
auditor:                 cursor-cloud
target_branch:           master
target_commit:           16ff5ac77e618551b033b3bccda88ba83523c423
created_at:              2026-08-13T08:55:00Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit post-fusion du commit `16ff5ac` — le contre-audit existait, personne ne l'a vu

Audit de la fusion de la pull request
[#60](https://github.com/PLiagre/ForgeHistory/pull/60) sur `master`
(« Brief 012 : le monde vivant vit »), parents `9eef958` (master) et
`a4de4bb` (branche de lot), 30 fichiers, +3258 / −220.

Cet audit **ne décide rien** : il propose. La décision reste au propriétaire
et à la boucle (`architecture/README.md`, ADR-0005/0006). Les trois drapeaux
d'autorisation du frontmatter sont à `false`, comme l'exige le contrat
`architecture/agents/cursor-auditor.md`. Aucun point ci-dessous n'est une
instruction : la source unique d'instruction reste le brief
(`CLAUDE.md` › Single Source of Instruction).

Cette PR avait déjà été critiquée **avant** fusion par l'audit
`CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois` (10 constats, dont un P0).
Le présent audit ne re-instruit aucun de ces dix constats. Il porte sur ce
que la fusion elle-même a produit, et sur un fait que l'audit pré-fusion ne
pouvait pas connaître : **le contre-audit de ses constats a bien été produit,
il les a tous confirmés, et il n'a jamais atteint `master`.**

Toutes les mesures ci-dessous ont été rejouées par l'auditeur dans un arbre
de travail séparé positionné sur `16ff5ac`
(`git worktree add /tmp/audit16 16ff5ac`), sans aucune écriture dans le
dépôt. Les sondes sont publiées en clair au § 7.

## 0. Synthèse

| # | Sévérité | Constat en une phrase |
|---|---|---|
| 1 | **P0** | Le contre-audit de la PR #60 **existe** : produit à 08:20:30 UTC, il confirme (`CONFIRMED`) les neuf constats techniques de l'audit, P0 compris. Il dort sur une branche `forge-bot/*` jamais fusionnée, parce que l'étape qui devait ouvrir sa PR échoue en `::warning::` au lieu d'échouer tout court. La fusion a eu lieu **8 minutes plus tard**, sans aucune relecture GitHub. |
| 2 | **P1** | La CI de `master` est **rouge** sur le SHA fusionné : `pipeline-orchestrate` rejoue une transition déjà enregistrée et sort en erreur. La garde écrite pour empêcher exactement ce rejeu teste le mauvais prédicat (« état terminal ? ») au lieu de celui qu'exige l'orchestrateur (« état `AUDIT_CHALLENGED` ? »). |
| 3 | **P1** | Le défaut sémantique confirmé est maintenant dans le tronc, et son coût est **mesuré pour la première fois** : +39 442 morts (+0,52 %), et le nombre de cellules en faim au dernier tick passe de 9 à 30 (×3,3). Surtout : les trois ordres de tick défendables donnent **trois mondes différents**, et aucun document du dépôt ne tranche lequel est le bon. |
| 4 | **P2** | Deux contre-audits (`a600532`, `a4de4bb`) sont bloqués sur des branches poussées mais sans PR, et 13 audits sur 27 n'ont **aucune ligne** au registre. L'arriéré de la boucle n'est visible nulle part au moment de décider d'une fusion. |
| 5 | **P3** | Classification CI du commit audité : 6 workflows déclenchés, 5 verts, 1 rouge (`pipeline-orchestrate`, constat 2). La fusion elle-même est propre : elle n'a rien introduit qui ne vienne de ses deux parents. |

Ce qui tient et mérite d'être dit avant les critiques : § 4.

## 1. Le fait central, en trois horodatages

| Heure (UTC) | Événement | Preuve |
|---|---|---|
| 08:10:02 | L'audit pré-fusion de la PR #60 est daté (1 constat P0, 4 P1). | `created_at` de `architecture/inbox/CURSOR-a4de4bb-...md` |
| 08:17:02 | Cet audit arrive sur `master` (commit `0120b8c`, PR #61) ; `pipeline-challenge` démarre trois secondes après. | `git log`, run [31681378615](https://github.com/PLiagre/ForgeHistory/actions/runs/31681378615) |
| **08:20:30** | **Le contre-audit est écrit** : 124 lignes, `reviewer: claude-code`, verdict `CONFIRMED` sur les 9 points techniques. | `reviewed_at` du fichier, § 7.3 |
| 08:22:39 | L'étape de publication pousse la branche, puis `gh pr create` est refusé. Le workflow émet un `::warning::` et **se termine en succès**. | log du run, § 7.4 |
| 08:28:17 | La PR #60 est fusionnée. `reviewDecision` vide, **0 relecture**. | `gh pr view 60`, § 7.5 |

Huit minutes séparent un contre-audit qui confirme un P0 de la fusion de ce
P0 dans le tronc. Le maillon de vérification n'était pas en panne : il a
fonctionné, il a produit le bon résultat, et ce résultat a été perdu à la
dernière étape — celle qui devait le rendre visible.

## 2. Constats

### Constat 1 — P0 — Le contre-audit est produit, puis perdu en silence

La branche existe et son contenu est vérifiable :

```
$ git ls-remote --heads origin 'refs/heads/forge-bot/review-CURSOR-a4de4bb*'
25b31852...  refs/heads/forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615
```

Elle porte un seul commit, un seul fichier, 124 lignes :
`architecture/reviews/CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois.md`.
Ce n'est pas un gabarit vide. Son tableau de verdicts porte `CONFIRMED` sur
les neuf points techniques, chacun avec une sonde réécrite indépendamment.
Sur le P0, il écrit (§ 7.3 pour la citation complète) :

> « Sévérité P0 justifiée : les trois compteurs vedettes de SC5 […] sont
> mesurés sur ce mécanisme, et j'ai reproduit ces trois chiffres exacts
> moi-même en rejouant la simulation complète. »

La cause de la perte est une seule ligne de
`.github/workflows/pipeline-challenge.yml`, dernière commande de l'étape
« Publish the review as a pull request » :

```yaml
gh pr create \
  --head "$branch" ... \
  || echo "::warning::gh pr create refused (repository setting or permissions) -- branch $branch is pushed; open the PR manually."
```

Le `|| echo` est le défaut. En bash, la dernière commande exécutée détermine
le code de sortie de l'étape : `echo` réussit toujours, donc l'étape réussit,
donc le job réussit, donc le workflow est vert. Le repli est écrit comme un
avertissement adressé à un humain (« open the PR manually ») dans un système
dont ADR-0006 dit qu'il tourne sans humain dans la boucle. Personne n'était
là pour lire l'avertissement, et rien dans la vue du propriétaire
(`hermes/DASHBOARD.md`) ne signale une revue en souffrance.

Distinction avec l'audit `CURSOR-a600532-fusion-sans-contre-audit`, qui
portait sur la fusion précédente et **n'a jamais été contre-audité ni
décidé** (§ constat 4) : là, le maillon était en panne franche (onze échecs
HTTP 429 consécutifs, plafond de dépense atteint), et la conclusion était
« la panne était visible pour une machine, invisible pour la personne qui
devait décider ». Ici, c'est l'inverse et c'est pire : **rien n'était en
panne**. Le crédit était disponible (`CLAUDE_CODE_OAUTH_TOKEN` présent,
précontrôle de budget `{"month_total_usd": 0.0, "status": "PROCEED"}`),
l'invocation a duré cinq minutes, le travail a été fait correctement — et il
a été jeté par un `|| echo`. Un système qui échoue bruyamment finit par être
réparé ; un système qui réussit à vide ne le sera pas.

C'est la distinction que l'état de l'art 2026 fait entre *alerter* et
*imposer* : une alerte notifie après coup et n'arrête rien, une contrainte
refuse l'action [S6] ; et la contrainte doit vivre en dehors du code qu'elle
contrôle, précisément pour qu'il ne puisse pas la contourner [S7]. Appliqué
ici : tant que « aucune revue publiée » vaut vert, la chaîne à quatre acteurs
d'ADR-0010 est facultative en pratique.

### Constat 2 — P1 — `master` est rouge : une transition rejouée

Sur le SHA fusionné, `pipeline-orchestrate` échoue
(run [31682196140](https://github.com/PLiagre/ForgeHistory/actions/runs/31682196140)) :

```
error: audit 'CURSOR-3b47ffe-pr57-monde-sans-faim' is AUDIT_CONVERTED, not
AUDIT_CHALLENGED; only a challenged audit can be decided (--policy auto included)
##[error]Process completed with exit code 2.
```

Mécanique, reproduite par l'auditeur (§ 7.2). Le workflow se déclenche sur
tout push à `master` touchant `architecture/reviews/*.md`. Or la branche de
lot transportait **à la fois** le fichier de revue de l'audit `CURSOR-3b47ffe`
**et** les trois lignes de registre qui actent son sort
(`AUDIT_CHALLENGED`, `AUDIT_APPROVED`, `AUDIT_CONVERTED`). Au moment de la
fusion, le déclencheur voit donc une revue « nouvelle » pour un audit dont la
vie est déjà écrite.

La garde censée couvrir ce cas existe, et son commentaire nomme l'incident
qui l'a motivée (`harness/pipeline/trigger_resolve.py`, lignes 111-120) :

```python
def is_terminal(state: str | None) -> bool:
    """A ledger FSM state is terminal iff `audit_ledger.TRANSITIONS` maps it
    to an empty successor set (today: only AUDIT_ARCHIVED)."""
```

Elle demande « la vie de cet audit est-elle finie ? ». La question qu'il
fallait poser est celle que l'orchestrateur pose ensuite : « cet audit est-il
exactement dans l'état que l'événement suppose ? ». `AUDIT_CONVERTED` n'est
pas terminal (il mène encore à `AUDIT_IMPLEMENTED`), donc la garde laisse
passer, et l'orchestrateur refuse deux lignes plus loin. Rejeu :

```
etat au ledger         : AUDIT_CONVERTED
etat juge terminal ?   : False
resolution du push     : event='review_recorded' payload={'audit_id': 'CURSOR-3b47ffe-...'}
notices                : aucune
```

C'est le problème d'idempotence que la littérature 2026 sur l'exécution
durable traite comme le cas de base : un moteur qui rejoue une histoire doit
*sauter* les activités déjà accomplies plutôt que les retenter, et une
opération non idempotente doit être protégée par une clé de déduplication
[S1, S2]. Ici le registre **est** l'historique — il contient déjà la réponse
— mais il est consulté avec le mauvais prédicat.

Portée honnête : l'échec est bruyant, sans perte de données, et n'a pas
altéré le registre. Il pollue durablement le signal « CI de master », ce qui
est justement le signal sur lequel s'appuient les portes de fusion. D'où P1
et non P0.

### Constat 3 — P1 — Ce que coûte réellement le défaut fusionné, mesuré

L'audit pré-fusion écrivait que les compteurs vedettes « sous-estiment la
faim et la mortalité d'un montant **non mesuré** ». Le contre-audit a
confirmé le mécanisme sans mesurer l'ampleur non plus. Le voici mesuré, sur
le code tel qu'il est aujourd'hui dans le tronc, mêmes graines que le
manifeste (42/42, 200 ticks) :

| Variante | morts cumulés | survie | cellules en faim au tick 200 | kg comptés |
|---|---|---|---|---|
| 1. code fusionné (`16ff5ac`) | 7 544 299 | 0,887172 | 9 | 8 171 507 |
| 2. même code, nourriture échangée comptée **une** fois | 7 583 741 | 0,886582 | 30 | 7 418 965 |
| 3. ordre littéral de SC3 (commerce **avant** consommation) | 7 560 137 | 0,886935 | 30 | 8 144 114 |

La variante 2 ne change qu'une chose : la nourriture reçue qui solde un
déficit est **mangée** au lieu de rester en stock. Même flux, même plafond
par arête, même ordre d'arêtes. Écart : **+39 442 morts (+0,52 %)**, survie
−0,000590, et le nombre de cellules encore affamées au dernier tick passe de
9 à 30.

Deux lectures, et il faut donner les deux.

D'abord, à décharge : sur les agrégats mondiaux, l'effet est **petit**.
Un demi-pour-cent de morts en plus ne renverse aucune conclusion, et la
fraction de survie reste au-dessus du seuil SC5. L'étiquette P0 posée avant
fusion décrivait correctement la nature du défaut (un kilogramme nourrit deux
fois, ce que le principe 3 de `docs/rules/simulation-principles.md` interdit)
mais, sur les chiffres publiés, sa magnitude est celle d'un P2. Le dire est
la contrepartie du cadrage adverse : on cherche aussi où l'accusation est
exagérée.

Ensuite, à charge, et c'est le point neuf : **les trois ordres donnent trois
mondes différents, et rien dans le dépôt ne dit lequel est le bon.** Le brief
012 (ligne 70) spécifie le commerce avant la consommation ; le code fait
l'inverse ; la variante qui respecte le principe physique n'est ni l'un ni
l'autre. Le compteur `cellules affamées` triple selon le choix. Un chiffre
publié dans `HANDOFF.md` (« 261 cellules ont connu la faim […] fraction de
survie 0.887 ») dépend donc d'un arbitrage que personne n'a rendu. Corriger
le double comptage sans trancher d'abord l'ordre du tick reviendrait à
remplacer un monde non arbitré par un autre.

Limite de cette mesure, à dire clairement : je compte les cellules dont
`hunger_ticks > 0` au tick 200. Le compteur publié `261` est produit par un
autre script (`measure_cellules_affamees.py`) qui compte autre chose ; je ne
l'ai **pas** recalculé sous les variantes, donc je n'affirme rien sur ce
chiffre-là. De même, la variante 2 ne conserve pas la somme des stocks au
sein de la seule étape de commerce — c'est voulu : la nourriture est
consommée.

### Constat 4 — P2 — L'arriéré de la boucle est invisible au moment de décider

Mesure sur `16ff5ac` (§ 7.6) :

```
audits dans inbox            = 27
audits presents au ledger    = 14
audits SANS aucune ligne     = 13
```

Nuance nécessaire, pour ne pas transformer une convention en défaut :
l'absence de ligne `AUDIT_PROPOSED` est **documentée comme normale**
(`harness/audit_ledger.py`, lignes 74-83 : « la présence d'un audit dans
`architecture/inbox/` **est** sa proposition »). Un audit sans ligne n'est
donc pas mal enregistré ; il est simplement resté à `PROPOSED`.

Le défaut n'est pas l'enregistrement, c'est l'absence de contre-pression :
près de la moitié des audits n'ont jamais avancé d'un cran, dont les deux
plus récents — ceux des deux dernières fusions, qui portaient chacun un P0 —
et rien ne le signale au moment où une fusion est décidée. Deux
contre-audits sont d'ailleurs déjà écrits et poussés, mais restent sans PR :

```
forge-bot/review-CURSOR-a600532-fusion-sans-contre-audit-31673848038
forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615
```

Le travail est fait deux fois, jeté deux fois, et l'arriéré grandit sans
compteur. L'état de l'art 2026 mesure précisément ce régime : quand la
validation avant fusion est faible, les défauts atteignent le tronc, et les
échecs du tronc sont les plus coûteux parce qu'ils bloquent tout le monde
[S5]. La réponse outillée qui s'est standardisée est une porte de fusion
déterministe, distincte du commentaire consultatif d'un relecteur automatique
et exprimée en réussite/échec binaire [S3, S4].

### Constat 5 — P3 — Classification CI et propreté de la fusion

Six workflows déclenchés sur `16ff5ac` : `security`, `hermes-dashboard`,
`audit-guard`, `pipeline-audit`, `harness-ci` **verts** ;
`pipeline-orchestrate` **rouge** (constat 2). `hermes-observer` ne s'est pas
déclenché sur ce push.

La fusion elle-même est propre : `git diff a4de4bb..16ff5ac` ne contient que
ce que `master` avait déjà de son côté (les deux audits précédents et le
tableau de bord d'Hermes). Aucune résolution de conflit n'a introduit de
contenu inédit.

## 3. Risques par sévérité

| Sévérité | Constats | Risque si rien n'est fait |
|---|---|---|
| P0 | 1 | La chaîne à quatre acteurs d'ADR-0010 est facultative en pratique : un contre-audit peut confirmer un P0 et disparaître sans que rien ne devienne rouge. La prochaine fusion se fera dans les mêmes conditions. |
| P1 | 2, 3 | Un échec permanent pollue le signal « CI de master » sur lequel s'appuient les portes de fusion ; la couche 1 du monde publie des chiffres qui dépendent d'un ordre de tick que personne n'a arbitré. |
| P2 | 4 | L'arriéré d'audits non traités croît sans compteur ; le travail de contre-audit est refait et jeté à chaque cycle. |
| P3 | 5 | « CI verte » peut être énoncé alors qu'un workflow est rouge sur le SHA fusionné. |

## 4. Ce qui tient (cadrage adverse, résultat négatif)

La quatrième lentille demande de chercher où les affirmations sont fausses.
Plusieurs ne le sont pas :

- **Les portes mécaniques sont vertes sur le tronc, rejouées ici**
  (§ 7.1) : `verdict_audit.py` `ACCEPT` sur les lots 011 et 012,
  `314 passed, 16 skipped` côté harnais, `25 passed` côté `sim/`,
  `harness_audit.py` à `20/24` — identique à l'avant-fusion, donc aucune
  régression introduite par la fusion.
- **Le contre-audit perdu est un vrai travail, pas un gabarit.** Il rejoue
  ses propres sondes, corrige l'audit d'un kilogramme sur un chiffre
  (7 450 806 contre 7 450 807, arrondi flottant) et délimite honnêtement ce
  qu'il n'a pas pu vérifier (le statut CI, faute d'accès `gh`). La perte est
  d'autant plus regrettable.
- **La garde d'idempotence du constat 2 n'est pas absente**, elle est mal
  ciblée. Elle est unitairement testable hors GitHub Actions, dérive sa
  notion de terminalité de la table `TRANSITIONS` vivante plutôt que d'une
  constante recopiée, et documente l'incident qui l'a motivée. Le correctif
  porte sur le prédicat, pas sur l'architecture.
- **Le budget n'a pas été le facteur limitant cette fois** :
  `{"month_total_usd": 0.0, "status": "PROCEED"}` avant invocation,
  `{"cap_usd": 5.0, "over_cap": false}` après. La panne de crédit qui avait
  causé la fusion précédente ne s'est pas reproduite.

## 5. Limite de cet audit (à lire avant de s'en servir)

Cet audit est produit par un agent Cursor ; le lot audité a été produit par
des agents Cursor. L'indépendance revendiquée par le contrat est donc, sur ce
commit encore, une séparation de sessions et de contextes, pas
d'infrastructures. Ce que cet audit offre en compensation, et a fait :
n'énoncer aucun constat sans mesure rejouée par lui-même, publier ses sondes,
et signaler explicitement (constat 3) l'endroit où l'audit précédent a
sur-évalué la magnitude d'un défaut qu'il avait par ailleurs correctement
diagnostiqué.

## 6. Briefs atomiques proposés (3 au maximum — proposition, pas instruction)

1. **Une revue qui n'est pas publiée doit rendre la CI rouge.** Faire échouer
   l'étape de publication de `pipeline-challenge` quand `gh pr create` est
   refusé alors qu'un commit de revue a été poussé (aujourd'hui `|| echo`
   avale le code de sortie), et donner à la boucle un compteur visible des
   revues poussées sans PR et des audits restés à `PROPOSED`. Preuve exigée :
   un test rouge qui simule un `gh pr create` refusé et voit le job échouer ;
   la reprise des deux branches `forge-bot/*` aujourd'hui orphelines.
2. **La garde de rejeu doit tester l'état attendu, pas la terminalité.**
   Dans `trigger_resolve.py`, n'auto-dispatcher `review_recorded` que si
   l'audit est effectivement dans l'état que l'orchestrateur exige, au lieu
   de se contenter d'écarter les états terminaux — et ne pas laisser un
   déclenchement légitime finir en `exit 2`. Preuve exigée : un test rouge
   rejouant exactement la situation de `16ff5ac` (une revue arrivant sur
   `master` en même temps que les lignes de registre qui la périment).
3. **Arbitrer l'ordre du tick avant de corriger le double comptage.** Le même
   objet que la proposition 1 de `CURSOR-a4de4bb` — à traiter comme **un
   seul** lot avec elle, pas comme un second — augmenté de ce que la mesure
   du constat 3 a révélé : décider explicitement lequel des trois ordres fait
   foi, puis re-publier les compteurs de `HANDOFF.md` et de `ROADMAP.md` sous
   l'ordre retenu. Sans cet arbitrage, le correctif remplace un monde non
   arbitré par un autre.

## 7. Commandes rejouées (sorties collées)

Environnement : arbre de travail séparé sur `16ff5ac`
(`git worktree add /tmp/audit16 16ff5ac`), interpréteur
`/workspace/.venv/bin/python`. Aucune écriture dans le dépôt.

### 7.1 Portes mécaniques sur le tronc fusionné

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/012-monde-vivant-commerce-inter-cellules
VERDICT: ACCEPT
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage
VERDICT: ACCEPT
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 16.68s
$ .venv/bin/python -m pytest sim/tests/ -q
25 passed in 0.90s
$ .venv/bin/python harness/harness_audit.py
SCORE: 20/24
```

### 7.2 Rejeu du dispatch fautif (constat 2)

```python
# sonde : /tmp/audit16, sys.path sur harness/ et harness/pipeline/
import trigger_resolve, audit_ledger
fname = "architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md"
aid   = trigger_resolve.audit_id_from_review_filename(fname)
etat  = audit_ledger.current_state_for(aid, trigger_resolve.LEDGER_PATH)
out   = trigger_resolve.resolve_push([fname])
```

```
fichier de revue apporte par la fusion : architecture/reviews/CLAUDE-CURSOR-3b47ffe-pr57-monde-sans-faim.md
audit_id deduit        : CURSOR-3b47ffe-pr57-monde-sans-faim
etat au ledger         : AUDIT_CONVERTED
etat juge terminal ?   : False
successeurs possibles  : ['AUDIT_IMPLEMENTED', 'AUDIT_STALE']
resolution du push     : event='review_recorded' payload={'audit_id': 'CURSOR-3b47ffe-pr57-monde-sans-faim'}
notices                : aucune
```

Et l'échec réel côté CI :

```
$ gh run view 31682196140 --log-failed
error: audit 'CURSOR-3b47ffe-pr57-monde-sans-faim' is AUDIT_CONVERTED, not
AUDIT_CHALLENGED; only a challenged audit can be decided (--policy auto included)
##[error]Process completed with exit code 2.
```

### 7.3 Le contre-audit perdu (constat 1)

```
$ git ls-remote --heads origin 'refs/heads/forge-bot/*'
4822662...  refs/heads/forge-bot/review-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur-31594124761
ae66c1a...  refs/heads/forge-bot/review-CURSOR-73022bd-hermes-dashboard-modele-auditeur-31593583378
8319f55...  refs/heads/forge-bot/review-CURSOR-779d97c-revue-verdicts-illisibles-31596321701
25b3185...  refs/heads/forge-bot/review-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois-31681378615
ab0e7f0...  refs/heads/forge-bot/review-CURSOR-a600532-fusion-sans-contre-audit-31673848038

$ git show --stat 25b3185
    challenge: revue CLAUDE-CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
    (claude-challenger headless, run 31681378615)
 ...OR-a4de4bb-pr60-nourriture-comptee-deux-fois.md | 124 +++++++++++++++++++++
 1 file changed, 124 insertions(+)
```

En-tête et premier verdict du fichier :

```
review_of:     CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
reviewer:      claude-code
target_commit: a4de4bb91f39452c3d469792d883d0a6b83b1560
reviewed_at:   2026-08-13T08:20:30Z

| 1 | P0 — la nourriture transférée par le commerce nourrit deux fois | **CONFIRMED** | […]
  Sévérité P0 justifiée : les trois compteurs vedettes de SC5 sont mesurés sur
  ce mécanisme, et j'ai reproduit ces trois chiffres exacts moi-même […] |
```

Les neuf points techniques portent `CONFIRMED`.

### 7.4 Le workflow vert qui n'a rien publié (constat 1)

```
$ gh run list --workflow pipeline-challenge.yml --limit 3
2026-08-13T08:17:05Z push 0120b8c -> completed/success
2026-08-13T06:26:41Z push 4acb8e2 -> completed/success
2026-08-12T17:09:19Z push ee229e0 -> completed/failure

$ gh run view 31681378615 --log --job <invoke-claude-challenger>
Monthly CI budget precheck   : {"month_total_usd": 0.0, "status": "PROCEED"}
Post-hoc budget marking      : {"cap_usd": 5.0, "over_cap": false, ...
                                "step": "challenge:CURSOR-a4de4bb-..."}
Publish the review as a PR   : ##[warning]gh pr create refused (repository setting
                               or permissions) -- branch forge-bot/review-CURSOR...
Job conclusion               : success
```

### 7.5 La fusion (constat 1)

```
$ gh pr view 60 --json mergedAt,mergedBy,reviewDecision,reviews
{"mergedAt":"2026-08-13T08:28:17Z","mergedBy":"PLiagre","reviewDecision":"","nbReviews":0}
```

### 7.6 Compteurs, contrefactuels et arriéré (constats 3 et 4)

```
$ .venv/bin/python /tmp/probe16.py
=== A. Compteurs publies par master, rejoues et contrefactuels ===
graine 42/42, 200 ticks, conso=2.0 kg/hab/tick, capacite arete=200.0 kg

1. code fusionne sur master (16ff5ac)              morts=  7544299  survie=0.887172  affamees=   9  kg_transportes=  8171507
2. meme code, nourriture echangee comptee une fois morts=  7583741  survie=0.886582  affamees=  30  kg_transportes=  7418965
3. ordre litteral de SC3 (commerce avant conso)    morts=  7560137  survie=0.886935  affamees=  30  kg_transportes=  8144114

Ecart 2-1 : morts +39442 (+0.52 %), survie -0.000590, cellules affamees +21
```

```
$ .venv/bin/python -  # comptage des audits au registre
audits dans inbox            = 27
audits presents au ledger    = 14
audits SANS aucune ligne     = 13
   - CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois
   - CURSOR-a600532-fusion-sans-contre-audit
   - (11 autres)
```

Le cœur de la variante 2, pour qu'un tiers puisse la contredire :

```python
transfer = min(src.food_stock_kg, dst.food_deficit_kg, cap)
src.food_stock_kg -= transfer
satisfait = min(transfer, dst.food_deficit_kg)
dst.food_deficit_kg -= satisfait
dst.food_stock_kg += transfer - satisfait   # nul : la ration reçue est mangée
```

## 8. Veille externe (section `cursor-qa-scout`, compagnon de session)

Contrat : `architecture/agents/cursor-qa-scout.md`. Cette section alimente
l'audit en état de l'art ; elle ne formule aucune instruction.

**Non-duplication.** Les treize briefs ouverts sous `harness/queue/briefs/`
ont été relus par leur titre avant d'écrire. Deux recouvrements sont
signalés plutôt que réécrits : le brief **009**
(`009-full-auto-agent-invocation`) porte déjà « give recurring CI spend a
real ceiling » — le thème *plafond de coût* n'est donc **pas** réinstruit
ici, et les sources [S6, S7] sont fournies comme point de comparaison, pas
comme demande. Le brief **006** (`006-full-auto-agent-pipeline`) porte la
boucle full-auto elle-même ; le constat 1 ne propose pas un nouveau pipeline
mais la fermeture d'un échec silencieux à l'intérieur de celui-ci. Aucun
autre doublon avec un brief ouvert.

**Comparaison dépôt / état de l'art — portes de fusion.** L'état de l'art
2026 sépare deux objets que le dépôt confond encore : le *commentaire
consultatif* d'un relecteur automatique et la *porte déterministe* qui
autorise ou refuse la fusion, exprimée en réussite/échec binaire [S3, S4].
La raison invoquée est chiffrée : la protection de branche GitHub ne sait pas
conditionner la fusion à l'auteur d'une PR, d'où des passerelles dédiées qui
tiennent les PR d'agents à une barre plus stricte que celles des humains
[S3]. Les mesures 2026 vont dans le même sens — 32,7 % de taux de fusion pour
les PR assistées par IA contre 84,4 % pour les humaines, succès du tronc à
70,8 %, diagnostiqué comme « un problème de validation avant fusion exprimé
en symptôme après fusion » [S5]. Le dépôt possède déjà la matière d'une telle
porte (l'audit, le contre-audit, le registre) : ce qui manque n'est pas
l'analyse, c'est qu'un résultat d'analyse puisse rendre la CI rouge.

**Comparaison dépôt / état de l'art — boucles agentiques.** Les moteurs
d'exécution durable de 2026 traitent le rejeu comme le cas normal, pas comme
l'exception : on rejoue l'orchestration, on saute les activités déjà
accomplies grâce à l'historique persisté, et toute opération non idempotente
reçoit une clé de déduplication [S1, S2]. `audit-ledger.jsonl` **est** cet
historique et la table `TRANSITIONS` **est** la machine à états ; le constat 2
montre que la consultation ne pose pas la bonne question. L'écart est mince
et local, pas architectural.

## 9. Sources externes

| # | source | date de la source | consulté le |
|---|---|---|---|
| S1 | Zylos Research — *Durable Execution for Agent Runtimes: Workflow Engines, Replay, and Recoverable AI Work* — <https://zylos.ai/research/2026-04-27-durable-execution-agent-runtimes> | 2026-04-27 | 2026-08-13 |
| S2 | AWS — *Agent orchestration* (AI Agent Learning Series ; rejeu déterministe, activités déjà complétées sautées, clés d'idempotence) — <https://aws.amazon.com/marketplace/build-learn/ai-agent-learning-series/agent-orchestration> | 2026 | 2026-08-13 |
| S3 | *mergegate — author-aware merge gate for autonomous-agent PRs* — <https://github.com/deemwar-products/mergegate> | 2026 | 2026-08-13 |
| S4 | DEV Community — *I built a deterministic CI firewall for AI-generated pull requests* (Agent Gate ; portes déterministes contre commentaires consultatifs) — <https://dev.to/sjh9714/i-built-a-deterministic-ci-firewall-for-ai-generated-pull-requests-4o3c> | 2026 | 2026-08-13 |
| S5 | Medium — *The bottleneck moved and nobody rebuilt the pipeline* (CircleCI 02-2026, LinearB 03-2026 : 32,7 % contre 84,4 % de taux de fusion, tronc à 70,8 %) — <https://medium.com/@roanmonteiro/the-bottleneck-moved-and-nobody-rebuilt-the-pipeline-20ec12806404> | 2026 | 2026-08-13 |
| S6 | Waxell — *AI Agent Token Budget Enforcement [2026]* (alerter après coup contre imposer avant l'appel) — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026 | 2026-08-13 |
| S7 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers* (la contrainte doit vivre hors du code qu'elle contrôle) — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026 | 2026-08-13 |

Les trois thèmes de veille exigés par le contrat sont couverts : pipeline de
développement autonome [S3, S4, S5], orchestration d'agents en CI [S1, S2],
budget de jetons des agents [S6, S7].

---

Fin de l'audit. Statut `PROPOSED` : aucun point ci-dessus n'est une
instruction, aucun n'autorise une implémentation. Le contre-audit
(`architecture/reviews/`), puis la décision (`architecture/decisions/` ou la
politique automatique d'ADR-0006), restent seuls compétents.
