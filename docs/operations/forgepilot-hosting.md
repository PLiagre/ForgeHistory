# Hébergement du pilote ForgePilot

**Décision propriétaire corrigée du 2026-08-14.** Unity est installé
nativement sous Windows. Le control-plane et le worker Unity sont donc deux
rôles distincts ; le double démarrage sur la partition Linux ne peut pas servir
de base à une validation Unity disponible.

## 1. Trois lots sans coût sur le PC Windows

Les trois premiers lots ForgeHistory tournent sur le PC Windows, soit
nativement, soit dans WSL2. Windows reste démarré pour conserver Unity
6000.0.43f1 disponible. Pendant ce pilote :

- environ 32 Go de RAM ;
- un seul lot actif ;
- aucun cron **de fusion** ; un cron quotidien de lecture est autorisé
  depuis ADR-0016 (`hermes/crons/`) ;
- aucun modèle local, navigateur automatisé, mode voix ou sous-agent ;
- Claude Code planifie et relit ; Cursor exécute dans un worktree ;
- un lot CityLab est refusé tant que son worker Unity Windows n'est pas livré.

Éteindre le PC arrête Hermes local et Unity. Ce comportement est accepté pendant
les trois lots : aucune disponibilité permanente n'est encore promise.

## 2. VPS seulement après le bilan

Si Hermes apporte un confort mesurable, migrer le control-plane sur un VPS
Linux persistant :

| ressource | cible minimale |
|---|---:|
| RAM | 4 Go |
| CPU | 2 vCPU |
| disque | 40 Go SSD |
| swap | 2 Go |
| parallélisme | 1 lot |

Quatre Go suffisent à Hermes, ForgePilot et aux CLI lorsque les modèles restent
distants, que Cursor et Claude Code ne tournent pas en parallèle et qu'Unity
reste sur le PC Windows. Passer à 8 Go si les contrôles portables dépassent
régulièrement 85 % de RAM ou utilisent durablement plus de 1 Go de swap.

Repères de coût vérifiés le 2026-08-14 : Hetzner CX23, environ 5,99 € par
mois ; OVH VPS-1, environ 5 à 7 € par mois selon TVA et région :

- <https://www.hetzner.com/cloud/cost-optimized/> ;
- <https://www.ovhcloud.com/en/vps/vps-france/>.

La migration réinstalle les CLI et refait leurs authentifications. Les secrets
et le contenu de `~/.hermes` ne sont jamais commités.

## 3. Architecture hybride cible

| composant | emplacement | responsabilité |
|---|---|---|
| Hermes + ForgePilot | VPS Linux | dialogue, plan, worktrees, PR et suivi |
| Claude Code | invoqué depuis le VPS | plan et revue en lecture seule |
| Cursor CLI | VPS par défaut | écrit dans le worktree et pousse la branche |
| Cursor Cloud | facultatif, explicite | autre mode d'exécution, jamais supposé |
| Unity 6000.0.43f1 | PC Windows | import, compilation, tests et builds |
| GitHub | distant | transport, file d'attente, preuves et statut |

`agent -p` s'exécute sur la machine qui lance ForgePilot. Une conversation
Cursor n'est envoyée dans le cloud que par un transfert Cloud Agent explicite.
Dans les deux cas, Unity valide ensuite le commit Git exact produit.

Le PC Windows devient un worker GitHub Actions auto-hébergé, sous un compte
Windows dédié sans droits administrateur. Le runner communique vers GitHub en
sortie ; aucun port domestique n'est exposé. Le contrat détaillé vit dans
[`pc-windows-worker.md`](pc-windows-worker.md) (ADR-0020). Unity reste
archivé ; les labels `unity` et `local-llm` sont réservés, sans job.

Quand le PC est éteint :

- Hermes reste joignable sur le VPS ;
- les plans, revues et tâches ForgeHistory portables continuent ;
- la validation Unity reste en attente et la fusion CityLab est interdite ;
- le travail peut être relancé lorsque le worker revient en ligne.

Un Wake-on-LAN à travers un VPN privé pourra être étudié après le pilote, sans
faire partie du premier déploiement.

## 4. Pourquoi Render et le double démarrage sont écartés

Render reste utile pour une API ou une démonstration CityLab, mais pas comme
poste de travail Hermes : compute et disque persistant sont facturés séparément.
La partition Linux locale reste disponible pour des usages manuels, mais booter
dessus éteint Windows et rend Unity indisponible.

Unity Build Automation est la seule variante qui supprime complètement la
dépendance au PC ; elle exige une configuration et une consommation Unity
DevOps séparées. Elle sera évaluée uniquement après mesure du worker Windows.

Sources :

- <https://render.com/pricing> ;
- <https://render.com/docs/background-workers> ;
- <https://docs.unity.com/en-us/build-automation/get-started-with-build-automation/connect-your-version-control-system>.

## Garde-fous d'exploitation

- ne pas exposer le dashboard Hermes directement à Internet ;
- ne pas installer Ollama ou Unity sur le VPS 4 Go ;
- ne pas exécuter Cursor et Claude Code en parallèle ;
- conserver les worktrees sous `.forgepilot/` ;
- ne jamais considérer un worker Unity hors ligne comme un succès ;
- ne jamais exécuter automatiquement une PR publique ou le code d'un fork sur
  le PC Windows ;
- conserver la fusion humaine et réévaluer coût, RAM et sécurité après trois
  lots.
