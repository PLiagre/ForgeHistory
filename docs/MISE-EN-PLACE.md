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

## La feuille de route décide, le pilote dépose

Qui a besoin d'un brief, qui est prêt à coder, qui attend qui : ça ne se
devine pas dans de la prose. Le dépôt produit tient un **registre des
lots** dans sa feuille de route (`[projet].feuille` de l'`atelier.toml`,
chez ForgeHistory `ROADMAP.md`) — une fiche par lot, un état parmi six,
ses dépendances, ses PR. Le format et le cycle sont décrits là-bas, à
côté du registre ; ici on dit seulement ce que l'atelier en fait.

```bash
python3 -m atelier feuille valider --projet /srv/ForgeHistory   # FAIL sur toute incohérence
python3 -m atelier feuille etat    --projet /srv/ForgeHistory   # chaque lot, état écrit + dérivé
python3 -m atelier piloter         --projet /srv/ForgeHistory   # la décision du matin, à sec
```

`piloter` fait trois choses, dans cet ordre, en Python et sans agent :

1. **rapprocher** — une carte encore en boîte pour un lot que la feuille
   dit `livre` a été fusionnée par toi : elle passe dans `fusionnee/` et
   son verrou est rendu. Idem pour la carte d'un brief dont la fiche est
   passée à `pret`. Tu n'as plus à `atelier lever` après une fusion ;
