# Rapport d'incident — consommation Claude sans livrable sur le lot 035

**Date de l'incident** : 2026-08-26
**Périmètre** : worktree `035-claude-reauthorship`
**Gravité** : élevée — consommation importante d'un quota propriétaire sans
livrable
**Responsable de l'orchestration** : Hermes
**État final** : aucun fichier du lot 035 modifié, aucune PR, aucun commit

## Résumé exécutif

Hermes a lancé deux sessions Claude Opus 5 successives pour réécrire réellement
le brief 035 et sa grille d'évaluation. Les deux sessions ont exploré le dépôt
et tenté des mesures, mais aucune n'a appelé un outil d'écriture. La première a
été interrompue au délai du terminal ; la seconde a atteint sa limite de tours.
Une tentative ultérieure de reprise a échoué avant tout appel modèle parce que
le lanceur ne trouvait plus son binaire natif.

Le propriétaire a constaté environ 50 % de quota d'abonnement consommé. Les
journaux locaux ne savent pas convertir les jetons en pourcentage de quota, mais
ils prouvent une activité considérable : 2 955 639 unités de jetons
comptabilisées après déduplication des réponses, pour zéro écriture.

L'incident est une faute d'orchestration Hermes. La mission était trop ouverte,
les permissions ne permettaient pas d'exécuter sans interaction toutes les
mesures demandées, les limites d'arrêt n'étaient pas alignées, et la deuxième
session a été relancée avec le même prompt complet au lieu de traiter d'abord
l'échec de la première. Hermes n'avait pas de gate « aucun début de livrable
après N tours = arrêt » ni de frontière interdisant ce fournisseur.

## Mission demandée à Claude

Produire une édition réellement écrite par Claude de :

- `harness/queue/briefs/035-la-saison-joue-le-rendement/brief.md` ;
- `harness/queue/briefs/035-la-saison-joue-le-rendement/eval-rubric.md`.

Le prompt demandait aussi de lire ADR-0019, les règles du dépôt et les fichiers
produit nécessaires pour vérifier la cohérence. Cette dernière formule n'était
pas assez bornée : elle a transformé une réécriture ciblée en exploration du
moteur, des tests et de la carte.

## Chronologie vérifiée

| UTC | événement | résultat |
|---|---|---|
| 13:17:36 | démarrage session `2c88f693-6f4c-4868-ab8e-d990cfd9cbcc` | exploration et mesures |
| 13:24:36 | fin du journal de la première session, au délai de 7 minutes du terminal | aucun livrable |
| 13:24:52 | démarrage d'une nouvelle session `6b0448d4-a53d-4a5c-b172-8d1c3ea0aa0a` avec le prompt complet | exploration répétée |
| 13:30:17 | fin du second journal ; la commande a ensuite signalé la limite de 20 tours | aucun livrable |
| après 13:30 | Hermes vérifie le worktree | arbre propre, aucun diff |
| après 13:30 | Hermes tente de reprendre la seconde session avec Bash autorisé | échec du lanceur avant appel modèle : binaire natif absent |
| ensuite | le propriétaire demande `stop` | arrêt immédiat ; aucun processus Claude actif |

Les heures viennent des deux JSONL locaux. La cause de terminaison « délai
terminal » et la limite de 20 tours viennent des résultats des commandes
Hermes ; les JSONL s'arrêtent tous deux sur un `tool_use` et ne contiennent pas
de résultat terminal autonome.

## Consommation mesurée

Les lignes `assistant` peuvent répéter le même message lors du streaming. Les
chiffres ci-dessous sont donc calculés après déduplication par identifiant de
message API.

| mesure | session 1 | session 2 | total |
|---|---:|---:|---:|
| réponses API uniques | 17 | 20 | 37 |
| appels d'outil uniques | 26 | 32 | 58 |
| lectures (`Read`) | 13 | 14 | 27 |
| commandes (`Bash`) | 12 | 17 | 29 |
| autres outils | 1 | 1 | 2 |
| outils d'écriture (`Edit`, `Write`, `MultiEdit`) | **0** | **0** | **0** |
| commandes refusées | 4 | 3 | 7 |
| input tokens hors cache | 34 | 40 | 74 |
| cache créé | 120 327 | 184 385 | 304 712 |
| cache lu | 1 193 252 | 1 406 304 | 2 599 556 |
| output tokens | 28 611 | 22 686 | 51 297 |
| total des champs de jetons | 1 342 224 | 1 613 415 | **2 955 639** |

Le terminal de la seconde invocation a rapporté `3.114352 USD`. Ce chiffre ne
couvre pas honnêtement la première invocation, interrompue avant le résumé
final, et ne représente pas directement le quota d'abonnement affiché à
l'utilisateur. Aucun coût monétaire total fiable n'est donc déclaré.

## Résultat produit

