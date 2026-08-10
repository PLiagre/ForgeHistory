---
audit_id: CURSOR-32640da-entry-point-force-proposition
auditor: cursor-cloud
target_branch: master
target_commit: 32640da5b3d2fbd484335f0f62aef65897f77e30
created_at: 2026-08-10T18:03:48Z
audit_type: entry-point-and-force-de-proposition
status: PROPOSED
implementation_authorized: false
ci_changes_authorized: false
code_changes_authorized: false
---

# 1. Résumé exécutif

Demande du propriétaire (session Cursor Cloud
`bc-26801513-6a9f-4ead-a9dd-4504745779aa`, 2026-08-10) :

> « j'aimerais que cursor sur ce projet soit plus qu'un auditeur. je
> voudrais qu'il soit mon point d'entrée pour des éléments
> d'amélioration du projet. qui soit force de proposition. »

État de fraîcheur : **CURRENT**. `origin/master` et `HEAD` pointent tous
deux sur `32640da5b3d2fbd484335f0f62aef65897f77e30`.

Verdict de cet audit : la demande est **fondée**, et elle est déjà
partiellement possible via `architecture/inbox/` — mais le contrat
actuel (ADR-0005 + `cursor-auditor.md`) réduit Cursor à un **réacteur
post-merge**. Ce n'est pas un point d'entrée, et ce n'est pas une force
de proposition continue. Ce document fait les deux choses à la fois :

1. **Propose** l'élévation de rôle (ADR-0007 + contrats), sans
   l'implémenter — les flags `*_authorized` restent à `false`, et cette
   PR `cursor/*` ne touche que `architecture/inbox/**`.
2. **Démontre** immédiatement le rôle demandé : un backlog priorisé
   d'améliorations, mesuré sur l'état live du dépôt au SHA ci-dessus.

## Cinq constats majeurs

1. **P0 — Cinq audits réels sont bloqués en `AUDIT_PROPOSED`.**
   `py harness/audits.py list` (venv) montre 5 audits non traités + 1
   fixture archivée. Aucune revue Claude, aucune décision propriétaire,
   aucune conversion — sauf le démo full-auto. La boucle « Cursor
   propose → Claude challenge → propriétaire tranche » n'est pas
   alimentée en pratique.
2. **P1 — ADR-0005 cadre Cursor comme auditeur en lecture seule
   déclenché par un merge.** Utile, mais trop étroit pour un point
   d'entrée propriétaire. Une conversation « j'aimerais améliorer X »
   n'a aujourd'hui aucun contrat de rôle dédié.
