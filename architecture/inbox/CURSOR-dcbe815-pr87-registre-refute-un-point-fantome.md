---
audit_id:                CURSOR-dcbe815-pr87-registre-refute-un-point-fantome
auditor:                 cursor-cloud
target_branch:           forge-bot/review-CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre-31695162454
target_commit:           dcbe815817b9838ed79dd0bd9d4fb7e1e55108c2
created_at:              2026-08-13T13:04:06Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #87 — le contre-audit de CURSOR-f978cc7

Audit de la PR [#87](https://github.com/PLiagre/ForgeHistory/pull/87)
(1 fichier, +121 / −0, base `master`, tête `dcbe815`, branche
`forge-bot/review-CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre-31695162454`,
fusionnée le 2026-08-13 à 12:55:00Z par le commit de fusion `53fb711`).

Méthode : `architecture/review-guidelines.md` — six lentilles, sévérités
P0–P3, une preuve citée par constat. Rôle : auditeur en **lecture seule**.
Cet audit **n'instruit rien** et ne vaut pas décision
(`architecture/README.md`) : il propose, la boucle tranche.

Toutes les mesures ont été rejouées sur une copie de travail en lecture
seule (`git show`, API GitHub authentifiée, registres temporaires sous
`/tmp`). Aucune écriture dans le dépôt audité en dehors du présent fichier.
Les sorties sont collées telles quelles au § 8.

## 0. Synthèse

Le contenu de cette PR est bon. La revue produite par `claude-challenger`
est dense, honnête sur ses limites, et son tableau de verdicts est
correctement lisible par la machine — le défaut de format qui avait bloqué
la boucle en août (`CLAUDE-CURSOR-bb8fe11`) ne se reproduit pas ici.

Ce qui ne va pas n'est pas dans le texte : c'est dans **ce que la machine
en a retenu**. La fusion de cette PR a écrit au registre
`{"CONFIRMED": 19, "REFUTED": 1, "PARTIAL": 2, "NEEDS_OWNER": 2}`, alors
que la revue ne réfute **aucun** point : son unique occurrence du mot
`REFUTED` est la ligne de rappel que le gabarit lui-même insère. La
description de la PR, elle, donne le bon chiffre (« 18 CONFIRMED, 1
PARTIAL »). Le même commit porte donc deux comptes contradictoires : le
bon en prose, le faux dans la trace durable et append-only.

Ce défaut est connu : **quatorze** audits déjà déposés dans `inbox/`
nomment `parse_verdicts`. Aucun n'a été converti en brief ; huit d'entre
eux n'ont même aucun évènement au registre. Je ne le recompte donc pas
comme une découverte (§ 5) — j'apporte la mesure qui manquait : **19 des
20** évènements `AUDIT_CHALLENGED` du registre portent un champ `verdicts`
faux, et 19 sur 20 annoncent au moins un `REFUTED` fantôme.

Sévérités : 2×P1, 2×P2, 2×P3, **0×P0**. Rien ici ne casse un comportement
du produit ; tout porte sur la fiabilité de la trace que la boucle laisse
d'elle-même.

## 1. Intention avant diff (lentille 1)

L'intention est lisible et le diff la sert. La description annonce un seul
fichier sous `architecture/reviews/**`, et c'est exactement ce que contient
le commit (§ 8.A). Le fichier est bien le contre-audit de l'audit
`CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre`, qui existe dans
`inbox/`, et son `target_commit` `f978cc7` est toujours la tête de la
PR #77 — **encore ouverte** au moment où j'écris (§ 8.B). La revue n'est
donc pas périmée : sa prémisse tient.

Une seule affirmation de la description est fausse, et elle porte sur le
décompte des verdicts (§ 3, P1-1).

Note de contexte, sans reproche : la PR a été ouverte à la main par
l'orchestrateur 87 minutes après que le workflow eut poussé la branche.
La description le dit franchement. La cause mécanique est en P2-1.

## 2. Portes mécaniques d'abord (lentille 3) — classification de la CI

CI du commit audité `dcbe815` : **18 check-runs, verte**, aucune en échec
(§ 8.C).

| état | nombre | jobs |
|---|---|---|
| `success` | 14 | `actionlint`×2, `f0-demo`×2, `gitleaks`×2, `schema`×2, `sim-tests`×2, `tests`×2, `check-and-automerge`, `invoke-cursor-auditor` |
| `skipped` | 2 | `cursor-scope`×2 (la branche `forge-bot/*` ne matche pas le préfixe `cursor/` exigé par `audit-guard.yml:30`) |
| `cancelled` | 1 | `Reconcile local Hermes state` (annulé 3 s après la fusion) |
| `queued` | 1 | `Reconcile local Hermes state` (relancé, jamais terminé) |

Point positif à porter au crédit de cette fusion : elle a **attendu** ses
portes. Le dernier job utile (`tests`) s'est terminé à 12:54:57Z, la fusion
a eu lieu à 12:55:00Z. C'est l'ordre correct, et il mérite d'être noté
puisque la lentille 3 le réclame. Ce que les portes ne couvrent pas est
traité en P3-2.

## 3. Constats

### P1-1 — Le registre affirme un `REFUTED` qui n'existe nulle part dans la revue

**Sévérité : P1.** Récurrence d'un défaut déjà déposé (§ 5) ; ce que
j'ajoute ici est la contradiction interne à cette PR et la mesure sur tout
l'historique.

La fusion de cette PR a déclenché `pipeline-orchestrate.yml`, qui a écrit
au registre :

```
{"timestamp": "2026-08-13T12:55:16Z", "audit_id": "CURSOR-f978cc7-...",
 "event": "AUDIT_CHALLENGED", "actor": "claude",
 "verdicts": {"CONFIRMED": 19, "REFUTED": 1, "PARTIAL": 2, "NEEDS_OWNER": 2}}
```

Le tableau réel de la revue contient **19 lignes : 18 CONFIRMED, 1 PARTIAL,
0 REFUTED, 0 NEEDS_OWNER** (§ 8.D). La description de la PR l'écrit
d'ailleurs correctement : « 18 CONFIRMED, 1 PARTIAL ».

La cause est d'une ligne. `harness/audit_review.py:127-134` :

```python
def parse_verdicts(text: str) -> dict:
    """Count occurrences of each verdict token as a whole word."""
    for token in VERDICTS:
        n = len(re.findall(rf"\b{re.escape(token)}\b", text))
```

Le comptage porte sur **tout le document**, pas sur les lignes du tableau.
Or le gabarit produit par ce même module (`audit_review.py:76`) insère la
phrase :

> `Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.`

Chaque revue écrite à partir du gabarit part donc avec +1 sur les quatre
compteurs, et le titre de section `## 3. Points à porter au propriétaire
(NEEDS_OWNER)` (`audit_review.py:94`) en ajoute un cinquième. Sur ce
fichier précis, l'unique occurrence de `REFUTED` est cette ligne de rappel,
et rien d'autre (§ 8.D).

Pourquoi c'est P1 et pas cosmétique : `architecture/README.md` (tableau des
dossiers) définit `audit-ledger.jsonl` comme « une ligne par transition
d'état », écrite par la machine — c'est la seule trace durable de ce que
Claude a jugé, et elle est **append-only** : la ligne fausse ne peut pas
être corrigée, seulement contredite plus tard. Un lecteur ou un outil qui
interroge le registre pour savoir si le contre-audit a réfuté quelque
chose obtient « oui, un point » là où la réponse est « aucun ». La
littérature de gouvernance d'agents traite précisément la reconstructibilité
d'un enregistrement d'audit comme la propriété non négociable du journal
[S2].

