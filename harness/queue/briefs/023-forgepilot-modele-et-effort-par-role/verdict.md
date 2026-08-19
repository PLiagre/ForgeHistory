**Author**: forge-evaluateur
**Authored**: 2026-08-19T18:45:00Z

# Verdict — Brief `023` : le modèle et l'effort, rôle par rôle

> **Note de transparence — à lire avant le verdict.** L'acteur réel est Claude
> Code endossant le rôle natif `forge-evaluateur`, sans suffixe ajouté à la
> signature, pour que `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.
>
> **Séparation producteur / juge : réelle.** Ce lot a été produit par **Cursor**
> dans le worktree `agent/023-forgepilot-modele-et-effort-par-role`. Le juge est
> Claude. Les acteurs diffèrent pour de bon.
>
> **Aucun conflit de rôle sur ce lot.** Contrairement au lot `021`, la session
> qui écrit ce verdict n'a rédigé ni le brief, ni de rubrique, ni de feedback
> pour le lot `023` — ce lot n'a d'ailleurs reçu aucun feedback, voir le constat
> `F2`. Elle le lit entièrement de l'extérieur.
>
> **Rien n'est repris du manifeste.** Chaque compteur a été reconstruit par une
> commande reproductible, en extrayant `control-plane/` au commit du lot dans un
> dossier jetable — jamais depuis la racine du dépôt (règle durement acquise
> n° `2`).

**Ce qui est jugé.** L'état du dépôt à la fusion de la PR `#109` (`88864b6`),
contre `brief.md` et `eval-rubric.md`. La base réelle du lot est `e6fdd28`,
celle-là même que le manifeste déclare.

---

## 1. La porte mécanique, d'abord

Commande : `py harness/verdict_audit.py harness/queue/briefs/023-forgepilot-modele-et-effort-par-role`

Avant ce verdict : `VERDICT: REJECT`, sur deux contrôles seulement —
`verdict_numbers_traceable` et `verdict_is_not_self_authored`, tous deux au motif
`verdict.md missing`. Les huit autres passaient. Ces deux-là ne constataient pas
un défaut du lot, mais **l'absence du présent document**.

## 2. Les vingt-et-un compteurs, reconstruits

Tous concordent avec le manifeste. Le détail de ce que j'ai réellement exécuté :

### SC1 — chaque rôle a son modèle

Une `config.toml` jetable déclarant `modele-planner` pour `planner` et
`modele-reviewer` pour `reviewer` a été chargée, puis les deux invocations
produites. Résultat lu dans argv :

| rôle | `--model` | `--effort` |
|---|---|---|
| `planner` | `modele-planner` | `xhigh` |
| `reviewer` | `modele-reviewer` | `low` |

Deux modèles distincts, donc `modeles_par_role_distincts` = `1`. Une section
`[roles.nimportequoi]` fait lever une `PilotError` dont le message nomme bien les
trois rôles valides — `planner`, `reviewer`, `executor` : `role_inconnu_refuse`
= `1`, `roles_declares` = `3` sur `3`.

### SC2 — l'effort atteint Claude, et seulement Claude

`--effort` est présent sur les deux invocations Claude, avec la valeur du rôle :
`effort_transmis_claude` = `1`.

Le refus côté Cursor a été sondé sur **les trois chemins** que la condition
exige, pas sur un seul :

| chemin | résultat mesuré |
|---|---|
| clé `effort` sous `[roles.executor]` | `PilotError` au chargement |
| `execute --effort high` | `REFUS` |
| `iterate --effort high` | `REFUS` |

Les trois portent le même message, qui explique la raison — Cursor cuit l'effort
dans le nom du modèle. `effort_refuse_sur_cursor` = `1`.

Les cinq niveaux ont été chargés un par un : `low`, `medium`, `high`, `xhigh`,
`max` acceptés ; un sixième inventé refusé. `niveaux_effort_acceptes` = `5` sur
`5`.

### SC3 — la priorité à l'appel

Trois mesures directes, sur la même configuration :

- un `--model` passé à l'appel remplace la valeur de `[roles.planner]` →
  `priorite_cli_sur_role` = `1` ;
- `[roles.planner]` l'emporte sur `[tools] claude_model` → `priorite_role_sur_tools`
  = `1` ;
- une configuration sans aucune section `[roles.*]` retrouve la valeur de
  `[tools]` → `repli_sur_tools` = `1` ;
- avec `claude_model` vide et aucun `[roles.*]`, `--model` est **absent** de
  argv → `aucun_drapeau_si_rien_declare` = `1`.

### SC4 — les garanties d'`ADR-0013` et du lot `022` survivent

`--permission-mode plan`, `--tools`, `--disallowedTools mcp__*` et `--safe-mode`
sont tous présents sur les invocations Claude : `drapeaux_lecture_seule_intacts`
= `1`. Le prompt reste hors argv : `prompt_absent_de_argv` = `1`, l'acquis du lot
`022` tient.

