---
audit_id:                CURSOR-1da49ea-pr43-challenge-verdicts-sans-preuve
auditor:                 cursor-cloud
target_branch:           master
target_commit:           1da49eac982124d71f35f0d10293c801d017af7f
created_at:              2026-08-12T14:05:00Z
audit_type:              pr-review
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de la PR #43 — « challenge: revue de l'audit CURSOR-0269d8e-hermes-console-droit-executer »

Critique menée selon `architecture/review-guidelines.md` (six lentilles,
sévérités P0–P3, une preuve citée par constat). Cible : la pull request
<https://github.com/PLiagre/ForgeHistory/pull/43>, commit de tête
`1da49eac982124d71f35f0d10293c801d017af7f`.

## Résumé en une page

La PR ajoute **un seul fichier**, le contre-audit produit sans intervention
humaine par `claude-challenger` :
`architecture/reviews/CLAUDE-CURSOR-0269d8e-hermes-console-droit-executer.md`
(+95 / −0). Rien d'autre. Le périmètre est propre, la CI est verte, et la
revue couvre bel et bien les neuf constats de l'audit qu'elle relit — sur ces
trois points, elle est meilleure que la moyenne de ce que le harnais a produit
jusqu'ici.

Le problème n'est pas ce que la revue dit, c'est **la force qu'elle
s'attribue**. Deux de ses douze verdicts sont marqués `CONFIRMED` alors que la
cellule de preuve, juste à côté, écrit que la vérification n'a pas pu être
faite — et un troisième point, dans exactement la même situation, est marqué
`PARTIAL`. Le rôle du contre-audit est de mesurer la véracité d'un audit ; il
ne peut pas se permettre d'être plus affirmatif que sa propre preuve.

