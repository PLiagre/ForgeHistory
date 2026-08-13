---
audit_id:                CURSOR-546a9d4-etape-declenchee-sans-jalon
auditor:                 cursor-cloud
target_branch:           master
target_commit:           546a9d496b242a04336143c4e872ebf83790e085
created_at:              2026-08-13T20:34:46Z
audit_type:              architecture-and-qa
status:                  PROPOSED
implementation_authorized: false
ci_changes_authorized:   false
code_changes_authorized: false
---

# Audit de grande étape — premier déclenchement du dispositif ADR-0012

Audit d'étape au sens d'ADR-0012 : le périmètre demandé est **tout ce qui est
entré sur `master` depuis le jalon précédent**, et le workflow a résolu ce
jalon précédent en `<origine du dépôt>`. Le périmètre réel est donc
l'intégralité du dépôt : **359 commits, 1 615 fichiers, 308 505 lignes
ajoutées** (mesure ci-dessous). Rôle : `architecture/agents/cursor-auditor.md`,
compagnon `architecture/agents/cursor-qa-scout.md`, méthode de jugement :
`architecture/review-guidelines.md` (six lentilles, sévérités P0–P3, une preuve
citée par constat).

Cet audit **n'instruit rien** : il propose. La décision reste au propriétaire /
policy engine (`architecture/README.md`, ADR-0005/0006). Les trois flags
`*_authorized` sont `false`.

## Ce que l'étape devait réunir

`hermes/milestones/README.md` (le contrat) et `ROADMAP.md` § « Grandes étapes —
jalons d'audit » définissent six jalons E1–E6. Au commit audité, `ROADMAP.md`
porte : **E1 « Fondations monde complètes » = « à venir »**, **E2 « Le monde
vivant compte juste » = « prochain jalon »**. Aucune étape n'est donc déclarée
close, et `hermes/milestones/` ne contient **aucun** fichier `ETAPE-NN-*.md` :

```
$ ls hermes/milestones/
README.md
```

L'étape auditée est par conséquent l'accumulation F0→F2 (harnais + boucle
d'audit + pipeline geo + moteur `sim/` + port Unity), et non un jalon clos.
Ce fait n'est pas une remarque de forme : il produit le constat P0 ci-dessous.

## Commandes rejouées (sorties collées)

Interpréteur : `.venv/bin/python` (AGENTS.md § Cursor Cloud).

```
$ git rev-list --count 546a9d4
359
$ git diff --stat $(git rev-list --max-parents=0 546a9d4)..546a9d4 | tail -1
 1615 files changed, 308505 insertions(+), 141 deletions(-)

$ .venv/bin/python -m pytest harness/tests/ -q
348 passed, 16 skipped in 17.12s          # 16 skips = Unity/PowerShell, attendu sur Linux

$ .venv/bin/python -m pytest sim/ -q
35 passed in 2.09s

$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/013-sim-tick-nourrit-une-fois
VERDICT: ACCEPT
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/014-pipeline-contre-audit-porte
VERDICT: ACCEPT

$ .venv/bin/python harness/audit_schema.py
All 48 audit(s) valid.

$ .venv/bin/python harness/audits.py list      # états lus dans le registre
AUDIT_PROPOSED 24 | AUDIT_CHALLENGED 3 | AUDIT_APPROVED 9 | AUDIT_CONVERTED 3 | AUDIT_ARCHIVED 9

$ .venv/bin/python harness/backends/ledger.py tokens
No Claude transcripts found ... Nothing measured.
```

Le gate mécanique est vert sur les deux derniers briefs exécutés (013, 014).
Sur les briefs 015/016 il renvoie `REJECT` faute de `verdict.md` — **c'est
attendu** : ce sont des conversions d'audits pas encore générées
(`ls` ne montre que `brief.md`, `eval-rubric.md`, `deliverables`), pas un
défaut.

## Classification de la CI au commit audité

Sur `546a9d496b242a04336143c4e872ebf83790e085` (`gh run list --commit …`) :
`harness-ci` **success**, `security` **success**, `audit-guard` **success**,
`hermes-dashboard` **success**, `pipeline-audit` **success** (c'est le run qui
a produit cet audit), `pipeline-failure-escalate` **skipped**.
Aucun job rouge. En revanche `hermes-observer` apparaît **en file** de façon
massive — voir P1-4.

---

# Constats

## P0-1 — Le dispositif ADR-0012 s'est déclenché sans jalon, sur un fichier de contrat, et a ouvert un périmètre non bornable

**Preuve.** Le déclencheur est un filtre de chemins qui ne distingue pas le
jalon du contrat qui le décrit :

```29:29:.github/workflows/pipeline-audit.yml
      - 'hermes/milestones/*.md'
```

alors que la décision, elle, nomme un motif précis : « la fusion sur `master`
d'un fichier-jalon `hermes/milestones/ETAPE-NN-<slug>.md` »
(`docs/adr/0012-audit-contre-audit-par-grandes-etapes.md:31-33`). Le merge
audité n'apporte que `hermes/milestones/README.md` (38 lignes,
`git diff --stat 546a9d4^1..546a9d4`) — c'est-à-dire **le contrat lui-même**,
pas un jalon. Le run a néanmoins démarré (`pipeline-audit … success`, événement
`push`), et le dépôt de cet audit dans `architecture/inbox/` déclenchera à son
tour `pipeline-challenge.yml` (`paths: architecture/inbox/*.md`,
`pipeline-challenge.yml:22-25`), c'est-à-dire un contre-audit Claude facturé.

