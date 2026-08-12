---
audit_id: CURSOR-949ecf1-pr42-revue-non-consommable
auditor: cursor-cloud
target_branch: forge-bot/review-CURSOR-e849633-hermes-demande-pilotage-31598805647
target_commit: 949ecf1c60d36a6657689d67f8b044b5ff0f7a61
created_at: 2026-08-12T14:05:00Z
audit_type: architecture-and-qa
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

**Objet** : critique de la pull request [#42](https://github.com/PLiagre/ForgeHistory/pull/42)
« challenge: revue de l'audit CURSOR-e849633-hermes-demande-pilotage ».
Conduite selon `architecture/review-guidelines.md` (six lentilles, sévérités
P0–P3, une preuve citée par constat). Cet audit **n'instruit rien** : il
propose, la décision reste à la boucle (`architecture/README.md`,
ADR-0005/0006).

**Le fait central, mesuré, pas déduit** : le fichier livré par cette PR est
illisible par l'étape qui doit le consommer immédiatement après la fusion.
Ses dix verdicts sont écrits en gras (`**CONFIRMED**`), et le moteur de
décision `audit_decision._parse_point_verdicts` n'en capte **aucun**. J'ai
rejoué la fusion en bac à sable : `pipeline-orchestrate` sort en **code 2**,
aucun fichier de décision n'est écrit, l'audit `CURSOR-e849633` reste bloqué.
Ce n'est pas une hypothèse : le même échec s'est produit en vrai **une heure
avant**, sur la fusion de la PR #31 (run
[31603243066](https://github.com/PLiagre/ForgeHistory/actions/runs/31603243066),
13:47:25Z), avec le message d'erreur mot pour mot identique.

**Antériorité, déclarée d'emblée** : la cause de fond est déjà décrite par
l'audit `CURSOR-779d97c-revue-verdicts-illisibles` (déposé le 2026-08-12 à
12:20Z, encore `PROPOSED`, ses `P0-1`/`P0-2`/`P1-3`). Je ne le redécouvre pas
et je ne rejoue pas ses briefs. Ce que cet audit ajoute, et lui seul :

1. la **récurrence** — le fichier fautif de #42 a été écrit à 12:59:53Z, soit
   *après* le dépôt de 779d97c, et amendé à 13:48:39Z, soit *après* l'échec
   observé de #31 : rien dans la chaîne n'a empêché la troisième occurrence ;
2. la mesure que cette PR est **éligible à l'auto-fusion machine** (le
   merge-bot a validé son périmètre), donc que l'échec peut se produire sans
   aucun humain dans la boucle ;
3. une **correction partielle serait pire que l'échec actuel** : retirer le
   gras seul récupère 9 verdicts sur 10 et perd silencieusement un point
   `PARTIAL` retenu (mesure en § 4.2) ;
4. la garde « aucun verdict » de `record_challenge` est **infalsifiable par
   construction**, parce que le gabarit fournit lui-même les quatre jetons
   qu'elle cherche (§ 4.3) ;
5. les deux `PARTIAL` de la revue sont **résolubles** : je les ai résolus en
   deux commandes, et leur cause est l'absence de `GH_TOKEN` dans l'étape
   d'invocation du challenger (§ 4.4).

**Rien de ce qui suit n'invalide le fond de la revue livrée.** Son contenu
technique tient : j'ai vérifié indépendamment la classification CI qu'elle
laissait en suspens, et elle est exacte. Le défaut est de **forme**, et la
forme est ici le contrat entre deux machines.

# 2. Provenance et périmètre

| | |
|---|---|
| PR | #42, ouverte le 2026-08-12T12:59:55Z, non-brouillon |
| base → tête | `master` → `forge-bot/review-CURSOR-e849633-hermes-demande-pilotage-31598805647` |
| commit audité | `949ecf1c60d36a6657689d67f8b044b5ff0f7a61` |
| auteur de la PR | `app/github-actions` (bot) |
| diff net | 1 fichier, +114 / −0 |
| fichier | `architecture/reviews/CLAUDE-CURSOR-e849633-hermes-demande-pilotage.md` (créé) |
| état | `MERGEABLE`, `mergeStateStatus: UNSTABLE` |

