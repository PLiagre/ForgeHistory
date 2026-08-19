**Author**: forge-evaluateur
**Authored**: 2026-08-19T18:30:00Z

# Verdict — Brief `022` : la relecture par stdin et la commande `iterate`

> **Note de transparence — à lire avant le verdict.** L'acteur réel est Claude
> Code endossant le rôle natif `forge-evaluateur`, sans suffixe ajouté à la
> signature, pour que `verdict_is_not_self_authored` puisse comparer les acteurs
> de part et d'autre du lot.
>
> **Séparation producteur / juge : réelle.** Ce lot a été produit par **Cursor**
> dans le worktree `agent/forgepilot-stdin`. Le juge est Claude. Les acteurs
> diffèrent pour de bon — ce n'est pas une discipline déclarée, c'est un fait.
>
> **Ce qu'il faut savoir malgré tout.** `amendment-001-six-tests-pas-quatre.md`
> et `feedback/feedback-001.md` ont été écrits le `2026-08-15` par une **autre
> session** du même acteur. La session qui écrit ce verdict, le `2026-08-19`,
> n'a rédigé ni le brief, ni l'amendement, ni le feedback de ce lot : elle les
> lit comme n'importe quel juge. C'est exactement l'option `B` arrêtée par le
> propriétaire le `2026-08-11` — « session distincte, jamais celle qui a produit
> le lot ». Le conflit qui pesait sur le verdict du lot `021` n'existe pas ici.
>
> **Rien n'est repris du manifeste.** Chaque compteur ci-dessous a été
> reconstruit par une commande dont la sortie est reproductible. Là où ma mesure
> diffère de celle déclarée, je l'écris.

**Ce qui est jugé.** L'état du dépôt à la fusion de la PR `#108` (`6c6a807`),
contre `brief.md` tel qu'amendé par `amendment-001-six-tests-pas-quatre.md`.

**Méthode.** Les mesures n'ont **jamais** été prises depuis la racine du dépôt :
`control-plane/` a été extrait par `git archive` au commit du lot dans un dossier
jetable, et la suite exécutée depuis cette copie. C'est la règle durement acquise
n° `2` — un red-first joué depuis la racine importe le module intact et ne prouve
rien.

---

## 1. La porte mécanique, d'abord

Commande : `py harness/verdict_audit.py harness/queue/briefs/022-forgepilot-review-stdin-et-iteration`

Avant ce verdict : `VERDICT: REJECT`, sur deux contrôles seulement —
`verdict_numbers_traceable` et `verdict_is_not_self_authored`, tous deux au motif
`verdict.md missing`. Les huit autres passaient. Ces deux-là ne constataient donc
pas un défaut du lot, mais **l'absence du présent document**.

## 2. Les quatorze compteurs, reconstruits

| compteur | déclaré | reconstruit | concordance |
|---|---|---|---|
| `longueur_argv_max_relecture` | `24` | `24` | identique |
| `octets_diff_du_test` | `131200` | `131174` | entrées différentes, **toutes deux** au-dessus de la borne `131072` |
| `prompt_absent_de_argv` | `1` | `1` | identique |
| `drapeaux_claude_inchanges` | `1` | `1` | identique |
| `tests_existants_intacts` | `1` | `1` | identique |
| `format_invocation_ne_fuit_pas_le_prompt` | `1` | `1` | identique |
| `iterate_reutilise_worktree` | `1` | `1` | identique |
| `iterate_refuse_sans_worktree` | `1` | `1` | identique |
| `iterate_porte_le_sandbox` | `1` | `1` | identique |
| `iterate_sans_run_ne_lance_rien` | `1` | `1` | identique |
| `tests_ajoutes` | `6` / `12` | `6` / `12` | identique |
| `tests_rouges_avant_correction` | `6` / `6` | `6` / `6` | identique |
| `suite_control_plane_verte` | `1` / `12` | `12` exécutés, `0` échec | identique |
| `fichiers_hors_perimetre_modifies` | `0` / `9` | `0` — voir constat `F2` | dénominateur précisé |

### SC1 — la relecture ne déborde plus

Un diff synthétique de `131174` octets a été injecté en remplaçant `workflow.git`,
puis `review_invocation` appelée. Le plus grand élément d'argv mesuré fait `24`
octets — c'est `--no-session-persistence`. Le prompt, lui, pèse `131657` octets
et voyage par le champ `prompt` de l'`Invocation`, hors argv. La borne système
n'est jamais recopiée dans le test : il lit `32 * os.sysconf("SC_PAGESIZE")`.

### SC2 — les garanties d'`ADR-0013` survivent

`git diff` de `_claude_argv` entre la base et la fusion : la **seule**
modification est le retrait du paramètre `prompt` et de sa place dans la liste.
`--output-format json`, `--permission-mode plan`, `--tools`, `--disallowedTools
mcp__*`, `--safe-mode`, `--disable-slash-commands`, `--no-chrome`,
`--no-session-persistence` sont présents, inchangés, dans le même ordre.

`test_workflow.py` : `0` ligne supprimée entre la base et la fusion, et les six
tests d'origine nommés par l'amendement `001` sont tous présents. L'aperçu sans
`--run` imprime `"prompt": "<prompt>"` — vérifié sur l'invocation dont le prompt
fait `131657` octets.