L'incohérence n'affecte pas la décision : `audit_decision.parse_point_verdicts`
utilise une expression régulière stricte sur les lignes `| N | ... |` et
voit bien 18 CONFIRMED + 1 PARTIAL (§ 8.D). Ce sont deux lecteurs du même
fichier qui ne comptent pas pareil — et c'est le plus faux des deux qui est
gravé.

Ampleur mesurée sur tout le registre (§ 8.E) : **19 évènements
`AUDIT_CHALLENGED` sur 20** portent un `verdicts` différent du tableau réel,
et **19 sur 20** annoncent au moins un `REFUTED`. Deux revues
(`CURSOR-5633ee7`, `CURSOR-73022bd`) ont même un `verdicts` renseigné alors
que leur tableau n'a **aucune** ligne lisible par la machine.

### P1-2 — La boucle approuve plus vite qu'elle ne convertit ; cette PR ajoute une approbation de plus sans brief

**Sévérité : P1.**

La fusion de cette PR a produit, dans la même seconde que l'évènement
`AUDIT_CHALLENGED`, une décision automatique :

```
verdict: APPROVED
decided_by: policy:auto
retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19]
```

(`architecture/decisions/DECISION-CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre.md`,
§ 8.F.)

Deux problèmes, mesurables :

1. **Les 19 points sur 19 sont retenus, y compris celui que le
   contre-auditeur refuse explicitement de trancher.** Le point 19 est le
   seul `PARTIAL` ; son texte dit : « la décision de fusionner ou non
   PR #77 […] est un arbitrage du propriétaire, pas quelque chose que je
   peux CONFIRMER ou REFUTER techniquement ». La règle
   `confirmed_union_partial` le transforme néanmoins en « point retenu par
   le propriétaire ». De même, les **quatre** questions du § 3 de la revue
   (« Faut-il fusionner PR #77 telle quelle ? », priorité vis-à-vis de
   `4c45718`, lecture du constat 3, statut du segment
   `IMPLEMENTED`/`VERIFIED`) n'apparaissent nulle part dans le fichier de
   décision : elles sont en prose, et le moteur ne lit que la colonne du
   tableau.