Deux commits :

- `0caab3a` (12:59:53Z, `forge-bot`) : dépose la revue **et** une ligne de
  ledger `AUDIT_CHALLENGED`.
- `949ecf1` (13:48:39Z, `Cursor Agent` + `Pierre-Edouard Liagre`) : retire la
  ligne de ledger. Diff rejoué : `architecture/audit-ledger.jsonl | 1 -`.

Le diff net de la PR est donc bien **un seul fichier sous
`architecture/reviews/`**.

# 3. Classification de la CI du commit audité

```
$ gh api "repos/PLiagre/ForgeHistory/commits/949ecf1c60d36a6657689d67f8b044b5ff0f7a61/check-runs?per_page=100" \
    --jq '"total=\(.total_count)", (.check_runs | group_by(.conclusion // "en_cours") | map("\(.[0].conclusion // "en_cours"): \(length) [\(map(.name)|unique|join(", "))]") | .[])'
total=16
en_cours: 1 [Reconcile local Hermes state]
skipped: 2 [cursor-scope]
success: 13 [Reconcile local Hermes state, actionlint, check-and-automerge, f0-demo, gitleaks, invoke-cursor-auditor, schema, tests]
```

**Verte : aucun échec.** Les deux `skipped` sont structurels (`cursor-scope`
ne s'exécute que sur une branche `cursor/*`, or la tête est `forge-bot/*`).
Le job encore en cours est `Reconcile local Hermes state`.

C'est précisément le problème de la lentille 3 (« portes mécaniques
d'abord ») : **treize portes vertes, et aucune ne mesure la seule propriété
dont dépend l'étape suivante.** `schema` valide `architecture/inbox/**`
(`harness/audit_schema.py:26` : `INBOX = REPO_ROOT / "architecture" /
"inbox"`) ; rien ne valide `architecture/reviews/**` avant la fusion.

# 4. Constats

## 4.1 — P0-1 : la fusion de cette PR casse la boucle, et le précédent est déjà mesuré

**Preuve 1 — le fichier livré ne contient aucun verdict lisible.**

```
$ python3 -c "import sys; sys.path.insert(0,'harness'); import audit_decision; \
    print(len(audit_decision._parse_point_verdicts(open('/tmp/review42.md').read())), 'points')"
0 points
```

Le motif attendu par le consommateur (`harness/audit_decision.py:65`) exige
que la cellule soit **exactement** le jeton :

```
^\|\s*(\d+)\s*\|.*?\|\s*(CONFIRMED|REFUTED|PARTIAL|NEEDS_OWNER)\s*\|
```

Les dix cellules livrées, telles quelles :

```
**PARTIAL**
**CONFIRMED**          (x7)
**CONFIRMED** (mesurabilité) / **NEEDS_OWNER** (reformulation)
**PARTIAL — non rejouable ici**
```

**Preuve 2 — simulation de la fusion, en bac à sable, sans toucher au dépôt.**

```
$ python3 harness/pipeline/orchestrator.py run --event review_recorded \
    --payload '{"audit_id": "CURSOR-e849633-hermes-demande-pilotage"}' \
    --ledger /tmp/sim42/ledger.jsonl --inbox /tmp/sim42/inbox \
    --reviews /tmp/sim42/reviews --decisions /tmp/sim42/decisions
error: /tmp/sim42/reviews/CLAUDE-CURSOR-e849633-hermes-demande-pilotage.md has no
'| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
EXIT=2
--- ledger resultant ---
{"timestamp": "...", "audit_id": "CURSOR-e849633-hermes-demande-pilotage",
 "event": "AUDIT_CHALLENGED", "actor": "claude", "review": "...",
 "verdicts": {"CONFIRMED": 9, "REFUTED": 1, "PARTIAL": 5, "NEEDS_OWNER": 3}}
--- decisions ---
(vide)
```

L'audit s'arrête à `AUDIT_CHALLENGED`, aucune décision n'est produite, et en
CI réelle le job échoue **avant** son `git push` : même la ligne de ledger est
perdue.

**Preuve 3 — ce n'est pas une prédiction, c'est un précédent.** Le run
[31603243066](https://github.com/PLiagre/ForgeHistory/actions/runs/31603243066)
(fusion de la PR #31, 13:47:25Z, conclusion `failure`) :

```
error: .../architecture/reviews/CLAUDE-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md
has no '| N | ... | VERDICT | ... |' rows; --policy auto refuses to guess a verdict
##[error]Process completed with exit code 2.
```

**Preuve 4 — l'ampleur, sur les revues déjà fusionnées.**

```
ZERO 0 CLAUDE-CURSOR-5633ee7-automation-completeness.md
ZERO 0 CLAUDE-CURSOR-65c3ac1-dashboard-hermes-modele-auditeur.md
ZERO 0 CLAUDE-CURSOR-73022bd-hermes-dashboard-modele-auditeur.md
OK   19 CLAUDE-CURSOR-779d97c-revue-verdicts-illisibles.md
OK    1 CLAUDE-CURSOR-FIXTURE-full-auto-demo.md
OK    9 CLAUDE-CURSOR-cdc683f-hermes-workflow-quatre-acteurs.md
ZERO 0 CLAUDE-CURSOR-e9a6f4c-codex-passation-full-auto.md
```

Quatre revues sur sept sont muettes pour le moteur. Corrélation avec les
exécutions de `pipeline-orchestrate` : `success` sur la fusion de #26 (revue
`cdc683f`, 9 points lisibles), `failure` sur celles de #30 et #31 (0 point).

**Preuve 5 — élément nouveau : la machine peut fusionner seule.** Job
`check-and-automerge` du run 31603378519, 13:48:58Z :

```
Changed files:
architecture/reviews/CLAUDE-CURSOR-e849633-hermes-demande-pilotage.md
All changed paths are within the allowlist and none are denied.
```

`architecture/reviews/` est dans l'allowlist (`.github/workflows/merge-bot.yml:50`).
Le défaut n'attend donc pas une décision humaine pour se produire.

**Sévérité P0** : la PR ne tient pas l'affirmation de sa propre description
(« La fusion de cette PR déclenche pipeline-orchestrate.yml (event
review_recorded) ») — le workflow se déclenche et **échoue**. C'est la
lentille 2 : « ça marche » n'est pas adossé à une preuve d'exécution. La cause
racine est déjà consignée dans `CURSOR-779d97c` (`P0-1`, `P0-2`) et attend
l'arbitrage ; **ce constat-ci porte sur la fusion de cette PR précise**, pas
sur une redécouverte.

## 4.2 — P1-1 : une correction limitée au gras perdrait silencieusement un point retenu

Élément nouveau, mesuré. Le brief 1 proposé par `CURSOR-779d97c` évoque
« accepter le gras **et** une cellule nuancée ». Voici pourquoi le « et » n'est
pas cosmétique :

```
AVANT (fichier livré tel quel)            : 0 points
APRÈS (gras retiré sur la colonne Verdict) : 9 points
  [(1,'PARTIAL'), (2,'CONFIRMED'), (3,'CONFIRMED'), (4,'CONFIRMED'),
   (5,'CONFIRMED'), (6,'CONFIRMED'), (7,'CONFIRMED'), (8,'CONFIRMED'),
   (9,'CONFIRMED')]
```

Neuf, pas dix. Le point 10 reste invisible parce que sa cellule porte du texte
en plus du jeton (`**PARTIAL — non rejouable ici**`). Or `PARTIAL` fait partie
des verdicts **retenus** (`harness/audit_decision.py:255` : `retained =
sorted({n for n, v in points if v in ("CONFIRMED", "PARTIAL")})`).

Conséquence : aujourd'hui la panne est **bruyante** (code 2, tout le monde le
voit). Après une correction « gras seulement », elle deviendrait **silencieuse**
— un `AUDIT_APPROVED` d'apparence normale avec `retained_points` amputé du
point 10, et donc une semence de brief incomplète. C'est exactement le piège
« porte de test affaiblie pour faire passer » de la lentille 6.

## 4.3 — P1-2 : la garde « aucun verdict » ne peut jamais se déclencher, et le ledger publie un `REFUTED` fantôme

`harness/audit_review.py:126-133` compte les **mots dans tout le document** :

```
$ python3 -c "... print(audit_review.parse_verdicts(text))"
{'CONFIRMED': 9, 'REFUTED': 1, 'PARTIAL': 5, 'NEEDS_OWNER': 3}
```

Décompte réel de la colonne « Verdict » du même fichier :

```
{'CONFIRMED': 8, 'PARTIAL': 2, 'NEEDS_OWNER': 1}   # REFUTED : 0
```

La **seule** occurrence de `REFUTED` du fichier :

```
Un verdict par point : CONFIRMED / REFUTED / PARTIAL / NEEDS_OWNER.
```

Cette ligne n'est pas de l'auteur : c'est le gabarit qui l'écrit
(`harness/audit_review.py:75`, `scaffold_text`). D'où l'élément nouveau, plus
grave que le décompte faux déjà signalé par `CURSOR-779d97c` (`P1-3`) :

> **toute** revue issue du gabarit contient au moins une occurrence des quatre
> jetons ; donc `parse_verdicts` ne peut **jamais** renvoyer un dictionnaire
> vide ; donc la garde de `harness/audit_review.py:173-178` — « a challenge
> with no verdict is not a challenge » — est **infalsifiable**. Elle est écrite
> comme une porte fail-closed, elle ne peut mécaniquement pas se fermer.

Preuve que c'est bien ce compteur qui alimentait le ledger : la ligne retirée
par le commit `949ecf1` portait `{"CONFIRMED": 9, "REFUTED": 1, "PARTIAL": 5,
"NEEDS_OWNER": 3}` — identique au caractère près à la sortie de
`parse_verdicts` ci-dessus.

Sévérité P1 et non P0 : la décision automatique n'utilise pas ce champ (elle
relit les lignes), donc le `REFUTED` fantôme salit la trace sans fausser le
verdict. Mais c'est une trace **publiée**, et la règle `review_all_refuted`
(`harness/pipeline/auto_policy.yaml:32-35`) raisonne, elle, sur du « tout
REFUTED » : deux définitions de « un verdict » coexistent dans une même boucle.

