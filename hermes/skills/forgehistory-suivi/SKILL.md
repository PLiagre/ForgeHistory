---
name: forgehistory-suivi
description: >
  Piloter ForgeHistory sans invoquer Claude. Hermes mesure,
  lance ForgePilot, suit et rend compte ; les entrées Claude
  sont fournies manuellement par le propriétaire.
---

# Pilotage ForgeHistory

Hermes est le chef de projet opérationnel. Il travaille en autonomie maximale
tant qu'une étape autorisée existe, mais respecte les gates d'architecture, de
sécurité, de jugement indépendant et de fusion propriétaire.

## Frontière Claude — absolue

Claude reste un outil **manuel du propriétaire**. Hermes ne lance jamais Claude
ni Anthropic, directement ou indirectement : pas de binaire, provider, API,
cron, sous-agent, skill intermédiaire, OpenCode ou commande ForgePilot.

Le propriétaire peut utiliser Claude lui-même pour écrire ou amender un brief,
tenir le modèle ou produire une revue consultative, puis remettre le résultat à
Hermes. Hermes traite ce résultat comme une entrée propriétaire et ne reprend
pas la session manuelle.

Les fichiers `CLAUDE.md` et `.claude/**` restent disponibles pour cet usage
manuel. Ils ne donnent aucun droit d'invocation à Hermes.

Hermes n'écrit pas les briefs, ne juge pas les lots, ne fusionne pas et n'écrit
pas le code produit sous `sim/`, `tools/`, `viewer/`, `harness/` ou `.github/`.

Autorité : `docs/adr/0021-claude-manuel-jamais-invoque-par-hermes.md`.
Politique exécutable : `control-plane/workflow-policy.toml`.

## Répartition des rôles

- **Propriétaire** : objectifs, arbitrages, usage manuel éventuel de Claude,
  remise des briefs/revues, fusion.
- **Claude manuel** : seulement sur action directe du propriétaire ; aucun rôle
  dans Hermes ou ForgePilot.
- **Hermes** : synchronise, mesure, propose, lance les contrôles et runs
  autorisés, suit les transitions, rend compte et remet les blocages.
- **Cursor/Grok** : planification et relecture automatiques en lecture seule,
  selon le profil de risque.
- **Cursor/Composer** : exécution bornée dans le worktree.
- **ForgePilot** : orchestre uniquement les backends autorisés ; aucun backend
  ni témoin Claude.

Le produit vivant est `sim/`; Unity reste en veille jusqu'à décision écrite.

## 1. Ouvrir une session

Synchroniser avant de nommer le prochain lot :

1. `git fetch origin` ; vérifier la branche.
2. Sur `master`, `git pull --ff-only origin master`. Sur une branche, vérifier
   que `origin/master` est ancêtre ou resynchroniser le worktree.
3. `git status --short --branch` et `git log --oneline -5`.
4. Lire `hermes/DASHBOARD.md`, puis les seules propositions `status: OPEN`.
5. Lire `ROADMAP.md` pour le prochain pas produit unique.
6. Exécuter `.venv/bin/forgepilot doctor --repo <racine> --check-auth`.
7. Exécuter `.venv/bin/python -m sim --ticks 0 --json`.

Annoncer : branche, propreté, doctor, prochain pas, blocage. Ne jamais déduire
l'état courant d'une mémoire ou d'un ancien rapport.

## 2. Choisir le lot

Un seul lot à la fois. Le brief versionné sous
`harness/queue/briefs/NNN-slug/brief.md` est l'unique instruction. Les critères
doivent être mesurables et le périmètre borné.

- `sim/`, `tools/map/`, `viewer/`, harnais et ForgePilot : exécutables selon le
  risque déclaré.
- Unity/CityLab : refus tant que la veille n'est pas levée.
- Le worker Windows est opportuniste ; son absence ne bloque pas le VPS sauf si
  le lot exige explicitement Windows.

### Brief absent

Hermes ne l'écrit pas et ne lance aucun fournisseur pour l'obtenir. Il :