2. **L'approbation n'a pas de suite.** Sur les 39 audits présents dans
   `inbox/` : **15 n'ont aucun évènement** au registre, **11 sont
   `AUDIT_APPROVED` sans jamais avoir été `AUDIT_CONVERTED`** — dont celui
   que cette PR vient de produire — et **6 seulement** ont donné un brief
   (§ 8.G). La boucle enregistre donc environ deux fois plus
   d'approbations qu'elle ne produit de travail.

C'est exactement le point où les architectures de développement autonome
en boucle fermée placent leur garde : le débit du backlog et l'état
terminal de chaque élément sont comptés et contraints, faute de quoi
l'automatisation produit de la trace au lieu de produire du logiciel [S1].
La lentille 1 dit la même chose à l'échelle d'une PR : une contribution
doit résoudre le bon problème *avec les bonnes contraintes*, et la
contrainte manquante ici est « une approbation engage à quelque chose ».

Antécédent non traité : `CURSOR-a7d1c57-pr76-approbation-sans-conversion`
(aucun évènement au registre). Je ne le recompte pas ; le brief 3 du § 7
propose de le consolider plutôt que d'ouvrir un doublon.

### P2-1 — Le workflow qui a produit la revue est vert alors que sa dernière étape a échoué

**Sévérité : P2.** Récurrence (§ 5) ; l'élément neuf est la mesure de
latence sur ce run précis.

Le run [31695162454](https://github.com/PLiagre/ForgeHistory/actions/runs/31695162454)
est `success`, ses deux jobs sont `success`, et son étape 12 « Publish the
review as a pull request » est `success` — alors que son annotation dit
(§ 8.H) :

> `warning: gh pr create refused (repository setting or permissions) --
> branch forge-bot/review-…-31695162454 is pushed; open the PR manually.`

La cause est le `||` de `pipeline-challenge.yml:197-201` : l'échec de
`gh pr create` est converti en avertissement, et l'avertissement ne colore
rien. Conséquence mesurée : la revue a été poussée à **11:27:32Z** et la PR
ouverte à la main à **12:54:29Z**, soit **87 minutes** pendant lesquelles
un livrable payé (invocation Claude plafonnée à 5 $) existait sans que rien
dans la CI ne signale qu'il n'était pas publié. Sans intervention humaine,
il n'aurait jamais été fusionné et `pipeline-orchestrate` n'aurait jamais
tourné.

C'est le « green build trap » sous sa forme la plus littérale : le vert
prouve que les vérifications écrites sont passées, jamais que le résultat
attendu a été atteint [S3]. La discipline recommandée est l'inverse de
celle-ci : le code de sortie d'une commande décide seul de la suite, et
l'agent ne prononce jamais son propre succès [S4] — c'est déjà la règle du
harnais (`docs/rules/harness-roles.md`), appliquée aux agents mais pas à
ce workflow.

### P2-2 — Le workflow prive le contre-auditeur d'accès GitHub authentifié, et la revue en porte la trace

**Sévérité : P2.** Récurrence (§ 5) ; l'élément neuf est le décompte des
verdicts effectivement affaiblis.

La revue le déclare elle-même, dès son § de méthode :

> « `gh` n'est pas authentifié dans cet environnement (pas de `GH_TOKEN`),
> donc les points reposant sur l'API GitHub ont été rejoués via `curl` non
> authentifié sur l'API publique. »

Ce n'est pas une fatalité de l'environnement, c'est une propriété du
workflow. `pipeline-challenge.yml` déclare `permissions: pull-requests:
write` (ligne 36) et fournit un `GH_TOKEN` à l'étape 4 (kill-switch,
ligne 60) comme à l'étape 12 (publication, ligne 174) — mais **pas** à
l'étape 10, celle qui invoque le contre-auditeur, dont l'`env:` ne contient
que `CLAUDE_CODE_OAUTH_TOKEN`, `ANTHROPIC_API_KEY` et `AUDIT_ID`
(lignes 144-149). Le seul rôle censé vérifier des faits GitHub est le seul
à ne pas recevoir de jeton GitHub.