Le calcul de bornes se fonde sur la même imprécision, et j'ai reproduit sa
sortie exacte :

```84:95:.github/workflows/pipeline-audit.yml
          jalons="$(git log --format='%H' -- hermes/milestones/*.md 2>/dev/null || true)"
          courant="$(printf '%s\n' "$jalons" | head -1 || true)"
          precedent="$(printf '%s\n' "$jalons" | sed -n '2p' || true)"
          echo "previous=${precedent:-<origine du dépôt>}" >> "$GITHUB_OUTPUT"
```

```
$ jalons="$(git log --format='%H' -- hermes/milestones/*.md)"
nb commits touchant le dossier: 1
courant=73dcee96fd471fe9547a4c25d4ee16c6cc54bee8
precedent=                       # vide -> previous=<origine du dépôt>
```

**Pourquoi c'est un P0 et pas une remarque de forme.** ADR-0012 existe pour une
raison chiffrée : « plafond mensuel de l'abonnement Claude atteint deux fois en
vingt-quatre heures », `7.2771804` USD de transcripts sur une journée
(`docs/adr/0012-…:14-22`). Le mécanisme censé supprimer cette dépense a, à sa
**première exécution réelle**, dépensé un audit Cursor et armé un contre-audit
Claude alors qu'aucune étape n'était close — et il recommencera à chaque
édition du README de `hermes/milestones/`, un fichier documentaire. La garantie
affichée par la décision n'est pas celle que le câblage tient. À noter aussi :
`courant` est calculé (73dcee9) mais l'audit est adressé sur `github.sha`
(546a9d4, le merge) ; les deux ne coïncident pas, et seul le second est
transmis à l'agent.

Effet secondaire mesurable de la même imprécision : le glob `hermes/milestones/*.md`
est développé par le shell **avant** git, donc sur les fichiers présents au
`HEAD` du run. Un jalon renommé ou supprimé disparaîtrait du calcul de bornes ;
le bornage n'est pas fondé sur l'historique mais sur l'arborescence courante.

## P1-1 — Un audit d'étape n'a pas de contrat de profondeur : 308 505 lignes pour un budget de 60 appels

**Preuve.** Le contrat de mon propre rôle plafonne l'audit :
« ≤ 60 appels outils par audit » (`architecture/agents/cursor-auditor.md:62-66`).
Le périmètre transmis est l'étape entière depuis l'origine
(`pipeline-audit.yml:146-153`), soit 1 615 fichiers et 308 505 lignes
(commande ci-dessus). Le guide de critique du dépôt écrit lui-même que « la
revue humaine s'effondre au-delà d'environ 400 lignes »
(`architecture/review-guidelines.md:32-33`) et impose de recommander le
découpage au-delà de ~5 fichiers (`ibid.:38-41`).

