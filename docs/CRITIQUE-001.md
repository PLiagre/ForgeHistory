# Critique 001 — le plan lu de près, avant d'ouvrir l'invocation

Compte rendu de la phase de lecture du lot 001. Lu : `VISION.md`,
`ANALYSE.md`, `AGENTS.md`, `docs/MISE-EN-PLACE.md`, `docs/PUBLIER.md`,
`briefs/001-profils-hermes-et-invocation.md`, `atelier/boite.py`,
`crons/tour.sh`, `ROADMAP.md`, et les modules qu'ils touchent.

Une critique sans commande qui échoue n'est pas une critique. Chaque
point porte donc la mesure qui le tient.

---

## 1. Ce qui tient

| ce qui tient | la preuve |
|---|---|
| Deux dépôts, frontière nette. L'atelier ne porte aucune formule du monde. | `grep -rn "sim/" atelier/` ne rend rien |
| La boîte, pas le pipeline. Le briefer avance vers `a-coder`, jamais vers `a-planifier`. | `boite.SUIVANT`, `tests/test_boite.py::test_coder_ne_depend_pas_du_planificateur` |
| Boîte vide = `RIEN`, code 0. Personne n'appelle le rôle suivant. | `atelier prochain --projet /tmp --role coder` |
| `fusionner` refuse, et la CI le rejoue. | `.github/workflows/tests.yml`, dernier pas |
| Un quota inconnu vaut `-1`, jamais `0` ; un échantillon vide de quotas échoue. | `atelier/quota.py`, `tests/test_quota.py` |
| Le canal d'échange porte sa propre garde `*` : il ne dépend pas du `.gitignore` du produit (leçon du lot 033). | `echange.git_ignore_le_canal` |
| Un module, une couche, et les sept sont occupées. | `atelier couches` |
| `ATELIER_INVOQUER` comme interrupteur unique, par défaut fermé. | `crons/tour.sh` |
| Superpowers et llmquota ne sont pas des dépendances. | `pyproject.toml` n'a aucune dépendance de production |

Ce socle n'est pas à refaire. Le lot 001 ne le rouvre pas.

---

## 2. Ce qui casse

