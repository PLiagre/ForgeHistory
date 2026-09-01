# Mise en place — Hermes pilote, crons indépendants

Ce n'est pas un pipeline. C'est **cinq réveils** qui regardent chacun
leur boîte. Si elle est vide, ils se recouchent (code 0). Personne
n'attend la fin de personne.

Le lot 035 a brûlé un quota Claude parce qu'un pilote lançait la suite
dans le même processus. Ici Hermes **écrit une carte**, et s'arrête.
Cursor la trouvera à 14 h, ou demain, ou jamais — ça ne bloque pas Claude.

---

## Tes trois abonnements, sans se marcher dessus

| abo | qui l'utilise | pour quoi | pas pour ça |
|---|---|---|---|
| **ChatGPT Plus** | Hermes, uniquement | le cerveau du pilote (`openai-codex` OAuth, pas l'API payante) | Codex en plus : **même quota hebdo**. Un seul consommateur. |
| **Claude Pro** | deux crons, **pas la même heure** | 08:30 briefs · 19:00 relecture de PR | coder. Ni Grok ni Composer. |
| **Cursor Pro** | deux crons, **pas la même heure** | 10:00 Grok planifie (facultatif) · 14:00 Composer code | relire. Ni les briefs. |

Claude écrit le brief **et** relit le diff : c'est légal. Il n'a pas
écrit le code. Les deux jobs partagent le quota Pro : on les écarte
de dix heures.

Ce tableau décrit **un** branchement — celui qui tient avec ces trois
abonnements. Ce n'est pas l'atelier qui le décide : c'est
l'`atelier.toml` du produit, et lui seul. Voir « Qui tient quel poste »
plus bas.

Grok n'est **pas** sur le chemin critique de Composer. Mais il est sur
le même **compteur** : les deux tirent Cursor Pro. La boîte découple
l'ordre, elle ne découple pas la ressource. D'où la réserve, plus bas.

## Ce qui décide de la facture, et qu'on ne voit pas

Une variable d'environnement suffit à faire payer l'API à l'unité au
lieu de l'abonnement : `ANTHROPIC_API_KEY`, `ANTHROPIC_AUTH_TOKEN`,
`CURSOR_API_KEY`, `OPENAI_API_KEY`, `OPENAI_BASE_URL`. On ne veut pas
découvrir la réponse sur la facture : **`tour.sh` et `pilote.sh` les
retirent** de l'environnement de l'agent qu'ils lancent, et le crontab
n'en pose aucune.

Hermes n'utilise **pas** l'OAuth Anthropic : Pro le refuse, Max
facture l'extra hors forfait. `crons/installer-profils.sh` refuse
d'écrire un profil qui nommerait un fournisseur Anthropic.

```bash
hermes model          # OpenAI → ChatGPT / Codex Subscription
hermes auth list      # tu veux openai-codex + oauth
```

Tu ne lances **pas** Codex CLI à côté. Hermes *est* l'usage ChatGPT
du VPS. Un modèle léger pour le pilote (il dialogue, il dépose une
carte, il ne code pas). Si tu veux garder Codex comme relecteur de
brief, il te faut un quatrième quota. Avec tes trois abos, on ne le
fait pas.

---

## Qui tient quel poste

Une seule réponse, dans le `atelier.toml` du **produit** :

```toml
[roles]
ecriture  = "claude"   # écrit les briefs
execution = "cursor"   # écrit le code
controle  = "claude"   # relit le diff
```

Les quatre rôles de la boîte lisent ces trois champs : `briefer` lit
`ecriture`, `planifier` et `coder` lisent `execution`, `relire` lit
`controle`. Le pilote n'y est pas — Hermes tient l'horloge, ce n'est
pas un poste du produit.

**`ecriture` et `controle` peuvent être le même agent.** La règle est
que celui qui a écrit le *code* ne dit pas s'il est recevable ; écrire
un brief n'est pas écrire du code. **`execution` et `controle` ne le
peuvent pas** — `doctor` refuse le branchement.

Le gabarit à copier est [`profiles/forgehistory.toml`](../profiles/forgehistory.toml).
Un `controle = "codex"` reste valide, mais coûte un quatrième
abonnement que tu n'as pas : Codex tire le **même quota ChatGPT
hebdomadaire** que Hermes.

Pour voir qui tient quoi, sans deviner :

```bash
python3 -m atelier poste --projet /srv/ForgeHistory --role relire
# role          relire
# backend       claude
# binaire       claude
# abo           claude-pro
# modele
# lecture_seule tenue
```

`lecture_seule` dit si le binaire du relecteur sait qu'on lui retire
les outils qui écrivent. `tenue` : il l'a. `non-tenue` : il garde la
main qui écrit, et `tour.sh` te le dit sur stderr avant de l'invoquer
— une absence se déclare, elle ne se devine pas.

---

## La boîte, pas le pipeline

```
.atelier/boite/          # sur le VPS, git-ignoré
  a-briefer/             # Hermes a déposé → cron Claude 08:30
  a-planifier/           # facultatif, cron Grok 10:00
  a-coder/               # brief prêt → cron Composer 14:00
  a-relire/              # PR ouverte → cron Claude 19:00
  echec/                 # un cron a perdu : les autres continuent
  faite/                 # relue, en attente de TA fusion
```

Chaque cron :

1. `python3 -m atelier prochain --role <lui> --projet /srv/ForgeHistory`
2. `RIEN` → exit 0
3. sinon il imprime l'invocation exacte, la lance (sous drapeau), puis
   `python3 -m atelier avancer` **ou** `python3 -m atelier echouer`
4. il n'appelle pas le cron suivant

**Une carte qu'on a invoquée ne reste jamais en place.** Elle avance ou
elle tombe dans `echec/` avec sa raison. Une carte qu'on n'a *pas*
invoquée — boîte vide, quota épuisé, rôle déjà pris, drapeau baissé —
reste intacte : on n'a rien dépensé, il n'y a rien à déclarer.

Le seul humain dans la boucle : tu lis la PR et tu fusionnes. Ça n'est
pas un point de blocage *entre agents*.

---

## Horaires (Europe/Paris)

Le fichier prêt à poser est [`crons/crontab`](../crons/crontab).

```cron
15 6  * * * ubuntu /opt/ForgeAtelier/crons/veille.sh
0  7  * * * ubuntu /opt/ForgeAtelier/crons/pilote.sh
30 8  * * * ubuntu /opt/ForgeAtelier/crons/tour.sh briefer
0  10 * * * ubuntu /opt/ForgeAtelier/crons/tour.sh planifier
0  14 * * * ubuntu /opt/ForgeAtelier/crons/tour.sh coder
0  19 * * * ubuntu /opt/ForgeAtelier/crons/tour.sh relire
```

| heure | script | agent | s'il n'a rien |
|---|---|---|---|
| 06:15 | `veille.sh` | aucun (script) | silence — mais elle exige `atelier.toml` |
| 07:00 | `pilote.sh` | Hermes / ChatGPT Plus | une proposition, ou rien |
| 08:30 | `briefer` | Claude Pro | `RIEN` |
| 10:00 | `planifier` | Cursor Grok 4.6 | `RIEN` — Composer code quand même |
| 14:00 | `coder` | Cursor Composer | `RIEN` |
| 19:00 | `relire` | Claude Pro | `RIEN` |

Le `flock` est **dans** `tour.sh`, un par rôle
(`$ATELIER_VERROUS/atelier-<rôle>.lock`). Deux *briefer* ne se marchent
pas ; un *briefer* et un *coder* tournent ensemble. Un rôle déjà pris
imprime « un tour est déjà en cours » et sort 0 : la carte sera là au
prochain réveil. Jamais un flock global.

`ATELIER_TIMEOUT` (1800 s par défaut) borne chaque agent. Un agent qui
pend rend 124 : la carte va dans `echec/`, pas dans les limbes.

**La veille a besoin du branchement.** Sa commande de fumée vient de
l'`atelier.toml` du produit — l'atelier ne sait pas ce que ton produit
fabrique. Sans ce fichier, `veille.sh` sort en erreur et le dit sur
stderr : elle ne prétend pas avoir mesuré quelque chose. Pose donc
l'`atelier.toml` dans le produit **avant** de poser le crontab.

---

## La garde de quota, facultative

Si `llmquota` est installé, `tour.sh` le lit tout seul. Sinon, rien ne
se passe et **on continue** : un quota qu'on n'a pas mesuré vaut `-1`,
jamais `0`. C'est un fait absent, pas un zéro.

```bash
ATELIER_QUOTA_CMD="llmquota restant"   # doit rendre un entier sur stdout
ATELIER_RESERVE_planifier=1            # Grok laisse sa marge à Composer
```

L'atelier attend un entier ; tout le reste (texte, silence, erreur) est
lu comme « inconnu ». Si ton `llmquota` parle une autre langue, pose
`ATELIER_QUOTA_CMD` : c'est une commande à toi, pas une dépendance.

Quand le restant tombe **à la réserve ou en dessous**, le rôle sort 0 et
laisse la carte. Le rôle facultatif (`planifier`) a une réserve de 1 par
défaut : il cède le compteur Cursor à `coder`, qui, lui, est sur le
chemin. Les autres rôles ont une réserve de 0.

llmquota *lit*. Il ne lance rien, et l'atelier ne l'installe pas.

---

## Worktrees : un agent, un répertoire

```bash
./crons/installer-profils.sh --dry-run   # imprime les worktrees et les profils
```

Trois agents sur le même clone se marchent dessus. Chaque rôle a son
répertoire (`ATELIER_WORKDIR_<rôle>`, posé par le crontab) et Hermes a
un profil par rôle qui pointe le même chemin.

Un seul lot de **code** à la fois par fichier. Le `coder` pose le verrou
avant d'invoquer Composer, et `prochain --role coder` **saute** une
carte dont un fichier est déjà tenu par un autre lot : 044 occupe
`sim/engine.py` ? 046 attend dans `a-coder`, le cron prend 047 s'il est
disjoint, ou sort `RIEN`. Les autres rôles n'écrivent pas de code : un
verrou ne les suspend pas.

Le périmètre du verrou vient du **brief**, pas de la carte : si la carte
ne nomme aucun fichier, l'atelier lit la section `Périmètre` du brief.
S'il n'y en a pas, il refuse — il ne devine pas.

**Après ta fusion, rends les fichiers :**

```bash
python3 -m atelier verrous --projet /srv/ForgeHistory
python3 -m atelier lever   --projet /srv/ForgeHistory --lot 044-un-metier-le-mineur
```

Sans ça, le lot suivant qui touche `sim/engine.py` attendra un lot déjà
fusionné.

---

## Installer, dans l'ordre

Sur le VPS, **une fois**.

1. Détacher ForgeAtelier (voir [PUBLIER.md](PUBLIER.md)) ou cloner la
   branche orpheline `cursor/forgeatelier-ced6` dans `/opt/ForgeAtelier`.
2. `cd /srv/ForgeHistory && git pull` — le fichier `atelier.toml` doit
   être là (PR de branchement). Rien ne marche sans lui : ni la veille,
   ni les rôles, ni les abonnements. Vérifie-le :

   ```bash
   python3 -m atelier doctor --projet /srv/ForgeHistory
   python3 -m atelier poste  --projet /srv/ForgeHistory --role relire
   ```
3. Authentifier **trois** binaires, pas plus :
   - `hermes model` → ChatGPT / Codex OAuth (**pas** Anthropic)
   - `claude` → Claude Pro (OAuth du compte Pro)
   - `agent login` → Cursor Pro
4. Les profils et les worktrees :

```bash
ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/installer-profils.sh --dry-run
# compare la syntaxe à `hermes profile --help` de ta version, puis :
ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/installer-profils.sh --run
```

5. Poser le crontab **sans** `ATELIER_INVOQUER=1`.
6. Déposer une carte à la main et regarder :

```bash
python3 -m atelier deposer --projet /srv/ForgeHistory \
    --etat a-coder --lot 044-un-metier-le-mineur \
    --brief briefs/044-un-metier-le-mineur.md \
    --fichier sim/engine.py --fichier sim/constants.py

ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh coder
# doit imprimer l'invocation Cursor, sans lancer
```

7. Quand tu as vu trois matins de `RIEN` / d'impressions justes :
   `ATELIER_INVOQUER=1` sur **un seul** cron, le `coder` d'un brief
   déjà relu par toi. Pas les six d'un coup.

---

## Superpowers : une note, pas une dépendance

Superpowers apporte des *skills* rejouables (worktrees, rouge-vert,
relecture par un autre). Installe-le si tu veux, sur le compte de
l'agent, à la main :

```bash
superpowers install    # ou la commande de ta version
```

L'atelier ne l'installe pas, ne le teste pas, et ne tombe pas s'il est
absent. Et surtout : **son brainstorm ne remplace pas le brief.** Le
brief reste la seule source d'instruction d'un lot ; une seconde langue
de planification redevient une source parallèle.

---

## Ce que Hermes a le droit d'écrire

Une carte, une proposition, le tableau de bord. **Pas** :

- le brief (Claude)
- le code (Composer)
- un jugement de recevabilité (Claude relit, toi tu fusionnes)
- `git merge`

Le prompt du pilote n'est pas recopié ici : il est construit par
`atelier/backends.py` et tu peux le lire tel qu'il partira.

```bash
python3 -m atelier invocation --role pilote --projet /srv/ForgeHistory
python3 -m atelier invocation --role coder  --projet /srv/ForgeHistory \
    --lot 044-un-metier-le-mineur --brief briefs/044-un-metier-le-mineur.md
```

Un seul endroit compose une ligne de commande d'agent, et c'est du
Python testé. Le cron l'exécute, il ne l'invente pas. Quand le binaire
du relecteur sait le faire, on lui retire les outils qui écrivent,
poussent ou fusionnent : « celui qui a écrit le code ne dit pas s'il est
recevable » ne tient que si le relecteur n'a pas la main qui écrit.
Quand il ne sait pas, `atelier poste --champ lecture_seule` répond
`non-tenue` et le cron le déclare.

---

## Points de blocage qu'on refuse

| ça | pourquoi c'est interdit |
|---|---|
| Hermes qui lance `agent` dans la même commande | lot 035 |
| Composer qui `wait` Grok | Grok est facultatif |
| Grok qui vide le quota Cursor avant 14 h | la réserve du rôle facultatif |
| Claude revue qui attend des tests verts | la CI GitHub le dit ; Claude relit le diff tel quel |
| Un flock global | deux rôles ne doivent pas se sérialiser |
| Une carte prise qui reste dans sa boîte | elle avance ou elle échoue |
| Un verrou que personne ne lève | `atelier lever` après ta fusion |
| Deux tableaux qui disent qui relit | le branchement du produit, et lui seul |
| Une veille qui sort 0 sans rien mesurer | une absence se déclare |
| Fusion automatique | le propriétaire regarde (règle 11) |

Le seul « blocage » accepté : **toi**, le soir, sur une PR. Les crons
du lendemain voient `faite/` ou une PR encore ouverte, et se recouchent.