## 4.4 — P2-1 : le challenger est privé de `GH_TOKEN`, donc ses `PARTIAL` sont une propriété du câblage, pas du monde

La revue déclare honnêtement, dès son § 1, ne pas avoir eu de `gh` authentifié,
et met en `PARTIAL` deux constats sourcés par l'API GitHub. J'ai vérifié la
cause dans le workflow. Étape d'invocation, `.github/workflows/pipeline-challenge.yml` :

```yaml
      - name: Invoke claude-challenger headless (/forge-audit-review)
        if: steps.check.outputs.available == 'true'
        env:
          CLAUDE_CODE_OAUTH_TOKEN: ${{ secrets.CLAUDE_CODE_OAUTH_TOKEN }}
          ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
          AUDIT_ID: ${{ steps.resolve.outputs.audit_id }}
```

Aucun `GH_TOKEN` — alors que d'autres étapes du **même fichier** en ont
(lignes 60 et 174). Le contre-audit est donc structurellement incapable de
vérifier tout fait sourcé par l'API GitHub, et dégrade en `PARTIAL` par
discipline.

J'ai résolu ses deux `PARTIAL` en deux commandes. Le point 10 portait sur la
classification CI du commit `e849633` (l'audit annonçait 13 `success` /
4 `skipped` / 1 `cancelled`) :

