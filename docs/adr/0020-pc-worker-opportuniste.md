# ADR-0020: le PC Windows est un worker opportuniste, pas un second chef

> **Statut actuel — 2026-08-30 : Le runner Windows peut rester un outil facultatif. Toute attribution de rôle ou dépendance obligatoire est obsolète.**

**Date**: 2026-08-26
**Status**: proposed
**Deciders**: le propriétaire (validation du chantier « Hermes VPS + PC
worker »), Cursor (rédaction)

Amende ADR-0013 (le contrat worker Windows, disparu au dégraissage). Ne
remplace pas ADR-0018 : les trois acteurs, la fusion humaine et
l'interdiction du full-auto restent.

## Contexte

ADR-0013 a déjà tranché l'architecture hybride : Hermes et ForgePilot sur
un VPS Linux ; le PC Windows comme atelier (alors Unity). Le contrat
détaillé vivait dans `docs/operations/unity-windows-worker.md`. ADR-0018
a archivé Unity et **supprimé ce contrat sans le remplacer**. Le VPS
pilote déjà ForgeHistory en permanence. Le PC, quand il est allumé, a
32 Go de RAM, Windows, éventuellement Unity et un LLM local. Rien dans
le dépôt ne permettait au VPS de savoir si ce PC était là, ni de lui
confier une tâche bornée.

Deux dérives à empêcher d'emblée : un second Hermes qui tiendrait sa
propre mémoire du projet ; un orchestrateur distribué (broker, file,
daemon) alors que GitHub Actions sait déjà dire `online` / `offline`
d'un runner auto-hébergé.

## Décision

1. **Hermes sur le VPS reste le seul chef de projet ForgeHistory.** Un
   Hermes local (y compris le backend embarqué d'Hermes Desktop) est un
   atelier : autre profil, pas la skill `forgehistory-suivi`, pas
   d'écriture de `ROADMAP.md` ni de brief. Les mémoires internes de deux
   backends Hermes ne se synchronisent pas.

2. **Hermes Desktop est un client du VPS**, pas un protocole de tâches.
   Branchement officiel : Settings → Gateways → Remote gateway (Tailscale
   + Basic Auth, ou OAuth Nous Portal si le port est sur Internet). Le
   dashboard `:9119` n'est jamais exposé en clair.

3. **GitHub est le bus de présence et de tâches machine.** Un runner
   auto-hébergé sur le PC annonce `online` / `offline` et ses capacités
   par **labels** (`windows`, `high-memory`, `unity`, `local-llm`). Une
   tâche bornée est un workflow `workflow_dispatch` uniquement. Le
   résultat revient en conclusion de run et en artefact. Aucun heartbeat
   n'est committé dans git.

4. **ForgePilot constate, il n'orchestre pas.** `forgepilot workers` lit
   l'API runners et **refuse** s'il n'y a pas de runner online compatible.
   Il ne dispatch pas, ne fusionne pas, n'installe pas de modèle. Hermes
   lance `gh workflow run` seulement après un constat vert. Un job mal
   labelisé ne doit jamais partir « au cas où » (file GitHub infinie).

5. **Le PC n'installe pas un second ForgePilot.** Cursor reste l'exécutant
   des lots de **code** sur le VPS. Le worker exécute des tâches
   **machine** (ping, plus tard tests lourds, Unity, LLM). Ce n'est pas
   une seconde source d'instruction : le brief reste l'unique consigne
   d'un exécutant de code.

6. **Unity et le LLM local sont des labels réservés.** Aucun job Unity
   tant qu'un ADR ne rouvre pas Unity (ADR-0016 / 0018). Aucun modèle
   local n'est installé par cet ADR : le rôle et l'interface se
   décident avant toute install. Le VPS (8 Go) n'infère pas.

7. **Inchangé, et dit ici pour ne pas l'introduire en silence :** pas de
   full-auto, pas d'auto-merge, pas de cron supplémentaire, pas de
   fusion par Hermes. PC éteint = tâche machine refusée, jamais un
   succès supposé. Dépôt public : jamais `pull_request` ni code de fork
   sur le runner personnel.

Le contrat opératoire vit dans
[`docs/operations/pc-windows-worker.md`](../operations/pc-windows-worker.md).

## Alternatives considérées

### ForgePilot orchestrateur de workers
- **Pour** : un registre `requires: [unity]` collé au pilote.
- **Contre** : l'état ForgePilot est local et git-ignoré ; il faudrait
  quand même GitHub pour parler au PC. Deux ForgePilot se battraient sur
  les verrous `hostname + pid`.
- **Pourquoi non** : trop pour le besoin. La lecture `forgepilot workers`
  suffit.

### Hermes ↔ Hermes
- **Pour** : Desktop sait déjà se brancher sur plusieurs gateways.
- **Contre** : c'est un sélecteur d'UI ; la délégation reste
  gateway-locale ; deux mémoires ne se synchronisent pas.
- **Pourquoi non** : retenu seulement comme client du chef VPS.

## Conséquences

### Positives
- Le VPS continue sans le PC. Le PC apparaît comme une capacité, pas
  comme une autorité.
- Présence et résultat vivent là où le projet a déjà sa mémoire : GitHub.
- Le contrat Unity disparu est recréé, généralisé, sans réveiller Unity.

### Négatives
- Lister les runners exige un `gh` VPS avec le droit Actions (admin
  lecture). `doctor --check-auth` ne le prouve pas à lui seul.
- Un label oublié au dispatch resterait en file : d'où le pré-contrôle
  obligatoire.

### Risques
- **Un dépôt public déclenche du code sur le PC.** Atténuation :
  `workflow_dispatch` seul ; `contents: read` ; compte Windows non-admin ;
  jamais de fork. Un `workflow_dispatch` n'est lançable que par un compte
  avec droit d'écriture.
- **Deux chefs.** Atténuation : skill et runbook ; le profil Desktop
  local n'a pas `forgehistory-suivi`.
- **Le ping est pris pour une preuve Unity / LLM.** Atténuation : ces
  labels n'ont pas de job ; un succès `ping` ne parle que du runner.

## Ce que cet ADR ne décide pas

1. Le réveil d'Unity.
2. Quel modèle local, quelle interface, quel plafond RAM.
3. Wake-on-LAN, Tailscale comme prérequis réseau du *worker* (le runner
   parle vers GitHub en sortie ; Tailscale sert Desktop, pas le bus).
4. La gouvernance du canal Discord (toujours ouverte, ADR-0015).