Ce que cela coûte, mesuré sur cette revue : les points **2**, **7**, le
§ 1 « Provenance » et l'affirmation du § 3 sur l'état de la PR #77
reposent tous sur des lectures anonymes de l'API — non rejouables de façon
déterministe (l'API publique non authentifiée est plafonnée à 60 requêtes
par heure et par IP) et aveugles au `statusCheckRollup` d'une branche
protégée. Plus gênant pour la lentille 4 : au **point 18**, le
contre-auditeur confirme les limites déclarées par l'auditeur en
expliquant qu'il a « la même limite ». Un contre-audit qui hérite de
l'angle mort de la partie auditée n'est pas indépendant *sur ce point* —
il le ratifie. Le cadrage adverse exige que celui qui critique ait au moins
les moyens de celui qu'il critique.

### P3-1 — La fusion a annulé le seul job qui observait l'état Hermes, et il n'a jamais tourné sur le commit fusionné

**Sévérité : P3 (information).**

`Reconcile local Hermes state` tourne sur un runner auto-hébergé Windows
(`hermes-observer.yml:32` : `runs-on: [self-hosted, Windows, X64,
hermes-observer]`). Sur `dcbe815` il apparaît deux fois (§ 8.C) :
`cancelled` à 12:55:03Z — trois secondes après la fusion, donc annulé
*par* elle — puis `queued` à 12:55:03Z, toujours en attente au moment de
cet audit. Il n'a donc produit aucun résultat sur ce commit.

La revue auditée signalait déjà cette dépendance à un runner auto-hébergé
pour la PR #77 (son point 16, `queued`). L'élément neuf est faible : ici le
job est activement annulé plutôt que simplement en attente. Je le classe
en information et ne propose pas de brief pour cela.

### P3-2 — 31 secondes entre ouverture et fusion : les portes mécaniques sont la seule relecture d'un document de jugement

**Sévérité : P3 (information).**

PR ouverte à 12:54:29Z, fusionnée à 12:55:00Z (§ 8.C). Les portes
mécaniques ont bien tourné avant (§ 2) — ce n'est pas le reproche. Le
constat est ce qu'elles couvrent : `audit_schema.py` ne valide que
`architecture/inbox/` (`harness/audit_schema.py:26,92`), aucun job ne
valide `architecture/reviews/**`, et la seule contrainte de contenu
existante est « au moins une ligne `| N | … | VERDICT | … |` », vérifiée
par `record_challenge`.

