---
audit_id: CURSOR-a600532-fusion-sans-contre-audit
auditor: cursor-cloud
target_branch: master
target_commit: a600532e714a9ff4d1b3c739859a9357884d5f81
created_at: 2026-08-13T06:30:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet** : audit post-fusion du commit `a600532` sur `master` — fusion de la
pull request [#57](https://github.com/PLiagre/ForgeHistory/pull/57)
« Brief 011 : amorçage du moteur sim/ — monde vivant, couche 1 (F2) »,
28 fichiers, +3098 / −14, parents `7a81aa4` (master) et `3b47ffe` (branche
de lot).

Cet audit **ne décide rien** : il propose. La décision reste au propriétaire
et à la boucle (`architecture/README.md`, ADR-0005/0006). Les trois drapeaux
d'autorisation du frontmatter sont à `false`, comme l'exige le contrat
`architecture/agents/cursor-auditor.md`.

**Ce qui va bien.** La fusion est propre et n'a rien cassé : la CI du commit
fusionné est verte sur les cinq workflows déclenchés, le gate mécanique
répond `ACCEPT` sur le lot 011, `sim/tests/` donne 20 succès et
`harness/tests/` 314 succès et 16 tests ignorés — toutes ces valeurs
re-mesurées sur `a600532` lui-même (§ 5). Le contenu fusionné est
exactement celui de la branche : le diff entre `3b47ffe` et `a600532` ne
contient que ce que `master` avait déjà de son côté (l'audit pré-fusion et
le tableau de bord d'Hermes). Aucune surprise n'a été introduite par la
fusion.

**Ce qui ne va pas, et c'est le cœur de cet audit.** La fusion a eu lieu
alors que le maillon « contre-audit » de la chaîne à quatre acteurs était
**en panne depuis seize heures**, et rien ni personne n'en a été averti au
moment de décider :

1. Le workflow `pipeline-challenge` — celui qui fait relire chaque audit par
   Claude — a échoué **onze fois de suite** le 2026-08-12, entre 13:53 et
   17:09 UTC, toujours pour la même raison : le fournisseur refuse l'appel
   (HTTP 429, « You've hit your org's monthly spend limit »). Le onzième
   échec est précisément celui qui devait faire relire l'audit de la PR #57
   (constat P0-1).
2. Résultat : la PR #57 a été fusionnée le 2026-08-13 à 06:12:59 UTC avec
   **zéro relecture GitHub**, alors qu'un audit portant 1 constat P0 et 4
   constats P1 sur cette même PR attendait dans `architecture/inbox/` sans
   avoir été ni confirmé ni réfuté par quiconque.
3. Le mécanisme d'escalade prévu pour ce cas **s'est bien déclenché** — et
   n'a servi à rien, parce qu'il n'écrit que dans un journal de workflow. La
   seule vue lisible par le propriétaire (`hermes/DASHBOARD.md`, générée
   3 h avant la fusion) lui proposait une seule action concrète :
   « Fusionner (ou refuser) la PR #57 » (constat P1-2).

Autrement dit : la panne était visible pour une machine, invisible pour la
personne qui devait décider.

Compte : 1 constat P0, 3 constats P1, 2 constats P2, 1 constat P3 (celui-ci
purement informatif : deux fausses pistes vérifiées et écartées). Trois
briefs atomiques sont proposés au § 7 — jamais plus (contrat).

# 2. Périmètre : ce que cet audit ajoute à l'audit pré-fusion

Un audit de la même PR existe déjà :
`architecture/inbox/CURSOR-3b47ffe-pr57-monde-sans-faim.md`, déposé le
2026-08-12 à 17:15 UTC sur la tête de branche `3b47ffe`. Le guide de
critique interdit de répéter un motif sans élément nouveau
(`architecture/review-guidelines.md`, « Forme imposée des constats »). Cet
audit-ci ne rejuge donc pas le contenu du lot 011. Il porte sur ce qui n'a
pu être observé qu'**après** la fusion :

- le sort réservé à l'audit pré-fusion (aucun) et la mécanique qui l'a rendu
  sans effet (§ 3, P0-1) ;
- la panne de la chaîne de contre-audit, invisible avant qu'on ne regarde
  l'historique des exécutions (P0-1, P1-1) ;
- l'état de `master` au commit fusionné, re-mesuré et non plus supposé
  (P1-3, P2-2) ;
- la file d'attente des audits telle qu'elle est réellement enregistrée
  (P2-1).

# 3. Constats

## P0-1 — La fusion s'est faite avec le maillon de contre-audit en panne depuis seize heures, et rien ne pouvait l'empêcher

**Preuve 1 — onze échecs consécutifs du contre-audit.** Historique complet
des exécutions du workflow (sortie brute au § 5.3) :

```
2026-08-12T17:09:19Z  push  ee229e0  failure   <- l'audit de la PR #57
2026-08-12T15:42:45Z  push  b8dbecd  failure
2026-08-12T14:59:25Z  push  01e7c24  failure
2026-08-12T14:05:02Z  push  bad1ffb  failure
2026-08-12T14:03:28Z  push  0302274  failure
2026-08-12T14:03:26Z  push  b85e2a7  failure
2026-08-12T13:58:17Z  push  0677724  failure
2026-08-12T13:54:54Z  push  8d9d8f2  failure
2026-08-12T13:53:59Z  push  67206a9  failure
2026-08-12T13:53:46Z  push  109df6a  failure
2026-08-12T13:53:34Z  push  b759ee2  failure
2026-08-12T12:56:10Z  push  6ab4f59  success  <- dernier succès
```

**Preuve 2 — la cause, identique aux onze fois.** Journal de l'exécution
31621195096 (celle de l'audit de la PR #57), étape « Invoke
claude-challenger headless » :

```
"result":"You've hit your org's monthly spend limit · ask your admin to
raise it at claude.ai/settings/usage", "api_error_status":429,
"is_error":true, "total_cost_usd":0
##[error]Process completed with exit code 1.
```

L'identifiant était bien présent (`CLAUDE_CODE_OAUTH_TOKEN: ***` dans
l'environnement du job) : ce n'est pas le cas « pas de clé » que le workflow
sait traiter, c'est le cas « clé valide, fournisseur qui refuse ».

**Preuve 3 — la PR a été fusionnée sans aucune relecture.** Métadonnées
GitHub de la PR #57 :

```
"reviews":[], "state":"MERGED", "mergedAt":"2026-08-13T06:12:59Z",
"mergedBy":{"login":"PLiagre"},
"mergeCommit":{"oid":"a600532e714a9ff4d1b3c739859a9357884d5f81"}
```

**Preuve 4 — l'audit de cette PR n'a laissé aucune trace d'arbitrage.**
Aucun fichier `architecture/reviews/CLAUDE-CURSOR-3b47ffe-*`, aucun fichier
`architecture/decisions/DECISION-CURSOR-3b47ffe-*`, et aucune ligne au
registre :

```
$ rg -n "3b47ffe" architecture/audit-ledger.jsonl
(aucune correspondance)
```

**Preuve 5 — par construction, le contre-audit ne peut pas garder une PR.**
`.github/workflows/pipeline-challenge.yml` ne se déclenche que sur
`push: branches:[master]`, chemins `architecture/inbox/*.md`. Il s'exécute
donc **après** qu'un audit soit déjà sur `master`, jamais pendant la vie
d'une PR de lot. Aucun statut « audit adjugé » n'existe donc parmi les
vérifications d'une PR : même en pleine forme, ce maillon n'aurait rien
bloqué. (Les réglages de protection de branche ne sont pas lisibles avec les
droits de cet auditeur — `HTTP 403` sur l'API, § 5.6 — mais la conclusion ne
dépend pas d'eux : un contrôle qui ne s'exécute pas sur les PR ne peut pas
être exigé sur les PR.)

**Pourquoi c'est un P0 et pas un rappel de principe.** ADR-0010 fait de
Cursor le maillon **critique** de chaque PR et le commentaire de tête de
`pipeline-audit.yml` le répète. La critique a bien été produite. C'est
l'anneau suivant — la contradiction de cette critique, puis la décision —
qui a été silencieusement absent. Une critique qu'aucun acteur ne
contredit et qu'aucune porte ne consulte n'est pas une garantie : c'est un
document.

**Sources externes** : S1 (le rayon d'action d'une panne : un signal d'arrêt
dans l'état partagé, lu par chaque nœud aval, plutôt qu'une exception qui
laisse la chaîne continuer), S6 (dans une boucle agent branchée sur la CI,
la règle d'arrêt et l'absence de fusion automatique au premier tour sont ce
qui protège le dépôt).

## P1-1 — « Le fournisseur refuse » n'est pas un état prévu du pipeline : onze fois le même échec dur, aucune bascule

**Preuve.** Le commentaire de tête de `pipeline-challenge.yml` énumère les
garde-fous, dans l'ordre où ils coupent : label `pipeline/pause`, mode
`manual`, plafond mensuel `ci_budget_guard`, plafond natif
`--max-budget-usd 5` sur l'appel, marquage post-hoc. Puis :

> Sans identifiant Claude (`CLAUDE_CODE_OAUTH_TOKEN` d'abonnement, ou
> `ANTHROPIC_API_KEY` en repli) : dérogation consignée, aucun appel simulé.

Les cinq garde-fous portent sur la dépense **de Forge**. Aucun ne porte sur
le refus **du fournisseur**, qui est pourtant ce qui est arrivé : plafond
mensuel de l'organisation atteint côté Anthropic, `total_cost_usd: 0` —
Forge n'a rien dépensé, et le plafond de Forge n'était donc pas en cause
(le tableau de bord affiche « 0.0 USD mesurés sur 0 invocation(s), plafond
200 USD »). La dérogation prévue ne s'applique pas, l'étape termine en
`exit 1`, et le pipeline recommence à l'identique au push suivant.

Le dépôt possède pourtant de quoi ne pas s'arrêter là : ADR-0008 (Codex
Évaluateur sous plafond de crédit) et ADR-0009 (Codex Générateur officiel)
établissent que les rôles sont tenables par un autre backend, et
`harness/backends/` en fait un contrat explicite. Rien de tout cela n'est
mobilisé quand le backend par défaut est refusé.

Le brief 009 a bien donné à la dépense **récurrente de Forge** un plafond
mécanique (lot 009b) ; il ne traite pas le cas symétrique — le fournisseur
qui dit non. Ce constat ne redemande donc pas ce que 009 a livré.

**Source externe** : S2 (quand la limite est atteinte, la réponse utile est
une chaîne de repli — modèle moins cher, plan simplifié, erreur structurée
remontée à l'humain — pas un échec brut relancé à l'identique), S5 (un
compteur qui mesure n'est pas un frein ; le frein est ce qui refuse ou
dévie **avant** l'appel).

## P1-2 — L'escalade a bien eu lieu, et n'a atteint aucune surface lisible par le propriétaire

**Preuve 1 — l'escalade s'est déclenchée.** `pipeline-failure-escalate.yml`
surveille les quatre workflows `pipeline-*` par `workflow_run`. Pour l'échec
du contre-audit de 17:09:19, une exécution s'est bien lancée à 17:09:44 et
a conclu `success` :

```
2026-08-12T17:09:44Z  workflow_run  87b6d4f  success
```

**Preuve 2 — elle n'écrit que dans un journal.** Le commentaire de tête du
workflow le dit de lui-même :

> Log-only, same wiring depth as that existing escalation path (...): no
> real `gh issue create` call here either, matching this brief's Non-Goals.

**Preuve 3 — la vue du propriétaire ne portait aucun signal.**
`hermes/DASHBOARD.md` livré sur `master` au commit `7a81aa4`, généré le
2026-08-13 à 03:02 UTC — soit trois heures avant la fusion — contient dans
sa rubrique « Ce qui attend le propriétaire » exactement quatre lignes, dont
la première est :

```
- Fusionner (ou refuser) la PR #57 — « Brief 011 : amorçage du moteur sim/
  — monde vivant, couche 1 (F2) » (branche `forge/011-sim-monde-vivant-a67c`).
```

L'audit qui critique cette même PR n'y figure pas comme action : il apparaît
quarante lignes plus bas, comme une ligne d'un tableau d'état —

```
| CURSOR-3b47ffe-pr57-monde-sans-faim | déposé — attend le contre-audit de
Claude | — (fichier inbox, pas encore au ledger) |
```

— sans aucun lien avec la PR qu'il critique. La cause est dans le
générateur : `hermes/dashboard.py` lignes 234-236 n'émet une action que
pour les audits déjà `AUDIT_APPROVED`.

```python
for audit in audits_en_cours:
    if audit["event"] in ("AUDIT_APPROVED",):
        attentes.append(f"- Convertir l'audit retenu `{audit['audit_id']}` ...")
```

Un audit `PROPOSED` — c'est-à-dire un audit dont personne ne s'est encore
occupé — ne produit donc jamais d'action. Aucune rubrique du tableau de bord
ne rapporte non plus la santé des workflows `pipeline-*`.

**Élément nouveau par rapport à une décision déjà prise.** Le brief 008
range explicitement en non-but la construction d'un vrai mécanisme de
notification, et ce constat ne demande pas de revenir sur cet arbitrage.
L'élément nouveau est la conséquence, désormais mesurée sur un cas réel :
onze escalades déclenchées, zéro signal humain, et une décision de fusion
prise dans cet angle mort.

## P1-3 — Les constats de l'audit pré-fusion sont entrés tels quels sur `master`, sans qu'aucun acteur les ait confirmés ni réfutés

Ce constat ne rejuge pas le fond, déjà exposé dans
`CURSOR-3b47ffe-pr57-monde-sans-faim.md`. Il établit deux choses nouvelles :
que ces mesures se reproduisent à l'identique sur le commit fusionné (elles
décrivent donc `master`, plus une branche), et qu'elles y sont entrées sans
arbitrage.

**Preuve — re-mesure sur `a600532`** (script et sortie complète au § 5.4) :

```
cellules chargees      = 596 | aretes adjacence = 1364
production/km2/tick    = 50.0 | consommation/km2/tick (densite nominale) = 20.0
apres N=200 ticks :
  population 66865505 -> 66865505 (delta 0)
  stock kg   4011930300 -> 43937193599 (facteur x11.0)
  cellules hunger_ticks>0 : 0
  cellules food_stock_kg<=0 : 0
hash rng=42     : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
hash rng=999999 : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
condenses egaux : True | etat interne rng inchange : True True
```

Sur le monde réellement chargé, la couche « monde vivant » ne produit après
200 pas de temps ni faim, ni mort, ni pénurie — seulement un stock qui
grossit d'un facteur 11 ; et deux graines aléatoires totalement différentes
donnent le même état final, le générateur passé à `tick()` n'étant jamais
consommé. `ROADMAP.md` et `HANDOFF.md`, modifiés par cette même PR,
annoncent la couche 1 comme commencée.

Le risque n'est pas que ces mesures soient fausses — elles sont
reproductibles — mais que **personne n'ait eu à dire si elles comptaient**.
La boucle prévoit exactement cela (`CONFIRMED` / `REFUTED` / `PARTIAL` /
`NEEDS_OWNER`, `architecture/README.md`) ; c'est l'étape qui a sauté.

## P2-1 — Le registre d'audits ne peut pas dire ce qui attend : 12 audits sur 25 n'y ont aucune ligne, et l'événement `AUDIT_PROPOSED` n'y apparaît jamais

**Preuve — recensement complet** (script et sortie au § 5.5) :

```
audits inbox: 25 | audit_id présents au ledger: 13
audits sans aucune ligne au ledger : 12
événements AUDIT_PROPOSED au ledger : 0
répartition : PROPOSED implicite 12 | ARCHIVED 7 | APPROVED 3 | CHALLENGED 3
```

Les douze audits sans ligne ont tous été déposés le 2026-08-12, dans la
fenêtre exacte où le contre-audit était en panne (12:45 → 17:15 UTC) : le
registre ne dit pas qu'ils attendent, il ne dit rien du tout à leur sujet.

Ce n'est pas un bug isolé mais une convention assumée :
`harness/pipeline/orchestrator.py` ligne 146 traite l'événement comme
facultatif (« no audit_id in payload; AUDIT_PROPOSED is optional ») et
`hermes/dashboard.py` compense en considérant tout fichier d'`inbox/` absent
du registre comme un `AUDIT_PROPOSED` implicite. La compensation rend
l'affichage correct ; elle ne rend pas le registre analysable. Deux
conséquences mesurables :

- `architecture/README.md` décrit le registre comme « une ligne par
  transition d'état » ; pour douze audits sur vingt-cinq, il n'y a aucune
  transition enregistrée, donc aucune date d'entrée exploitable par machine ;
- l'état `AUDIT_STALE` (« `target_commit` obsolète avant acceptation ») n'est
  calculable pour aucun d'eux, faute d'horodatage d'entrée au registre. Le
  cas est déjà concret : `master` a avancé d'un commit (`7b09200`,
  régénération du tableau de bord) dans les minutes suivant `a600532`, et le
  présent audit vieillira de la même manière.

## P2-2 — Au commit fusionné, les 20 tests du moteur `sim/` ne tournent toujours dans aucun job de CI

**Preuve.** Classification des jobs de `a600532` (§ 5.2) : le workflow
`harness-ci` a deux jobs, `tests` et `f0-demo`, et son étape de test est
`python -m pytest harness/tests/ -v`. Aucun job ne collecte `sim/tests/`.
La CI verte de ce commit n'a donc exécuté **aucune ligne** du code livré par
la PR fusionnée — y compris les deux tests qui gardent les modes d'échec
n°2 et n°5 du dépôt (`sim/tests/test_write_coverage.py`,
`sim/tests/test_no_hardcoded.py`).

Ce point avait été signalé avant la fusion (P2-5 de l'audit précédent) et
assumé comme réserve connue dans la description de PR. L'élément nouveau
est qu'il n'est plus une réserve sur une branche : c'est l'état de `master`,
et il vaut maintenant pour toute régression future du moteur.

## P3-1 — Deux fausses pistes vérifiées et écartées (information, pas constat)

Consigné pour éviter qu'un prochain auditeur ne les signale à tort :

1. **`harness_audit.py` affiche 20/24 sur une machine fraîche**, contre les
   23/24 documentés par `AGENTS.md`. Ce n'est pas une régression de la
   fusion : le contrôle `fake_honest_demo_pair` (3 points) cherche un
   `run_demo.log` qui n'existe qu'après exécution de la démo F0. Après
   `py harness/demo/fake_brief_001/run_demo.py`, le score remonte à 23/24
   (§ 5.6). La seule vraie remarque tient en une ligne : le FAIL restant
   (`no_premature_stub_content`) liste maintenant aussi `sim/**`, l'outil
   d'auto-audit continuant de croire ce répertoire vide.
2. **Régénérer `hermes/DASHBOARD.md` localement produit un écart** de 4
   lignes ajoutées et 20 supprimées par rapport à la version commise. La
   quasi-totalité de l'écart est la rubrique « Activité GitHub récente »,
   qui devient « Non disponible dans cette génération » faute d'accès à
   l'API GitHub depuis un poste local. Ce n'est pas une divergence de
   contenu : le fichier commis n'est pas périmé.

# 4. Lentille « taille et découpage » appliquée à la fusion

Le commit fusionné apporte 28 fichiers et +3098 lignes en une fois, très
au-delà du seuil que `architecture/review-guidelines.md` (lentille 5) donne
pour une relecture honnête. La particularité, ici, n'est pas la taille : le
guide prévoit ce cas et la réponse (découper). C'est que le seul acteur
capable d'absorber ce volume sans se fatiguer — la vérification mécanique —
est justement celui qui ne regardait pas : le gate n'inspecte que le dossier
du brief (constat P2-2 de l'audit précédent), et la CI n'exécute pas
`sim/`. Le volume et l'angle mort se sont additionnés sur la même PR.

# 5. Commandes rejouées et sorties

Toutes les commandes ci-dessous ont été exécutées sur un dépôt positionné
sur le commit audité (`git rev-parse HEAD` =
`a600532e714a9ff4d1b3c739859a9357884d5f81`), depuis la racine, avec
l'interpréteur du dépôt (`.venv/bin/python`). L'arbre de travail était
propre avant et après (`git status --porcelain` vide).

**5.1 — Gate mécanique et suites de tests.**

```
$ .venv/bin/python harness/verdict_audit.py harness/queue/briefs/011-sim-monde-vivant-amorcage
[PASS] files_declared_exist / mtime_after_brief / captures_differ_when_should /
       waivers_have_command_and_error / no_empty_sample_pass /
       verdict_numbers_traceable / no_bare_python_alias /
       verdict_is_not_self_authored / rubric_predates_deliverables /
       declared_files_are_tracked
VERDICT: ACCEPT

$ .venv/bin/python -m pytest sim/tests/ -q
20 passed in 0.37s

$ .venv/bin/python -m pytest harness/tests/ -q
314 passed, 16 skipped in 17.11s
```

**5.2 — Classification de la CI du commit audité** (détail au § 6) :

```
$ gh run view <id> --json workflowName,conclusion,jobs   (pour chaque run de a600532)
harness-ci     | success   - tests: success        - f0-demo: success
pipeline-audit | success   - invoke-cursor-auditor: success
audit-guard    | success   - schema: success       - cursor-scope: skipped
security       | success   - gitleaks: success     - actionlint: success
hermes-dashboard | success - regenerate: success
```

**5.3 — Historique du contre-audit** (constat P0-1) :

```
$ gh run list --workflow pipeline-challenge.yml --limit 12 \
    --json databaseId,headSha,createdAt,conclusion,event
2026-08-12T17:09:19Z push ee229e0 failure id=31621195096
2026-08-12T15:42:45Z push b8dbecd failure id=31613788360
2026-08-12T14:59:25Z push 01e7c24 failure id=31609830891
2026-08-12T14:05:02Z push bad1ffb failure id=31604845486
2026-08-12T14:03:28Z push 0302274 failure id=31604701305
2026-08-12T14:03:26Z push b85e2a7 failure id=31604699495
2026-08-12T13:58:17Z push 0677724 failure id=31604232544
2026-08-12T13:54:54Z push 8d9d8f2 failure id=31603929697
2026-08-12T13:53:59Z push 67206a9 failure id=31603848729
2026-08-12T13:53:46Z push 109df6a failure id=31603828676
2026-08-12T13:53:34Z push b759ee2 failure id=31603810355
2026-08-12T12:56:10Z push 6ab4f59 success id=31598872392

$ gh run view 31621195096 --json jobs
mechanical-scaffold-smoke: success
invoke-claude-challenger:  failure | étape en échec : « Invoke claude-challenger
                                     headless (/forge-audit-review) »

$ gh run view 31621195096 --log-failed   (extrait)
AUDIT_ID: CURSOR-3b47ffe-pr57-monde-sans-faim
CLAUDE_CODE_OAUTH_TOKEN: ***
{"type":"rate_limit_event","rate_limit_info":{"status":"rejected",
 "rateLimitType":"five_hour","overageStatus":"rejected"}}
{"result":"You've hit your org's monthly spend limit · ask your admin to raise
 it at claude.ai/settings/usage","api_error_status":429,"is_error":true,
 "total_cost_usd":0,"num_turns":1}
##[error]Process completed with exit code 1.
```

Le job `mechanical-scaffold-smoke` réussit à chaque fois : la moitié
non-LLM du rôle fonctionne, c'est bien la seule moitié qui produit une revue
qui est tombée.

**5.4 — Re-mesure du monde vivant sur le commit fusionné** (constat P1-3).
Script exécuté tel quel :

```python
import random, hashlib, json
from sim.world import World
from sim import engine
from sim.constants import (FOOD_PRODUCTION_KG_PER_KM2_PER_TICK as P,
                           FOOD_CONSUMPTION_KG_PER_PERSON_PER_TICK as C,
                           INITIAL_POPULATION_PER_KM2 as D)
w = World.from_g3(rng_seed=42); rng = random.Random(42)
print("cellules chargees      =", len(w.cells), "| aretes adjacence =", len(w.adjacency))
print("production/km2/tick    =", P, "| consommation/km2/tick (densite nominale) =", C*D)
pop0 = sum(c.population for c in w.cells.values())
stock0 = sum(c.food_stock_kg for c in w.cells.values())
N = 200
for _ in range(N): engine.tick(w, rng)
pop1 = sum(c.population for c in w.cells.values())
stock1 = sum(c.food_stock_kg for c in w.cells.values())
print("apres N=%d ticks :" % N)
print("  population %d -> %d (delta %d)" % (pop0, pop1, pop1 - pop0))
print("  stock kg   %.0f -> %.0f (facteur x%.1f)" % (stock0, stock1, stock1 / stock0))
print("  cellules hunger_ticks>0 :", sum(1 for c in w.cells.values() if c.hunger_ticks > 0))
print("  cellules food_stock_kg<=0 :", sum(1 for c in w.cells.values() if c.food_stock_kg <= 0.0))
def h(sw, sr):
    w = World.from_g3(rng_seed=sw); r = random.Random(sr); st = r.getstate()
    for _ in range(10): engine.tick(w, r)
    return hashlib.sha256(json.dumps(w.to_dict(), sort_keys=True).encode()).hexdigest(), st == r.getstate()
a, ua = h(42, 42); b, ub = h(42, 999999)
print("hash rng=42     :", a); print("hash rng=999999 :", b)
print("condenses egaux :", a == b, "| etat interne rng inchange :", ua, ub)
```

Sortie :

```
cellules chargees      = 596 | aretes adjacence = 1364
production/km2/tick    = 50.0 | consommation/km2/tick (densite nominale) = 20.0
apres N=200 ticks :
  population 66865505 -> 66865505 (delta 0)
  stock kg   4011930300 -> 43937193599 (facteur x11.0)
  cellules hunger_ticks>0 : 0
  cellules food_stock_kg<=0 : 0
hash rng=42     : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
hash rng=999999 : 3d41d13dec0c35bc26d423e580a200b27f1edde5fe7d7a90314f82d3e85e50a8
condenses egaux : True | etat interne rng inchange : True True
```

**5.5 — État réel de la file d'audits** (constat P2-1). Script stdlib
exécuté tel quel (croisement `architecture/inbox/*.md` ×
`architecture/audit-ledger.jsonl`), sortie abrégée aux lignes utiles :

```
2026-08-12T09:37:46Z  age=0.9 j  AUDIT_APPROVED     CURSOR-cdc683f-hermes-workflow-quatre-acteurs
2026-08-12T10:30:00Z  age=0.8 j  AUDIT_CHALLENGED   CURSOR-73022bd-hermes-dashboard-modele-auditeur
2026-08-12T12:45:00Z  age=0.7 j  PROPOSED (aucune ligne au ledger)  CURSOR-063d7eb-pr35-challenge-perte-decision
2026-08-12T12:45:00Z  age=0.7 j  PROPOSED (aucune ligne au ledger)  CURSOR-bb8fe11-hermes-console-adr-0011
... (8 autres audits dans le même état) ...
2026-08-12T17:15:00Z  age=0.5 j  PROPOSED (aucune ligne au ledger)  CURSOR-3b47ffe-pr57-monde-sans-faim

audits inbox: 25 | audit_id présents au ledger: 13 | sans aucune ligne : 12
événements AUDIT_PROPOSED au ledger : 0
Counter({'PROPOSED (aucune ligne au ledger)': 12, 'AUDIT_ARCHIVED': 7,
         'AUDIT_APPROVED': 3, 'AUDIT_CHALLENGED': 3})
```

**5.6 — Vérifications des deux fausses pistes** (constat P3-1) :

```
$ .venv/bin/python harness/harness_audit.py       (poste neuf)
[FAIL] (3 pt) fake_honest_demo_pair: missing: ['run_demo.log (has it been run?)']
[FAIL] (1 pt) no_premature_stub_content: unexpected files in stub dirs: ['sim/engine.py', ...]
SCORE: 20/24

$ .venv/bin/python harness/demo/fake_brief_001/run_demo.py && .venv/bin/python harness/harness_audit.py
SCORE: 23/24

$ .venv/bin/python hermes/dashboard.py && git diff --stat hermes/DASHBOARD.md
 hermes/DASHBOARD.md | 24 ++++-------------------- (4 insertions, 20 deletions,
 dont 15 lignes du tableau « Activité GitHub récente », indisponible hors CI)
$ git checkout -- hermes/DASHBOARD.md && git status --porcelain
(vide)
```

Limite de droits assumée : `gh api repos/PLiagre/ForgeHistory/branches/master/protection`
répond `403 Resource not accessible by integration`. Les règles de
protection de branche n'ont donc pas pu être lues ; aucun constat de cet
audit n'en dépend (voir P0-1, preuve 5).

# 6. Classification de la CI du commit audité

Commit `a600532` — **verte**, cinq workflows, huit jobs, aucun échec.

| workflow / job | état | remarque |
|---|---|---|
| `harness-ci` / `tests` | success | n'exécute que `harness/tests/` — voir P2-2 |
| `harness-ci` / `f0-demo` | success | rejet du faux brief |
| `pipeline-audit` / `invoke-cursor-auditor` | success | déclencheur du présent audit |
| `audit-guard` / `schema` | success | frontmatter des audits validé |
| `audit-guard` / `cursor-scope` | skipped | ne s'applique qu'aux PR de branche `cursor/*` |
| `security` / `gitleaks`, `actionlint` | success | — |
| `hermes-dashboard` / `regenerate` | success | a produit le commit suivant `7b09200` |
| `hermes-observer` (×2) | queued | encore en file au moment de l'audit |

Aucun workflow `pipeline-challenge` ne figure dans cette liste : la fusion ne
touche pas `architecture/inbox/*.md`, donc le contre-audit n'était pas
censé se déclencher ici. Sa dernière exécution reste celle du 2026-08-12
17:09, en échec (P0-1).

# 7. Risques par sévérité

| sévérité | constat | risque si rien n'est fait |
|---|---|---|
| P0 | P0-1 — fusion pendant la panne du contre-audit, sans porte ni signal | La chaîne à quatre acteurs devient déclarative : produire un audit suffit à dire qu'on a audité, même si personne ne le lit. Toute PR future peut être fusionnée dans le même angle mort. |
| P1 | P1-1 — refus fournisseur non prévu comme état | Chaque épuisement de plafond côté fournisseur arrête le maillon en silence et le relance à l'identique, sans repli vers un backend substituable pourtant contractualisé (ADR-0008/0009). |
| P1 | P1-2 — escalade log-only, invisible dans la vue du propriétaire | Le propriétaire décide sur une vue qui affiche des tâches, jamais l'état de santé de la boucle qui les alimente. |
| P1 | P1-3 — constats pré-fusion entrés sans arbitrage | `master` porte des mesures reproductibles jamais confirmées ni réfutées ; les couches suivantes du moteur se construiront dessus. |
| P2 | P2-1 — registre incapable de dire ce qui attend | Douze audits sans transition enregistrée : aucune ancienneté machine, aucun calcul de péremption (`AUDIT_STALE`) possible. |
| P2 | P2-2 — `sim/tests/` hors CI | Une régression du moteur laissera la CI verte. |
| P3 | P3-1 — deux fausses pistes | Aucun risque ; consigné pour éviter un faux constat au prochain tour. |

# 8. Briefs atomiques proposés (3, jamais plus)

Ces propositions ne sont **pas** des instructions et ne s'autorisent rien :
la source unique d'instruction reste un brief, écrit après décision du
propriétaire (`CLAUDE.md` › Single Source of Instruction).

1. **Faire du contre-audit une porte, et du refus fournisseur un état.**
   Que l'adjudication d'un audit (contre-audit puis décision) soit un
   préalable observable à la fusion de la PR qu'il critique, plutôt qu'un
   effet de bord d'un push déjà fusionné ; et qu'un refus du fournisseur
   (429, plafond d'organisation) devienne un état explicite du pipeline —
   consigné, escaladé, avec repli sur un backend substituable — au lieu d'un
   `exit 1` répété à l'identique. Ferme P0-1 et P1-1.
2. **Rendre l'état réel de la boucle lisible là où la décision se prend.**
   Que `hermes/DASHBOARD.md` porte la santé des workflows `pipeline-*`
   (dernière exécution, échecs consécutifs) et une action pour tout audit
   `PROPOSED` — pas seulement pour les `AUDIT_APPROVED` —, et que l'entrée
   d'un audit soit systématiquement écrite au registre pour que son
   ancienneté et sa péremption soient calculables. Ferme P1-2 et P2-1.
3. **Faire exécuter `sim/tests/` par la CI.** Le moteur est désormais du
   code de `master` ; ses 20 tests, dont les deux gardes structurelles,
   doivent tourner à chaque push comme ceux du harnais. Transparence : ce
   point avait déjà été proposé par l'audit `CURSOR-3b47ffe` (encore
   `PROPOSED`, donc jamais arbitré) ; il est re-proposé parce que son statut
   a changé — ce n'était qu'une réserve de branche, c'est maintenant l'état
   de `master`.

Le fond du moteur (unités de temps, chaîne faim → mortalité, générateur
aléatoire non consommé) n'est **pas** re-proposé ici : il est déjà couvert
par les propositions 1 et 2 de l'audit `CURSOR-3b47ffe`, qui attend
toujours son contre-audit. Le re-proposer gonflerait la file sans rien
ajouter.

# 9. Veille comparative — section compagnon `cursor-qa-scout`

Section produite par le rôle compagnon
(`architecture/agents/cursor-qa-scout.md`), en append-only à l'intérieur de
cet audit, comme le prévoit son contrat. Thème du cycle : **orchestration et
plafonds de coût**. Elle compare, elle n'instruit pas.

**Axe 1 — Portes de fusion (GitHub Actions, merge queues).** L'état de l'art
outillé consiste à faire d'une vérification une *condition de fusion*
déclarée : protection de branche avec vérifications requises, file de fusion
qui rejoue les contrôles sur la combinaison réelle « base + PR », et
retrait automatique de la PR dont un contrôle requis échoue [S4]. La
documentation insiste sur un point directement pertinent ici : une
vérification qui n'est pas déclenchée par l'événement attendu n'est jamais
rapportée, donc n'a aucun effet de porte [S4]. Dans Forge, le contre-audit
se déclenche sur `push: master` (§ 3, P0-1, preuve 5) : il ne peut
structurellement pas être une vérification de PR. L'écart n'est pas un
réglage manquant, c'est le déclencheur choisi.

**Axe 2 — Boucles agentiques et propagation de panne.** L'état de l'art
décrit le « rayon d'action » d'une panne : un signal d'arrêt inscrit dans
l'état partagé, lu par chaque nœud aval comme première opération, plutôt
qu'une exception qui laisse le reste de la chaîne avancer ; et une file de
mise en attente humaine pour les exécutions interrompues, plutôt qu'un
échec qui disparaît [S1]. La même littérature, côté CI, recommande une
règle d'arrêt explicite et déconseille l'auto-fusion au premier tour de
boucle [S6]. Forge possède la brique amont (`pipeline-failure-escalate`
détecte bien l'échec) mais s'arrête au journal (§ 3, P1-2) : le signal
existe, il ne se propage pas jusqu'à l'acteur qui décide.

**Axe 3 — Plafonds de coût.** L'état de l'art distingue le compteur du
frein : un budget qui mesure après coup n'empêche rien, seul un contrôle
prédictif — estimer le coût de l'étape suivante et refuser avant l'appel —
constitue une porte [S5] ; et lorsqu'une limite est atteinte, la réponse
attendue est une chaîne de repli (modèle moins cher, plan réduit, erreur
structurée remontée à l'humain), pas un échec brut relancé [S2]. La
littérature académique va dans le même sens avec une estimation pessimiste
du coût avant engagement [S3]. Forge a déjà la moitié amont (plafond mensuel
propre, plafond natif par appel, brief 009 lot 009b) ; il lui manque la
symétrique : que faire quand c'est le **fournisseur** qui refuse (§ 3,
P1-1).

**Déclaration de non-duplication.** Les douze briefs de
`harness/queue/briefs/**` ont été relus (001 à 011, y compris les deux
briefs numérotés 008). Aucun n'est ouvert : chacun porte un verdict tracé
`ACCEPT`. Les deux plus proches des constats ci-dessus ont été vérifiés
ligne à ligne : le brief 009 traite le plafond de la dépense **de Forge**
(lot 009b) et le câblage réel de `pipeline-challenge` (lot 009c), pas le
refus du fournisseur ; le brief 008 (`008-full-auto-automation-gaps`) livre
l'escalade `pipeline_job_failed` en se donnant explicitement pour non-but
le mécanisme de notification. Aucun constat de cet audit ne double donc un
brief ouvert ; les recoupements sont signalés en toutes lettres dans les
constats concernés (P1-1, P1-2) et dans la proposition 3 du § 8.

# 10. Sources externes

| # | source | date de publication | consulté le |
|---|---|---|---|
| S1 | Ranjan Kumar — *Multi-Agent Pipeline Orchestration and Failure Propagation: Designing for Blast Radius* — <https://ranjankumar.in/ai-control-plane-multi-agent-pipeline-orchestration-failure-propagation> — signal d'arrêt dans l'état partagé, lu par chaque nœud aval ; file d'attente humaine pour les exécutions interrompues | non affichée par l'éditeur | 2026-08-13 |
| S2 | TrueFoundry — *Rate Limiting AI Agents: Preventing LLM API Exhaustion with a 3-Layer Gateway* — <https://www.truefoundry.com/blog/rate-limiting-ai-agents-preventing-llm-api-exhaustion> — disjoncteur sur vélocité de dépense, chaîne de repli plutôt qu'échec brut ; désigne les pipelines agentiques de CI comme la classe la plus à risque (« there is no human pacing mechanism ») | non affichée pour l'article (page datée 2026 côté éditeur) | 2026-08-13 |
| S3 | *Budget-Constrained Agentic Large Language Models: Intention-Based Planning for Costly Tool Use*, arXiv:2602.11541 — <https://arxiv.org/pdf/2602.11541> — estimation pessimiste du coût d'un plan avant engagement, refus de l'appel qui épuiserait le budget restant | 2026-02 (identifiant arXiv) | 2026-08-13 |
| S4 | GitHub Docs — *About protected branches* (vérifications requises) et *Managing a merge queue* — <https://docs.github.com/en/repositories/configuring-branches-and-merges-in-your-repository/managing-protected-branches/about-protected-branches> — une vérification requise bloque la fusion ; une vérification non déclenchée par l'événement attendu n'est jamais rapportée | documentation vivante | 2026-08-13 |
| S5 | dreaming.press — *How to Enforce a Token Budget on an AI Agent (Not Just Measure It)* — <https://dreaming.press/posts/how-to-enforce-a-token-budget-on-an-ai-agent.html> — « un compteur n'est pas un frein » : seule une vérification prédictive, avant l'appel, constitue une porte | non affichée par l'éditeur | 2026-08-13 |
| S6 | samuelfaj.com — *When CI sends the failure back to the agent* (boucle agent auto-correctrice avec limites) — <https://www.samuelfaj.com/en/blog/when-ci-sends-the-failure-back-to-the-agent/> — règle d'arrêt explicite, preuve exécutée exigée dans la PR, pas d'auto-fusion au premier tour | non affichée par l'éditeur | 2026-08-13 |

Les cinq sources internes de `architecture/review-guidelines.md` restent
celles qui définissent la **forme** de cet audit ; les six ci-dessus sont
celles consultées pour ce commit, conformément à la preuve de fin des
contrats `architecture/agents/cursor-auditor.md` et
`architecture/agents/cursor-qa-scout.md`.