Deux effets de bord aggravent le tableau, tous deux reproduits ici commande en
main. D'abord, la ligne de journal que la fusion écrira annonce
`"REFUTED": 3` pour une revue qui dit littéralement « Aucun `REFUTED` » — j'ai
rejoué la porte et obtenu la ligne exacte, au chiffre près, que le run CI avait
produite. Ensuite, le corps de la PR affirme que « la fusion de cette PR
déclenche `pipeline-orchestrate.yml` » : or l'auto-fusion a été **refusée** par
GitHub, le job qui la tente est resté **vert**, et les trois PR de challenge du
même lot (#42, #43, #44) sont toutes encore ouvertes une heure après. Personne
n'est prévenu que la chaîne s'est arrêtée.

Bilan : **0 P0, 3 P1, 4 P2, 2 P3**. Aucun constat ne demande de jeter la
revue ; les deux correctifs les plus utiles tiennent en quelques lignes
(re-étiqueter deux verdicts, et faire dire au job de fusion ce qu'il a vraiment
obtenu).

## Ce que la PR change

```
$ gh pr view 43 -R PLiagre/ForgeHistory --json additions,deletions,changedFiles,isDraft,author
{"additions":95,"author":{"is_bot":true,"login":"app/github-actions"},
 "changedFiles":1,"deletions":0,"isDraft":false}

$ gh pr diff 43 -R PLiagre/ForgeHistory --name-only
architecture/reviews/CLAUDE-CURSOR-0269d8e-hermes-console-droit-executer.md
```

Deux commits :

| commit | date | auteur | effet |
|---|---|---|---|
| `1a3bc17` | 2026-08-12T13:00:27Z | forge-bot | ajoute la revue **et** une ligne `AUDIT_CHALLENGED` dans `architecture/audit-ledger.jsonl` |
| `1da49ea` | 2026-08-12T13:48:40Z | cursoragent + PLiagre | retire cette ligne de ledger (convention post-#46 : elle sera écrite sur `master` après fusion) |

Le second commit est **correct et voulu** : `pipeline-challenge.yml:178-185`
explique que deux challenges simultanés entreraient en conflit d'append sur le
ledger partagé, et le commit `8ebe5f9` (« la ligne AUDIT_CHALLENGED s'écrit sur
master après fusion, plus dans la PR de challenge ») a figé cette convention.
L'état final de la PR est donc conforme à la convention en vigueur — je le
signale parce que la même PR aurait été non conforme deux heures plus tôt.

## État du dépôt et CI au commit audité

CI **verte**, sans exception :

```
$ gh pr checks 43 -R PLiagre/ForgeHistory
Reconcile local Hermes state  pass   (hermes-observer)
schema        pass  x2   (audit-guard)
tests         pass  x2   (harness-ci)
f0-demo       pass  x2   (harness-ci)
actionlint    pass  x2   (security)
gitleaks      pass  x2   (security)
check-and-automerge  pass (merge-bot)      <- voir P1-3
invoke-cursor-auditor pass (pipeline-audit)
cursor-scope  skipping x2 (audit-guard)    <- attendu : branche forge-bot/*, pas cursor/*
```

Mesures rejouées sur ce checkout (`master` à `b133c25`, qui contient bien
`0269d8e` en ancêtre) :

```
$ .venv/bin/python -m pytest harness/tests/ -q
311 passed, 16 skipped in 16.67s

$ python3 harness/audit_schema.py | tail -1
All 14 audit(s) valid.        # 14 avant l'ajout du présent fichier, 15 après

$ python3 harness/harness_audit.py | grep -E "SCORE|\[FAIL\]"
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: unexpected files in stub dirs: [...]
SCORE: 20/24
```

Les deux `[FAIL]` sont ceux que la revue elle-même signale à son point 10 ; son
chiffre `20/24` est exact. L'écart « 13 audits » (revue) contre « 14 » (ici)
s'explique par le temps écoulé, pas par une erreur.

## Constats

### P0 — aucun

Aucun élément de cette PR ne détruit d'information, ne contourne une porte, ni
ne s'attribue une autorité qu'il n'a pas. Le fichier ajouté est dans le seul
dossier que son rôle a le droit d'écrire (`architecture/README.md:30`), et
aucun chemin de code, de test ou de workflow n'est touché.

### P1-1 — deux verdicts `CONFIRMED` sur des points que la revue déclare elle-même ne pas avoir pu vérifier

C'est le constat central. Le même motif — « je n'ai pas eu accès à l'API
GitHub » — reçoit deux étiquettes différentes selon le point :

| ligne | point | verdict posé | ce que la cellule de preuve dit |
|---|---|---|---|
| 49 | 2 (P1-1 de l'audit) | `CONFIRMED` | « Le timing exact de la PR (56s, `reviews: []`) n'a pas pu être revérifié faute de jeton GitHub — voir §1. » |
| 55 | 8 (P3-1 de l'audit) | `CONFIRMED` | « …non re-vérifiables ici faute de `gh auth` — voir §1 ; **je fais confiance à la commande citée** (`gh repo view` → `PRIVATE`) **sans avoir pu la rejouer**. » |
| 58 | 11 (veille externe) | `PARTIAL` | « …je ne conteste pas leur existence ni leur teneur, **je ne l'ai simplement pas vérifiée**. » |

Le point 11 est traité correctement : preuve absente → `PARTIAL`. Les points 2
et 8 décrivent la même absence de preuve et reçoivent malgré tout `CONFIRMED`.
Le point 8 est le plus net : il porte sur un risque de sécurité (`P3-1` de
l'audit, `pull_request_target` + runner persistant), et son classement en P3
plutôt qu'en P1 **dépend** d'un atténuant que la revue dit explicitement ne pas
avoir pu constater (le dépôt est privé). Confirmer un classement de sévérité
dont on n'a pas pu vérifier la prémisse, c'est valider par confiance.

Pourquoi c'est un P1 et pas un détail de vocabulaire : le champ `verdicts` du
ledger et la décision automatique en aval (`audit_decision._parse_point_verdicts`,
`harness/audit_decision.py:185-191`) lisent ces étiquettes, pas les cellules de
preuve. J'ai vérifié que le parseur strict lit bien les douze lignes :

```
$ python3 -c "import sys; sys.path.insert(0,'harness'); import audit_decision as d; \
    print(d._parse_point_verdicts(open('<la revue>').read()))"
[(1,'CONFIRMED'),(2,'CONFIRMED'),(3,'CONFIRMED'),(4,'CONFIRMED'),(5,'CONFIRMED'),
 (6,'CONFIRMED'),(7,'CONFIRMED'),(8,'CONFIRMED'),(9,'CONFIRMED'),(10,'CONFIRMED'),
 (11,'PARTIAL'),(12,'CONFIRMED')]
```

Donc en aval, les points 2 et 8 pèsent exactement autant qu'un point rejoué
commande en main. `architecture/README.md:30` demande un verdict « avec
preuve » ; la lentille 2 du guide de critique refuse l'affirmation sans preuve
rejouable ; ici la revue **fournit elle-même le contre-exemple** dans la même
cellule.

Remède minimal, sans rien changer au fond : `PARTIAL` sur les points 2 et 8,
avec la phrase de délimitation déjà employée au point 11. Les deux constats
sont probablement justes — c'est l'étiquette qui surqualifie la preuve, pas le
raisonnement qui est faux.

### P1-2 — la ligne de journal que la fusion écrira annonce trois `REFUTED` sur une revue qui dit « Aucun `REFUTED` »

Reproduction complète, sur une copie isolée du ledger (aucune écriture dans le
dépôt) :

```
$ gh api ".../contents/architecture/reviews/CLAUDE-CURSOR-0269d8e-....md?ref=1da49ea..." \
    --jq .content | base64 -d > /tmp/rej2/reviews/CLAUDE-CURSOR-0269d8e-....md
$ cp architecture/audit-ledger.jsonl /tmp/rej2/ledger.jsonl
$ python3 harness/audit_review.py record \
    --audit-id CURSOR-0269d8e-hermes-console-droit-executer \
    --reviews /tmp/rej2/reviews --ledger /tmp/rej2/ledger.jsonl
recorded AUDIT_CHALLENGED for CURSOR-0269d8e-hermes-console-droit-executer:
  {'CONFIRMED': 12, 'REFUTED': 3, 'PARTIAL': 3, 'NEEDS_OWNER': 2}
```

Ce n'est pas une projection : la ligne réellement produite par le run CI
`31598845934` est encore lisible dans le **premier** commit de la PR
(`1a3bc17`, retirée par `1da49ea`) et elle est identique au chiffre près :

```
{"timestamp": "2026-08-12T12:59:46Z", "audit_id": "CURSOR-0269d8e-hermes-console-droit-executer",
 "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "architecture/reviews/CLAUDE-...md",
 "verdicts": {"CONFIRMED": 12, "REFUTED": 3, "PARTIAL": 3, "NEEDS_OWNER": 2}}
```

Ce que le tableau porte réellement :

```
lignes de tableau: 12 -> verdicts reels: {'CONFIRMED': 11, 'PARTIAL': 1}
```

Soit **aucun** `REFUTED` et **aucun** `NEEDS_OWNER`. Les trois `REFUTED`
comptés viennent de : la ligne 11 (phrase de gabarit « Un verdict par point :
CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER. »), la ligne 55 (le mot apparaît
dans une cellule de preuve : « plutôt qu'un `REFUTED` pur »), et la ligne 87
(« Aucun `REFUTED`. » — la négation se compte elle-même).

**Antériorité, à dire clairement** : la cause mécanique est déjà posée par un
audit ouvert — `CURSOR-779d97c-revue-verdicts-illisibles`, constat P1-3
(`architecture/inbox/CURSOR-779d97c-...md:175-179`), confirmé par sa propre
revue (`architecture/reviews/CLAUDE-CURSOR-779d97c-...md:45`). Je ne le
re-propose pas comme brief : ce serait du bruit. L'élément **nouveau** est
qu'au moment de fusionner cette PR le défaut est toujours actif, et que les
chiffres qui entreront dans un journal append-only sont ceux ci-dessus. Le
préjudice reste borné au journal (`grep -n "verdicts" hermes/dashboard.py` →
aucune occurrence), mais il est définitif.

### P1-3 — `check-and-automerge` est vert alors que l'auto-fusion a été refusée : le corps de la PR annonce un effet que rien ne produira

Le corps de la PR affirme : « La fusion de cette PR déclenche
`pipeline-orchestrate.yml` (event `review_recorded`). » Cette phrase est vraie
*si* la fusion arrive. Or :

```
$ gh run view 31603379363 -R PLiagre/ForgeHistory --log | grep warning
check-and-automerge  gh pr merge --auto  ##[warning]gh pr merge --auto refused
  (e.g. required checks still pending, or branch protection absent/misconfigured)
  -- this is a soft failure, the PR stays open for the CI-gated merge to complete.

$ gh pr view 43 -R PLiagre/ForgeHistory --json autoMergeRequest,reviews,mergeStateStatus
{"autoMergeRequest":null,"mergeStateStatus":"CLEAN","reviews":[]}
```

Le job est pourtant classé `SUCCESS` : `.github/workflows/merge-bot.yml:71-72`
enveloppe l'appel dans `|| echo "::warning::..."`, donc l'échec devient un
succès et l'avertissement n'apparaît nulle part dans le récapitulatif de la PR.
Conséquence observable à l'échelle du lot : les trois PR de challenge ouvertes
à 13:00Z le 2026-08-12 (#42, #43, #44) étaient **toutes encore ouvertes** à
13:58Z, aucune auto-fusion en attente.

C'est le motif « silent green » décrit par [S6] et [S7] : le journal dit une
chose, l'état du monde en dit une autre, et aucun statut ne l'attrape. C'est
aussi, mot pour mot, le constat P1-2 de l'audit que cette PR relit (« un
`invoke-cursor-auditor` vert ne prouve pas qu'un audit existe ») appliqué à un
autre job — donc la même famille de défaut, avec une preuve nouvelle et un job
différent.

À délimiter honnêtement : que la fusion revienne au propriétaire est
parfaitement légitime, et rien n'oblige la chaîne à être entièrement
automatique. Le défaut n'est pas « ça ne fusionne pas tout seul », c'est
« ça affirme que ça va fusionner et le seul signal disponible est vert ».

### P2-1 — le contre-audit est structurellement aveugle à toute la moitié « API GitHub » de l'audit qu'il relit

Ce n'est pas un accident d'environnement : l'étape qui invoque l'agent ne reçoit
pas de jeton GitHub, alors que les étapes voisines du même fichier en reçoivent
un.

```
$ sed -n '144,157p' .github/workflows/pipeline-challenge.yml
      - name: Invoke claude-challenger headless (/forge-audit-review)
        if: steps.check.outputs.available == 'true'
        env:
          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
          AUDIT_ID: ${{ steps.resolve.outputs.audit_id }}
```

Aucun `GH_TOKEN`, là où la ligne 60 (kill-switch) et la ligne 174 (publication
de la PR) en exposent un. Effet mesuré, écrit noir sur blanc par la revue
elle-même (lignes 35-42) : « `gh pr view 34 ...`, `gh api
.../branches/master/protection`, `gh repo view` : **non rejouables dans ce
sandbox** (pas de `GH_TOKEN`, `gh auth status` → non authentifié) ». Trois
assertions de l'audit restent donc non rejouées, et c'est la racine du P1-1
ci-dessus : le rôle n'a pas les moyens de vérifier ce qu'on lui demande de
juger, donc il comble par la confiance. Le correctif est d'une ligne.

### P2-2 — `reviewed_at` est un horodatage fabriqué, postérieur d'environ deux heures à la fin du run qui l'a produit

```
revue, ligne 5 :   reviewed_at: 2026-08-12T15:10:00Z

$ gh run view 31598845934 --json createdAt,updatedAt,conclusion
{"createdAt":"2026-08-12T12:55:51Z","updatedAt":"2026-08-12T13:00:34Z","conclusion":"success"}
commit forge-bot 1a3bc17 : authoredDate 2026-08-12T13:00:27Z
```

Le fichier prétend avoir été relu à 15:10Z, soit 2 h 09 après la fin du run et
2 h 10 après le commit qui l'introduit. L'explication la plus probable est une
heure locale de Paris (UTC+2) étiquetée `Z` ; même dans cette lecture (13:10Z)
l'horodatage resterait postérieur à la fin du run. La cause est structurelle :
le gabarit pose `reviewed_at: <<TODO: ISO 8601 UTC, ex. ...>>`
(`harness/audit_review.py:69`), donc le champ est rempli à la main par le
modèle, jamais dérivé d'une horloge — et rien ne le contrôle (voir P2-3).
Conséquence : le seul champ de provenance temporelle de l'artefact n'est pas
fiable, alors que l'ordre des événements est précisément ce que la boucle
d'audit prétend pouvoir prouver.

### P2-3 — aucune porte mécanique ne regarde `architecture/reviews/**`, alors que `architecture/inbox/**` en a une

```
$ sed -n '26p;98p' harness/audit_schema.py
INBOX = REPO_ROOT / "architecture" / "inbox"
    for path in sorted(inbox.glob("CURSOR-*.md")):

$ sed -n '26p;30p' .github/workflows/audit-guard.yml
        run: python harness/audit_schema.py
    if: github.event_name == 'pull_request' && startsWith(github.head_ref, 'cursor/')
```

Le job `schema` n'appelle que `audit_schema.py`, qui ne parcourt que
`architecture/inbox/CURSOR-*.md` ; le job `cursor-scope` ne s'active que sur
une branche `cursor/*` et est donc `skipping` sur cette PR (attendu). Et
`architecture/README.md:60-94` ne documente le frontmatter que pour l'inbox :
il n'existe aucun schéma écrit pour une revue. Résultat : le seul fichier de
cette PR entre au dépôt sans qu'aucune porte n'ait lu son frontmatter — c'est
ce qui laisse passer le P2-2 sans un mot. Lentille 3 du guide : on dépense du
jugement d'agent (le mien) sur ce qu'un validateur de dix lignes attraperait,
et la chaîne applique une exigence à l'auditeur qu'elle n'applique pas au
contre-auditeur.

### P2-4 — le marquage budgétaire de l'invocation est bien produit, puis jeté avec le runner

```
$ gh run view 31598845934 --json jobs --jq '.jobs[0].steps[]|select(.name|startswith("Post-hoc"))'
{"name":"Post-hoc budget marking (lot 009b, arbitrage n°2)","conclusion":"success"}

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1 harness/pipeline/ci-budget-ledger.jsonl

$ git log --oneline -- harness/pipeline/ci-budget-ledger.jsonl
cd89141 harness: poser un plafond budgétaire CI traçable
```

L'étape écrit bien sa ligne dans le fichier du runner, mais l'étape de
publication ne commite que `architecture/reviews`
(`pipeline-challenge.yml:194`) et remet le ledger d'audit à l'état HEAD
(ligne 185) : rien ne commite `harness/pipeline/ci-budget-ledger.jsonl`. Il n'a
donc jamais reçu une seule ligne depuis sa création. Or c'est exactement le
fichier que lit `ci_budget_guard.py precheck`, appelé en amont de chaque
invocation (`pipeline-challenge.yml:115`) : le plafond mensuel additionne
toujours zéro et ne peut jamais se déclencher. La littérature 2026 est
unanime sur ce point précis — l'application d'un plafond doit vivre **en dehors**
de l'agent et être capable d'accumuler entre exécutions [S9, S10].

**Antériorité** : le fait que ce fichier soit vide est déjà constaté par
`CURSOR-65c3ac1-dashboard-hermes-modele-auditeur` (ligne 48, « littéralement
vide, 1 octet »). L'élément nouveau que j'apporte est la **cause** — l'écriture
a bien lieu, elle n'est simplement jamais commitée — et le fait que cela vaut
aussi pour le chemin `challenge`, pas seulement pour `forge-run`.

### P3-1 — taille et couverture : conformes (constat favorable)

Un fichier, +95 / −0 : très en dessous du seuil au-delà duquel une relecture
honnête décroche (~5 fichiers, quelques centaines de lignes — lentille 5).
Aucun `NEEDS_SPLIT` à recommander.

Et la couverture est complète. L'audit relu porte neuf constats :

```
$ grep -cE "^### P[0-3]-" architecture/inbox/CURSOR-0269d8e-hermes-console-droit-executer.md
9      (0 P0, 2 P1, 4 P2, 3 P3)
```

Les douze lignes du tableau de la revue couvrent ces neuf constats (points 2 à
10) plus le résumé (1), la veille externe (11) et les briefs proposés (12).
**Aucun constat de l'audit n'est laissé sans verdict** — c'est la qualité
première de cette revue et il faut le dire.

### P3-2 — zéro `REFUTED` sur douze points : à surveiller, pas à reprocher en soi

Onze `CONFIRMED`, un `PARTIAL`, rien d'autre. Prise seule, cette distribution
ne prouve rien : un audit peut être juste. Et le rôle sait réfuter — le champ
`verdicts` du ledger (comptage de mots, cf. P1-2, donc surévalué de deux ou
trois unités) porte `REFUTED: 15` pour la revue de `CURSOR-779d97c`, chiffre
qu'aucune phrase de gabarit ne peut expliquer.

Ce constat n'existe que parce que le P1-1 lui donne un contenu : deux verdicts
sont affirmatifs sans preuve, ce qui est la signature de la validation par
confiance que la lentille 4 (cadrage adverse) et [S8] demandent de traquer.
Sans le P1-1, je n'émettrais pas ce constat.

## Veille externe — ce que l'état de l'art dit de ces trois défauts

Trois recoupements utiles, et un contre-point honnête.

1. **Le « vert silencieux » est le mode de défaillance le plus coûteux de 2026**
   pour les agents non surveillés : l'agent sort en code 0, le tableau de bord
   est vert, et rien n'a été produit [S6]. La parade recommandée est exactement
   ce qui manque au P1-3 : journaliser, à côté de la revendication de succès,
   un **pointeur d'artefact ou un compte** vérifiable, et faire échouer quand il
   est vide. Traduit ici : `check-and-automerge` devrait publier « auto-fusion
   activée : oui/non », pas « tentative faite ».
2. **Vérifier le monde, pas la phrase** [S8], et **au niveau de l'action, pas du
   test isolé** [S7] : un statut vert est une affirmation sur un test, pas une
   preuve qu'un changement d'état a eu lieu. C'est la même discipline que la
   lentille 2 du guide interne, et c'est ce que le P1-1 enfreint à l'échelle
   d'un verdict.
3. **L'application d'un plafond de dépense doit vivre hors de l'agent et
   accumuler entre exécutions** [S9, S10] — un plafond par invocation qui ne
   totalise rien laisse passer douze invocations à 5 $ [S10]. C'est
   littéralement l'état du P2-4.
4. **Contre-point à verser au dossier** : la séparation stricte
   producteur / validateur que Forge applique est bien la pratique de référence
   [S1, S4] — « aucun modèle ne juge sa propre sortie » y est un invariant, pas
   une option. Le harnais ne se trompe donc pas de structure ; ce qui manque
   n'est pas un rôle de plus, c'est un **artefact machine-lisible par étape**
   [S2, S3] pour que le verdict soit une donnée vérifiable et pas une étiquette
   de prose. Les deux plus gros constats de cet audit (P1-1, P1-2) sont
   exactement ça : l'étiquette et la donnée ne coïncident pas.

## Doublons — ce qui est déjà couvert ailleurs

- **P1-2 est déjà posé** par l'audit ouvert `CURSOR-779d97c-revue-verdicts-illisibles`
  (P1-3, et son P0-2 sur les deux définitions concurrentes d'« un verdict »).
  Je n'en fais pas un brief. Élément nouveau apporté : les chiffres exacts de
  cette PR et la preuve que la ligne fautive a réellement été écrite en CI.
- **Le vide de `ci-budget-ledger.jsonl` est déjà posé** par
  `CURSOR-65c3ac1-dashboard-hermes-modele-auditeur` (ligne 48). Élément
  nouveau : la cause précise, et son extension au chemin `challenge`.
- **P1-3 est de la même famille que le P1-2 de l'audit relu** (« vert =
  déclenchement, pas résultat »), mais sur un **autre job** (`merge-bot`), avec
  une conséquence observable différente (trois PR bloquées). Ce n'est pas une
  répétition sans élément nouveau.
- **P1-1, P2-1, P2-2, P2-3 : aucun doublon.** Quatre briefs (006, 008, 009,
  010) parlent bien de `architecture/reviews`, mais aucun n'y attache de porte :
  `rg -n "audit_schema" harness/queue/briefs/*/brief.md` ne renvoie rien, et
  aucune des occurrences de « reviews » dans ces briefs ne porte sur un schéma,
  un frontmatter ou une validation. Aucun brief ne traite non plus de la
  discipline de verdict du contre-auditeur.

## Briefs proposés (3 au plus — ici 3)

Aucun n'est une instruction : ce sont des propositions, la décision reste au
propriétaire et à la boucle (`architecture/README.md`, ADR-0005/0006).

**Proposition 1 — une porte de schéma pour `architecture/reviews/**`.**
Documenter, dans `architecture/README.md` et au même endroit que celui de
l'inbox, le frontmatter d'une revue ; l'imposer par une extension
d'`audit_schema.py` appelée par le job `schema` d'`audit-guard.yml` ; y inclure
une contrainte de cohérence temporelle sur `reviewed_at` (postérieur au
`created_at` de l'audit relu, non postérieur à l'horodatage du commit qui
l'introduit) et le refus d'un `<<TODO>>` résiduel. Couvre P2-2 et P2-3. Test
rouge d'abord : la revue de cette PR, telle quelle, doit faire échouer la
nouvelle porte sur `reviewed_at`.

**Proposition 2 — un job de fusion qui dit ce qu'il a obtenu.**
Dans `merge-bot.yml`, distinguer « auto-fusion activée » de « tentative
faite » : lire l'état réel (`gh pr view --json autoMergeRequest`) après
l'appel, et échouer — ou publier un statut distinct explicitement neutre —
quand il vaut `null`. Couvre P1-3. À noter : `.github/workflows/**` est dans la
denylist du merge-bot lui-même, donc ce lot devra passer par une PR non
auto-fusionnée, ce qui est cohérent avec sa nature.

**Proposition 3 — donner au contre-audit les moyens de vérifier ce qu'il juge.**
Exposer un `GH_TOKEN` en lecture à l'étape d'invocation de
`pipeline-challenge.yml`, et persister le marquage budgétaire (soit en le
commitant dans la PR de challenge, soit en le ré-écrivant sur `master` après
fusion, exactement comme la ligne `AUDIT_CHALLENGED` depuis `8ebe5f9`). Couvre
P2-1 et P2-4. Effet attendu sur la qualité : les points aujourd'hui
`CONFIRMED`-par-confiance deviennent soit `CONFIRMED`-par-preuve, soit
`REFUTED`.

Le P1-1 lui-même ne demande pas de brief : il se corrige en re-étiquetant deux
cellules de cette revue — mais `architecture/reviews/` appartient à Claude, pas
à l'auditeur, donc c'est à lui (ou au propriétaire) de le faire.

## Sources externes

Toutes consultées le **2026-08-12** depuis ce Cloud Agent.

| # | source | consulté le |
|---|---|---|
| S1 | TestQuality — *The Agentic SDLC: Build, Test & Verify AI Code in 2026* — <https://testquality.com/agentic-sdlc-guide-build-test-verify-ai-generated-code/> | 2026-08-12 |
| S2 | Engineered AI Systems — *Deterministic Artifact Verification Pipelines for AI-Generated Software Systems* — <https://www.engineeredaisystems.com/assets/papers/deterministic-verification-pipelines.pdf> | 2026-08-12 |
| S3 | `onchainyaotoshi/agent-review-pipeline` — pipeline de revue autonome, tiérage P0–P3, consensus pondéré — <https://github.com/onchainyaotoshi/agent-review-pipeline> | 2026-08-12 |
| S4 | `mohamedameen-io/AutoDev` — orchestrateur multi-agents, « aucun modèle ne juge sa propre sortie », journal JSONL chaîné SHA-256 — <https://github.com/mohamedameen-io/AutoDev> | 2026-08-12 |
| S5 | DevTools Academy — *AI Coding Agents: A Practical Guide for Software Developers* — <https://www.devtoolsacademy.com/blog/ai-coding-agents-practical-guide> | 2026-08-12 |
| S6 | OperatorIQ — *Agentic AI failure modes: silent green exits and other gotchas* — <https://operatoriq.io/blog/agentic-ai-failure-modes-silent-green-exits/> | 2026-08-12 |
| S7 | DebuggAI — *Your CI Is Green Because It Never Clicked the Button* — <https://debugg.ai/resources/your-ci-is-green-because-it-never-clicked-the-button> | 2026-08-12 |
| S8 | DEV Community (S. Bhattacharya) — *Your Agent Said It Worked. Go Check the World, Not the Sentence.* — <https://dev.to/saurav_bhattacharya/your-agent-said-it-worked-go-check-the-world-not-the-sentence-1m2f> | 2026-08-12 |
| S9 | AI Security Gateway — *LLM Token Budget Strategies for Agents: 5 Layers (2026)* — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026-08-12 |
| S10 | dsplce.co Academy — *Budgets, caps and a kill-switch for unattended runs* — <https://dsplce.co/academy/core/budgets-and-kill-switches> | 2026-08-12 |
| S11 | Digital Applied — *AI Agent Observability 2026: Tracing & Monitoring Stack* — <https://www.digitalapplied.com/blog/ai-agent-observability-2026-tracing-monitoring-stack-guide> | 2026-08-12 |

## Commandes rejouées

Toutes exécutées sur ce Cloud Agent Linux, dépôt à `b133c25`
(= `origin/master`), sans aucune écriture hors `architecture/inbox/`.

```
$ .venv/bin/python -m pytest harness/tests/ -q
311 passed, 16 skipped in 16.67s

$ python3 harness/audit_schema.py | tail -1
All 14 audit(s) valid.        # puis « All 15 audit(s) valid. » avec ce fichier

$ python3 harness/harness_audit.py | grep SCORE
SCORE: 20/24

$ python3 harness/audit_review.py record --audit-id CURSOR-0269d8e-hermes-console-droit-executer \
    --reviews /tmp/rej2/reviews --ledger /tmp/rej2/ledger.jsonl
recorded AUDIT_CHALLENGED for CURSOR-0269d8e-hermes-console-droit-executer:
  {'CONFIRMED': 12, 'REFUTED': 3, 'PARTIAL': 3, 'NEEDS_OWNER': 2}

$ python3 - (comptage par ligne sur le fichier réel de la PR)
CONFIRMED: total=12 aux lignes [11, 48, 49, 50, 51, 52, 53, 54, 55, 56, 57, 59]
REFUTED:   total=3  aux lignes [11, 55, 87]
PARTIAL:   total=3  aux lignes [11, 58, 87]
NEEDS_OWNER: total=2 aux lignes [11, 61]
lignes de tableau: 12 -> verdicts reels: {'CONFIRMED': 11, 'PARTIAL': 1}

$ grep -c "0269d8e" architecture/audit-ledger.jsonl
0     (aucun événement journalisé pour cet audit ; `audits.current_state`
       retombe sur AUDIT_PROPOSED par défaut, harness/audits.py:94-99 —
       la porte `record` passera donc bien, ce n'est pas un blocage)

$ wc -c harness/pipeline/ci-budget-ledger.jsonl
1

$ gh pr view 43 --json autoMergeRequest,reviews  ->  {"autoMergeRequest":null,"reviews":[]}
$ gh pr list --state open  ->  #42, #43, #44 (les trois challenges) encore ouvertes
```

Une vérification que j'ai faite et qui **n'a pas** produit de constat, pour
mémoire : `grep "0269d8e"` sur le ledger ne renvoie rien, ce qui laissait
craindre que `record_challenge` refuse après fusion (il exige l'état
`AUDIT_PROPOSED`). `harness/audits.py:94-99` retourne `AUDIT_PROPOSED` par
défaut en l'absence d'événement, et la reproduction ci-dessus le confirme : la
porte passe. Il n'y a donc pas de P0 ici, contrairement à ce que la lecture du
ledger seule suggérait.

## Ce que cet audit n'autorise pas

Cet audit est une **entrée**, pas une instruction. Il ne vaut ni approbation ni
rejet de la PR #43 : la décision appartient au propriétaire et à la boucle
(`architecture/README.md`, ADR-0005/0006), et la source unique d'instruction
reste le brief (`CLAUDE.md` › Single Source of Instruction). Les trois flags
`*_authorized` du frontmatter sont à `false`. Aucun des trois briefs proposés
n'est pré-autorisé ; aucun fichier hors `architecture/inbox/` n'a été touché
par cette PR d'auditeur.
