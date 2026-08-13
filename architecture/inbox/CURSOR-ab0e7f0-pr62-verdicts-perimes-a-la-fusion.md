---
audit_id:                CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion
auditor:                 cursor-cloud
target_branch:           forge-bot/review-CURSOR-a600532-fusion-sans-contre-audit-31673848038
target_commit:           ab0e7f0c0a2b7bf313e9cc8d86b8188eb143072e
created_at:              2026-08-13T09:05:00Z
audit_type:              pull-request-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Critique de la pull request #62 — « challenge : revue de l'audit CURSOR-a600532-fusion-sans-contre-audit »

Audit produit selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat) et le contrat
`architecture/agents/cursor-auditor.md`. Cet audit **n'instruit rien** : il
propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006).

## 1. Résumé exécutif

La PR #62 ajoute **un seul fichier** de 107 lignes,
`architecture/reviews/CLAUDE-CURSOR-a600532-fusion-sans-contre-audit.md` :
le contre-audit de Claude sur l'audit `CURSOR-a600532`. Sur la forme, c'est
un bon artefact — 18 lignes de verdict lisibles par la machine, couverture
complète des 7 constats de l'audit, périmètre respecté (rien hors
`architecture/reviews/**`), et plusieurs de ses mesures se rejouent à
l'identique chez moi.

Trois choses ne tiennent pas, et toutes les trois portent sur le **décalage
entre ce que l'artefact dit à la machine et ce qu'il dit à un humain** :

1. La PR a été fusionnée **31 secondes après son ouverture**, soit
   **6 secondes après le lancement** du job qui invoque son auditeur. Le
   maillon que l'ADR-0010 appelle « critique » ne pouvait pas exister à
   temps : son livrable est une autre PR (P0-1).
2. Un verdict `CONFIRMED` **périmé** est entré dans la décision automatique.
   La revue confirme que `sim/tests/` ne tourne dans aucun job de CI ; c'était
   vrai sur la base qu'elle a mesurée (06:26 UTC), c'était faux sur la base
   dans laquelle elle a fusionné (le job `sim-tests` est sur `master` depuis
   08:28 UTC, et il a même tourné vert **sur cette PR**). Le point 12 a
   pourtant été retenu par `policy:auto`, ce qui rend la proposition 3 du § 8
   de l'audit un travail déjà fait (P1-1).
3. La description de la PR annonce « 11 CONFIRMED, 1 PARTIAL ». Le tableau
   du fichier dit 13 / 4 / 1, le registre dit 16 / 2 / 6 / 2. Trois chiffres,
   aucun identique, et le **seul `REFUTED`** — celui qui établit qu'une
   déclaration de l'audit est fausse — n'apparaît pas dans la description
   (P1-2).

État au moment de cet audit : la PR est **déjà fusionnée** (`96d1565`,
08:34:57Z) et la boucle a déjà tourné derrière elle — `AUDIT_CHALLENGED`
puis `AUDIT_APPROVED` par `policy:auto` à 08:35:12Z, avec
`retained_points: [1..16, 18]`. Aucun de mes constats ne peut donc plus
bloquer cette fusion ; ils qualifient les portes manquantes, pas cette PR
prise isolément.

## 2. Provenance et intention (lentille 1 : intention avant diff)

| Élément | Valeur |
|---|---|
| PR | #62, ouverte 2026-08-13T08:34:26Z, fusionnée (squash) 08:34:57Z par `PLiagre` |
| Tête auditée | `ab0e7f0c0a2b7bf313e9cc8d86b8188eb143072e` (commit unique, auteur `forge-bot`, 06:32:51Z) |
| Branche | `forge-bot/review-CURSOR-a600532-fusion-sans-contre-audit-31673848038` |
| Diff | 1 fichier, +107 / −0, ajout pur |
| Commit de fusion | `96d15654ed43183602e748d472f5944b9ef59643` |

**Intention annoncée** (description de la PR, extrait) : « Contre-audit
produit par **claude-challenger** (workflow `pipeline-challenge`, run
31673848038 — le quota d'abonnement Claude est de retour) […] Contenu :
uniquement `architecture/reviews/CLAUDE-CURSOR-a600532-fusion-sans-contre-audit.md`
(verdicts par point : 11 CONFIRMED, 1 PARTIAL […]) ».

**Intention contractuelle** (`architecture/agents/claude-challenger.md`, via
`harness/audit_review.py` : « vérifier la *véracité technique* de l'audit,
pas sa valeur métier », un verdict par point). Vérifié : les 18 lignes du
tableau couvrent les 7 constats de l'audit (`P0-1`, `P1-1`, `P1-2`, `P1-3`,
`P2-1`, `P2-2`, `P3-1`) et leurs sous-preuves, sans lacune. Le périmètre est
respecté : le seul chemin touché est dans l'allowlist du merge-bot
(`.github/workflows/merge-bot.yml:50`).