Rien dans le workflow ni dans le contrat ne dit **comment** un audit d'étape
échantillonne : quels sous-systèmes sont obligatoirement couverts, quelle
profondeur par sous-système, ce qui est explicitement hors périmètre. En
l'état, la couverture d'un audit d'étape dépend entièrement du jugement de
l'agent au moment du run, et n'est ni reproductible ni falsifiable. C'est le
même défaut que la discipline `NEEDS_SPLIT` corrige côté briefs
(`harness/budget.py split-check`), mais côté audit il n'existe pas.

## P1-2 — L'arriéré que la décision déclare résorbé ne l'est pas, et aucun outil ne permet de l'adjuger en lot

**Preuve.** ADR-0012 affirme : « L'arriéré structurel disparaît… Les 15 audits
`PROPOSED` hérités ne sont plus une dette individuelle : ils seront adjugés en
lot au prochain jalon ou purgés `STALE` (motivés) »
(`docs/adr/0012-…:77-79`). Mesure au commit audité :

```
inbox: 48 | avec contre-audit: 21 | sans: 27 | avec decision: 17 | sans decision: 31
```

Le registre en compte 24 en `AUDIT_PROPOSED` (`harness/audits.py list`), et 24
audits de `inbox/` n'ont **aucune** ligne dans `architecture/audit-ledger.jsonl`.
Le chiffre « 15 » de l'ADR ne correspond à aucune de ces mesures.

Surtout, l'« adjudication en lot » n'a pas d'outil :

```
$ .venv/bin/python harness/audit_ledger.py --help
usage: audit_ledger.py [-h] {append,show} ...
$ .venv/bin/python harness/audits.py --help
usage: audits.py [-h] {list,status} ...
```

`append` traite un audit à la fois, `show`/`list`/`status` ne font que lire.
Et le contre-audit se déclenche par **push de fichier** dans `inbox/`
(`pipeline-challenge.yml:22-25`) : passer 27 audits en revue suppose 27
déclenchements, ce que la décision voulait précisément éviter. La conséquence
positive annoncée par l'ADR repose donc sur une capacité qui n'existe pas dans
le dépôt. Neuf audits sont `AUDIT_APPROVED` sans conversion en brief — un
arbitrage rendu, sans suite mécanique.

## P1-3 — L'auto-audit de maturité du harnais mesure le disque local, pas le dépôt : 20/24 sur clone frais, 23/24 après une exécution locale

**Preuve.** Même commit, deux scores selon un fichier que git ignore :

```
$ .venv/bin/python harness/harness_audit.py | tail -1
SCORE: 20/24
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: unexpected files in stub dirs: [...]

$ .venv/bin/python harness/demo/fake_brief_001/run_demo.py
PROVEN: fake brief was REJECTED by verdict_audit.py. See …/run_demo.log

$ .venv/bin/python harness/harness_audit.py | tail -1
SCORE: 23/24
$ git status --short         # rien : le log produit n'est pas versionnable
$ git check-ignore -v harness/demo/fake_brief_001/run_demo.log
.gitignore:7:*.log	harness/demo/fake_brief_001/run_demo.log
```

Le contrôle exige un artefact que `.gitignore` exclut :

```68:76:harness/harness_audit.py
def check_demo_pair() -> AuditCheck:
    fake_ok = exists("harness", "demo", "fake_brief_001", "run_demo.py")
    honest_ok = exists("harness", "demo", "honest_brief_001", "verdict.md")
    ran_ok = exists("harness", "demo", "fake_brief_001", "run_demo.log")
```

Trois conséquences. (1) Sur tout clone frais — donc pour tout auditeur — la
preuve fondatrice F0 « un faux brief est rejeté » est comptée **absente**,
alors qu'elle passe quand on la rejoue. La CI exécute bien la démo
(`harness-ci.yml:57-58`, job `f0-demo`), mais `harness_audit.py` n'est invoqué
par **aucun** workflow (`grep -rn 'harness_audit' .github/workflows/` : aucune
occurrence) : le score de maturité du harnais n'est donc jamais mesuré
ailleurs que sur une machine de développement, là où le fichier ignoré traîne. (2) `AGENTS.md:50` documente « 23/24,
un seul FAIL, `no_premature_stub_content` » : la mesure sur clone frais donne
20/24 et **deux** FAIL. La documentation décrit l'état d'une machine, pas celui
du dépôt. (3) C'est exactement le défaut de compteur que les règles maison
proscrivent — un chiffre dérivé qui n'est pas reproductible depuis la source de
vérité (`docs/rules/hard-won-rules.md`, règles sur les compteurs dérivés et la
preuve rejouable ; `architecture/review-guidelines.md:21-27`).