`test_workflow.py` : `0` ligne supprimée entre `e6fdd28` et la fusion, et le
fichier passe de `12` à `26` tests. `tests_existants_intacts` = `1`.

`sandbox_intact` et `settings_retrocompatible` sont portés par des tests qui
existaient **avant** ce lot et qui restent verts à son état — c'est la forme la
plus forte pour une garantie de non-régression.

### SC6 — le rouge-d'abord, refait et non cru

Le `control-plane/` de la base `e6fdd28` a été extrait, le `test_workflow.py`
**d'après le lot** copié par-dessus, et la suite jouée depuis cette copie :
`26` tests, **`12` rouges**. Exactement le chiffre déclaré.

À l'état du lot, la même suite rend `26` tests et `0` rouge —
`suite_control_plane_verte` = `1` sur `26`.

### SC7 — le périmètre

`git diff --name-only e6fdd28 88864b6` rend `11` fichiers, exactement le
dénominateur déclaré, et tous relèvent de la liste D8.
`fichiers_hors_perimetre_modifies` = `0`. `harness/tests/` est vert.

---

## 3. Constats

**F1 — `12` rouges sur `14` ajoutés est le chiffre juste, pas un manque.**
La condition exigeait « au moins `3` ». Le lot en déclare `12`, et j'ai voulu
savoir pourquoi pas `14`. En soustrayant les rouges mesurés de la liste des tests
ajoutés, les deux qui manquent sont :

- `test_no_model_flag_when_nothing_declared`
- `test_settings_ten_fields_still_constructible`

Ce sont précisément les deux qui décrivent un comportement **préservé** :
ils doivent être verts avant *et* après. Un test de non-régression ne peut pas
être rouge d'abord sans que la non-régression soit fausse. Le Générateur aurait
pu écrire `14` sans que rien ne le contredise mécaniquement — il a écrit le
chiffre exact. C'est le contraire d'un compteur gonflé, et cela mérite d'être dit.

**F2 — ce lot n'a reçu aucune relecture avant sa fusion.**
Il est le seul des trois lots pilotes sans fichier de feedback : le `021` en a
eu, le `022` aussi. Le `023` est passé directement. Ce n'est pas un défaut du
travail livré — que je viens de reconstruire et qui tient — mais c'est un fait
sur le **processus** : ce lot a subi moins de contrôle que ses deux
prédécesseurs avant d'entrer sur `master`. À verser au bilan des trois lots.

**F3 — la valeur livrée pour l'effort du relecteur est un défaut documenté, pas
une valeur mesurée.**
Le lot livre `[roles.reviewer] effort = "low"`. Le brief déclare explicitement en
non-objectif n° `3` qu'il ne choisit pas les valeurs par la mesure — le lot est
donc **dans son périmètre** et je ne lui reproche rien. Mais la conséquence doit
être écrite : le pilote tourne aujourd'hui avec le jugement réglé au niveau
d'effort le plus bas, et le lot de mesure que le brief annonce n'a pas eu lieu.
Or ce même brief fait dépendre cette mesure du « verdict de référence du lot
`022` » — qui existe depuis aujourd'hui. La dépendance est levée ; la mesure
reste à faire.

**F4 — la suite du pilote reste exécutable sous Linux seulement.**
Hérité du lot `022` et non aggravé : les deux tests qui lisent la borne système
par `os.sysconf` échouent sous Windows. Mesures ci-dessus prises en fournissant
`SC_PAGESIZE = 4096`, la valeur Linux. Sur le VPS, aucun problème.

**Ce qui n'a pas été trouvé.** Aucun compteur gonflé, aucune condition
contournée, aucune garantie affaiblie, aucun test d'origine touché, aucun fichier
hors périmètre.

---

## 4. Verdict

**ACCEPT.**

Les huit conditions de succès sont remplies et reconstruites. Le pilote sait
désormais régler modèle et effort par rôle, avec un ordre de priorité qui
fonctionne dans les trois sens éprouvés, un refus motivé là où Cursor n'a pas
d'effort séparé, et sans qu'aucune garantie d'`ADR-0013` ni du lot `022` ne
bouge.

Les quatre constats sont des précisions et une dette de mesure, pas des réserves
sur la recevabilité.

**Dette soldée.** Ce lot avait été fusionné le `2026-08-16` sans verdict. Ce
document juge du code déjà intégré — moins confortable qu'un jugement avant
fusion, mais il conclut que la fusion était méritée.

**Ce que ce verdict débloque.** Le brief `023` conditionnait le lot de mesure
qualité/coût au verdict de référence du lot `022`. Les deux existent désormais :
comparer `low` et `high` sur le rôle relecteur est le prochain lot possible, et
il porte une décision réelle — celle du niveau auquel le projet veut être jugé.
