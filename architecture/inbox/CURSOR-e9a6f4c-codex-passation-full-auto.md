---
audit_id: CURSOR-e9a6f4c-codex-passation-full-auto
auditor: cursor-cloud
target_branch: master
target_commit: e9a6f4cffe093e982fe262de0ef6e70d713206d3
created_at: 2026-08-10T20:42:02Z
audit_type: role-reallocation-and-handoff
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

Demande du propriétaire (2026-08-10, session Cursor Cloud
`bc-26801513-6a9f-4ead-a9dd-4504745779aa`) : brancher **Codex** comme
développeur du projet, garder **Cursor** comme auditeur externe des PR,
laisser **Hermes** produire les rapports quotidiens et les propositions
d'amélioration continue, le tout sur GitHub — et disposer d'un **prompt de
passation** permettant à Codex de terminer la full automatisation en une
session autonome.

Ce document fait trois choses, sans rien autoriser :

1. Il **mesure** l'état réel de la full automatisation au commit
   `e9a6f4c` (section 3).
2. Il **propose** la nouvelle répartition des rôles (section 4) et les
   briefs qui la rendraient officielle (section 7).
3. Il **fournit** le prompt de passation Codex, prêt à coller
   (section 6).

Constat central : la full automatisation n'est pas « presque finie ». Un
seul des trois maillons agents est en cours de câblage, et il attend une
re-évaluation. Le prompt de la section 6 est donc construit sur cet état
réel, pas sur l'état annoncé par `HANDOFF.md`.

# 2. Provenance et fraîcheur

- Branche cible : `master`, commit
  `e9a6f4cffe093e982fe262de0ef6e70d713206d3` (court `e9a6f4c`).
- Fraîcheur : **CURRENT** — `git fetch origin master` puis
  `git rev-parse origin/master` donnent ce même SHA au moment d'écrire.
- Branche documentaire : `cursor/codex-handoff-full-auto-79aa`.
- Environnement de mesure : conteneur Linux, Python du dépôt
  (`.venv/bin/python`), pas la machine Windows du propriétaire — les
  chemins Unity et `py` n'ont donc pas été exercés ici, et ce document ne
  prétend pas le contraire.

Commandes rejouées pour cet audit :

| commande | résultat observé |
|---|---|
| `python -m pytest harness/tests/ -q` | **268 passed, 16 skipped** |
| `python harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation` | **10/10, VERDICT: ACCEPT** (gate mécanique seul) |
| `python harness/harness_audit.py` | **20/24** (2 FAIL : `fake_honest_demo_pair`, `no_premature_stub_content`) |
| `python harness/audit_schema.py` | tous les audits valides |
| `rg -n "TODO\(operator" .github/workflows/` | 3 occurrences : `pipeline-audit.yml`, `pipeline-challenge.yml`, `pipeline-forge-run.yml` |
| `git log --oneline -3 -- .../009-.../verdict.md` puis `.../generator-log.md` | le verdict `de6db4b` **précède** l'itération 2 `a16b18c` |

# 3. État réel de la full automatisation

## 3.1 Le point le plus important

**Le lot 009a a été REJETÉ par l'Évaluateur, corrigé par une itération 2,
et cette itération 2 n'a jamais été re-jugée.** Le fichier `verdict.md`
du brief 009 se termine encore sur `Overall Verdict: REJECT (Lot 009a)`
et sur trois points de feedback (B1, B2, B3), alors que le commit
`a16b18c` postérieur annonce les avoir traités. Le gate mécanique répond
`ACCEPT`, mais le gate n'a jamais eu autorité pour clore un REJECT
d'Évaluateur — c'est exactement l'anti-pattern que
`.claude/skills/forge-harness/SKILL.md` interdit.

Conséquence pratique : **rien ne doit démarrer sur 009b/009c avant que
009a ait un verdict à jour.** La règle de dépendance du brief 009 le dit
déjà pour 009c.

## 3.2 Les trois maillons agents

| maillon | workflow | état réel |
|---|---|---|
| `claude-challenger` | `pipeline-challenge.yml` | **non câblé** — `TODO(operator` toujours présent ; c'est l'objet du lot 009c |
| `cursor-auditor` | `pipeline-audit.yml` | **non câblé** — `TODO(operator` ; hors périmètre du brief 009 |
| `forge-run` (développeur) | `pipeline-forge-run.yml` | **non câblé** — `TODO(operator` ; volontairement dernier |