```
$ gh api "repos/PLiagre/ForgeHistory/commits/e8496336391ada87719ee0fa210de4d71a8f9487/check-runs?per_page=100" ...
total=18
cancelled: 1
skipped: 4
success: 13
```

**Exact, au check près.** Le `PARTIAL` ne traduisait aucun doute sur le fait :
il traduisait un secret manquant. Un contre-audit dont la moitié des réserves
s'expliquent par une ligne de YAML absente coûte deux invocations de modèle
pour rendre un verdict que la CI aurait pu rendre complet du premier coup — le
gaspillage que les sources S6/S7 décrivent comme le vrai poste de dépense des
boucles agentiques.

## 4.5 — P2-2 : le frontmatter de la revue n'est vérifié par personne, et il est faux

```yaml
review_of: CURSOR-e849633-hermes-demande-pilotage
reviewer: claude-code
target_commit: e8496336391ada87719ee0fa210de4d71a8f9487
reviewed_at: 2026-08-12T15:10:00Z
```

Deux défauts, une seule cause (aucun validateur) :

1. `reviewed_at: 15:10:00Z` est **dans le futur**. Le commit qui introduit le
   fichier est daté `2026-08-12T12:59:53Z`, et l'heure au moment de cet audit
   est `2026-08-12T13:51:30Z` (`date -u -Is`). La revue s'horodate 2 h 10 après
   sa propre écriture, à la minute ronde.
