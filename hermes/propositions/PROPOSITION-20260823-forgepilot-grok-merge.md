---
author: cursor-cloud
kind: proposition
created_at: 2026-08-23T10:00:00Z
concerns: projet
status: OPEN
---
# ForgePilot : Grok 4.6 partout sauf le code, juge haute capacité seulement avant fusion

Ce n’est pas une instruction. Ce n’est pas un brief. Le propriétaire tranche.

## Constat

Aujourd’hui `control-plane/workflow-policy.toml` envoie **Claude Opus 5**
sur le plan (effort `xhigh` / `max`) et sur la revue (`low` / `high`),
pour **chaque** lot R1 et R2. Cursor `composer-2.5` ne fait que le code.
Hermes lance et notifie, sans boucle de poursuite. La fusion reste
humaine. ADR-0014 et la politique interdisent à Hermes de fusionner.

Le propriétaire veut : moins de Claude ; Opus 5 n’est plus le juge ;
**Cursor Grok 4.6** pour le maximum de tâches ; Composer seulement pour
écrire le code ; un modèle haute capacité **uniquement** avant une PR
finale ; fusion automatique si tests, checks et relecture IA sont verts ;
Hermes qui **lit l’avancement** et relance des sous-agents pour
continuer.

## Ce que disent les pratiques 2026 (sources externes)

- Cursor : planifier avant de coder ; **Grok 4.6** pour le travail long
  et difficile (efforts `low` / `medium` / `high` / `xhigh`, défaut
  `high`) ; **Composer** pour le code quotidien, rapide et bon marché.
  Revue = passe séparée, pas l’auteur qui se relit.
  https://cursor.com/blog/agent-best-practices
  https://cursor.com/help/models-and-usage/grok-4-6
- Routage : un modèle fort pour décider, un modèle bon marché pour
  exécuter. Mélanger n’est rentable que si les sous-tâches sont
  indépendantes. Le juge et l’auteur ne doivent pas être le même
  agent (contexte neuf, idéalement autre famille).
- Auto-fusion : checks déterministes d’abord (CI, secrets, tests),
  puis revue IA **bloquante** sur le SHA exact, invalidée à chaque
  nouveau commit. La doc publique garde souvent un humain sur
  `main` ; ici le propriétaire remplace cet humain par **un seul
  passage haute capacité** avant la PR finale.

## Cible proposée (qui fait quoi)

| Étape | Qui | Modèle | Effort | Quand |
|---|---|---|---|---|
| Cadence, lecture d’état, relance | Hermes | son modèle Nous Portal | — | toujours |
| Diagnostic bloqué (logs, CI, contradictions) | sous-agents Hermes | lecture seule | — | seulement si blocage |
| Plan | Cursor CLI `--mode=plan` | `cursor-grok-4.6` | R1 `high` · R2 `xhigh` | chaque lot |
| Code et correctifs | Cursor | `composer-2.5` | aucun (cuit dans le modèle) | chaque lot |
| Tests / CI | mécanique | — | — | chaque poussée |
| Relectures intermédiaires | **aucune** haute capacité | — | — | jamais |
| Juge avant PR finale | Cursor, **nouvelle** invocation | `cursor-grok-4.6` | **`xhigh`** | une fois, SHA final, draft → ready |
| Claude | **absent par défaut** | — | — | réserve R2 seulement si le propriétaire nomme un autre modèle que Opus 5 |
| Fusion | ForgePilot, pas un clic Discord | — | — | CI verte + juge `PASS` sur **ce** SHA |

Composer n’écrit que le code. Grok planifie et, une seule fois, juge.
Hermes ne juge pas et n’écrit pas le code.

Pourquoi ce découpage : Grok 4.6 est le modèle Cursor prévu pour les
longues sessions et le suivi d’instructions ; Composer reste le moins
cher pour les éditions. La revue haute capacité une seule fois évite
de payer Opus à chaque lot. Auteur (Composer) et juge (Grok, contexte
neuf) sont distincts.

## Hermes : piloter, pas seulement déléguer

Hermes tient une **boucle** dont la vérité est `forgepilot status` +
`gh pr checks` + le SHA, pas le récit d’un sous-agent.

1. Rien d’actif → prochain pas `ROADMAP.md` + brief existant →
   `forgepilot start --run`.
2. Lot en cours → silencieux tant que l’étape ne change pas ;
   notifier le changement.
3. Blocage (tests, fournisseur, revue intermédiaire Composer) →
   jusqu’à trois sous-agents **lecture** (doc, dépôt, contre-exemples)
   → Hermes décide `resume` / `iterate` / escalade propriétaire.
4. Draft + CI verte + SHA stable → **un** juge Grok `xhigh`.
5. Juge `PASS` + checks toujours verts sur le même SHA →
   `forgepilot merge` (nouveau).
6. Après fusion → rapport + dashboard + prochain pas.

Les sous-agents Hermes ne survivent pas à un redémarrage. La boucle
durable = cron / processus suivi / `forgepilot status`, pas
`delegate_task` pour un lot entier.

## Fusion automatique — conditions strictes

Autoriser **seulement** si tout est vrai en même temps :

- checks GitHub requis verts (harness, sim, forgepilot, audit, gitleaks,
  risk-gate, …) ;
- profil de tests du risque (`pr` ou `certify`) passé sur ce SHA ;
- juge haute capacité `PASS` sur **ce** SHA (pas un SHA plus ancien) ;
- conversations de revue résolues ou absentes ;
- pas de label `do-not-merge` / escalade propriétaire.

Un nouveau commit **annule** le juge. Il faut le relancer. Échec juge
→ `iterate` (Composer) puis nouveau juge. Hermes n’invente pas un
PASS.

R0 reste sans agent (profil `fast` seulement).

## Ce que le dépôt refuse aujourd’hui (à changer par brief)

La politique code en dur :

- planificateur / reliseur = Claude ou `none` seulement ;
- `can_merge` d’Hermes doit rester faux (Hermes ne clique pas) ;
- Cursor n’a pas le droit à `--effort` (vrai pour Composer ; faux
  pour Grok 4.6, dont l’effort est `low`–`xhigh`, souvent via le
  slug `cursor-grok-4.6-xhigh` ou `--mode=plan`).

Donc : un **ADR** qui amende 0014 / 0015 (juge = Grok haute
capacité une fois ; fusion mécanique si portes vertes), puis un
**brief** qui :

1. ouvre `cursor` comme backend plan / review ;
2. encode la table ci-dessus dans `workflow-policy.toml` ;
3. ajoute `forgepilot merge` (checks + SHA + juge, zéro jugement
   Hermes) ;
4. met à jour skill `forgehistory-suivi` (boucle § ci-dessus) ;
5. ajuste les tests `control-plane/`.

Claude n’est pas réintroduit tant que le propriétaire n’a pas nommé
un modèle Claude autre qu’Opus 5.

## Ce que le propriétaire doit trancher

1. Accepter cette table (Grok plan + juge `xhigh` final, Composer
   code, Claude absent) ?
2. La fusion auto s’applique-t-elle à **R1 et R2**, ou R2 reste
   manuel le temps d’un lot pilote ?
3. Slug exact à pinner après `agent models` sur le VPS
   (`cursor-grok-4.6` vs `cursor-grok-4.6-xhigh`).
4. Si oui : session Claude pour écrire le brief, puis ForgePilot
   exécute ce brief (le premier lot de la nouvelle politique).