**C1 — `avancer` et `echouer` n'existent pas.** `docs/MISE-EN-PLACE.md`
décrit un cron qui « fait sa tâche, timeout, puis `avancer` ou
`echouer` ». La CLI n'a ni l'un ni l'autre : `python3 -m atelier
avancer` sort 2 sur une erreur d'argparse. Une carte prise ne peut
pas sortir de sa boîte. Les fonctions Python existent, la porte non.

**C2 — le pilote dépense sans drapeau.** `crons/pilote.sh` lance
`hermes -p` sans regarder `ATELIER_INVOQUER`. La règle 8 dit
« sans `ATELIER_INVOQUER=1`, aucun binaire d'agent n'est lancé », et
l'étape 4 de la mise en place dit de poser le crontab *sans* le
drapeau : dans cet état, le réveil de 07:00 consomme quand même
ChatGPT Plus. Le plan et le code se contredisent.

**C3 — ni `flock`, ni `timeout`, nulle part.** Le crontab porte le
commentaire « un flock par rôle, jamais un flock global » et n'appelle
jamais `flock` ; `tour.sh` non plus. Un `claude -p` qui pend tient le
rôle jusqu'au lendemain, et le lendemain en lance un deuxième sur la
même carte. C'est le seul point de blocage *entre processus* du
montage, et il est dans le fichier qui prétend l'interdire.

**C4 — l'abo mal branché est une variable d'environnement.** Le plan
compte trois abonnements et ne regarde pas ce qui décide de la
facture. Si `ANTHROPIC_API_KEY`, `CURSOR_API_KEY` ou `OPENAI_API_KEY`
traîne dans l'environnement du cron, l'appel bascule de l'abonnement
vers l'API à l'unité. On ne veut pas apprendre la réponse sur la
facture : le cron retire ces clés de son environnement avant
d'invoquer.

**C5 — la boîte découple l'ordre, pas le compteur.** « Grok n'est pas
sur le chemin de Composer » est vrai dans la boîte et faux dans le
quota : `planifier` (10:00) et `coder` (14:00) tirent le même Cursor
Pro ; `briefer` (08:30) et `relire` (19:00) le même Claude Pro. Le
rôle facultatif peut donc affamer le rôle critique — le blocage
inter-agents que le plan n'a pas vu. Et rien ne mesure quoi que ce
soit : `quota.hop` attend des nombres que personne ne lit.

**C6 — une carte peut changer de brief.** `boite.avancer(**champs)`
fait un `brut.update(...)` sans liste blanche :
`avancer(projet, "planifier", lot, brief="autre.md")` réécrit la seule
source d'instruction du lot. Le planificateur devient une seconde
source, exactement ce que VISION interdit.

**C7 — un échantillon vide passe.** `deposer` n'exige que `lot`. Une
carte `brief=""` traverse toute la boîte, et le coder invoquerait
Composer sur un brief qui n'existe pas : dépenser sans livrable, la
forme du lot 035.

**C8 — une carte illisible bloque un rôle pour toujours.** `lister`
lève sur le premier JSON infirme du dossier, donc `prochain` sort 1,
donc — avec `set -euo pipefail` — `tour.sh` meurt à l'affectation,
avant le `if`. Tous les jours, sans message utile, et la carte reste.

**C9 — rien ne lève un verrou, et personne ne le regarde.**
`verrou.poser` est appelé par `start --run` ; `verrou.lever` n'a pas
de CLI. Après ta fusion, `verrous.json` garde le lot indéfiniment. À
l'inverse, le comportement promis par la mise en place — « 044 occupe
`engine.py` ? 046 attend, le cron prend 047 s'il est disjoint » —
n'existe pas : `prochain` rend `cartes[0]`, verrou ou pas.

**C10 — l'atelier connaît le jeu.** `crons/veille.sh` lance
`python3 -m sim --ticks 0 --json` en dur, alors que `atelier.toml`
porte déjà `fumee`. VISION : « Il ne sait pas ce qu'est une cellule. »

**C11 — le relecteur a les pouvoirs de l'exécutant.** « Claude revue :
mode lecture (`claude -p` sans écrire le code) » est une phrase, pas
un drapeau. Un `claude -p` nu peut éditer, committer, et appeler
`gh pr merge` : la fusion déguisée n'est pas dans l'atelier, elle est
dans les outils qu'on tend au relecteur.

**C13 — regarder une boîte l'écrit.** `_dossier` fait un `mkdir` sur
le chemin de *lecture* : `atelier prochain --projet /tmp --role coder`
crée `/tmp/.atelier/boite/a-coder/`. « Sans `--run`, rien n'est écrit »
— un aperçu n'est pas une dépense, et lire n'est pas déposer.

**C14 — une carte qui ne peut pas avancer reste en place.** Si Grok
avance `044` vers `a-coder` où Composer a déjà la même carte, `deposer`
lève, le cron meurt, et la carte reste dans `a-planifier` : le rôle la
retrouve demain, la repaie demain, tous les jours. L'invocation a eu
lieu ; la carte doit bouger.

**C12 — ce lot a deux sources d'instruction.** Le brief 001 et le
prompt du lot divergent : le brief n'autorise ni `crons/crontab` ni
`docs/CRITIQUE-001.md`, le prompt oui. Et la SC4 du brief
(`ATELIER_INVOQUER=0 ./crons/tour.sh coder` « imprime l'invocation »)
est infalsifiable telle quelle : boîte vide, `tour.sh` sort `RIEN`
avant d'imprimer quoi que ce soit. Le prompt prime — on le note au
lieu de le deviner, et la mesure pose d'abord une carte.

---

## 3. Ce qu'on change

| # | change | la commande qui échouait |
|---|---|---|
| A1 | `atelier avancer` / `atelier echouer` dans la CLI. Invariant de fin de tour : une carte **invoquée** finit avancée ou en `echec/`, jamais en place. | `python3 -m atelier avancer --projet … --role coder --lot X` |
| A2 | `atelier invocation --role …` construit l'argv en Python. Le shell ne compose plus de ligne de commande ; il exécute celle-là. Le prompt **cite le chemin du brief** et interdit toute autre consigne (carte, plan, message). | `python3 -m atelier invocation --role coder --lot X --brief b.md` |
| A3 | `tour.sh` : `flock -n` par rôle, `timeout`, purge des clés d'API, garde de quota, puis `avancer`/`echouer`. Jamais le rôle suivant. | deux `tour.sh coder` en parallèle ; `ANTHROPIC_API_KEY` posée |
| A4 | `pilote.sh` passe sous `ATELIER_INVOQUER`, avec `flock` et `timeout`. | `ATELIER_INVOQUER=0 ./crons/pilote.sh` avec un faux `hermes` |
| A5 | Garde de quota **facultative** : `ATELIER_QUOTA_CMD` (défaut : `llmquota` s'il est installé). Épuisé → exit 0, carte intacte. Absent, muet ou non numérique → inconnu, on continue. | faux `llmquota` rendant `0` |
| A6 | Réserve du rôle facultatif : `planifier` ne descend pas sous `ATELIER_RESERVE_planifier` (défaut 1). Le coder passe avant Grok sur le même abo. Réponse à C5. | faux `llmquota` rendant `1` |
| A7 | `boite.avancer` : liste blanche `pr`/`note`/`fichiers` (et les fichiers seulement s'ils sont vides). Un brief ne se réécrit pas. | `avancer(..., brief="autre.md")` |
| A8 | `deposer` refuse une carte sans brief ; `tour.sh` refuse un brief introuvable dans le produit. | `deposer --brief ""` |
| A9 | `prochain --role coder` saute les cartes dont un fichier est déjà verrouillé par un autre lot. `atelier verrouiller` / `atelier lever`. Le coder pose le verrou avant d'invoquer, le lève s'il échoue. | verrou sur `engine.py`, deux cartes |
| A10 | `prochain --champ lot|brief|pr|note` : le shell ne lit plus de JSON. | — |
| A11 | `atelier fumee --projet` ; `veille.sh` oublie `sim`. | `grep -c sim crons/veille.sh` |
| A12 | `crons/installer-profils.sh --dry-run` : imprime les quatre profils Hermes et leur `terminal.cwd`, n'écrit rien sous `~/.hermes`, et ne nomme aucun fournisseur Anthropic. | `test ! -e ~/.hermes/profiles/pilote` |
| A13 | Une carte illisible : `tour.sh` déclare le fichier fautif sur stderr et sort 1 sans rien invoquer, au lieu de mourir à l'affectation. | carte `{}` dans `a-coder` |
| A14 | Lire ne crée plus rien : `_dossier` regarde, `_ouvrir` dépose. | `prochain --projet /tmp` puis `test ! -e /tmp/.atelier` |
| A15 | Une carte invoquée qui ne peut pas avancer tombe dans `echec/`, avec sa raison. | même lot dans `a-planifier` et `a-coder` |

---

## 4. Ce qu'on refuse

- **Un ordonnanceur.** Aucun cron n'appelle le suivant, même « juste
  pour enchaîner ». C'est le lot 035.
- **Le kanban Hermes comme file.** La file est `.atelier/boite/`. Un
  tableau qui décide est une base parallèle (ANALYSE, Mission Control).
- **Superpowers et llmquota en dépendance dure.** `pyproject.toml`
  reste sans dépendance de production ; les deux s'installent à la
  main, et leur absence n'échoue jamais. Superpowers reste une note
  d'installation : le brief garde le monopole de l'instruction, son
  brainstorm ne le remplace pas.
- **Codex CLI à côté de Hermes.** Même quota hebdomadaire ChatGPT.
- **Hermes sur un fournisseur Anthropic.** Pro le refuse, Max facture
  l'extra hors forfait. Le script de profils le vérifie.
- **Toute fusion.** Aucun `gh pr merge`, aucun `git merge` dans
  `crons/` ni dans `atelier/` ; `atelier fusionner` reste à 2.
- **Grok en préalable.** `planifier` reste facultatif et cède le quota.
- **ForgePilot.** Pas de R0/R1/R2, pas de bot de fusion, pas de
  comptage de jetons comme critère.
- **Un relecteur qui corrige.** Il relit le diff ; il ne pousse pas.
- **Conseil, Mem0, E2B, Browser Use, Qwen, Goose** — pas dans ce lot.

---

## 5. Comment on le mesure

```bash
python3 -m pytest tests/ -q                                   # tout le contrat
python3 -m atelier fusionner                                  # code 2
python3 -m atelier prochain --projet /tmp --role coder        # RIEN, code 0
./crons/installer-profils.sh --dry-run                        # imprime
test ! -e ~/.hermes/profiles/pilote || echo "dry-run a écrit : FAIL"
./crons/installer-profils.sh --dry-run | grep -ci anthropic   # 0
grep -rniE "gh pr merge|git merge" crons/ atelier/            # rien
grep -c "sim" crons/veille.sh                                 # 0
```

Les contrôles neufs vivent dans `tests/test_invocation.py` et
`tests/test_boite.py`. Aucun n'appelle `claude`, `agent`, `hermes` ni
`llmquota` : ils posent de faux binaires dans un `PATH` de test, et
c'est le faux binaire qui prouve qu'on l'a — ou qu'on ne l'a pas —
lancé. La CI ne dépense aucun quota.