## P1-4 — `hermes-observer` sature la file CI et transmet des événements de PR de fork à un script hors dépôt

**Preuve, volet saturation.**

```
$ gh run list --workflow hermes-observer --limit 100 --json status \
    --jq 'group_by(.status)[] | {status: .[0].status, n: length}'
{"n":2,"status":"completed"}
{"n":98,"status":"queued"}
```

Le workflow se déclenche sur la fin de **neuf** workflows plus cinq types
d'événements de PR (`.github/workflows/hermes-observer.yml:3-17`), sur un
runner unique auto-hébergé (`ibid.:32`), avec un groupe de concurrence
indexé sur l'identifiant de chaque run — donc sans plafond global
(`ibid.:25-27`). Comme `harness-ci`, `security` et `audit-guard` n'ont aucun
filtre de chemins (`harness-ci.yml:18-20`), chaque push sur `master` produit
trois fins de workflow, donc trois observateurs de plus. Et chaque push
engendre un commit de bot supplémentaire : mesuré dans les 32 secondes suivant
le merge audité,

```
$ git log --format='%h %ad %an %s' --date=format:'%H:%M:%S' 546a9d4..origin/master
af9381d 20:22:32 hermes hermes: tableau de bord régénéré
2824fe3 20:22:09 hermes hermes: tableau de bord régénéré
```

ADR-0012 a réduit la dépense **LLM** ; la dépense et la latence **CI**
restent, et elles sont aujourd'hui en saturation observable.

**Preuve, volet surface d'exécution.** `hermes-observer` est le seul workflow
en `pull_request_target` du dépôt (`hermes-observer.yml:4`). Ses permissions
sont en lecture seule et il ne fait pas de checkout — c'est bien. Mais il
transmet l'événement complet (donc titre et corps d'une PR, y compris venant
d'un fork) à un script **hors du dépôt**, sur une machine persistante :

```35:40:.github/workflows/hermes-observer.yml
        run: >-
          & 'C:\Users\liagr\Documents\ChatGPT\hermes\scripts\runner-event.ps1'
          -EventName '${{ github.event_name }}'
          -EventPath '${{ github.event_path }}'
```

Je ne peux pas auditer `runner-event.ps1` : il n'est pas versionné ici. Le
constat porte donc sur ce qui est vérifiable — une part du dispositif de
pilotage échappe à la revue, sur le seul point d'entrée du dépôt qui reçoit du
contenu non fiable.

## P2-1 — Les clés `cursor_*` de `config.yaml` sont mortes : basculer la cadence par la configuration ne désarme rien

**Preuve.** ADR-0012 se conclut sur « `harness/pipeline/config.yaml` :
`cursor_review_on_pr: false`, `cursor_audit_on_master_push: false`,
`cursor_audit_on_milestone: true` » (`docs/adr/0012-…:98-99`). Or aucun
workflow ne lit ces clés : seule `mode` est lue à l'exécution
(`pipeline-challenge.yml:76-80`, `pipeline-forge-run.yml`), et `pipeline-audit.yml`
n'ouvre pas `config.yaml` du tout. La cadence vit entièrement dans le `on:` du
YAML. Un opérateur qui mettrait `cursor_audit_on_milestone: false` croirait
avoir désarmé l'audit d'étape sans rien changer. Même remarque pour
`auto_merge_*` et `claude_challenge_on_inbox_merge`, déclaratives elles aussi.

## P2-2 — Deux moteurs de simulation coexistent ; `sim/` n'a aucun consommateur