Le diff résout donc **le bon problème**. Ce qui ne va pas est ailleurs : la
fraîcheur des preuves et la fidélité des surfaces de lecture.

## 3. Constats

### P0-1 — Fusionnée 6 secondes après le lancement de son auditeur : le maillon « critique » d'ADR-0010 ne pouvait pas peser

Chronologie, à la seconde, tirée de l'API GitHub :

| Heure (UTC) | Événement | Preuve |
|---|---|---|
| 08:34:26 | PR #62 ouverte | `gh pr view 62 --json createdAt` |
| 08:34:31 | job `invoke-cursor-auditor` **démarre** (workflow `pipeline-audit`, run `31682657161`) | `gh api .../actions/runs/31682657161/jobs` → `started_at` |
| 08:34:42 | auto-fusion **armée** (squash) par `merge-bot` (run `31682657145`) | `autoMergeRequest.enabledAt`, et l'étape `gh pr merge --auto` du log ne produit **aucun** message de refus |
| 08:34:51 | job `invoke-cursor-auditor` se termine (`success`, 20 s) | idem `completed_at` |
| 08:34:57 | PR **fusionnée** | `gh pr view 62 --json mergedAt` |
| 08:35:12 | `AUDIT_CHALLENGED` puis `AUDIT_APPROVED` (`policy:auto`) | `architecture/audit-ledger.jsonl` sur `master` |

Le problème n'est pas une lenteur : le job d'invocation a mis 20 secondes et
il a réussi. Le problème est **structurel**. Le livrable d'un auditeur est,
par contrat, *une autre PR* qui touche `architecture/inbox/**`
(`architecture/agents/cursor-auditor.md` › Sorties). Un livrable qui est une
PR distincte ne peut jamais être une vérification (« check ») de la PR
qu'il critique : il n'existe pas encore quand la fusion se décide, et rien
dans `merge-bot.yml` ne l'attend. Sur une PR de bot dont toutes les
vérifications passent en une vingtaine de secondes, l'audit d'ADR-0010 est
donc **décoratif par construction**.

Honnêteté sur la récurrence (lentille : pas de rubber-stamping inverse) :
ce motif est déjà consigné deux fois. `CURSOR-063d7eb` P2-6 mesurait une
porte rendant son verdict 10 secondes après la fusion ; `CURSOR-a600532`
P0-1 mesurait une fusion pendant une panne de 16 h du contre-audit, et sa
proposition 1 du § 8 (« que l'adjudication d'un audit soit un préalable
observable à la fusion ») vient précisément d'être retenue par la décision
automatique de cette même fusion. **Je ne propose donc aucun brief ici.**
L'élément nouveau est la mesure : cette fois le maillon était vivant,
financé et rapide, et il n'a quand même rien pu peser — ce qui montre que
le manque est un **lien de dépendance**, pas une question de disponibilité
ni de budget.

### P1-1 — Un verdict `CONFIRMED` périmé est entré dans la décision automatique ; la proposition 3 du § 8 de l'audit est du travail déjà fait

Ligne 12 du tableau de la revue :

> « P2-2 — au commit fusionné, `sim/tests/` ne tourne dans aucun job de CI ;
> `harness-ci` n'exécute que `harness/tests/` | CONFIRMED |
> […] `grep -rn "sim" .github/workflows/*.yml` : aucune correspondance dans
> aucun workflow. »

C'était vrai sur la base réelle de la revue, et c'est faux sur la base dans
laquelle elle a fusionné :

```
$ git merge-base ab0e7f0 origin/master
4acb8e21280464bc92edae1dcf2b740020ebfaa1        # base de la revue, 06:26:37Z

$ git log --oneline -S"sim-tests" -- .github/workflows/harness-ci.yml
444ec45 generateur: lot 012 — … CI sim/tests, R1-R3

$ git merge-base --is-ancestor 444ec45 ab0e7f0 && echo OUI || echo NON
NON        # la revue a mesuré une base où sim-tests n'existait pas

$ git merge-base --is-ancestor 444ec45 d61f02d && echo OUI || echo NON
OUI        # mais sim-tests était sur master depuis 08:28:16Z, avant l'ouverture de la PR

$ rg -n "sim-tests|sim/tests" .github/workflows/harness-ci.yml
8:#   * sim-tests -- the sim/ unit and integration test suite (brief 012, SC6).
38:  sim-tests:
48:        run: python -m pytest sim/tests/ -v
```

Plus direct encore : le job `sim-tests` a **tourné vert sur cette PR même**
(`gh pr checks 62` → `sim-tests  pass  15s`, run `31682657292`). La revue
affirme donc, dans une PR dont la CI exécute `sim/tests/`, que `sim/tests/`
ne tourne dans aucun job de CI.