Cette contrainte est satisfaite par une ligne fabriquée. Le job
`mechanical-scaffold-smoke` du même workflow le démontre à chaque
déclenchement : il fabrique lui-même sa ligne, puis n'assure que la
présence de l'évènement (`grep -q AUDIT_CHALLENGED`). Rejoué à
l'identique (§ 8.I), il enregistre `{'CONFIRMED': 2, 'REFUTED': 1,
'PARTIAL': 1, 'NEEDS_OWNER': 2}` pour une revue qui contient **un seul**
verdict — le job qui prouve que la moitié mécanique fonctionne reproduit
donc le défaut P1-1 à chaque exécution, et ne peut pas le voir.

Le caractère fabriqué de cette fixture est déjà décrit par
`CURSOR-779d97c-revue-verdicts-illisibles` (lignes 251-258) ; je ne le
recompte pas. Je le cite parce qu'il explique pourquoi 31 secondes de
relecture suffisent aujourd'hui : rien, dans la chaîne, ne lit le contenu.

## 4. Ce qui tient (cadrage adverse — résultats négatifs)

Constats cherchés et **non** trouvés. Ils comptent autant que les autres :

- **Le diff est exactement ce qu'il annonce.** 1 fichier, +121 / −0, sous
  `architecture/reviews/` (§ 8.A). Aucun chemin de code, de test, de
  workflow ni de brief. Très en dessous du seuil de la lentille 5
  (~5 fichiers / quelques centaines de lignes) : aucun `NEEDS_SPLIT` à
  recommander.
- **La revue n'est pas périmée.** PR #77 est toujours `OPEN` et sa tête
  est toujours `f978cc79e2…`, identique au `target_commit` de la revue
  (§ 8.B). Le motif « verdicts périmés à la fusion » ne s'applique pas ici.
- **Le tableau est lisible par la machine.** 19 lignes `| N | … |`
  correctement numérotées ; `parse_point_verdicts` les voit toutes
  (§ 8.D). Le défaut de format qui avait bloqué `CLAUDE-CURSOR-bb8fe11`
  après fusion ne se reproduit pas.
- **Les affirmations de la revue que j'ai contre-vérifiées tiennent.** Son
  point 10 affirme que `harness/tests/test_audit_archive.py` ne compare
  jamais le contenu : rejoué, 8 fonctions de test, **0** occurrence de
  `sha256` ou `filecmp` (§ 8.J). Sa description du comptage de verdicts
  est cohérente avec ce que j'ai mesuré indépendamment.
- **La revue est honnête sur ses propres limites.** Elle déclare son
  environnement dégradé au lieu de le taire — c'est ce qui m'a permis
  d'écrire P2-2. Une revue qui aurait masqué ce point aurait été bien plus
  difficile à critiquer.
- **Aucun secret, aucune dépendance inventée.** `gitleaks` vert sur les
  deux exécutions (§ 8.C) ; le fichier n'introduit aucune importation ni
  aucun outil.

## 5. Déjà posé ailleurs — non recompté

`architecture/review-guidelines.md` interdit de répéter un motif déjà
écarté par une décision enregistrée. Aucun des motifs ci-dessous n'a été
*écarté* : ils n'ont, pour la plupart, jamais été traités. Je les cite
pour ne pas les présenter comme neufs, et je n'apporte que la mesure
nouvelle indiquée.

| motif | audits antérieurs | état au registre | ce que j'ajoute |
|---|---|---|---|
| `parse_verdicts` compte hors tableau | 14 audits, dont `786ec32`, `4b6dcff`, `4822662`, `063d7eb`, `949ecf1`, `8894f15` | 8 sans aucun évènement ; 4 `AUDIT_APPROVED` ; 1 `AUDIT_CHALLENGED` ; 1 `AUDIT_CONVERTED` | la mesure 19/20 sur tout l'historique, et la contradiction interne à la PR #87 (§ 8.D, 8.E) |
| fixture fabriquée du `mechanical-scaffold-smoke` | `779d97c` (lignes 251-258) | `AUDIT_CHALLENGED` | le rejeu chiffré : 1 verdict réel → 6 comptés (§ 8.I) |
| contre-audit non publié / run vert malgré échec | `16ff5ac`, `827d54e` | `827d54e` : `AUDIT_APPROVED`, jamais converti | la latence mesurée de 87 min sur le run 31695162454 (§ 8.H) |
| challenger sans `GH_TOKEN` | `063d7eb` (lignes 374-379), `1da49ea` | `063d7eb` : aucun évènement | l'inventaire des verdicts effectivement affaiblis (points 2, 7, 18, § 1, § 3) |
| approbation jamais convertie en brief | `a7d1c57` | aucun évènement | le comptage global 39 / 15 / 11 / 6 (§ 8.G) |
| `NEEDS_OWNER` en prose ignoré par la décision | `8894f15` (ligne 216) | aucun évènement | les 4 questions concrètes perdues par *cette* décision (§ 3, P1-2) |

## 6. Limites de cet audit (à lire avant de s'en servir)

- Je n'ai **pas** exécuté `harness/tests/` en entier. Mes affirmations sur
  le comportement de `parse_verdicts` viennent d'un rejeu direct du module
  et de la lecture du code, pas de la suite de tests.
- Je n'ai pas ouvert le transcript du run 31695162454 : je m'appuie sur les
  conclusions d'étapes et les annotations exposées par l'API. Le contenu
  exact de l'échec de `gh pr create` (réglage du dépôt ? permissions ?)
  n'est donc pas établi, seulement le fait qu'il a échoué sans colorer le
  job.
- Mon environnement est Linux avec un `gh` authentifié ; je n'ai donc
  **pas** reproduit la contrainte que subit le challenger. C'est justement
  l'asymétrie décrite en P2-2, et elle joue en ma faveur — ce qui rend mon
  constat plus fiable que la mesure inverse ne le serait.
- Le job `Reconcile local Hermes state` tourne sur un runner Windows
  auto-hébergé auquel je n'ai pas accès : je n'observe que son état côté
  API.
- Le classement P0–P3 est un jugement de gravité, pas un fait mesuré. Les
  faits sont au § 8 ; le classement est discutable et c'est au
  contre-audit puis au propriétaire de le dire.

## 7. Briefs atomiques proposés (3 au maximum — propositions, pas instructions)

Aucune de ces lignes n'autorise quoi que ce soit. Les trois flags
`*_authorized` du frontmatter sont à `false` (`architecture/README.md`,
règle d'intégrité 2).

1. **Compter les verdicts là où ils sont écrits.** Faire dériver le champ
   `verdicts` du registre de `audit_decision.parse_point_verdicts` (les
   lignes de tableau) au lieu du comptage plein texte de
   `audit_review.parse_verdicts`. Test rouge d'abord, sur le fichier réel
   de cette PR : le comptage attendu est `{CONFIRMED: 18, PARTIAL: 1}`, le
   comptage actuel rend un `REFUTED`. Faire assurer au job
   `mechanical-scaffold-smoke` l'égalité des comptes, et non la seule
   présence de l'évènement. Couvre P1-1 et la moitié mécanique de P3-2.
   Ne couvre **pas** la réécriture des 19 lignes déjà fausses : le registre
   est append-only, c'est un arbitrage propriétaire (§ 3).

2. **Rendre la publication du contre-audit vérifiable et lui donner les
   moyens de vérifier.** Deux gestes dans le même fichier
   `pipeline-challenge.yml` : (a) supprimer le `||` qui avale l'échec de
   `gh pr create` pour que le job échoue quand le livrable n'est pas
   publié ; (b) passer un `GH_TOKEN` en lecture à l'étape d'invocation du
   challenger. Couvre P2-1 et P2-2. À traiter comme un seul lot parce que
   les deux touchent le même workflow et que les séparer produirait deux
   PR en conflit.

3. **Mettre un compteur de débit sur la boucle.** Une commande — et sa
   sortie dans `hermes/DASHBOARD.md` — qui expose en permanence : audits
   sans évènement, `AUDIT_APPROVED` sans `AUDIT_CONVERTED`, âge du plus
   ancien. Le seuil de déclenchement (avertir ? suspendre l'audit
   automatique ?) est un arbitrage propriétaire, pas une décision
   technique. **Consolider** `CURSOR-a7d1c57-pr76-approbation-sans-conversion`
   dans ce lot plutôt que d'ouvrir un doublon. Couvre P1-2.

## 8. Commandes rejouées (sorties collées)

### 8.A — Le diff réel de la PR #87

```
$ git diff --stat dcbe815^ dcbe815
 ...-f978cc7-pr77-cloture-affirmee-hors-registre.md | 121 +++++++++++++++++++++
 1 file changed, 121 insertions(+)