Autrement dit : fournir les secrets aujourd'hui ne déclencherait aucun
appel d'agent, parce que le code qui les utiliserait n'existe pas encore.

## 3.3 Ce qui est réellement acquis

- Le ledger d'audits, la table de politique, l'orchestrateur, les quatre
  workflows `pipeline-*.yml` et l'escalade sur panne
  (`pipeline-failure-escalate.yml`) existent et sont testés.
- `mode:` vaut `full_auto_decision_only` (ADR-0007), avec un garde
  fail-closed qui refuse un `full_auto` nu tant que `forge-run` n'est pas
  câblé.
- **Hermes est branché** : `.github/workflows/hermes-observer.yml`
  écoute les PR et la fin des neuf workflows, en permissions **lecture
  seule**, et transmet l'événement à un runner auto-hébergé Windows
  (`runner-event.ps1`). Hermes n'écrit rien dans le dépôt aujourd'hui.
- La suite de tests est verte (268), ce qui rend un red-first honnête
  possible sans bruit de fond.

## 3.4 Ce qui manque pour parler de « full automatisation »

1. Verdict à jour sur 009a (bloquant, section 3.1).
2. Lot 009b — le module de plafond budgétaire CI.
3. Lot 009c — le câblage réel du maillon challenge.
4. Les deux maillons restants (`cursor-auditor`, `forge-run`), qui n'ont
   **aucun brief** aujourd'hui.
5. Le remplissage automatique d'un brief converti
   (`<<TODO>>` documenté), toujours ouvert.

Points 4 et 5 : pas de brief = pas d'instruction. Ils exigent une passe
Planificateur avant toute ligne de code.

# 4. Répartition des rôles proposée

| acteur | rôle proposé | écrit où | n'écrit jamais |
|---|---|---|---|
| **Codex** | Développeur (Générateur), et Évaluateur uniquement sur du travail produit par un autre | code, tests, `deliverables/` du lot qu'il produit | le `verdict.md` de son propre lot |
| **Cursor** | Auditeur externe des PR | `architecture/inbox/**` | code, CI, briefs |
| **Hermes** | Observateur, rapports quotidiens, propositions d'amélioration continue | rien aujourd'hui (lecture seule) ; à terme `architecture/inbox/**` | code, CI, briefs |
| **Propriétaire** | Décide, arbitre, merge | `architecture/decisions/**` | — |

Ce que cette répartition **ne change pas**, et ne doit pas changer :
« celui qui produit ne prononce pas la recevabilité » (ADR-0001). Codex
peut légitimement juger le lot 009a, parce que 009a a été produit par un
autre agent. Il ne peut pas juger 009b/009c s'il les produit.

Point d'attention honnête : `.claude/commands/forge-run.md` ne connaît
aujourd'hui que `--backend claude|cursor`. Faire de Codex un backend
officiel demande un wrapper conforme au contrat de
`harness/backends/README.md`, un ADR, et une mise à jour de la commande —
c'est le brief proposé BRIEF-PROP-002 ci-dessous, pas un détail
d'implémentation à improviser en séance.

# 5. Risques nommés

| # | risque | sévérité | conséquence si ignoré |
|---|---|---|---|
| R1 | Codex démarre 009b sans clore 009a | P0 | Un lot bâti sur un lot rejeté ; retravail garanti |
| R2 | Codex produit **et** juge le même lot | P0 | Rupture d'ADR-0001 ; verdict sans valeur |
| R3 | Codex modifie un chemin de la liste d'exclusion (`\.github/workflows/**` hors `pipeline-challenge.yml`, `verdict_audit.py`, `VISION.md`) | P1 | Contourne la seule barrière réelle, la protection de branche étant indisponible sur ce plan GitHub |
| R4 | Codex paraphrase le brief dans un autre fichier | P1 | `test_single_source_of_instruction.py` rouge, et perte de la source unique |
| R5 | Session sans suivi de budget | P2 | Dépassement silencieux ; le dépôt a déjà mesuré un Générateur à 982 appels |
| R6 | Hermes passé en écriture sans brief | P2 | Un quatrième acteur écrivant dans le dépôt sans contrat |

