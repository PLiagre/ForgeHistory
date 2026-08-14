# Hébergement du pilote ForgePilot

**Décision propriétaire du 2026-08-14.** Ce document fixe où tourne Hermes
pendant le pilote ADR-0013. Les tarifs sont des repères vérifiés à cette date,
pas des engagements fournisseurs.

## Déploiement en deux temps

### 1. Essai sans coût sur le PC Linux

Les trois premiers lots tournent sur la partition Linux du propriétaire :

- environ 32 Go de RAM et 140 Go libres ;
- un seul lot actif ;
- aucun cron, aucune fusion automatique et aucune boucle sans ordre humain ;
- pas de modèle local, de navigateur automatisé, de voix ni de sous-agents ;
- Claude Code planifie et relit ; Cursor exécute dans un worktree.

Les clones mesurés occupent environ 522 Mo pour ForgeHistory et 1,2 Go pour
VictoriaCityLab. Le disque local est donc très largement suffisant.

Éteindre le PC arrête Hermes et toute commande locale. Ce n'est pas un défaut
du pilote : aucune boucle autonome n'est autorisée pendant ces trois lots.

### 2. VPS seulement si le bilan est positif

Si Hermes apporte un confort mesurable, migrer ensuite la console sur un VPS
Linux persistant :

| ressource | cible |
|---|---:|
| RAM | 4 Go |
| CPU | 2 vCPU |
| disque | 40 Go SSD |
| swap | 2 Go |
| parallélisme | 1 lot |

Repères de coût vérifiés : Hetzner CX23, 4 Go/2 vCPU/40 Go, environ 5,99 € par
mois ; OVH VPS-1, caractéristiques comparables, environ 5 à 7 € par mois selon
TVA et région. Sources :

- <https://www.hetzner.com/cloud/cost-optimized/> ;
- <https://www.ovhcloud.com/en/vps/vps-france/>.

La migration réinstalle les CLI sur le VPS et refait les authentifications.
Les secrets et le contenu de `~/.hermes` ne sont jamais commités dans Git.

## Architecture hybride ultérieure

Le VPS garde Hermes, ForgePilot, les clones et les tâches ordinaires. Le PC
Linux reste un worker facultatif pour Unity, les tests lourds ou un éventuel
modèle local. Hermes peut lui déléguer une commande par son backend SSH, à
travers un VPN privé ; aucun port SSH domestique n'est exposé publiquement.

Quand le PC est éteint :

- Hermes reste joignable sur le VPS ;
- les plans, revues, opérations Git et contrôles exécutables sur le VPS
  continuent ;
- une tâche qui exige Unity ou le PC reste bloquée et visible, sans être
  déclarée réussie ;
- elle ne reprend qu'après le retour du PC et une décision explicite pendant
  le pilote.

Référence du backend SSH Hermes :
<https://hermes-agent.nousresearch.com/docs/user-guide/features/tools>.

## Pourquoi Render n'est pas l'hôte Hermes

Un Background Worker Render peut rester actif, mais le plan du workspace et
la machine du service sont facturés séparément. Les ordres de grandeur vérifiés
sont 25 $/mois pour 2 Go, 85 $/mois pour 4 Go, puis 0,25 $/Go/mois pour le
disque persistant. Le palier 512 Mo à 7 $/mois est inférieur au minimum Hermes.

Le stockage gratuit est éphémère et les services gratuits s'endorment. Render
reste utilisable plus tard pour une API ou une démonstration CityLab, mais pas
comme poste de travail persistant du pilote.

Sources :

- <https://render.com/pricing> ;
- <https://render.com/docs/background-workers> ;
- <https://render.com/docs/free> ;
- <https://render.com/docs/compute-plans>.

## Garde-fous d'exploitation

- ne pas exposer le dashboard Hermes directement à Internet ; utiliser
  loopback + tunnel/VPN, ou activer son authentification ;
- ne pas installer Ollama sur le VPS 4 Go ;
- ne pas exécuter Cursor et Claude Code en parallèle ;
- conserver les worktrees sous `.forgepilot/`, hors des sources produit ;
- sauvegarder la configuration privée séparément et tester sa restauration ;
- réévaluer RAM, disque et coût après les trois lots avant toute automatisation.