3. **P1 — `HANDOFF.md` est obsolète sur un point structurel.** Il demande
   encore de « review & merge PR #4 / `forge/cursor-audit-loop` », alors
   que `git merge-base --is-ancestor origin/forge/cursor-audit-loop
   origin/master` réussit : la boucle est déjà dans `master` (merge
   `198cfd9`, PR #10). Un point d'entrée qui ment sur l'état du projet
   n'est pas un point d'entrée.
4. **P1 — `harness_audit.py` marque encore `pipeline/geo/` et `unity/`
   comme stubs interdits** (`SCORE: 20/24`, FAIL
   `no_premature_stub_content`), alors que les briefs 002/003/007 les
   ont légitimement peuplés. Signal faux = priorisation fausse.
5. **P2 — Les briefs ouverts ne manquent pas ; ce qui manque, c'est un
   triage actionnable.** Briefs 004 (gate REJECT faute de logs Unity
   absents ici), 005, 006c, 007, 008 existent. Sans force de proposition
   qui classe et recommande, le propriétaire doit tout re-découvrir.

## Ce que cet audit n'autorise pas

Rien. Aucune écriture hors `architecture/inbox/**`. Aucune conversion
en brief. Aucune modification d'ADR, de workflow, ni de contrat d'agent.
Seule une décision propriétaire + conversion (ou un brief Claude issu
de cette proposition) peut faire bouger le contrat.

# 2. Provenance et fraîcheur

- Branche cible : `master`.
- Commit cible complet :
  `32640da5b3d2fbd484335f0f62aef65897f77e30` (court : `32640da`).
- Fraîcheur : **CURRENT** au moment de la rédaction.
- Branche documentaire :
  `cursor/entry-point-force-proposition-79aa`.
- Déclencheur : demande explicite du propriétaire dans cette session
  Cloud Agent, pas un `push` post-merge automatique.
- Preuves rejouées (venv `/workspace/.venv/bin/python`) :
  - `python -m pytest harness/tests/ -q` → **235 passed, 15 skipped**.
  - `python harness/audits.py list` → 5× `AUDIT_PROPOSED`, 1×
    `AUDIT_ARCHIVED`.
  - `python harness/harness_audit.py` → **20/24**, FAIL
    `fake_honest_demo_pair` + `no_premature_stub_content`.
  - `python harness/verdict_audit.py harness/queue/briefs/004-polish-visuel`
    → **REJECT** (`files_declared_exist` : logs Unity absents sur ce
    runner Linux ; les checks de timestamps passent).
  - `python harness/verdict_audit.py harness/queue/briefs/008-contexte-opus5-right-sizing`
    → **REJECT** (`verdict.md` absent — Générateur/Évaluateur non
    terminés).
  - `python harness/audit_schema.py` → **6/6 OK**.
  - `gh pr list --state open` → 1 PR ouverte
    (`cursor/setup-dev-environment-86b0`, draft).
  - `git merge-base --is-ancestor origin/forge/cursor-audit-loop origin/master`
    → succès (boucle déjà mergée).

# 3. Diagnostic du rôle actuel

## Ce qu'ADR-0005 a bien verrouillé (à conserver)

- Cursor ne développe pas le code produit qu'il audite.
- Une entrée inbox n'est **jamais** une instruction exécutable.
- Les trois flags `*_authorized` restent à `false`.
- Une PR `cursor/*` ne touche que `architecture/inbox/**`
  (`.github/workflows/audit-guard.yml` job `cursor-scope`).
- Un audit accepté redevient un **brief normal** — seule source
  d'instruction.

Ces garde-fous restent non négociables. « Force de proposition » ≠
« force d'exécution ».

## Ce qu'ADR-0005 rend trop étroit

| Dimension | Contrat actuel (`cursor-auditor`) | Besoin propriétaire |
|---|---|---|
| Déclencheur | `push` sur `master` (post-merge) | Conversation propriétaire + cycles proactifs |
| Livrable | Audit d'un commit | Proposition d'amélioration priorisée |
| Posture | Réactif (« ce merge a-t-il un défaut ? ») | Propositif (« voici quoi faire ensuite, et pourquoi ») |
| Point d'entrée | Non — Claude Code reste le développeur canonique ; Cursor n'est pas le canal d'idées | Oui — le propriétaire vient d'abord à Cursor pour déposer / faire émerger des améliorations |
| Suite | Challenge Claude → décision humaine → conversion | Identique (ne pas court-circuiter) |

Le trou n'est donc pas « Cursor devrait coder ». Le trou est : **il
n'existe pas de contrat qui dise à Cursor d'être le canal d'entrée et
la force de proposition**, en restant structurellement incapable
d'auto-autoriser l'implémentation.

# 4. Élévation proposée (ADR-0007 — proposé, non accepté)

## Décision proposée

Cursor Cloud devient le **point d'entrée propriétaire** pour les
éléments d'amélioration de ForgeHistory, et une **force de
proposition** permanente. ADR-0005 n'est pas annulé : il est
**étend**u. Cursor reste interdit d'implémenter et d'autoriser ; il
gagne le droit (et le devoir) de proposer hors cycle post-merge.

Deux modes complémentaires, même périmètre d'écriture
(`architecture/inbox/**`) :

1. **Mode audit** (existant) — `CURSOR-<sha>-<slug>.md`, déclenché par
   un merge, contrat `cursor-auditor.md`.
2. **Mode proposition** (nouveau) — même schéma de frontmatter (pour
   ne pas casser `audit_schema.py` / le ledger), déclenché par :
   - une demande propriétaire (« je veux améliorer X ») ;
   - un cycle proactif planifié (`workflow_dispatch` / automation) ;
   - une session Cloud Agent dont le brief métier est « proposer le
     prochain meilleur coup ».

`audit_type` distingue les modes (`architecture-and-qa` vs
`improvement-proposal` / `entry-point-and-force-de-proposition`). Le
lifecycle ledger reste le même : `PROPOSED → CHALLENGED →
APPROVED/REJECTED → CONVERTED → …`.

## Ce que le mode proposition doit produire (contrat)

Un fichier inbox qui, pour être recevable, contient au minimum :

1. **État live mesuré** (commandes rejouées, sorties citées).
2. **Backlog priorisé** (P0–P3) avec preuves, pas des opinions.
3. **≤ 3 briefs atomiques** convertibles (même discipline que
   `cursor-auditor`).
4. **Décisions humaines explicites** (autoriser / refuser / reporter).
5. **Déclaration de non-autorisation** (flags + prose).
6. **Anti-doublon** : croisement avec `harness/queue/briefs/**` ouverts
   et audits `AUDIT_PROPOSED` non convertis.

## Ce qui doit changer hors inbox (via brief Claude, pas cette PR)

| Livrable | Pourquoi |
|---|---|
| `docs/adr/0007-cursor-as-entry-point-and-proposal-force.md` | Figer la décision ; pointer ADR-0005 comme étendu, pas superseded aveuglément |
| `architecture/agents/cursor-proposer.md` | Contrat à 7 sections, miroir de `cursor-auditor.md` |
| Mise à jour `architecture/README.md` + `architecture/agents/README.md` | Dualité audit / proposition ; table d'invocation |
| Mise à jour `CLAUDE.md` Routing | `architecture/**` → ADR-0005 **et** ADR-0007 |
| Règle Cursor persistante (`.cursor/rules` ou équivalent projet) | Pour que chaque session interactive hérite du devoir de proposition |
| Optionnel : `/forge-propose` (slash command Claude) | Déclencher un challenge/convert sur une proposition, symétrique à `/forge-audit-*` |

## Alternatives écartées (pour l'ADR)

### Alt. 1 — Laisser ADR-0005 tel quel et « bien se comporter » en conversation
- **Pour** : zéro structure.
- **Contre** : le comportement meurt avec la session ; aucun ledger ;
  aucune conversion mécanique.
- **Pourquoi non** : une force de proposition non versionnée n'est pas
  une force de proposition Forge.

### Alt. 2 — Autoriser Cursor à ouvrir des PR de code depuis ses propositions
- **Pour** : moins d'étapes.
- **Contre** : autodéveloppement + auto-bénédiction ; brise ADR-0001 /
  ADR-0005.
- **Pourquoi non** : déjà rejeté dans ADR-0005 Alternative 2 ; la
  demande propriétaire parle d'*entrée* et de *proposition*, pas
  d'exécution.

### Alt. 3 — Remplacer les audits par des GitHub Issues
- **Pour** : UX familière.
- **Contre** : état hors dépôt ; pas de gate ; pas de ledger.
- **Pourquoi non** : même motif qu'ADR-0005 Alternative 3.

# 5. Démonstration immédiate — backlog priorisé (état live `32640da`)

Voici le rôle demandé, exercé maintenant. Priorités pour le
propriétaire. Ce n'est **pas** une autorisation d'implémenter.

## P0 — Débloquer la boucle d'entrée elle-même

| ID | Élément | Preuve live | Action proposée |
|---|---|---|---|
| PROP-P0-1 | 5 audits `AUDIT_PROPOSED` jamais challengés | `audits.py list` | Lancer `/forge-audit-review` (ou bulk) sur les audits non-fixture, en commençant par `CURSOR-5633ee7-automation-completeness` (incident CI réel documenté) |
| PROP-P0-2 | `HANDOFF.md` dit encore « merge PR #4 / audit-loop » | texte HANDOFF vs `merge-base` OK | Réécrire HANDOFF via `/forge-checkpoint` pour refléter master réel |
| PROP-P0-3 | Élévation Cursor → point d'entrée | cette demande propriétaire | Accepter BRIEF-PROP-001 ci-dessous |

## P1 — Signaux faux / briefs bloqués

| ID | Élément | Preuve live | Action proposée |
|---|---|---|---|
| PROP-P1-1 | `harness_audit.py` stub assumption | FAIL `no_premature_stub_content`, score 20/24 | Brief dédié : apprendre que `pipeline/geo/` et `unity/` sont peuplés légitimement |
| PROP-P1-2 | Brief 008 rédigé, jamais exécuté | gate REJECT, `verdict.md` missing | `/forge-run harness/queue/briefs/008-contexte-opus5-right-sizing` après relecture Planificateur (provenance déjà déclarée dans le brief) |
| PROP-P1-3 | Brief 004 gate REJECT ici faute de logs Unity | `files_declared_exist` liste des `Logs/v004_*.log` absents | Sur machine Unity : confirmer présence des logs ou waiver explicite ; ne pas « fake-fix » depuis Linux |
| PROP-P1-4 | Audits 6231186 / POSTMERGE / bbe6da5 / 5633ee7 : briefs proposés non convertis | sections « Briefs proposés » des audits + ledger vide pour ces IDs | Challenge → décision → convert des points encore vrais ; archiver le reste en `STALE`/`REJECTED` |

## P2 — Produit jeu / carte

| ID | Élément | Preuve | Action proposée |
|---|---|---|---|
| PROP-P2-1 | Bug orientation labels (hors scope 004) | `004-polish-visuel/deliverables/generator-log.md` + HANDOFF | Brief dédié « map label upside-down » |
| PROP-P2-2 | Suite geo après G3 | `geo-pipeline-port-plan.md` briefs topic-only 004/005 plan ; brief 007 en cours | Continuer le plan geo (rivières/relief) une fois 007 stabilisé |
| PROP-P2-3 | Brief 005 refonte visuelle | dossier présent ; owner-verdict 004 a transféré les griefs | Décision propriétaire : prioriser 005 vs geo vs automation |

## P3 — Hygiène documentaire (bas risque, bas urgence)

| ID | Élément | Note |
|---|---|---|
| PROP-P3-1 | Liens morts dans `VISION.md` | Porté depuis plusieurs sessions ; ADR éventuel |
| PROP-P3-2 | Pas de README.md humain | Porté ; utile quand le point d'entrée Cursor sera officiel |
| PROP-P3-3 | Mismatch phase-order `forge-run.md` | Porté ; pure doc |

## Anti-doublon (briefs / audits déjà ouverts)

Vérifié avant rédaction :

- `008-contexte-opus5-right-sizing` couvre déjà le right-sizing Opus 5
  issu de `CURSOR-198cfd9` — **non re-proposé** ici.
- `006-full-auto-agent-pipeline` couvre la dérrogation full-auto —
  les BRIEF-PROP de `CURSOR-5633ee7` restent candidats à conversion,
  pas à réécriture.
- Aucun brief ouvert ne porte « Cursor point d'entrée / force de
  proposition » — c'est le trou que BRIEF-PROP-001/002/003 ferment.

# 6. Briefs proposés à Claude (proposés, non autorisés)

Ces briefs sont **proposés, jamais autorisés**. Seule une conversion
explicite (propriétaire, ou policy `full_auto` après challenge) peut
les transformer en travail réel. Maximum 3, discipline
`cursor-auditor`.

## BRIEF-PROP-001 — ADR-0007 : Cursor point d'entrée et force de proposition

- Finding source : demande propriétaire + constats §1 / §3.
- Objectif : accepter et publier
  `docs/adr/0007-cursor-as-entry-point-and-proposal-force.md` qui
  étend ADR-0005 (Cursor reste non-développeur ; il devient canal
  d'entrée + proposition proactive), et mettre à jour
  `docs/adr/README.md` + la ligne Routing de `CLAUDE.md`.
- Hors périmètre : implémenter des propositions concrètes de produit ;
  toucher au harness de brief ; activer `full_auto` ; écrire du code
  Unity/geo.
- Fichiers probablement concernés : `docs/adr/0007-*.md`,
  `docs/adr/README.md`, `CLAUDE.md` (Routing seulement).
- Critères d'acceptation : ADR `Status: accepted` (ou `proposed` si le
  propriétaire préfère trancher en deux temps) ; Routing CLAUDE.md
  pointe ADR-0005 **et** ADR-0007 ; aucune paraphrase des Success
  Conditions d'un brief existant.
- Budget estimé : 30–50 appels outils (documentaire).
- Risques : confusion « proposition = autorisation » — mitigation :
  l'ADR doit répéter noir sur blanc les trois flags et le
  `cursor-scope`.
- Rollback : marquer l'ADR `deprecated` et retirer la ligne Routing.
- Dépendances : aucune.

## BRIEF-PROP-002 — Contrat `cursor-proposer` + README architecture

- Finding source : trou de contrat §3.
- Objectif : ajouter `architecture/agents/cursor-proposer.md` (7
  sections obligatoires), mettre à jour
  `architecture/README.md` et `architecture/agents/README.md` pour
  documenter la dualité audit/proposition et la table d'invocation
  (conversation propriétaire, `workflow_dispatch`, automation).
- Hors périmètre : câbler les secrets Cloud Agent ; écrire les
  invocations réelles dans `pipeline-*.yml` (déjà BRIEF-PROP-003 de
  l'audit 5633ee7).
- Fichiers probablement concernés :
  `architecture/agents/cursor-proposer.md`,
  `architecture/README.md`, `architecture/agents/README.md`.
- Critères d'acceptation : le contrat interdit toujours toute écriture
  hors `architecture/inbox/**` et tout flag `*_authorized: true` ;
  la preuve de fin exige backlog priorisé + ≤3 briefs + anti-doublon.
- Budget estimé : 40–70 appels outils.
- Risques : dérive vers un second développeur — mitigation : section
  `# Interdits` alignée mot pour mot sur les gardes CI existantes.
- Dépendances : BRIEF-PROP-001 (l'ADR donne la légitimité) ; peut être
  fusionné en un seul brief si le Planificateur juge atomique.

## BRIEF-PROP-003 — Règle Cursor persistante + hygiène du signal d'entrée

- Finding source : PROP-P0-2, PROP-P1-1, besoin de continuité inter-sessions.
- Objectif : (a) ajouter une règle projet Cursor (`.cursor/rules` ou
  mécanisme équivalent déjà accepté par l'écosystème Cursor du
  propriétaire) qui impose le devoir de proposition et le dépôt
  inbox ; (b) corriger `harness_audit.py` pour que
  `no_premature_stub_content` ne punisse plus les peuplements issus
  des briefs 002/003/007 ; (c) exiger qu'un
  `/forge-checkpoint` réécrive HANDOFF sans la TODO morte « merge PR
  #4 ».
- Hors périmètre : traiter les 5 audits en attente (c'est une
  opération propriétaire / Claude challenger, pas ce brief) ; Unity.
- Fichiers probablement concernés : `.cursor/rules/**` (ou fichier
  choisi), `harness/harness_audit.py`, éventuellement un test sous
  `harness/tests/`, `HANDOFF.md` via checkpoint en fin de brief.
- Critères d'acceptation : `harness_audit.py` ≥ 23/24 sans FAIL
  `no_premature_stub_content` ; HANDOFF ne mentionne plus PR #4 /
  `forge/cursor-audit-loop` comme travail ouvert ; la règle Cursor
  cite ADR-0007 par pointeur.
- Budget estimé : 60–90 appels outils.
- Risques : la forme exacte des règles Cursor (fichier vs Cloud
  template) dépend du produit Cursor — le brief doit mesurer ce qui
  existe dans le repo / la doc Cursor au moment de l'exécution, pas
  inventer un chemin.
- Dépendances : BRIEF-PROP-001 fortement recommandé avant.

# 7. Décisions humaines requises

1. **Autoriser ou refuser** BRIEF-PROP-001 / 002 / 003 — ce document
   n'autorise aucune implémentation.
2. **Confirmer le modèle** : Cursor = point d'entrée + proposition ;
   Claude = développement + challenge ; propriétaire = décision
   (sauf `full_auto` déjà cadré par ADR-0006).
3. **Choisir le premier audit à challenger** parmi les 5
   `AUDIT_PROPOSED` — recommandation : `CURSOR-5633ee7-automation-completeness`
   (preuve d'incident CI), puis `CURSOR-6231186-execution-budgets`.
4. **Trancher la priorité produit** après l'élévation de rôle :
   automation (briefs issus de 5633ee7) vs brief 008 vs geo 007 vs
   brief 005 visuel.
5. **Décider si les sessions Cloud Agent conversationnelles** (comme
   celle-ci) doivent *toujours* déposer un fichier inbox quand le
   propriétaire demande une amélioration — recommandation : **oui**,
   c'est précisément le point d'entrée.

# 8. Comment utiliser Cursor comme point d'entrée dès maintenant

Même avant l'acceptation d'ADR-0007, le canal existe déjà :

```
Propriétaire → session Cursor (Cloud ou IDE)
            → fichier architecture/inbox/CURSOR-<sha>-<slug>.md
            → (optionnel) challenge Claude
            → décision propriétaire
            → /forge-audit-convert → brief
            → /forge-run
```

Règles de conduite pour toute session Cursor sur ce dépôt, en attendant
le contrat formel :

1. Mesurer avant de proposer (commandes live, pas mémoire).
2. Écrire dans `architecture/inbox/**` uniquement.
3. Proposer ≤ 3 briefs atomiques, avec anti-doublon.
4. Ne jamais poser `*_authorized: true`.
5. Finir par une liste de décisions humaines, pas par un « j'ai
   commencé à coder ».

Ce fichier est la première application de ces règles à la demande
explicite du propriétaire.

# 9. Sources externes

Comparaison courte « agent as intake / proposal force vs auditor-only »,
consultée le 2026-08-10 :

| Source | Date consultation | Pertinence | Limite |
|---|---|---|---|
| [Cursor — Cloud Agents](https://cursor.com/docs/cloud-agent) | 2026-08-10 | Les Cloud Agents sont déjà un canal d'entrée conversationnel versionné (branche + PR) — cohérent avec un rôle « intake » | Doc produit, pas une preuve que ce dépôt l'utilise bien |
| [Anthropic — How we built our multi-agent research system](https://www.anthropic.com/engineering/built-multi-agent-research-system) | 2026-08-10 | Séparer l'agent qui explore/propose de l'agent qui exécute réduit les auto-confirmations | Contexte research, pas Forge |
| [GitHub — About issue intake and triage automation](https://docs.github.com/en/issues/tracking-your-work-with-issues/learning-about-issues/about-issues) | 2026-08-10 | Un point d'entrée d'améliorations a besoin d'un objet durable (ici : fichier inbox versionné, pas seulement le chat) | Issues GitHub écartées volontairement (ADR-0005 Alt. 3) |

# 10. Clôture

Cursor sur ForgeHistory peut rester un auditeur utile — et devenir en
plus le **point d'entrée** que le propriétaire demande, à condition
d'accepter l'élévation de contrat (BRIEF-PROP-001…003) sans lâcher les
garde-fous d'ADR-0005.

Prochaine action recommandée au propriétaire, dans l'ordre :

1. Lire et trancher les 3 briefs proposés.
2. Demander le challenge Claude de *ce* fichier.
3. Lancer un `/forge-checkpoint` pour tuer la TODO morte de HANDOFF.
4. Choisir le premier audit `AUDIT_PROPOSED` à faire avancer.
)