2. `reviewer: claude-code` est codé en dur dans le gabarit
   (`harness/audit_review.py:67`), alors que la PR annonce
   « produit headless par claude-challenger ». Le champ ne distingue donc pas
   le rôle qui a produit l'artefact — ce que `architecture/README.md` § « Un
   seul rôle écrit dans chaque dossier » présente pourtant comme la propriété
   à pouvoir prouver mécaniquement.

Vérification de l'absence de contrôle :

```
$ rg -n "reviewed_at" harness/ .github/ --glob '!*.md'
harness/audit_review.py:69     reviewed_at: <<TODO: ISO 8601 UTC, ...>>
harness/tests/... (fixtures uniquement)
harness/pipeline/demo/run_full_auto_demo.sh:139  reviewed_at: $(date -u ...)
```

Seule la démo horodate depuis l'horloge ; le chemin réel accepte n'importe
quelle chaîne.

## 4.6 — P3-1 : ce que cette PR fait bien, et qu'une correction ne doit pas défaire

- **Le retrait de la ligne de ledger est correct et bien motivé.** Le commit
  `949ecf1` invoque la « convention post-#46 » : vérifié, la PR #46 a été
  fusionnée à 13:42:12Z et déplace l'écriture `PROPOSED → CHALLENGED` après la
  fusion (`harness/pipeline/orchestrator.py:169-177`, `if state in (None,
  "AUDIT_PROPOSED")`). La PR #42, ouverte à 12:59Z, précédait ce correctif :
  l'amendement manuel était la bonne action.
- **Taille (lentille 5)** : 1 fichier, 114 lignes. Très en dessous du seuil
  d'environ 400 lignes au-delà duquel une relecture honnête décroche. Aucun
  découpage à demander.
- **Séparation des rôles (lentille 4)** : la revue est produite par un acteur
  distinct de l'auteur de l'audit, et ne touche ni code, ni test, ni workflow
  — conforme aux « Interdits » de `architecture/agents/claude-challenger.md`.
- **Honnêteté méthodologique** : la revue déclare sa limite d'environnement
  avant ses conclusions, plutôt que d'affirmer une vérification qu'elle n'a pas
  faite. C'est l'inverse de la correction hallucinée. Le défaut relevé en
  § 4.4 porte sur le **câblage**, pas sur cette honnêteté.

# 5. Ce que la description de la PR affirme, et ce que la mesure dit

| Affirmation de la PR / de la revue | Mesure |
|---|---|
| « La fusion de cette PR déclenche pipeline-orchestrate.yml (event review_recorded) » | Déclenché et **rouge** : code 2, aucune décision écrite (§ 4.1, preuves 2 et 3) |
| « Un verdict par point » (contrat du challenger) | La table existe et est renseignée pour un lecteur humain ; **0** verdict lisible par le consommateur machine (§ 4.1) |
| Ledger `AUDIT_CHALLENGED` écrit par le module dédié | Correct sur le principe, mais le champ `verdicts` publierait `REFUTED: 1` sur une revue qui n'en contient aucun (§ 4.3) |
| « chronologie / check-runs non rejouables dans ce bac à sable » | Vrai pour l'environnement du challenger, **faux comme propriété du fait** : rejoué ici, exact au check près (§ 4.4) |
| `reviewed_at: 2026-08-12T15:10:00Z` | Postérieur de 2 h 10 au commit qui l'introduit ; jamais validé (§ 4.5) |