**Preuve.** `sim/` compte 581 lignes hors tests (`constants.py` 142,
`engine.py` 269, `world.py` 104, `model.py` 59) et une entité unique, `Cell`
(`sim/model.py:54-59`). En parallèle, `unity/game_unity/Assets/Scripts/`
contient ~41 900 lignes de C# et une soixantaine de systèmes de simulation :
`Core/Systems/SimulationTickSystem.cs` avance le tick,
`Economy/Systems/PhysicalProductionSystem.cs`, `MarketPricingSystem.cs`,
`Population/Systems/PopGrowthSystem.cs`, `Military/Systems/BattleResolutionSystem.cs`,
`Politics/Systems/RevolutionSystem.cs`… Le README d'Unity exige pourtant
« zero simulation/business logic » et « must only ever READ simulation state
exposed by sim/ » (`unity/README.md:3-9`), et les principes maison nomment ce
cas comme mode d'échec : « Presentation re-implementing the simulation »
(`docs/rules/simulation-principles.md:29`), plus la double clé primaire
`ProvinceId` / `cell_id` (`ibid.:26`, reconnue dans `unity/README.md:17-20`).
Aucun lien n'existe entre les deux : `grep -r 'import sim' unity/` ne renvoie
rien, et hors de `sim/` les seuls appelants de `tick()` sont deux scripts de
mesure de briefs (`harness/queue/briefs/012-…/measure_cellules_affamees.py`,
`013-…/measure_sc6_013.py`).

Cette dette est **assumée** par ADR-0004 et par les README — je ne la
re-propose donc pas comme une découverte. Ce qui est neuf, pour un audit
d'étape, c'est le rapport de forces : la cible « `sim/` fait foi, Unity rend »
est à 581 lignes contre ~42 000, aucune garde mécanique n'empêche d'ajouter un
système de simulation de plus dans `unity/`, et E6 (« Unity rend l'état du
moteur, zéro logique », `ROADMAP.md`) suppose de retirer ou de reporter la
quasi-totalité de ce code. C'est la plus grosse échéance implicite de la
trajectoire, et elle n'est chiffrée nulle part.

## P2-3 — `hunger_ticks` est une variable terminale, le mode d'échec que les règles nomment

**Preuve.** Le champ est écrit à chaque tick et sérialisé
(`sim/engine.py:208-212`, `sim/world.py:99`), et un test vérifie son
incrémentation (`sim/tests/test_causal_chain.py:80`).
Mais la mortalité ne le lit pas : elle ne dépend que de `food_deficit_kg`
(`sim/engine.py:232-237`). Aucune conséquence du tick suivant ne dépend de
`hunger_ticks`. Les principes exigent l'inverse : « Terminal variable
(computed, read by nobody) — before opening a lever, verify its consequence
reaches something measurable » (`docs/rules/simulation-principles.md:28`).
Pour l'étape E2, « le monde vivant compte juste », c'est le compteur de faim
lui-même qui ne compte pour rien.

Deux remarques adjacentes, même bloc de preuve : la production est créée à
partir de la superficie sans intrant ni origine (`sim/engine.py:48-51`), ce que
« rien ne se téléporte » proscrit (`ibid.:18-20`) ; et il n'existe aucune
natalité (`grep -E 'natalit|birth|naissance' sim/` ne renvoie rien), donc la
population ne peut que décroître — la « fraction de survie » testée mesure une
vitesse de décroissance, pas une viabilité.

## P2-4 — La fraîcheur d'un audit est structurellement inatteignable

**Preuve.** `architecture/README.md:57` définit `AUDIT_STALE` par
« `target_commit` obsolète avant acceptation ». Or `hermes-dashboard.yml`
commite sur `master` après chaque push (`hermes-dashboard.yml:98-110`) : deux
commits dans les 32 secondes suivant le merge audité (log cité en P1-4). Tout
audit est donc non-`tip` avant même d'être déposé. Mesure : 43 des 48 audits de
`inbox/` visent un ancêtre de `HEAD` différent de `HEAD`, tandis que le registre
ne porte que 4 événements `AUDIT_STALE`.

Le thème voisin a déjà été soulevé (`CURSOR-ab0e7f0-pr62-verdicts-perimes-a-la-fusion`,
`CURSOR-70380c6-pr83-porte-desarmee-par-la-derive-de-sha`) : je ne le
re-propose pas comme découverte, je note l'élément nouveau — avec la cadence
par jalon, un audit d'étape est long à produire, donc l'écart entre
`target_commit` et `master` sera plus grand qu'avant, pas plus petit.