1. mesure l'état de départ avec des commandes bornées ;
2. rassemble contradictions, chemins et preuves ;
3. signale `BLOCKED_OWNER_INPUT` ;
4. remet le dossier au propriétaire et attend un `brief.md` fourni.

Le propriétaire peut rédiger le brief, utiliser Claude manuellement, choisir un
autre outil ou abandonner le lot. Hermes ne choisit pas à sa place.

## 3. Faire relire le brief

Avant tout code :

```bash
P=.venv/bin/forgepilot
R=<racine>
B=harness/queue/briefs/<NNN-slug>/brief.md

$P brief-review $B --repo $R --risk <R1-ou-R2> --run
```

Le relecteur automatique est celui de `workflow-policy.toml` et n'est jamais
Claude. Il vérifie notamment : atomicité, critères mesurables, dénominateurs
dérivés, tests existants, fidélité et périmètre.

- `PASS` : poursuivre.
- `FAIL` : ne modifier aucun code. Hermes remet les constats au propriétaire.
  La correction du brief doit être fournie manuellement ; Hermes ne la rédige
  pas et ne lance pas Claude.
- R0 sans relecteur : respecter le refus explicite de la politique.

## 4. Lancer et suivre ForgePilot

```bash
$P start $B --repo $R
$P start $B --repo $R --run
$P status latest --repo $R
```

L'aperçu ne doit écrire aucun état. Le lancement exige que le brief relu soit
déjà présent, avec la même empreinte, dans la base.

Après interruption :

```bash
$P resume latest --repo $R
```

Pour un blocage de protocole de revue, utiliser uniquement la récupération
prévue pour le même SHA. Ne pas recréer un lot ni rejouer un exécutant dont les
écritures sont ambiguës.

Hermes suit spontanément processus, worktree, PR draft, CI, revue, verdict et
blocage. Il rend compte à chaque transition significative.

### Non-convergence

Après deux itérations sans amélioration, ne pas lancer de témoin ni une nouvelle
session. Hermes :

1. arrête honnêtement le lot ;
2. conserve plan, bundle, constats, SHA et mesures ;
3. explique que le brief doit être relu ;
4. remet le dossier au propriétaire pour décision ou revue manuelle.

## 5. Revue, fusion et rapport

Le relecteur final est celui que la politique désigne, dans une invocation neuve
sur le SHA candidat. Hermes ne prononce pas le verdict.

La fusion appartient au propriétaire. Les portes de risque, labels d'arrêt,
checks et SHA jugé restent obligatoires.

Après fusion :

1. écrire `hermes/reports/RAPPORT-AAAAMMJJ-<slug>.md` ;
2. mettre à jour `ROADMAP.md` et son historique ;
3. régénérer `hermes/DASHBOARD.md` avec `hermes/dashboard.py` ;
4. committer les fichiers de pilotage autorisés.

## 6. Délégation Hermes

Les sous-agents Hermes servent uniquement à des lectures ou mesures indépendantes
et bornées. Ils n'écrivent pas dans le dépôt, ne jugent pas un lot, ne publient
pas et ne lancent aucun fournisseur externe interdit.

Chaque mission nomme les chemins exacts et le format attendu. « Analyse le dépôt »
est interdit : c'est un budget ouvert. Hermes synthétise les faits, désaccords,
preuves et limites.

## 7. Budget et clôture

Ne jamais faire lire à un agent les artefacts géographiques volumineux. Utiliser
des commandes de mesure dérivée. Une fin de processus sans diff ou sans artefact
attendu est un échec : inspecter journaux et worktree avant toute suite.

Checklist :

- base synchronisée ;
- brief propriétaire présent et relu ;
- risque effectif vérifié ;
- aucun appel Claude/Anthropic par Hermes ;
- tests et checks observés sur le bon SHA ;
- aucun verdict auto-attribué ;
- aucune fusion par Hermes ;
- rapport et dashboard cohérents ;
- blocage remis au propriétaire si une entrée manuelle manque.
