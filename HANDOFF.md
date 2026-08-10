# HANDOFF.md

État de reprise vérifié le 2026-08-10. Ce fichier décrit l'état réel utile à
la prochaine session ; l'historique détaillé reste dans Git.

## Point de départ

- Branche par défaut : `master`, commit `1d973d9` au début et à la fin de la
  session. Aucun merge n'a été effectué.
- Jalon général : F0 terminé ; F1 en cours. Le jeu Unity porté, le pipeline
  géographique et les travaux visuels antérieurs n'ont pas été modifiés dans
  cette session.
- Source d'instruction du travail full-auto :
  `harness/queue/briefs/009-full-auto-agent-invocation/brief.md`.
- `py -m pytest harness/tests/ -q` sur `master` : **284 passed**, zéro échec.
- `py harness/audit_schema.py` : les sept audits sont valides.
- `py harness/harness_audit.py` : un seul contrôle reste rouge,
  `no_premature_stub_content`. Le contrôle suppose encore que `pipeline/geo/`
  est un stub vide alors que ce répertoire a été rempli par des lots acceptés ;
  ne pas supprimer son contenu pour satisfaire l'audit.

## Brief 009 : état exact

| lot | état | preuve / blocage |
|---|---|---|
| 009a — séparation du mode | **REJECT, itération 2 rejugée** | PR brouillon [#16](https://github.com/PLiagre/ForgeHistory/pull/16), commit `c9e9291`, CI verte. Gate mécanique vert et 284 tests verts. Le nouveau jugement est ajouté à `verdict.md`, sans effacer le premier REJECT. Quatre corrections C1-C4 sont demandées dans `feedback/feedback-009a-002.md`. |
| 009b — plafond budgétaire CI | **produit, non jugé** | PR brouillon [#17](https://github.com/PLiagre/ForgeHistory/pull/17), commit `cd89141`, CI verte. Dix tests ciblés, 294 tests complets et gate mécanique vert. Aucun Évaluateur indépendant n'a encore écrit de verdict pour ce lot. |
| 009c — invocation réelle de challenge | **non commencé, bloqué** | Ne pas commencer tant que 009a n'a pas un ACCEPT explicite et que 009b n'a pas franchi son jugement indépendant. |

Le `VERDICT: ACCEPT` du gate mécanique de 009b ne vaut pas jugement humain :
le `verdict.md` actuellement sur `master` concerne 009a. Le Générateur de 009b
n'a pas écrit son propre verdict.

## Ce qui a été fait dans cette session

### Réévaluation de 009a

Le garde, les compteurs, les frontières de périmètre et la suite ont été
reconstruits indépendamment. Une preuve red-first a été faite dans une copie
Git jetable hors du dépôt : rendre permissif le cas du workflow vide fait
échouer exactement le test attendu, puis la restauration le remet au vert.

Les corrections précédentes ont fermé la mention d'activation périmée et les
cas vide/espaces/non-UTF-8. La recherche adverse a cependant trouvé :

- trois faux workflows encore acceptés parce que le garde cherche seulement
  les sous-chaînes `jobs:` et `runs-on:` ;
- une documentation qui dit encore que le mode active ou arrête une boucle,
  alors qu'aucun workflow ne lit ce mode ;
- une sortie complète de l'itération 2 absente du journal ;
- une commande de compteur restée limitée à l'ancien intervalle Git.

Le détail exact et les commandes sont dans la PR #16. 009b étant indépendant,
sa génération a pu continuer ; 009c ne l'est pas.

### Génération de 009b

La PR #17 ajoute un module autonome de budget CI, un ledger JSONL suivi par
Git et dix tests. Le module :

- additionne le coût du mois civil UTC et refuse dès le plafond atteint ;
- remet uniquement la ligne `mode:` de la configuration à `manual` lors de ce
  refus ;
- réutilise `harness/backends/ledger.py` pour le prix et la lecture des
  transcripts ;
- ajoute les mesures au ledger sans réécrire l'historique ;
- marque les dépassements par invocation après coup avec `over_cap: true`.

La production a respecté le red-first. Le ledger n'est pas ignoré par Git.
Le périmètre 009b ne touche aucun workflow, aucun ADR et aucune valeur de
configuration.

## Fait nouveau à arbitrer avant 009c

La commande réellement exécutée `claude --help` expose maintenant :

```text
--max-budget-usd <amount>  Maximum dollar amount to spend on API calls
                           (only works with --print)
```

L'hypothèse de planification selon laquelle aucun plafond natif n'existait est
donc devenue fausse dans l'environnement actuel. 009b conserve le marquage
post-hoc demandé. Avant de rédiger ou produire 009c, décider explicitement si
l'appel headless doit aussi utiliser `--max-budget-usd 5`. Ne pas présenter
ce choix comme déjà autorisé par un texte qui disait l'option inexistante.

## Full automatisation : ne pas surannoncer

Les trois occurrences réelles suivantes existent toujours sur `master` :

```text
.github/workflows/pipeline-audit.yml       TODO(operator...)
.github/workflows/pipeline-challenge.yml   TODO(operator...)
.github/workflows/pipeline-forge-run.yml   TODO(operator...)
```

La full automatisation n'est donc pas terminée. Hermes reste branché en
lecture seule via `hermes-observer.yml` ; aucun droit d'écriture ne lui a été
ajouté. `pipeline-audit.yml` et `pipeline-forge-run.yml` n'ont toujours aucun
brief d'implémentation autorisé.

## Prochaines actions recommandées, dans l'ordre

1. Faire corriger 009a par un Générateur distinct à partir de
   `feedback/feedback-009a-002.md`, puis le faire rejuger par une session qui
   n'a pas produit la correction.
2. Faire juger 009b indépendamment à partir de la PR #17 et de la grille déjà
   écrite ; ne pas réutiliser le Générateur comme Évaluateur.
3. Arbitrer l'usage de `--max-budget-usd 5` pour l'appel challenge.
4. Seulement après ACCEPT de 009a et 009b, reprendre 009c. Son estimation reste
   proche du seuil de checkpoint ; respecter le point d'arrêt prévu.
5. Après 009c, une passe Planificateur peut écrire de nouveaux briefs pour
   `pipeline-audit.yml` et `pipeline-forge-run.yml`. Ne pas les câbler sans
   brief.

## Risques connus

- Le suivi `budget.py status` est ambigu pour le brief 009 : quatre anciens
  transcripts portent le même nom. La session n'a choisi aucun journal au
  hasard ; cette limite est consignée dans le journal 009b.
- La protection de branche GitHub reste indisponible sur le plan actuel ; les
  contrôles locaux de chemins interdits restent la barrière effective.
- Le contrôle `no_premature_stub_content` de `harness_audit.py` est obsolète ;
  corriger son hypothèse dans un futur brief, pas les répertoires légitimes.
- Les décisions produit encore ouvertes (Évaluateur des lots Codex, backend
  Codex officiel, contrat d'écriture Hermes) restent humaines.