2. **valider** — une feuille incohérente (fiche sans brief, brief
   orphelin, dépendance fantôme, carte d'un lot inconnu…) arrête tout :
   rien n'est déposé, la raison est dite ;
3. **déposer** — au plus une carte par rôle : la première fiche
   `a-briefer` sans carte va dans `a-briefer` ; la première fiche `pret`
   dont les dépendances sont livrées, dont le brief passe la porte et
   dont aucun fichier n'est tenu va dans `a-coder`, avec les fichiers de
   sa section Périmètre. L'ordre des fiches est la priorité.

Sans `--run`, rien n'est écrit. `pilote.sh` joue `piloter` à sec dans
tous les cas, et `piloter --run` seulement sous `ATELIER_INVOQUER=1`.

## La boîte, pas le pipeline

```
.atelier/boite/          # sur le VPS, git-ignoré
  a-briefer/             # piloter a déposé → cron Claude 08:30
  brief-a-fusionner/     # le brief est en PR : TA fusion, puis piloter rapproche
  a-planifier/           # facultatif, cron Grok 10:00
  a-coder/               # piloter a déposé (brief sur master) → cron Composer 14:00
  a-relire/              # PR ouverte → cron Claude 19:00
  faite/                 # relue, en attente de TA fusion
  fusionnee/             # la feuille dit livré : piloter a rangé la carte
  echec/                 # un cron a perdu : les autres continuent
```

Chaque cron :

1. `python3 -m atelier prochain --role <lui> --projet /srv/ForgeHistory`
2. `RIEN` → exit 0
3. sinon il imprime l'invocation exacte, la lance (sous drapeau), puis
   `python3 -m atelier avancer` **ou** `python3 -m atelier echouer`
4. il n'appelle pas le cron suivant

Le **numéro de PR** fait le dernier saut par le canal d'échange :
l'exécutant écrit son numéro dans `atelier-echange/pr.txt`, le cron le
lit, le range dans la carte et **efface le fichier** — un numéro périmé
ne s'attache pas au lot suivant. À 19 h, le relecteur reçoit « la PR 44,
sur la branche `agent/044-mineur` ». Sans numéro, il reçoit la branche
seule : l'atelier n'invente pas de coordonnée. Le briefer fait pareil
pour la PR de son brief.

Ce numéro n'est pas une consigne. Il dit *où regarder*, pas *quoi
faire* — le brief reste la seule source d'instruction.

**La fiche du lot voyage dans sa PR.** Le briefer passe la fiche à
`pret` dans la PR du brief ; le coder la passe à `livre` avec son numéro
dans la PR du lot (`atelier feuille marquer`). C'est ce qui fait que la
feuille de `master` dit « livré » à l'instant exact de ta fusion, jamais
avant, et sans correction à faire après — la CI du produit refuse une PR
de lot dont la fiche ne bouge pas.

**Une carte qu'on a invoquée ne reste jamais en place.** Elle avance ou
elle tombe dans `echec/` avec sa raison. Une carte qu'on n'a *pas*
invoquée — boîte vide, quota épuisé, rôle déjà pris, drapeau baissé —
reste intacte : on n'a rien dépensé, il n'y a rien à déclarer.

Le seul humain dans la boucle : tu lis la PR et tu fusionnes. Ça n'est
pas un point de blocage *entre agents*.

---

## Horaires (Europe/Paris)

Le fichier prêt à poser est [`crons/crontab`](../crons/crontab).
Le VPS ForgeHistory est en UTC et son cron Debian ne sait pas appliquer un
fuseau par crontab. Les lignes se réveillent donc chaque heure, puis comparent
l'heure locale sous `TZ=Europe/Paris` avant d'exécuter le rôle. Cela conserve
les mêmes heures pendant les changements saisonniers. `crons/reveil.sh`
conserve l'heure, la sortie et le code de retour dans
`/home/hermes/.atelier/logs/` : l'observation ne dépend ni d'un serveur de
courrier ni des droits de lecture du journal système.

```cron
15 * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 06:15 veille
0  * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 07:00 pilote
30 * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 08:30 briefer
0  * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 10:00 planifier
0  * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 14:00 coder
0  * * * * hermes /opt/ForgeAtelier/crons/reveil.sh 19:00 relire
```

Le `PATH` du crontab commence par `/srv/ForgeHistory/.venv/bin` et
`/home/hermes/.local/bin` : le Python système reste intact et les trois
binaires d'abonnement sont visibles sous l'utilisateur `hermes`.

Lire les dernières observations sans privilège administrateur :

```bash
tail -n 40 /home/hermes/.atelier/logs/*.log
```

| heure | script | agent | s'il n'a rien |
|---|---|---|---|
| 06:15 | `veille.sh` | aucun (script) | silence — mais elle exige `atelier.toml` |
| 07:00 | `pilote.sh` | `atelier piloter` (Python), puis Hermes / ChatGPT Plus **seulement s'il y a quelque chose à dire** | `RIEN`, Hermes n'est pas appelé |
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

**Une file bloquée n'est pas une file vide.** Si toutes les cartes de
`a-coder` réclament un fichier déjà tenu, le cron sort `RIEN` (code 0,
c'est normal) mais dit sur stderr *qui* tient *quoi* :

```
046-mer attend : sim/engine.py est tenu par 044-un-metier-le-mineur
aucune carte libre — `atelier lever --lot <lot>` après ta fusion.
```

Une file réellement vide, elle, ne dit rien. Le silence reste le silence.

**Après ta fusion, le pilote rend les fichiers** : la fiche du lot dit
`livre`, `piloter --run` range la carte dans `fusionnee/` et lève le
verrou au réveil suivant. Pour ne pas attendre demain matin, ou si le
pilote n'est pas armé :

```bash
python3 -m atelier verrous --projet /srv/ForgeHistory
python3 -m atelier lever   --projet /srv/ForgeHistory --lot 044-un-metier-le-mineur
```

Sans ça, le lot suivant qui touche `sim/engine.py` attendra un lot déjà
fusionné.

**Quand un agent a échoué**, sa carte est dans `echec/` avec la raison,
et le pilote ne redépose pas ce lot : il attend que tu aies lu.

```bash
python3 -m atelier feuille etat --projet /srv/ForgeHistory        # « en échec : … »
python3 -m atelier reprendre    --projet /srv/ForgeHistory --lot 046-la-mer-est-un-port-commun
```

Le lendemain, `piloter` redépose la carte. Une PR fermée sans fusion se
range de la même façon (`atelier echouer --role relire --lot … --raison
"PR fermée"`, puis `atelier lever`), et c'est la feuille du produit qui
dit ensuite si le lot repart (`pret`) ou non (`abandonne`).

---

## Installer, dans l'ordre

Sur le VPS, **une fois**.

1. Détacher ForgeAtelier (voir [PUBLIER.md](PUBLIER.md)) ou cloner la
   branche orpheline `cursor/forgeatelier-ced6` dans `/opt/ForgeAtelier`.
2. `cd /srv/ForgeHistory && git pull` — le fichier `atelier.toml` doit
   être là (PR de branchement), et nommer `feuille` pour que le pilote
   sache où lire le registre des lots. Rien ne marche sans lui : ni la
   veille, ni les rôles, ni les abonnements. Vérifie-le :

   ```bash
   python3 -m atelier doctor --projet /srv/ForgeHistory
   python3 -m atelier poste  --projet /srv/ForgeHistory --role relire
   python3 -m atelier feuille valider --projet /srv/ForgeHistory
   ```

   Le pilote fait un `git pull --ff-only` du dépôt produit avant de
   décider : la feuille qu'il lit est celle de `master` ce matin, pas
   celle d'hier. `ATELIER_SANS_PULL=1` le désactive.
3. Authentifier **trois** binaires, pas plus :
   - `hermes model` → ChatGPT / Codex OAuth (**pas** Anthropic)
   - `claude` → Claude Pro (OAuth du compte Pro)
   - `agent login` → Cursor Pro
4. Les profils et les worktrees :

```bash
ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/installer-profils.sh --dry-run
# vérifie les commandes imprimées, puis :
ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/installer-profils.sh --run
```

Le script vise Hermes 0.21 : il clone le profil `default`, puis règle
`terminal.cwd` avec `hermes --profile ROLE config set`. Le pilote utilise
`hermes --profile pilote -z PROMPT` ; `-p` n'est pas un drapeau de prompt
chez Hermes, il sélectionne un profil.

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

## Le jour où tu bascules

Une commande, avant de poser le drapeau :

```bash
python3 -m atelier pret --projet /srv/ForgeHistory
```

Elle lit le `PATH` et le disque. **Elle n'invoque personne** — regarder
n'est pas dépenser, même avec `ATELIER_INVOQUER=1`.

```
PASS  branchement — ForgeHistory (/srv/ForgeHistory/atelier.toml)
PASS  binaire agent (planifier, coder) — /usr/local/bin/agent
PASS  binaire claude (briefer, relire) — /usr/local/bin/claude
PASS  binaire hermes (pilote) — /usr/local/bin/hermes
PASS  le relecteur n'a pas la main qui écrit
PASS  flock — présent
PASS  timeout — présent
PASS  dossier des verrous — /tmp
?     quota — non lisible ; un inconnu ne se compte pas pour zéro
PASS  boîte a-coder — 1 carte(s)
PASS  feuille de route — ROADMAP.md, 19 lot(s), cohérente
?     ATELIER_INVOQUER n'est pas posé — mode à sec
```

Trois marques, et une seule bloque :

| marque | ce que ça veut dire |
|---|---|
| `PASS` | mesuré, et bon |
| `FAIL` | mesuré, et bloquant — code de sortie 1 |
| `?` | **non mesuré**. Ce n'est pas un échec, et ce n'est pas un feu vert. |

Un quota qu'on ne sait pas lire est un `?`, jamais un `0`. Un relecteur
sans garde de lecture seule est un `?` : ça tourne, mais tu sais ce que
tu acceptes.

Quand `pret` sort 0, tu peux armer un cron. Pas avant.

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

Un résumé pour toi, dans `atelier-echange/pilote.txt`, quand l'atelier a
trouvé quelque chose à dire — une carte déposée, une feuille incohérente.
**Pas** :

- une carte (c'est `atelier piloter` qui dépose, d'après la feuille)
- un numéro de lot, un statut, un chemin de brief (ils sont dans la
  décision qu'il reçoit ; il ne les invente pas)
- le brief (Claude)
- le code (Composer)
- un jugement de recevabilité (Claude relit, toi tu fusionnes)
- `git merge`

Le prompt du pilote n'est pas recopié ici : il est construit par
`atelier/backends.py`, la décision du jour y est insérée telle quelle, et
tu peux le lire tel qu'il partira.

```bash
python3 -m atelier invocation --role pilote --projet /srv/ForgeHistory \
    --decision "$(python3 -m atelier piloter --projet /srv/ForgeHistory 2>&1)"
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
| Hermes qui lit la feuille de route et devine le prochain lot | la décision se calcule (`atelier piloter`) ; Hermes la reçoit |
| Une carte du briefer qui file vers `a-coder` | le brief est en PR, le coder ne le trouverait pas sur master |
| Un lot déclaré livré avant sa fusion | la fiche voyage dans la PR du lot ; `master` ne dit livré qu'après |

Le seul « blocage » accepté : **toi**, le soir, sur une PR. Les crons
du lendemain voient `faite/` ou une PR encore ouverte, et se recouchent.