### SC3 — `iterate` existe, réutilise, refuse

Un worktree réel a été créé (`agent/verif-eval-022`), puis retiré. Sur ce
worktree, `iterate` sans `--run` : nombre de répertoires sous
`.forgepilot/worktrees/` avant `1`, après `1` — **aucun** créé. L'invocation
imprimée porte `--sandbox enabled` et masque le prompt. La commande rapporte
`Branche`, `Worktree` et `État git` **avant** d'agir, comme D2 l'exige.

Sans worktree, le code de sortie mesuré est `2` et le message nomme `execute` :
« *Worktree introuvable … Employer `execute` pour créer la branche et le
worktree.* »

### SC4 — le rouge-d'abord, refait et non cru

C'est la mesure la plus difficile à falsifier, et c'est celle que j'ai refaite
entièrement. Le `control-plane/` de la base `75b3dd0` a été extrait, puis le
`test_workflow.py` **d'après le lot** copié par-dessus, et la suite jouée depuis
cette copie : `12` tests, **`6` rouges**, et ce sont exactement les six ajoutés —

```
test_claude_flags_order_unchanged_after_dash_p
test_format_invocation_hides_prompt_keeps_output_format
test_iterate_without_worktree_refuses_naming_execute
test_review_keeps_argv_under_system_arg_limit
test_iterate_carries_sandbox_and_does_not_run_without_flag
test_iterate_reuses_existing_worktree
```

Les six d'origine restent verts sur le code d'avant. `tests_rouges_avant_correction`
= `6` sur `6` est donc **confirmé par reconstruction**, pas par lecture du journal.

À l'état du lot, la même suite rend `12` tests, `0` échec, `0` erreur.

### SC5 et SC6 — périmètre et documentation

`git diff --name-only` entre la base et la fusion rend `10` fichiers. Neuf sont
dans la liste D4 ; le dixième est traité au constat `F2`. `harness/tests/` est
vert : `364` tests passent sur l'état actuel du dépôt.

Le README documente `iterate` dans la séquence du premier essai et dit pourquoi
le prompt passe par stdin (la limite de `128` Ko par argument). L'instantané
pré-édition est présent et diffère du README publié — contrôle
`captures_differ_when_should` au vert.

---

## 3. Constats

**F1 — la suite du pilote est devenue exécutable sous Linux seulement.**
Les deux tests qui lisent la borne par `os.sysconf("SC_PAGESIZE")` **échouent
sous Windows**, où `os.sysconf` n'existe pas :

```
AttributeError: module 'os' has no attribute 'sysconf'
```

Ce n'est **pas** une entorse au brief : celui-ci exige explicitement que la borne
soit lue du système et jamais recopiée, et le lot obéit. Mais la conséquence est
réelle et n'est écrite nulle part — depuis ce lot, le propriétaire ne peut plus
jouer la suite complète du pilote sur son PC Windows. Sur le VPS Linux, aucun
problème. Vérifié en fournissant `SC_PAGESIZE = 4096` : les `12` tests passent
alors sans échec. Constat consigné, **non retenu contre le lot**.

**F2 — le dénominateur `9` face aux `10` fichiers de la fusion.**
Le dixième est `feedback/feedback-001.md`, écrit par le **relecteur**, pas par le
Générateur. La liste D4 borne le périmètre du Générateur ; un feedback de
relecture n'en relève pas. Le compteur est donc juste et son dénominateur
défendable. Écrit ici pour qu'une relecture future ne prenne pas cet écart pour
une dissimulation.

**F3 — `octets_diff_du_test` : `131200` déclaré, `131174` mesuré.**
Deux diffs synthétiques différents, construits indépendamment. La condition n'est
pas une valeur mais une inégalité — dépasser la borne système — et les deux la
remplissent. Aucune divergence de fond.

**Ce qui n'a pas été trouvé.** Aucun compteur gonflé, aucune condition contournée,
aucune garantie d'`ADR-0013` affaiblie, aucun test d'origine touché, aucun fichier
hors périmètre du Générateur.

---

## 4. Verdict

**ACCEPT.**

Les six conditions de succès sont remplies et reconstruites. Le défaut d'origine
— la relecture qui débordait sur `[Errno 7]` — est corrigé et **prouvé** corrigé
par une mesure qui échoue sur le code d'avant. La commande `iterate` existe,
réutilise, refuse proprement et conserve le sandbox. Les acquis d'`ADR-0013` sont
intacts.

Les trois constats sont des précisions pour la suite, pas des réserves sur la
recevabilité.

**Dette soldée.** Ce lot avait été fusionné le `2026-08-15` sans verdict, le
sous-agent Évaluateur ayant été tué par le plafond mensuel. La PR `#108` était
déjà sur `master` : ce verdict juge donc du code intégré, ce qui est moins
confortable qu'un jugement avant fusion, mais reste un jugement — et il conclut
que la fusion était méritée.

**Conséquence pour le lot `023`.** Son non-objectif n° `3` renvoie à un « verdict
de référence du lot `022` ». Il existe désormais.