Conséquence machine, et c'est là que ça coûte : `policy:auto` retient le
point 12 (`architecture/decisions/DECISION-CURSOR-a600532-fusion-sans-contre-audit.md`,
`retained_points: [1, …, 16, 18]`). La proposition 3 du § 8 de l'audit
(« Faire exécuter `sim/tests/` par la CI ») est ainsi validée alors qu'elle
est **déjà réalisée** ; convertie en brief, elle produirait un lot vide.

Aggravant interne : la revue **sait** traiter la péremption. Elle le fait
explicitement aux lignes 11 (« le compte d'inbox est maintenant 26 au lieu de
25 : différence attendue ») et 14 (« écart cohérent avec le temps écoulé »),
où la dérive ne change qu'un nombre. Elle ne le fait pas à la ligne 12, où
la dérive **inverse le verdict**. C'est le piège n° 6 de
`review-guidelines.md` (succès affirmé non mesuré) dans sa variante
symétrique : un défaut confirmé qui n'existe plus. La littérature 2026
appelle précisément cela une preuve périmée, et la contre-mesure est de lier
chaque preuve à l'état de source suivi au moment de la décision, pas au
moment de la mesure [S1, S2].

### P1-2 — La description de la PR affirme des verdicts qui n'existent nulle part, et efface le seul `REFUTED`

Trois comptes, trois valeurs, aucune identique :

| Surface | Contenu | Preuve |
|---|---|---|
| Description de la PR (surface humaine) | « 11 CONFIRMED, 1 PARTIAL » | corps de la PR #62 |
| Tableau du fichier (surface machine des décisions) | 13 `CONFIRMED`, 4 `PARTIAL`, 1 `REFUTED` sur 18 lignes | `audit_decision.parse_point_verdicts` (§ 5) |
| Registre `audit-ledger.jsonl` (journal de la boucle) | `{"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}` | ligne réelle sur `master` (§ 5) |

Le plus lourd n'est pas l'écart arithmétique, c'est **l'effacement** : la
description ne mentionne pas le `REFUTED` de la ligne 17, qui est le verdict
le plus décisionnel de toute la revue (il établit qu'une déclaration de
non-duplication de l'audit est factuellement fausse). Un relecteur qui s'en
tient à la description croit lire « tout confirmé, une nuance ».

Et il n'avait pas d'autre choix que la description : l'auto-fusion a été
armée **16 secondes** après l'ouverture (P0-1). Dans une boucle sans humain
(`harness/pipeline/config.yaml:33` → `mode: full_auto`), la description d'une
PR de bot est la seule chose écrite pour un humain ; si elle est fausse,
elle est pire que vide.

Récurrence assumée, avec escalade justifiée : `CURSOR-063d7eb` P3-8 avait
déjà signalé que l'intention d'une PR de revue est illisible sans ouvrir le
fichier — classé P3 parce que la description était alors seulement
**muette** (le gabarit du workflow ne dit que qui a produit et ce que la
fusion déclenche). Ici la description a été **rédigée à la main** et elle
**affirme faux**. Muet et faux ne sont pas la même classe de défaut : d'où
P1. La contre-mesure publiée est connue — rendre la zone factuelle du corps
de PR déterministe, produite par le même analyseur que la décision, et
laisser la prose du modèle dans une zone séparée [S3].

### P2-1 — Le registre a inscrit une troisième fois des comptes de verdicts faux (16 / 2 / 6 / 2)

`harness/audit_review.py:127-134` compte les **occurrences de mots** dans
tout le texte, y compris la phrase de gabarit « Un verdict par point :
CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER » et toute la prose de synthèse.
C'est ce compte, pas celui du tableau, qui part au registre
(`audit_review.py:195-204`). Ligne réellement écrite sur `master` :

```
{"timestamp": "2026-08-13T08:35:12Z", "audit_id": "CURSOR-a600532-fusion-sans-contre-audit",
 "event": "AUDIT_CHALLENGED", "actor": "claude", …,
 "verdicts": {"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}}
```

Le fichier ne contient ni 2 `REFUTED` (il y en a 1) ni aucun `NEEDS_OWNER`
en verdict (il y en a 0 ; les 2 comptés viennent du gabarit et du titre du
§ 3).

**Aucun brief proposé.** Ce motif est déjà consigné deux fois —
`CURSOR-779d97c` P1-3 et `CURSOR-063d7eb` P1-2 — et n'a **jamais été
arbitré** : `CURSOR-779d97c` est resté à `AUDIT_CHALLENGED` (seule ligne au
registre, aucun fichier dans `architecture/decisions/`). Le re-proposer
gonflerait la file sans rien ajouter. Le seul élément nouveau vaut d'être
noté : c'est la première fois que ces comptes faux cohabitent avec une
décision automatique **aboutie** — le journal de la boucle est donc faux à
l'endroit exact où il devient une décision.

### P2-2 — Le tableau et la synthèse de la revue ne comptent pas la même chose

Le § 4 de la revue écrit : « sur 16 points techniques vérifiables
indépendamment […] 13 sont CONFIRMED sans réserve, 1 est CONFIRMED avec une
réserve mineure […] et 2 sont PARTIAL ». Le tableau porte **18** lignes
(13 `CONFIRMED`, 4 `PARTIAL`, 1 `REFUTED`). La ligne 3 est `PARTIAL` dans le
tableau et décrite comme « CONFIRMED avec une réserve » dans la prose.

Ce n'est pas un détail de style : la machine lit le tableau
(`audit_decision.py:76`), l'humain lit la synthèse. Les deux surfaces du même
fichier donnent deux inventaires différents. Pas de brief proposé — c'est
éditorial —, mais c'est la même famille de défaut que P1-2, et cela
renforce la proposition 2 du § 8.

### P2-3 — `architecture/reviews/**` n'a toujours aucune porte de schéma, et `reviewed_at` en donne l'illustration

`harness/audit_schema.py` ne regarde que l'inbox (`INBOX` ligne 26,
`validate_inbox` lignes 92-98, `glob("CURSOR-*.md")`), et c'est ce script que
le job `schema` d'`audit-guard.yml` exécute. Rien ne vérifie, au moment de la
PR, qu'un fichier de revue a un frontmatter cohérent.

