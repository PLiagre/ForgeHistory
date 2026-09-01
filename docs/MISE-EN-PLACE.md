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
de dix heures. Si le matin a tout mangé, le cron du soir voit le
quota et sort `RIEN` / échec — il ne reste pas planté.

Grok n'est **pas** sur le chemin critique. Composer code à partir du
brief. Si Grok n'a rien produit, Composer s'en fiche.

---

## ChatGPT Plus nourrit-il Hermes ?

Oui, **à une condition** : tu branches Hermes en `openai-codex`
(OAuth ChatGPT), pas avec une clé `OPENAI_API_KEY`.

```bash
hermes model          # OpenAI → ChatGPT / Codex Subscription
hermes auth list      # tu veux openai-codex + oauth
```

Tu ne lances **pas** Codex CLI à côté. Hermes *est* l'usage ChatGPT
du VPS. Un modèle léger pour le pilote (il dialogue, il dépose une
carte, il ne code pas).

Si tu veux garder Codex comme relecteur de brief, il te faut un
quatrième quota (API, ou un autre compte). Avec tes trois abos,
on ne le fait pas.

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
3. sinon il fait **sa** tâche, timeout, puis `avancer` ou `echouer`
4. il n'appelle pas le cron suivant

Le seul humain dans la boucle : tu lis la PR et tu fusionnes.
Ça n'est pas un point de blocage *entre agents*.

---

## Horaires (Europe/Paris)

```cron
# /etc/cron.d/atelier  — un flock par rôle, jamais un flock global
15 6  * * * ubuntu /opt/ForgeAtelier/crons/veille.sh
0  7  * * * ubuntu ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/pilote.sh
30 8  * * * ubuntu ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh briefer
0  10 * * * ubuntu ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh planifier
0  14 * * * ubuntu ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh coder
0  19 * * * ubuntu ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh relire
```

| heure | script | agent | s'il n'a rien |
|---|---|---|---|
| 06:15 | `veille.sh` | aucun (script) | silence |
| 07:00 | `pilote.sh` | Hermes / ChatGPT Plus | une proposition, ou rien |
| 08:30 | `briefer` | Claude Pro | `RIEN` |
| 10:00 | `planifier` | Cursor Grok 4.6 | `RIEN` — Composer code quand même |
| 14:00 | `coder` | Cursor Composer | `RIEN` |
| 19:00 | `relire` | Claude Pro | `RIEN` |

`flock` par rôle : deux *briefer* ne se marchent pas. Un *briefer* et
un *coder* tournent ensemble sans se parler.

---

## Worktrees : un agent, un répertoire

```bash
cd /srv
git -C ForgeHistory worktree add ../fh-claude  -b claude/briefs origin/master
git -C ForgeHistory worktree add ../fh-grok    -b grok/plan     origin/master
git -C ForgeHistory worktree add ../fh-composer -b agent/courant origin/master
```

Claude revue : mode lecture (`claude -p` sans écrire le code). Composer
est le seul qui pousse `agent/NNN-slug` et ouvre la PR.

Un seul lot de **code** à la fois par fichier : `atelier verrou`.
044 occupe `engine.py` ? 046 attend dans `a-coder/`. Le cron coder
prend 047 s'il est disjoint, ou sort `RIEN`.

---

## Installer, dans l'ordre

Sur le VPS, **une fois**.

1. Détacher ForgeAtelier (voir [PUBLIER.md](PUBLIER.md)) ou cloner la
   branche orpheline `cursor/forgeatelier-ced6` dans `/opt/ForgeAtelier`.
2. `cd /srv/ForgeHistory && git pull` — le fichier `atelier.toml` doit
   être là (PR de branchement).
3. Authentifier **trois** binaires, pas plus :
   - `hermes model` → ChatGPT / Codex OAuth
   - `claude` → Claude Pro (OAuth du compte Pro)
   - `agent login` → Cursor Pro
4. Poser le crontab ci-dessus **sans** `ATELIER_INVOQUER=1`.
5. Déposer une carte à la main et regarder :

```bash
python3 -m atelier deposer --projet /srv/ForgeHistory \
    --etat a-coder --lot 044-un-metier-le-mineur \
    --brief briefs/044-un-metier-le-mineur.md \
    --fichier sim/engine.py --fichier sim/constants.py

ATELIER_PROJET=/srv/ForgeHistory /opt/ForgeAtelier/crons/tour.sh coder
# doit imprimer l'invocation Cursor, sans lancer
```

6. Quand tu as vu trois matins de `RIEN` / d'impressions justes :
   `ATELIER_INVOQUER=1` sur **un seul** cron, le `coder` d'un brief
   déjà relu par toi. Pas les six d'un coup.

---

## Ce que Hermes a le droit d'écrire

Une carte, une proposition, le tableau de bord. **Pas** :

- le brief (Claude)
- le code (Composer)
- un jugement de recevabilité (Claude relit, toi tu fusionnes)
- `git merge`

Le prompt du pilote, chaque matin :

```text
Tu es le pilote. Tu ne codes pas. Tu ne fusionnes pas.
Lis ROADMAP.md. S'il manque un brief pour le prochain lot
dont le périmètre est libre, dépose une carte a-briefer
avec python3 -m atelier deposer … et arrête-toi.
S'il n'y a rien à demander, écris RIEN et arrête-toi.
N'invoque ni claude ni agent.
```

---

## Points de blocage qu'on refuse

| ça | pourquoi c'est interdit |
|---|---|
| Hermes qui lance `agent` dans la même commande | lot 035 |
| Composer qui `wait` Grok | Grok est facultatif |
| Claude revue qui attend des tests verts | la CI GitHub le dit ; Claude relit le diff tel quel |
| Un flock global | deux rôles ne doivent pas se sérialiser |
| Fusion automatique | le propriétaire regarde (règle 11) |

Le seul « blocage » accepté : **toi**, le soir, sur une PR. Les crons
du lendemain voient `faite/` ou une PR encore ouverte, et se recouchent.