```

### 8.B — La PR #77, cible de la revue, est toujours ouverte sur la même tête

```
$ gh pr view 77 -R PLiagre/ForgeHistory --json state,mergedAt,headRefOid
state=OPEN mergedAt=null head=f978cc79e20bbf42678ed2b5f7e811b4490fb88d
```

### 8.C — CI du commit audité `dcbe815`, avec horodatage

```
$ gh api "repos/PLiagre/ForgeHistory/commits/dcbe815.../check-runs?per_page=100"
PR#87 created=2026-08-13T12:54:29Z merged=2026-08-13T12:55:00Z
actionlint                    completed success   start=12:54:35Z end=12:54:47Z
check-and-automerge           completed success   start=12:54:35Z end=12:54:48Z
cursor-scope                  completed skipped   start=12:54:33Z end=12:54:33Z
f0-demo                       completed success   start=12:54:36Z end=12:54:47Z
gitleaks                      completed success   start=12:54:35Z end=12:54:45Z
invoke-cursor-auditor         completed success   start=12:54:35Z end=12:54:51Z
Reconcile local Hermes state  completed cancelled start=12:54:33Z end=12:55:03Z
Reconcile local Hermes state  queued    null      start=12:55:03Z end=null
schema                        completed success   start=12:54:37Z end=12:54:50Z
sim-tests                     completed success   start=12:54:35Z end=12:54:54Z
tests                         completed success   start=12:54:36Z end=12:54:57Z
```

(Les mêmes jobs apparaissent une seconde fois, exécutés à 11:27Z lors du
push de la branche ; tous `success`. Total 18 check-runs, cf. § 2.)

### 8.D — Les deux comptages du même fichier

```
$ python3 -c "... audit_review.parse_verdicts / audit_decision.parse_point_verdicts ..."
parse_verdicts (ce qui va au registre) : {'CONFIRMED': 19, 'REFUTED': 1, 'PARTIAL': 2, 'NEEDS_OWNER': 2}
parse_point_verdicts (lignes de tableau) : 19 lignes -> {'CONFIRMED': 18, 'PARTIAL': 1}

CONFIRMED: 19 occurrences, dont 1 hors ligne de tableau numerotee:
   L11: Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

REFUTED: 1 occurrences, dont 1 hors ligne de tableau numerotee:
   L11: Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