## P3-1 — Poids du dépôt

`.git` pèse 237 Mo. Sont versionnés : `pipeline/geo/sources/10m_physical.zip`
(50 Mo), `unity/…/map/hillshade_lod0.png` (9,2 Mo),
`unity/…/Captures/v1_068/pilot_province_political_lod0.png` (9,0 Mo),
`harness/queue/briefs/003-port-unity-game/deliverables/evidence/victoriaproject-testresults_full.xml`
(4,2 Mo), `pipeline/geo/registry/cell_registry.json` (2,9 Mo). Information : le
coût de clonage est déjà supérieur à celui du code, et il croît à chaque
capture de brief.

## P3-2 — Textes opérationnels restés sur l'ancienne cadence

Après ADR-0012, disent encore que Cursor relit chaque PR :
`architecture/agents/cursor-auditor.md:43-45` (**mon propre contrat de rôle**),
`architecture/agents/README.md:17` et `:39-40`,
`docs/adr/0010-…:35` et `:49-50`, `harness/pipeline/config.yaml:18-19`,
`.github/workflows/pipeline-forge-run.yml:241`,
`.github/workflows/hermes-dashboard.yml:38-41` (invoque un filtre `hermes/**`
de `pipeline-audit.yml` qui n'existe plus),
`.github/workflows/merge-bot.yml:62-64`. Information : un agent qui lit son
contrat plutôt que l'ADR appliquera l'ancienne cadence.

---

# Briefs proposés (3 au maximum — ce plafond est tenu)

Ces propositions ne sont pas des instructions : seul un brief validé instruit
(`CLAUDE.md` › Single Source of Instruction).

1. **Resserrer le déclencheur et le bornage de l'audit d'étape** (P0-1, P1-1).
   Distinguer le jalon du contrat ; refuser de lancer un audit d'étape quand le
   diff ne contient aucun `ETAPE-NN-*.md` ; borner l'étape sur le dernier
   **jalon** réel via l'historique et non par un glob d'arborescence ; déclarer
   à l'agent le périmètre couvert et la profondeur attendue par sous-système.
   Preuve rouge/verte attendue : un push ne touchant que
   `hermes/milestones/README.md` ne déclenche rien ; un push apportant
   `ETAPE-02-*.md` déclenche avec `previous` = jalon E1.
2. **Rendre l'arriéré d'audits adjugeable et le registre vérifiable**
   (P1-2). Une commande de traitement en lot (marquage `AUDIT_STALE` motivé,
   ou mise en revue groupée) plus une garde qui échoue quand un fichier de
   `inbox/` n'a aucune ligne au registre, ou quand une review/décision sur
   disque n'a pas d'événement correspondant. Compteur attendu : 0 audit hors
   registre (aujourd'hui 24).
3. **Rendre l'auto-audit du harnais reproductible sur clone frais** (P1-3).
   Le contrôle F0 doit se fonder sur un artefact versionné ou sur l'exécution
   de la démo, pas sur un fichier exclu par `.gitignore` ; et le score
   documenté doit être celui d'un clone frais. Preuve attendue : deux clones
   propres du même commit donnent le même score.

Non proposés en briefs, faute de place et parce qu'ils demandent d'abord un
arbitrage du propriétaire : la trajectoire de convergence `sim/` ↔ Unity
(P2-2), la fraîcheur des audits face à l'auto-commit du tableau de bord (P2-4).

---

# Section cursor-qa-scout — veille et comparaison à l'état de l'art

Thèmes du cycle : `autonomous AI dev pipeline`, `agent orchestration CI`,
`token budget LLM agents`. **Déclaration de non-doublon** : les 17 briefs de
`harness/queue/briefs/` ont été listés et vérifiés (001, 002, 003, 004, 005,
006, 007, 008-contexte-opus5, 008-full-auto-automation-gaps, 009, 010, 011,
012, 013, 014, 015, 016) — aucun ne porte sur le déclenchement par jalon, sur
l'adjudication en lot de l'arriéré d'audits, ni sur la reproductibilité de
`harness_audit.py`. Aucun doublon.

**Enveloppe de dépense hors du code de l'agent.** L'état de l'art 2026 place
l'enveloppe budgétaire à la couche d'infrastructure (passerelle, plan de
gouvernance), précisément parce qu'un plafond porté par le code ou le prompt de
l'agent est contournable [S1, S3, S5]. Ici, le plafond par appel
(`--max-budget-usd 5`, `pipeline-challenge.yml:166`) et `ci_budget_guard`
vivent dans le même workflow que l'appel : c'est mieux que rien et
c'est vérifiable, mais la cadence — la seule vraie protection retenue par
ADR-0012 — dépend d'un filtre de chemins imprécis (P0-1). La littérature
distingue aussi le plafond cumulé de la **vélocité** de dépense [S1, S5] ;
le dépôt n'a pas d'indicateur de vélocité, et `backends/ledger.py tokens`
renvoie « Nothing measured » sur un clone frais.

**Cadence par jalon.** Les dispositifs comparables font de la porte une
mécanique (hook, politique évaluée en amont de l'exécution), pas une
convention écrite [S2, S4, S6]. `audit-guard` est déjà de cette famille. Le
maillon manquant est symétrique : rien ne vérifie mécaniquement qu'un audit
d'étape a bien été **déclenché par une étape**.

**Journal auditable.** L'état de l'art recommande un journal en ajout seul liant
identité, intention et résultat, reconstructible [S4, S6]. `audit-ledger.jsonl`
est exactement cela dans son intention — mais 24 audits n'y figurent pas
(P1-2), et l'outil documente lui-même son absence d'atomicité
(`harness/audit_ledger.py`, en-tête d'aide). L'écart n'est pas conceptuel, il
est de tenue.

# Sources externes

| # | source | consultée le |
|---|---|---|
| S1 | *Agent Cost Circuit Breaker Pattern Guide: How to Stop Runaway AI Spend Before It Starts* — <https://baeseokjae.github.io/posts/agent-cost-circuit-breaker-pattern-guide-2026/> | 2026-08-13 |
| S2 | *ai-sdlc* — cadre d'orchestration d'agents avec portes qualité et provenance par étape — <https://github.com/ai-sdlc-framework/ai-sdlc> | 2026-08-13 |
| S3 | *AgentBudget: Real-Time Cost Enforcement for AI Agents* (livre blanc v1) — <https://agentbudget.dev/agentbudget_whitepaper_v1.pdf> | 2026-08-13 |
| S4 | *Governing AI Agents in CI/CD with OPA and MCP* — <https://dev.to/devopsstart/governing-ai-agents-in-cicd-with-opa-and-mcp-58lb> | 2026-08-13 |
| S5 | *LLM Token Budget Strategies for Agents: 5 Layers* — <https://aisecuritygateway.ai/blog/llm-token-budget-strategies-for-agents> | 2026-08-13 |
| S6 | *ZOdyssey: making the plan-review gate a hard hook, not a prompt convention* — <https://dev.to/amartinawi/zodyssey-making-the-plan-review-gate-a-hard-hook-not-a-prompt-convention-3g9c> | 2026-08-13 |
| S7 | *Managing Rate Limits and Token Budgets in Production AI Agents* (2026-07-03) — <https://niteagent.com/blog/2026-07-03-agent-rate-limit-quota-management-guide/> | 2026-08-13 |

# Limites de cet audit

Ce qui n'a **pas** pu être vérifié, et doit être lu comme non couvert :
`runner-event.ps1` (hors dépôt) ; la compilation et les tests Unity (Windows +
licence requis, AGENTS.md) ; les preuves geo G2/G2b/G3 non rejouées ici (lues
seulement) ; l'existence effective des secrets `CURSOR_API_KEY` /
`FORGE_BOT_PAT` ; les montants réellement facturés (aucun transcript sur cette
VM). Trois `target_commit` d'audits antérieurs référencent des SHA absents de
ce clone (`FIXTURE`, `ab0e7f0`, `dcbe815`) — non tranché ici. Sur 1 615
fichiers, la couverture de cet audit est partielle **par construction** : c'est
l'objet du constat P1-1.