Illustration mesurable dans cette PR : le frontmatter déclare
`reviewed_at: 2026-08-13T07:15:00Z`, alors que le commit qui contient ce
fichier est daté `2026-08-13T06:32:51Z`. La revue se déclare relue
**42 minutes après** avoir été committée — un horodatage impossible, écrit à
la main par le modèle (`audit_review.py:70`, champ laissé en `<<TODO>>` dans
le gabarit).

**Aucun brief proposé** : `CURSOR-779d97c` P2-6 (absence de schéma côté
`reviews/`) et `CURSOR-063d7eb` P2-5 + brief 3 (horodater `reviewed_at` par
la machine) couvrent exactement ce point et attendent encore l'arbitrage.
Je n'apporte que la nouvelle occurrence.

### P2-4 — La revue vérifie la moitié de la déclaration qu'elle réfute, et re-cite l'autre moitié sans la tester

La ligne 17 réfute la déclaration de non-duplication de l'audit et la cite
ainsi : « Les douze briefs de `harness/queue/briefs/**` ont été relus…
Aucun n'est ouvert : chacun porte un verdict tracé `ACCEPT` ». La revue teste
la première moitié (« aucun n'est ouvert ») et a raison — j'ai rejoué :

```
008-contexte-opus5-right-sizing | AUCUN verdict.md
```

et j'ai confirmé au fond que sa Success Condition 1 n'est pas réalisée
(`docs/rules/prompt-defense-baseline.md` absent, bloc « Prompt Defense
Baseline » encore présent dans les trois fichiers `.claude/agents/forge-*.md`).

Mais elle re-cite la seconde moitié (« chacun porte un verdict tracé
`ACCEPT` ») sans la tester, et cette moitié est fausse aussi : quatre briefs
portent `REJECT` comme dernier verdict tracé.

```
001-spatial-primary-key-adr | VERDICT: REJECT
002-geo-pipeline-coastline-1400 | VERDICT: REJECT
005-refonte-visuelle-carte | VERDICT: REJECT
007-geo-pipeline-cells-adjacency | VERDICT: REJECT
```

Une citation reprise telle quelle dans un document dont la fonction est de
vérifier les citations est un angle mort de méthode (lentille 4 : le
cadrage adverse doit s'appliquer à chaque affirmation, pas seulement à celle
qu'on a choisi d'attaquer). Pas de brief : le correctif est une discipline
de relecture, pas du code.

### P2-5 — La question de gouvernance nommée par le § 3 n'a aucune existence machine

Le § 3 de la revue nomme lui-même un arbitrage propriétaire (« est-ce que la
boucle à quatre acteurs doit bloquer la fusion tant que le contre-audit n'a
pas statué ? ») mais n'émet **aucune ligne `NEEDS_OWNER`** dans le tableau.
`audit_decision.decide_auto` applique alors la règle 2 avant toute autre
(`audit_decision.py:283-290`) : dès qu'un point est `CONFIRMED` ou `PARTIAL`,
c'est `APPROVED`, et la question disparaît — le fichier de décision produit
ne contient que la liste des numéros retenus.

Je **ne rouvre pas** le fond : « pas de propriétaire en `full_auto` » est une
décision enregistrée (ADR-0006, règle `review_needs_owner_only`), et la
re-discuter serait du bruit. Je note seulement que la seule surface où cette
question survit est de la prose, dans un fichier que la boucle ne relit plus
après la décision. Pas de brief.

### P3-1 — Une imprécision de citation, et ce que la revue fait bien

Imprécision : la ligne 18 attribue les sources externes S1–S6 au « § 9 » de
l'audit ; elles sont au **§ 10** (`CURSOR-a600532…md:651`). Le § 9 est bien
l'endroit de la déclaration de non-duplication (ligne 639), donc la ligne 17
cite juste.

Ce qui tient, et qu'il ne faut pas défaire en corrigeant le reste :

| Affirmation de la revue | Rejeu indépendant | Résultat |
|---|---|---|
| Ligne 16 : `orchestrator.py` ligne 146, texte verbatim | `sed -n '146p' harness/pipeline/orchestrator.py` | `… "no audit_id in payload; AUDIT_PROPOSED is optional"` — identique |
| Ligne 9 : `dashboard.py` n'émet une action que pour `AUDIT_APPROVED` | `rg -n "AUDIT_APPROVED" hermes/dashboard.py` | ligne 235 : `if audit["event"] in ("AUDIT_APPROVED",):` — exact |
| Ligne 17 : `008-contexte-opus5-right-sizing` est ouvert, SC1 non réalisée | inventaire des 13 briefs + `ls docs/rules/` + `rg "Prompt Defense Baseline" .claude/agents/*.md` | confirmé (§ 5) |
| Couverture : un verdict par point de l'audit | `rg -n "^## P[0-3]-" ` sur l'audit → 7 constats | les 7 reçoivent un verdict, aucune lacune |
| Lisibilité machine du tableau | `audit_decision.parse_point_verdicts` | **18** lignes captées — la boucle n'a pas calé, contrairement au cas `bb8fe11` cité par `audit_review.py:180-186` |

Trois qualités à nommer : la **couverture** est complète ; le **périmètre**
est tenu (un seul fichier, dans l'allowlist) ; les **limites sont
déclarées** plutôt que devinées — l'en-tête explique que `gh` n'était pas
authentifié et que deux endpoints sont restés hors de portée, et les lignes 3
et 15 délimitent honnêtement ce qui n'a pas pu être re-observé. C'est
exactement la discipline que la lentille 2 exige.

## 4. Lentille « taille et découpage »

1 fichier, +107 / −0. Très en dessous du seuil (~400 lignes) au-delà duquel
une relecture honnête décroche. **Aucun `NEEDS_SPLIT`** à signaler.

À noter tout de même : la discipline de taille a échoué **là où on ne la
mesure pas**. Le diff est petit, mais la seule surface réellement lue avant
la fusion — la description — est celle qui contredit le contenu (P1-2). Un
diff court ne garantit pas une relecture correcte s'il est résumé faux.

## 5. Commandes rejouées et sorties

Toutes les commandes ci-dessous ont été exécutées dans un checkout en
lecture seule du dépôt, le 2026-08-13.

**(a) Ce que le tableau dit, et ce que le registre reçoit** — les deux
analyseurs, sur le fichier de la PR (`git show ab0e7f0:…`) :

```
$ .venv/bin/python -c "…audit_review.parse_verdicts / audit_decision.parse_point_verdicts…"
parse_verdicts (ce qui part au registre) : {'CONFIRMED': 16, 'REFUTED': 2, 'PARTIAL': 6, 'NEEDS_OWNER': 2}
parse_point_verdicts : 18 lignes
compte par verdict (table) : Counter({'CONFIRMED': 13, 'PARTIAL': 4, 'REFUTED': 1})
```

**(b) Simulation du chemin post-fusion** (bac à sable `/tmp`, registre et
dossier de décisions temporaires — aucune écriture dans le dépôt) :

```
1) AUDIT_CHALLENGED ecrit : {… "verdicts": {"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}}
2) decision auto : {… "event": "AUDIT_APPROVED", "actor": "policy:auto",
   "retained_points": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 18]}
```

**(c) Ce que la boucle a réellement écrit** — même résultat, sur `master`
(`git show origin/master:architecture/audit-ledger.jsonl | rg a600532`) :

```
{"timestamp": "2026-08-13T08:35:12Z", …, "event": "AUDIT_CHALLENGED", "actor": "claude",
 "verdicts": {"CONFIRMED": 16, "REFUTED": 2, "PARTIAL": 6, "NEEDS_OWNER": 2}}
{"timestamp": "2026-08-13T08:35:12Z", …, "event": "AUDIT_APPROVED", "actor": "policy:auto",
 "reason": "policy: ledger_AUDIT_APPROVED_retained_points_confirmed_union_partial …",
 "retained_points": [1, …, 16, 18]}
```

La simulation et la réalité coïncident exactement : mes constats P1-1 et
P2-1 ne dépendent pas d'une reconstitution, ils sont sur `master`.

**(d) Péremption du point 12** (voir § P1-1 pour les sorties complètes) :

```
$ git show a600532:.github/workflows/harness-ci.yml | rg -n "sim"
(aucune occurrence)
$ rg -n "sim-tests" .github/workflows/harness-ci.yml
38:  sim-tests:
$ gh pr checks 62 | rg sim
sim-tests	pass	15s	…/runs/31682657292/job/94391434011
```

**(e) Vérification de la ligne 17 de la revue** (inventaire des briefs et
état de la Success Condition 1 du brief ouvert) :

```
$ for d in harness/queue/briefs/*/; do … done
001-spatial-primary-key-adr | VERDICT: REJECT
002-geo-pipeline-coastline-1400 | VERDICT: REJECT
003-port-unity-game | VERDICT: ACCEPT
004-polish-visuel | VERDICT: ACCEPT
005-refonte-visuelle-carte | VERDICT: REJECT
006-full-auto-agent-pipeline | VERDICT: ACCEPT
007-geo-pipeline-cells-adjacency | VERDICT: REJECT
008-contexte-opus5-right-sizing | AUCUN verdict.md
008-full-auto-automation-gaps | VERDICT: ACCEPT
009-full-auto-agent-invocation | VERDICT: ACCEPT
010-repartition-roles-full-auto | VERDICT: ACCEPT
011-sim-monde-vivant-amorcage | VERDICT: ACCEPT
012-monde-vivant-commerce-inter-cellules | VERDICT: ACCEPT

$ ls docs/rules/prompt-defense-baseline.md
docs/rules/prompt-defense-baseline.md ABSENT
$ rg -c "Prompt Defense Baseline" .claude/agents/*.md
.claude/agents/forge-planificateur.md:1
.claude/agents/forge-evaluateur.md:1
.claude/agents/forge-generateur.md:1
```

**(f) Chronologie de la fusion** :

```
$ gh pr view 62 --json createdAt,mergedAt,autoMergeRequest
createdAt 2026-08-13T08:34:26Z | mergedAt 2026-08-13T08:34:57Z
autoMerge.enabledAt 2026-08-13T08:34:42Z (SQUASH, PLiagre)

$ gh api repos/PLiagre/ForgeHistory/actions/runs/31682657161/jobs
invoke-cursor-auditor  started 08:34:31Z  completed 08:34:51Z  success
```

## 6. Classification de la CI du commit audité

**Verte.** Aucun job rouge, ni sur la tête auditée `ab0e7f0`, ni sur le
commit de fusion `96d1565`.

| Commit | Workflow | Événement | Conclusion |
|---|---|---|---|
| `ab0e7f0` | `harness-ci` (`tests`, `sim-tests`, `f0-demo`) | `pull_request` + `push` | `success` |
| `ab0e7f0` | `audit-guard` (`schema`) | `pull_request` + `push` | `success` (`cursor-scope` `skipped` : branche non `cursor/*`) |
| `ab0e7f0` | `security` (`gitleaks`, `actionlint`) | `pull_request` + `push` | `success` |
| `ab0e7f0` | `merge-bot` (`check-and-automerge`) | `pull_request` | `success` — et c'est ce job qui arme l'auto-fusion (P0-1) |
| `ab0e7f0` | `pipeline-audit` (`invoke-cursor-auditor`) | `pull_request` | `success` (20 s) |
| `96d1565` | `harness-ci`, `audit-guard`, `security`, `pipeline-audit`, `pipeline-orchestrate`, `hermes-dashboard` | `push` | `success` |
| `96d1565` | `pipeline-failure-escalate` | `workflow_run` | `skipped` (attendu : rien n'a échoué) |
| `ab0e7f0` / `96d1565` | `hermes-observer` | `workflow_run` | 1 `cancelled`, **≥ 6 `queued`** jamais démarrés |

Portes mécaniques rejouées localement sur la tête courante, pour vérifier que
le vert de la CI n'est pas déclaratif :

```
$ python3 harness/audit_schema.py
All 28 audit(s) valid.
$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 16.89s
```

(les 16 tests ignorés sont les cas Unity/PowerShell, hors de portée sur
Linux — comportement attendu, pas un échec.)

Un seul point d'attention hors constats : les exécutions de
`hermes-observer` s'empilent en file (`queued`) sans jamais démarrer
(`gh run list --workflow hermes-observer.yml` : 5 des 5 dernières en
`queued`). Ce n'est pas causé par cette PR et je ne le compte pas comme
constat ; je le signale parce qu'un observateur qui ne démarre jamais
n'observe rien.

## 7. Risques par sévérité

| Sévérité | Constat | Portée |
|---|---|---|
| **P0** | P0-1 — le maillon critique d'ADR-0010 ne peut pas exister avant la fusion (6 s d'écart mesurés) | Toute PR de bot dont la CI passe vite se fusionne sans qu'aucun audit ait pu être lu ; la chaîne à quatre acteurs reste déclarative |
| **P1** | P1-1 — verdict `CONFIRMED` périmé retenu par `policy:auto` (point 12) | Une proposition déjà réalisée est validée ; convertie, elle produit un brief vide et consomme un tour de boucle |
| **P1** | P1-2 — description de PR fausse (11/1) et effacement du seul `REFUTED` | La seule surface humaine disponible avant l'auto-fusion induit en erreur |
| **P2** | P2-1 — troisième inscription de comptes de verdicts faux au registre (16/2/6/2) | Journal de la boucle faux à l'endroit où il devient décision ; motif déjà consigné, jamais arbitré |
| **P2** | P2-2 — tableau (18 points) et synthèse (16 points) divergents dans le même fichier | Deux inventaires pour un artefact ; lecture humaine et lecture machine ne concordent pas |
| **P2** | P2-3 — aucune porte de schéma sur `reviews/**` ; `reviewed_at` daté 42 min après le commit | Traçabilité d'une revue non vérifiable mécaniquement |
| **P2** | P2-4 — moitié de la déclaration réfutée re-citée sans être testée (4 briefs `REJECT`) | Un contre-audit propage une affirmation fausse qu'il était en position de démentir |
| **P2** | P2-5 — arbitrage propriétaire nommé en prose, absent du tableau | En `full_auto`, la question s'éteint avec la décision ; aucune trace au registre |
| **P3** | P3-1 — sources externes attribuées au § 9 au lieu du § 10 | Coût de vérification légèrement accru ; aucune conséquence sur un verdict |

## 8. Briefs atomiques proposés (2 — sous le plafond de 3)

Rappel : un audit **ne pré-autorise rien**. Ces propositions n'ont valeur
d'instruction qu'après conversion explicite en brief par le propriétaire
(`CLAUDE.md` › Single Source of Instruction). Les trois flags
`*_authorized` de cet audit sont à `false`.

**Brief 1 — Lier un verdict à la base réelle de la PR, et refuser un verdict
périmé (P1-1).**
Portée : `harness/audit_review.py` (le gate `record`),
`harness/audit_decision.py` (avant `decide_auto`). Objet : qu'un contre-audit
déclare mécaniquement le commit sur lequel ses mesures ont été prises, et que
la décision refuse — ou marque explicitement comme périmé — tout point dont
la base de mesure n'est plus un ancêtre de la base de fusion. Preuve rouge
attendue avant correctif : un test qui rejoue le cas de cette PR — un point
`CONFIRMED` mesuré sur `4acb8e2` alors que la base de fusion contient
`444ec45` — et qui échoue aujourd'hui parce que le point est retenu sans
réserve. Doctrine externe : preuve liée à l'état de source suivi, blocage ou
re-mesure quand elle est périmée [S1, S2, S4].

**Brief 2 — Rendre factuelle la partie factuelle de la description d'une PR
de bot (P1-2, renforcé par P2-2).**
Portée : `.github/workflows/pipeline-challenge.yml` (étape « Publish the
review as a pull request », `gh pr create --body`) et un petit rendu partagé
avec `harness/audit_decision.parse_point_verdicts`. Objet : que le corps
d'une PR de revue comporte une **zone produite par la machine** (nombre de
points, répartition des verdicts, liste des points non `CONFIRMED`) issue du
même analyseur que la décision, la prose libre restant dans une zone
séparée et clairement marquée. Preuve rouge attendue : un test qui compare
les chiffres du corps généré au résultat de `parse_point_verdicts` et qui
échoue aujourd'hui, le corps étant un texte libre. Note de périmètre : ce
brief touche `.github/workflows/**`, donc le denylist du merge-bot — jamais
auto-mergeable, arbitrage propriétaire requis. Doctrine externe : corps de PR
à deux zones, zone factuelle rendue déterministe [S3].

Aucun troisième brief. Les autres constats sont soit déjà consignés et non
arbitrés (P2-1, P2-3), soit tranchés par une décision enregistrée (P2-5,
ADR-0006), soit éditoriaux (P2-2, P2-4, P3-1) — les proposer gonflerait la
file sans rien ajouter.

## 9. Déclaration de non-duplication

Leçon tirée du § P2-4 : je vérifie ce que je déclare, et je déclare ce que
j'ai vérifié.

- **Briefs.** Les 13 dossiers de `harness/queue/briefs/**` ont été
  inventoriés (sortie complète en § 5 (e)). Neuf portent un dernier verdict
  `ACCEPT`, **quatre** portent `REJECT` (001, 002, 005, 007), et **un est
  ouvert sans aucun verdict** : `008-contexte-opus5-right-sizing`. Son
  contenu (déduplication du bloc « Prompt Defense Baseline », registre du ton
  de l'Évaluateur) ne recoupe ni la fraîcheur des verdicts (brief 1) ni le
  corps des PR de bot (brief 2).
- **Audits déjà déposés.** P0-1 est couvert par la proposition 1 du § 8 de
  `CURSOR-a600532`, désormais `AUDIT_APPROVED` : je n'ajoute pas de brief,
  seulement la mesure. P2-1 est couvert par `CURSOR-779d97c` P1-3 et
  `CURSOR-063d7eb` P1-2 (tous deux non arbitrés). P2-3 est couvert par
  `CURSOR-779d97c` P2-6 et le brief 3 du § 6 de `CURSOR-063d7eb`.
- **Décisions enregistrées.** L'absence de propriétaire en `full_auto`
  (ADR-0006) et l'indisponibilité de la protection de branche (dérogation
  consignée dans `.github/merge-bot.yaml`) ne sont pas réouvertes.

## 10. Sources externes

Recherche du jour, consultée le **2026-08-13**, sur les trois thèmes exigés
par le contrat (`autonomous AI dev pipeline`, `agent orchestration CI`,
`token budget LLM agents`).

| # | source | date de publication | consulté le |
|---|---|---|---|
| S1 | *Proof-or-Stop: Don't Trust the Agent, Trust the Evidence — Loop Engineering for Verifiable Evidence-Gated Lifecycle Control*, arXiv:2607.14890 — <https://arxiv.org/html/2607.14890v1> — toute sortie d'acteur est une *prétention* ; elle n'est admise que si la preuve est fraîche et **liée à l'état de source suivi** ; preuve manquante, périmée ou incomplète → réparation bornée, dégradation honnête, escalade ou arrêt | 2026-07 (identifiant arXiv) | 2026-08-13 |
| S2 | `sethdford/shipwright` — *Code Factory control-plane* — <https://github.com/sethdford/shipwright> — « SHA discipline : all checks, reviews and approvals validated against current PR head — stale evidence is never trusted » ; porte de risque en préflight avant CI coûteuse | dépôt vivant (2026) | 2026-08-13 |
| S3 | *Supspec Orchestration — From Spec to Evidenced Draft PRs, Autonomously* — <https://viblo.asia/p/supspec-orchestration-from-spec-to-evidenced-draft-prs-autonomously-1j4lQzGAJwl> — corps de PR à **deux zones** : un bloc `Auto` rendu déterministiquement depuis l'enregistrement d'exécution, un bloc `Asserted` seul endroit où le modèle écrit de la prose ; « evidence is fingerprinted, not narrated » | 2026 (page éditeur) | 2026-08-13 |
| S4 | Augment Code — *How AI Agent Verification Prevents Production Bugs Before Merge* — <https://www.augmentcode.com/guides/ai-agent-pre-merge-verification> — une vérification ne change l'issue que si elle est une **porte obligatoire à un point défini** du flux ; l'agent la joue en interne **et** la CI la rejoue en porte dure qu'il ne peut pas contourner | documentation vivante (2026) | 2026-08-13 |
| S5 | *LLM Token Budget Strategies for Agents: 5 Layers* — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> — l'application d'un budget doit vivre **hors du code de l'agent** (passerelle), sinon un agent bogué saute son propre contrôle ; cinq couches, dont le disjoncteur sur la vélocité de dépense | 2026 (page éditeur) | 2026-08-13 |
| S6 | *Token Budgets: An Empirical Catalog of 63 LLM-Agent Budget-Overrun Incidents*, arXiv:2606.04056 — <https://arxiv.org/html/2606.04056v1> — catalogue empirique des dépassements ; le motif « budget-primitive-missing » (aucun plafond dans le cadre) est structurel, un plafond d'exécution ne borne les autres qu'au niveau de la conséquence | 2026-06 (identifiant arXiv) | 2026-08-13 |

Les cinq sources internes de `architecture/review-guidelines.md` (S1–S5 de
ce fichier) restent celles qui définissent la **forme** de cet audit : six
lentilles, sévérités, une preuve par constat.

## 11. Ce que cet audit ne fait pas

- Il ne juge pas la valeur métier des constats de `CURSOR-a600532` : ce
  n'est pas son rôle, et la revue auditée a déjà fait ce travail de
  véracité technique.
- Il n'autorise aucune implémentation : les trois flags `*_authorized` sont
  à `false`, et aucun de mes constats ne doit être lu comme « doit être
  corrigé ». La décision appartient à la boucle.
- Il ne touche aucun fichier hors `architecture/inbox/**`, et ne modifie
  aucun audit existant (`inbox/` est append-only).