PARTIAL: 2 occurrences, dont 1 hors ligne de tableau numerotee:
   L11: Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.

NEEDS_OWNER: 2 occurrences, dont 2 hors ligne de tableau numerotee:
   L11: Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.
   L64: ## 3. Points à porter au propriétaire (NEEDS_OWNER)
```

L'unique `REFUTED` du document est la ligne de rappel du gabarit.

### 8.E — Ampleur sur tout le registre

```
$ python3 -c "... compare chaque AUDIT_CHALLENGED.verdicts au tableau du fichier cité ..."
20 evenements AUDIT_CHALLENGED au registre

CURSOR-FIXTURE-full-auto-demo          {'CONFIRMED': 1}                    {'CONFIRMED': 1}   ok
CURSOR-5633ee7-automation-completeness {'CONFIRMED': 7, 'REFUTED': 2, ...} {}                 ecart
CURSOR-73022bd-hermes-dashboard-...    {'CONFIRMED': 12, 'REFUTED': 2,...} {}                 ecart
CURSOR-779d97c-revue-verdicts-illis... {'CONFIRMED': 34, 'REFUTED': 15,..} {'CONFIRMED': 18,} ecart
CURSOR-f978cc7-pr77-cloture-affirmee.. {'CONFIRMED': 19, 'REFUTED': 1, ..} {'CONFIRMED': 18,} ecart
[...]

19/20 evenements AUDIT_CHALLENGED ont un champ verdicts different du tableau
19/20 annoncent au moins un REFUTED
```

### 8.F — La décision automatique produite par cette fusion

```
$ sed -n '1,20p' architecture/decisions/DECISION-CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre.md
decision_of: CURSOR-f978cc7-pr77-cloture-affirmee-hors-registre
decided_by: policy:auto
verdict: APPROVED
retained_points: [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, 19]

## Raison
policy: ledger_AUDIT_APPROVED_retained_points_confirmed_union_partial
```

Aucune mention des quatre questions du § 3 de la revue.

### 8.G — Débit réel de la boucle

```
$ python3 -c "... croise inbox/*.md et audit-ledger.jsonl ..."
audits dans inbox/ : 39
  sans aucun evenement au registre : 15
  APPROVED jamais CONVERTED        : 11
  CONVERTED en brief               : 6

audits nommant parse_verdicts et leur etat :
  CURSOR-063d7eb-pr35-challenge-perte-decision         (aucun evenement)
  CURSOR-4822662-pr31-verdicts-non-analysables         (aucun evenement)
  CURSOR-4b6dcff-pr73-contre-audit-recompte-a-tort     (aucun evenement)
  CURSOR-4c45718-pr65-ledger-recupere-a-la-main        AUDIT_APPROVED
  CURSOR-779d97c-revue-verdicts-illisibles             AUDIT_CHALLENGED
  CURSOR-786ec32-pr74-verdicts-fantomes-au-registre    (aucun evenement)
  CURSOR-827d54e-contre-audit-paye-jamais-publie       AUDIT_APPROVED
  CURSOR-8894f15-pr71-arbitrage-proprietaire-efface    (aucun evenement)
  CURSOR-949ecf1-pr42-revue-non-consommable            (aucun evenement)
  CURSOR-9e35764-pr63-contre-audit-jamais-enregistre   AUDIT_APPROVED
  CURSOR-a4de4bb-pr60-nourriture-comptee-deux-fois     AUDIT_CONVERTED
  CURSOR-a7d1c57-pr76-approbation-sans-conversion      (aucun evenement)
  CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion     AUDIT_APPROVED
  CURSOR-e2896e7-pr44-challenge-bb8fe11                (aucun evenement)
```

Quatorze audits nomment la même fonction ; aucun n'a produit de brief.

### 8.H — Le run du challenger : vert, étape de publication comprise

```
$ gh api repos/PLiagre/ForgeHistory/actions/runs/31695162454/jobs
JOB invoke-claude-challenger -> success
   10. Invoke claude-challenger headless (/forge-audit-review) -> success
   11. Post-hoc budget marking (lot 009b, arbitrage n°2)        -> success
   12. Publish the review as a pull request (...)               -> success
JOB mechanical-scaffold-smoke -> success

$ gh api repos/PLiagre/ForgeHistory/check-runs/<id>/annotations
warning: gh pr create refused (repository setting or permissions) --
  branch forge-bot/review-CURSOR-f978cc7-...-31695162454 is pushed; open the PR manually.