# 6. Prompt de passation Codex (prêt à coller)

Ce prompt est une **proposition de conduite de session**, pas une
autorisation : il envoie systématiquement Codex lire le brief comme seule
source d'instruction, et lui interdit d'inventer ce qu'aucun brief ne dit.

```text
Tu es l'agent de développement autonome du dépôt ForgeHistory
(github.com/PLiagre/ForgeHistory). Tu travailles seul pendant toute cette
session. Réponds et rédige TOUJOURS en français clair, sans jargon non
expliqué.

## 0. Ce que tu dois lire avant toute action (dans cet ordre)

1. CLAUDE.md — la carte du dépôt et les principes non négociables.
2. docs/rules/harness-roles.md — le contrat des trois rôles.
3. docs/rules/hard-won-rules.md — chaque règle vient d'un vrai défaut passé.
4. .claude/skills/forge-harness/SKILL.md — comment une tâche traverse le harnais.
5. HANDOFF.md — l'état déclaré (attention : il peut être en retard sur la réalité).
6. harness/queue/briefs/009-full-auto-agent-invocation/brief.md — TA SEULE
   SOURCE D'INSTRUCTION pour le travail de cette session.
7. harness/queue/briefs/009-full-auto-agent-invocation/verdict.md et
   feedback/feedback-009a.md — l'état du jugement en cours.

Ne recopie jamais le contenu du brief dans un autre fichier. Tu peux le
citer par chemin ; jamais le paraphraser. Un test du dépôt vérifie cette
règle et deviendra rouge si tu la casses.

## 1. Objectif de la session

Faire progresser la full automatisation le plus loin possible, dans
l'ordre imposé ci-dessous, sans jamais sauter une étape de vérification.
Terminer proprement une étape vaut infiniment mieux qu'en entamer trois.

## 2. Ordre imposé (ne pas réordonner)

ÉTAPE A — Rejuger le lot 009a (tu es ici ÉVALUATEUR).
  Contexte : le verdict actuel est un REJECT, écrit avant l'itération 2 du
  Générateur (commit a16b18c). Les trois points de feedback sont B1, B2, B3
  dans feedback/feedback-009a.md.
  Tu as le droit de juger ce lot : tu ne l'as pas produit.
  - Reconstruis CHAQUE compteur toi-même. Ne fais jamais confiance à un
    chiffre du manifest sans le recalculer par ta propre commande.
  - Vérifie B1, B2, B3 un par un, en citant la commande et sa sortie.
  - Fais une preuve « red-first » depuis une copie de travail jetable
    (jamais dans l'arbre du dépôt) : casse volontairement le garde, vérifie
    que le test vire au rouge, restaure.
  - Écris ton jugement en AJOUTANT une section à verdict.md. N'efface
    jamais le REJECT précédent : l'historique du jugement fait partie de la
    preuve.
  - Si tu conclus REJECT : écris feedback/feedback-009a-002.md, arrête
    l'étape A, et passe à l'étape B seulement si tu juges 009b réellement
    indépendant de la correction demandée (le brief dit qu'il l'est).

ÉTAPE B — Produire le lot 009b (tu es ici GÉNÉRATEUR).
  Le périmètre, les conditions de réussite, les compteurs obligatoires et
  les interdits sont dans brief.md. Ne les redéfinis pas ; applique-les.
  - Écris le test AVANT le code quand la condition parle d'un refus ou
    d'un garde, et prouve que le test échoue avant ta correction.
  - Chaque affirmation chiffrée que tu produis doit venir d'une commande
    réellement exécutée, avec sa sortie collée dans le journal.
  - Tu n'écris PAS verdict.md pour ce lot. Tu produis, tu ne juges pas.

ÉTAPE C — Produire le lot 009c (tu es ici GÉNÉRATEUR).
  À n'entreprendre que si A est conclu et B est passé au gate mécanique.
  Ce lot est le plus lourd du brief et son propre texte prévoit un point
  d'arrêt : si le budget atteint le seuil de checkpoint avant que toutes
  ses conditions soient remplies, ARRÊTE, écris le checkpoint, et propose
  la découpe prévue par le brief. Ne force pas jusqu'au seuil d'arrêt dur.

ÉTAPE D — Préparer la suite (tu es ici PLANIFICATEUR, pas développeur).
  Uniquement s'il reste du budget après C.
  Deux maillons restent non câblés (pipeline-audit.yml et
  pipeline-forge-run.yml) et aucun brief ne les couvre. Un agent sans
  brief n'a pas d'instruction : tu n'as donc PAS le droit de les câbler
  dans cette session. Ce que tu peux faire : rédiger un nouveau brief
  numéroté (brief.md + eval-rubric.md) au format du dépôt, en t'inspirant
  de la structure des briefs existants, et t'arrêter là.

## 3. Règles non négociables

- N'invoque jamais `python` nu : utilise `py` sur Windows, ou le Python
  du dépôt sur Linux. Un hook bloque l'appel nu, et un contrôle du gate le
  détecte dans les livrables.
- N'écris jamais le verdict d'un lot que tu as produit toi-même.
- Ne modifie aucun de ces chemins : `.github/workflows/**` (sauf
  `pipeline-challenge.yml`, et seulement à l'étape C),
  `harness/verdict_audit.py`, `VISION.md`. Ce sont les chemins que le
  dépôt refuse de fusionner automatiquement, et la protection de branche
  n'est pas disponible pour les défendre.
- Ne touche pas à `.github/workflows/hermes-observer.yml` : Hermes est un
  observateur en lecture seule, et son runner appartient au propriétaire.
- N'écris pas dans `architecture/inbox/**` : ce dossier appartient à
  l'auditeur externe, il est en append-only, et ce n'est pas ton rôle.
- Ne fusionne aucune pull request. Ne force jamais un push. Ne réécris
  jamais un commit existant.
- Ne pousse rien tant que la suite de tests n'est pas verte. Un hook du
  dépôt applique déjà cette règle ; ne cherche pas à la contourner.
- Si une vérification est impossible dans ton environnement, écris-le
  noir sur blanc avec la commande tentée et l'erreur obtenue. Une
  limitation déclarée est acceptable ; une limitation masquée par une
  formulation vague ne l'est pas.
- Ne fabrique jamais un horodatage, un hash, un chiffre ou une sortie de
  commande. Si tu ne l'as pas exécuté, tu ne l'as pas mesuré.

## 4. Preuves à produire pour chaque lot que tu génères

- `deliverables/manifest.json` : les fichiers produits, les compteurs
  demandés par le brief (chacun avec sa valeur, sa taille d'échantillon
  et la commande qui l'a mesuré), et les éventuelles dérogations (chacune
  avec la commande tentée et l'erreur réelle).
- `deliverables/generator-log.md` : en-tête `**Author**:
  forge-generateur-codex`, puis le récit de ce que tu as construit,
  comment tu as mesuré, ce qui a résisté, et ce que tu n'as pas pu faire.
- Le gate mécanique doit répondre ACCEPT avant que tu considères le lot
  livrable :
  `py harness/verdict_audit.py harness/queue/briefs/009-full-auto-agent-invocation`
- La suite complète doit être verte, avec sa sortie réelle et complète
  recopiée dans le journal : `py -m pytest harness/tests/ -q`

## 5. Budget et point d'arrêt

- Première action de chaque lot : estimer le nombre d'appels d'outils et
  faire tourner la vérification de découpe prévue par le dépôt
  (`py harness/budget.py split-check --brief <dossier> --estimated-calls <N>`).
- Pendant le travail : `py harness/budget.py status --brief <dossier>`.
- Seuils : avertissement à 100 appels, checkpoint à 130, arrêt dur à 160.
  Au checkpoint, tu écris l'état et tu t'arrêtes proprement — tu ne
  continues pas « puisque c'est presque fini ».
- Si deux itérations d'affilée n'améliorent rien, arrête et escalade au
  propriétaire. Ne rejoue pas le même prompt une troisième fois.

## 6. Travail sur GitHub

- Une branche par lot, préfixée `codex/`, par exemple
  `codex/009b-ci-budget-guard`.
- Des commits atomiques, en français, décrivant l'intention et non le
  diff.
- Une pull request en brouillon par lot, avec dans sa description : ce
  que le lot fait, la sortie du gate, la sortie des tests, et ce qui
  reste ouvert.
- Tu attends que la CI soit verte. Si un workflow échoue, tu lis le log
  et tu corriges — tu ne relances pas en espérant un résultat différent.
- Tu ne fusionnes pas. Cursor auditera la PR, le propriétaire tranchera.

## 7. Fin de session

Avant de rendre la main :
1. Suite de tests verte, sortie réelle recopiée.
2. Gate exécuté sur chaque lot touché, résultat cité.
3. `HANDOFF.md` réécrit à partir de l'état réel observé, pas de tes
   souvenirs : ce qui est fini, ce qui attend un jugement, ce qui est
   ouvert, et les risques connus.
4. Un compte-rendu final en français clair, avec : ce que tu as terminé,
   ce que tu as commencé sans finir et pourquoi, les décisions que tu
   laisses au propriétaire, et la prochaine action recommandée.

## 8. Ce que tu ne dois surtout pas faire

- Déclarer la full automatisation terminée alors que deux maillons
  restent non câblés. Le succès de cette session, c'est un pas honnête et
  prouvé — pas une annonce.
- Affaiblir un test pour le faire passer.
- Supprimer ou réécrire un verdict, un feedback ou un audit existant.
- Résoudre une question produit à la place du propriétaire : si une
  décision manque, écris-la dans ton compte-rendu final et arrête-toi.
```

# 7. Briefs proposés (proposés, jamais autorisés)

Trois au maximum, conformément à la discipline de l'auditeur.

## BRIEF-PROP-001 — Clore le brief 009 (jugement 009a, puis 009b et 009c)

- Origine : section 3.1 et 3.4 de ce document.
- Objectif : que le brief 009 atteigne un état terminal — verdict à jour
  sur 009a, puis les deux lots restants produits et jugés.
- Hors périmètre : les deux maillons non couverts par le brief 009.
- Ce brief existe déjà : c'est
  `harness/queue/briefs/009-full-auto-agent-invocation/brief.md`. Ce qui
  est proposé ici est son **exécution**, pas sa réécriture.
- Décision attendue du propriétaire : qui joue l'Évaluateur des lots
  produits par Codex, puisque Codex ne peut pas se juger lui-même.

## BRIEF-PROP-002 — Faire de Codex un backend de développement officiel

- Origine : demande propriétaire + section 4.
- Objectif : un wrapper conforme au contrat des backends, un ADR qui
  enregistre la décision (Codex développe, Cursor audite les PR, Hermes
  observe et propose), et la mise à jour de la commande d'orchestration
  pour connaître ce backend.
- Hors périmètre : câbler `pipeline-forge-run.yml` (dépend du brief 009).
- Risque nommé : un backend déclaré mais non mesuré par le compteur de
  coûts reproduirait le défaut déjà relevé pour le backend Cursor.

## BRIEF-PROP-003 — Donner à Hermes un contrat d'écriture

- Origine : section 3.3 (Hermes est branché mais n'écrit rien).
- Objectif : définir où Hermes dépose ses rapports quotidiens et ses
  propositions, sous quel format, avec quelles garanties de non-doublon
  vis-à-vis des briefs ouverts et des audits déjà déposés.
- Hors périmètre : tout droit d'implémentation pour Hermes — un rapport
  reste une entrée, jamais une instruction.
- Risque nommé : sans contrat, un quatrième acteur écrit dans le dépôt
  sans que personne puisse prouver qui a écrit quoi.

# 8. Décisions humaines requises

1. **Qui évalue le travail de Codex ?** Sans réponse, les lots 009b et
   009c seront produits mais jamais recevables.
2. **Codex devient-il un backend officiel** (BRIEF-PROP-002) ou reste-t-il
   un outil utilisé hors contrat ?
3. **Hermes doit-il écrire dans le dépôt** (BRIEF-PROP-003), ou rester un
   observateur qui ne produit que des rapports hors dépôt ?
4. **Le maillon `pipeline-audit.yml` doit-il appeler Cursor**, maintenant
   que Cursor est recentré sur l'audit des PR plutôt que sur l'audit des
   commits fusionnés ? Les deux ne sont pas le même déclencheur.
5. **`HANDOFF.md` doit être réécrit** : il décrit un état antérieur au
   brief 009 et à l'arrivée d'Hermes.