- modification de `brief.md` : **aucune** ;
- modification de `eval-rubric.md` : **aucune** ;
- autre modification : **aucune** ;
- commit : **aucun** ;
- PR : **aucune** ;
- sortie réutilisable déposée dans le projet : **aucune**.

Les 58 appels d'outil n'ont donc pas franchi la première gate de livraison.

## Causes racines

### 1. Périmètre de lecture ouvert

Le prompt bornait les fichiers à modifier, mais autorisait « les fichiers
produit strictement nécessaires » à lire. Claude a parcouru le moteur, les
tests, la carte, les appels de tick et le modèle au lieu de produire rapidement
une première édition. Un périmètre d'écriture borné n'est pas un budget de
lecture borné.

### 2. Contradiction entre mission et permissions

La mission exigeait de vérifier des mesures dérivées. Les commandes Python et
certaines commandes composées nécessaires ont demandé une approbation que le
mode non interactif ne pouvait pas fournir. Sept appels Bash ont été refusés.
Le système a laissé le modèle reformuler et retenter au lieu d'arrêter sur cette
contradiction de capacité.

### 3. Limites de temps et de tours non alignées sur un livrable

La première commande avait un délai de terminal de sept minutes et a été tuée
alors que Claude était encore en `tool_use`. La seconde avait 20 tours, mais
aucune condition n'imposait une écriture avant un tour donné. Les deux limites
ont borné la durée, pas garanti la valeur produite.

### 4. Relance injustifiée depuis zéro

Après la première interruption, Hermes a démarré une nouvelle session avec le
même prompt complet. Cela a recréé du cache, refait des lectures et répété
l'exploration. Hermes aurait dû lire le premier journal, constater zéro écriture
et arrêter avant toute nouvelle consommation.

### 5. Absence de gate de coût et de progression

Aucun contrôle n'arrêtait la session si :

- aucun `Edit`/`Write` n'était observé après les premières lectures ;
- plusieurs commandes nécessaires étaient refusées ;
- le même périmètre était relu ;
- un premier processus se terminait sans diff.

### 6. Mauvaise séparation d'autorité

Les skills et ADR actifs disaient à Hermes de demander des briefs à Claude et
de l'appeler comme témoin. Ils transformaient l'abonnement manuel du
propriétaire en ressource automatique implicite. Le propriétaire n'avait pas
accordé cette autorité de dépense.

## Facteurs contributifs, non causes principales

- Claude a privilégié l'analyse exhaustive à la création rapide d'une version
  amendable.
- Le lanceur Claude est devenu incohérent avant la tentative de reprise
  (binaire natif absent), mais cette panne est postérieure aux deux consommations
  et n'explique pas le quota perdu.
- Le résumé terminal de la première invocation a été perdu avec le délai ; le
  coût monétaire total ne peut plus être reconstitué proprement.

## Responsabilité

Hermes porte la responsabilité opérationnelle : il a choisi l'invocation, le
prompt, les permissions, les limites et la relance. Le comportement exploratoire
de Claude était observable après la première session ; le laisser recommencer
sans livrable constitue la décision fautive centrale.

## Corrections appliquées

1. ADR-0021 fixe Claude comme outil **manuel du propriétaire uniquement**.
2. `workflow-policy.toml` désactive le témoin automatique.
3. Le chargeur de politique refuse désormais tout backend `claude`.
4. Le constructeur de commandes Claude a été supprimé de ForgePilot.
5. La commande `forgepilot witness` a été supprimée.
6. `forgepilot doctor --check-auth` ne vérifie plus Claude.
7. Le plateau de non-convergence remet le dossier au propriétaire sans lancer
   de fournisseur.
8. Des tests de non-régression vérifient l'absence de route d'invocation Claude.
9. Les skills Hermes sont corrigés : ils peuvent décrire l'usage manuel du
   propriétaire, mais ne doivent plus contenir de procédure active permettant à
   Hermes de lancer Claude.
10. Les fichiers `.claude/**` du dépôt restent inchangés pour l'usage manuel.

## Preuves

- journaux locaux :
  `~/.claude/projects/-home-hermes-src-ForgeHistory--forgepilot-worktrees-035-claude-reauthorship/*.jsonl` ;
- sessions : `2c88f693-6f4c-4868-ab8e-d990cfd9cbcc` et
  `6b0448d4-a53d-4a5c-b172-8d1c3ea0aa0a` ;
- test de non-invocation :
  `control-plane/tests/test_no_claude_runtime.py` ;
- décision :
  `docs/adr/0021-claude-manuel-jamais-invoque-par-hermes.md`.

## Critère de clôture

L'incident est clos lorsque :

- la recherche des chemins exécutables ne trouve aucune construction ou
  commande Claude dans ForgePilot ;
- les skills Hermes actifs ne demandent plus à Hermes de lancer Claude ;
- la suite ForgePilot est verte ;
- les changements sont relus, commités et publiés sans modifier ni supprimer
  les outils manuels Claude du dépôt.