# 6. Risques par sévérité

| Sévérité | Constat | Preuve |
|---|---|---|
| **P0** | Fusionner cette PR reproduit un échec CI déjà observé ; l'audit `CURSOR-e849633` reste bloqué, et le merge-bot peut le faire sans humain | § 4.1 (preuves 1 à 5) |
| **P1** | Une correction limitée au gras rendrait la panne silencieuse et amputerait `retained_points` du point 10 | § 4.2 |
| **P1** | La garde « aucun verdict » est infalsifiable (le gabarit fournit les jetons) ; le ledger publierait un `REFUTED` fantôme | § 4.3 |
| **P2** | Le challenger n'a pas de `GH_TOKEN` : ses réserves sont un artefact de câblage, résolubles en deux commandes | § 4.4 |
| **P2** | Frontmatter de revue non validé : horodatage dans le futur, `reviewer` codé en dur | § 4.5 |
| **P3** | Points conformes à préserver (convention post-#46, taille, séparation des rôles, honnêteté déclarée) | § 4.6 |

# 7. Briefs atomiques proposés (3, plafond du contrat respecté)

Propositions, pas instructions — la conversion reste à la décision de la
boucle. Je **ne rejoue pas** les briefs 1 et 2 de `CURSOR-779d97c` (lecteur
unique de verdicts ; porte de schéma sur `reviews/**`) : ils couvrent déjà la
cause racine et attendent l'arbitrage. Les trois ci-dessous sont additifs.

1. **Verrouiller la recette du lecteur unique sur `10/10`, pas sur `9/10`.**
   Si le brief 1 de `CURSOR-779d97c` est retenu, exiger que sa fixture rouge
   soit la revue de la PR #42 **avec 10 points attendus**, de sorte qu'une
   implémentation acceptant seulement le gras soit rejetée par le test. La
   mesure de contrôle existe déjà toute faite (§ 4.2 : 0 avant, 9 avec le gras
   seul, 10 attendus). Objectif : interdire que la panne bruyante d'aujourd'hui
   devienne une perte silencieuse de point retenu.

2. **Rendre falsifiable la garde de `record_challenge`.** Deux volets
   indissociables : (a) que le gabarit ne fournisse plus lui-même les quatre
   jetons de verdict (ligne de rappel hors du texte compté, ou comptage
   restreint aux lignes de table) ; (b) que le champ `verdicts` du ledger soit
   le décompte des lignes réellement captées par le consommateur. Test rouge
   disponible : une revue sans aucune ligne de verdict mais issue du gabarit
   doit être **refusée** — elle est acceptée aujourd'hui.

3. **Donner au challenger de quoi vérifier ce qu'on lui demande de vérifier.**
   Ajouter un `GH_TOKEN` en lecture seule à l'étape « Invoke claude-challenger
   headless » de `pipeline-challenge.yml`, et distinguer explicitement, dans
   `architecture/agents/claude-challenger.md`, « non rejouable » de « non
   rejoué faute d'accès » — le second n'étant pas une réserve légitime mais un
   défaut d'outillage à signaler comme tel. Preuve du gain : les deux `PARTIAL`
   de cette revue, résolus ici en deux commandes (§ 4.4).
   ⚠️ Pour l'arbitrage : ce brief touche `.github/workflows/**`, chemin de la
   denylist du merge-bot — il ne peut pas passer par l'auto-fusion.

# 8. Sources externes

Recherche du 2026-08-12 sur « autonomous AI dev pipeline », « agent
orchestration CI » et « token budget LLM agents ». URL + date de consultation.

| # | source | consulté le |
|---|---|---|
| S1 | Augment Code — *CI/CD for AI Agents: How to Integrate Agent Orchestration into Your Pipeline* — <https://www.augmentcode.com/guides/cicd-ai-agents-pipeline-integration> | 2026-08-12 |
| S2 | Augment Code — *How AI Agent Verification Prevents Production Bugs Before Merge* — <https://www.augmentcode.com/guides/ai-agent-pre-merge-verification> | 2026-08-12 |
| S3 | TruLayer — *Orchestration patterns for agentic dev — what we learned shipping a product with AI agents* — <https://trulayer.ai/blog/orchestration-patterns-for-agentic-dev/> | 2026-08-12 |
| S4 | OSSA (Open Standard Agents) — *Specification: The Normative Contract for AI Agents*, v0.4.9 — <https://openstandardagents.org/specification/> | 2026-08-12 |
| S5 | tesserine/runa — *Schema-validated artifact handoffs between agent steps* — <https://github.com/tesserine/runa> | 2026-08-12 |
| S6 | waxell.ai — *AI Agent Token Budget Enforcement (2026)* — <https://waxell.ai/blog/ai-agent-token-budget-enforcement> | 2026-08-12 |
| S7 | Cockroach Labs — *Managing Agentic AI Costs at Scale* (analyse Gartner mars 2026) — <https://www.cockroachlabs.com/blog/agentic-ai-costs-at-scale/> | 2026-08-12 |

Ce que ces sources apportent, en une phrase chacune :

- **La porte de vérification ne change le résultat que si elle est
  obligatoire, au bon point du flux** [S1, S2] : ici treize portes sont vertes
  et aucune ne tourne sur la propriété dont dépend l'étape suivante.
- **Un artefact qui passe d'un agent à l'autre doit être validé contre son
  schéma à la frontière de réception, sinon la panne est silencieuse et
  tardive** [S4, S5] : c'est mot pour mot ce que montre le § 4.1, la validation
  arrivant après la fusion au lieu d'avant.
- **Un agent qui meurt ou échoue après avoir poussé son travail doit avoir un
  acteur distinct qui surveille l'état d'après** [S3] : le job qui écrit le
  ledger échoue avant son `push`, et personne ne relève l'audit resté bloqué.
- **Le poste de coût des boucles agentiques est la tâche refaite, pas l'appel
  unitaire** [S6, S7] : deux invocations de modèle pour un contre-audit dont
  deux constats sur dix restaient en réserve faute d'un secret présent
  ailleurs dans le même workflow.

# 9. Ce que cet audit ne fait pas

Il ne décide rien, n'autorise aucune implémentation, ne modifie aucun audit
existant, ne touche aucun chemin hors de `architecture/inbox/**`, et ne
demande la fusion ni le rejet de la PR #42 : il expose ce qui est mesuré et
laisse l'arbitrage au policy engine ou au propriétaire
(`architecture/README.md`, ADR-0005/0006). Les trois flags `*_authorized` de
son frontmatter valent `false`.

Il ne redécouvre pas non plus la cause racine déjà consignée par
`CURSOR-779d97c-revue-verdicts-illisibles`, encore en attente d'arbitrage :
il en établit la **récurrence** sur une PR ouverte, en quantifie deux
conséquences nouvelles (perte silencieuse d'un point retenu ; garde
infalsifiable), et signale un défaut de câblage distinct (§ 4.4).

Commandes rejouables, à la racine du dépôt, pour tout contrôler :

```bash
git show 949ecf1:architecture/reviews/CLAUDE-CURSOR-e849633-hermes-demande-pilotage.md > /tmp/review42.md
python3 -c "import sys; sys.path.insert(0,'harness'); import audit_decision, audit_review; \
  t=open('/tmp/review42.md').read(); \
  print('lignes lisibles :', audit_decision._parse_point_verdicts(t)); \
  print('comptage de mots :', audit_review.parse_verdicts(t))"
gh run view 31603243066 --log-failed | tail -5
gh api "repos/PLiagre/ForgeHistory/commits/e8496336391ada87719ee0fa210de4d71a8f9487/check-runs?per_page=100" \
  --jq '.check_runs | group_by(.conclusion) | map("\(.[0].conclusion): \(length)") | .[]'
```