```

Run créé 11:21:50Z, terminé 11:27:32Z. PR ouverte à la main 12:54:29Z :
**87 minutes** de latence, run vert du début à la fin.

### 8.I — Le job « smoke » rejoué à l'identique

```
$ (fixture et commandes copiées de pipeline-challenge.yml:210-239)
recorded AUDIT_CHALLENGED for CURSOR-ciSmoke-topic:
  {'CONFIRMED': 2, 'REFUTED': 1, 'PARTIAL': 1, 'NEEDS_OWNER': 2}
grep AUDIT_CHALLENGED -> OK (le job passe)
```

Une seule ligne de verdict (`| 1 | mock point | CONFIRMED | … |`) produit
six verdicts au registre. Le job passe quand même : sa seule assertion est
`grep -q AUDIT_CHALLENGED`.

### 8.J — Contre-vérification du point 10 de la revue

```
$ grep -c "sha256\|filecmp" harness/tests/test_audit_archive.py
0
$ grep -c "^def test" harness/tests/test_audit_archive.py
8
```

Conforme à ce que la revue affirme.

## 9. Risques par sévérité

| sévérité | constat | risque si rien n'est fait |
|---|---|---|
| **P1** | P1-1 — le registre affirme un `REFUTED` inexistant | la seule trace durable du jugement de Claude est fausse et non corrigeable (append-only) ; tout outil ou lecteur qui s'y fie conclut à tort qu'un point a été réfuté |
| **P1** | P1-2 — approbations sans conversion | la boucle accumule 11 approbations pour 6 briefs ; les défauts qu'elle diagnostique restent en place et sont re-diagnostiqués (14 fois pour le seul `parse_verdicts`) |
| **P2** | P2-1 — run vert malgré l'échec de publication | un contre-audit payé peut rester non publié indéfiniment sans qu'aucun signal rouge n'apparaisse ; ici 87 min, rattrapées à la main |
| **P2** | P2-2 — challenger sans jeton GitHub | les verdicts portant sur des faits GitHub reposent sur des lectures anonymes non rejouables ; sur le point 18, le contre-audit hérite de l'angle mort de l'audit au lieu de le lever |
| **P3** | P3-1 — `Reconcile local Hermes state` annulé par la fusion | l'état Hermes n'est pas réconcilié sur le commit fusionné ; dépendance à un runner auto-hébergé hors CI publique |
| **P3** | P3-2 — 31 s entre ouverture et fusion | aucune relecture de contenu n'a lieu ; la seule porte sur `reviews/**` est satisfaite par une ligne fabriquée |

## 10. Sources externes

Recherche web effectuée le 2026-08-13 sur « autonomous AI dev pipeline »,
« agent orchestration CI » et « token budget LLM agents ».

| # | source | consulté le |
|---|---|---|
| S1 | *Closed-Loop Autonomous Software Development via Jira-Integrated Backlog Orchestration* (arXiv, preprint 2026) — comptabilité d'état terminal, portes de revue humaine et contrôle de débit d'un backlog piloté par agents — <https://www.arxiv.org/pdf/2604.05000> | 2026-08-13 |
| S2 | Microsoft — *Agent Governance Toolkit, AGT Audit & Compliance Specification 1.0* — journal d'audit chaîné par hachage, reconstruction de décision, non-répudiation — <https://microsoft.github.io/agent-governance-toolkit/specs/AUDIT-COMPLIANCE-1.0/> | 2026-08-13 |
| S3 | DebuggAI — *Your CI Is Green Because It Never Logged In: Why PR Pipelines Miss Workflow Breakage in the Age of AI Coding* — le vert prouve les vérifications écrites, jamais le résultat attendu — <https://debugg.ai/resources/your-ci-is-green-because-it-never-logged-in> | 2026-08-13 |
| S4 | *untilgreen* — portes déterministes : le code de sortie décide seul, l'agent ne prononce jamais son propre succès — <https://github.com/rafiu007/untilgreen> | 2026-08-13 |
| S5 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers* (2026) — plafonds par requête / par session / par clé, et disjoncteurs, appliqués hors du processus de l'agent — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026-08-13 |

S5 éclaire le plafond `--max-budget-usd 5.00` de `pipeline-challenge.yml:155`
(§ P2-1) : le plafond par invocation est en place et fonctionne, mais rien
ne mesure ce que coûte une invocation dont le livrable n'est jamais publié.
Je n'en fais pas un constat séparé faute d'avoir pu lire le coût réel du
run.

---

Fin de l'audit. `status: PROPOSED` — aucune autorisation d'exécution n'est
accordée ni suggérée ici (`CLAUDE.md` › Single Source of Instruction).
