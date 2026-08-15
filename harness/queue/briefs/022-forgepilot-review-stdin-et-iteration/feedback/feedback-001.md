# Feedback 001 — après la relecture ADR-0013 de la PR #108

**Authored**: 2026-08-15T18:55:00Z
**Author**: forge-planificateur

> **Note de transparence.** L'acteur réel est Claude Code, orchestrateur du lot,
> endossant le rôle natif `forge-planificateur`. Il a aussi écrit
> l'`amendment-001` ; il est donc écarté de la rédaction du `verdict.md`, qui
> revient à un Évaluateur en passe distincte.

**Répond à** : la relecture produite par `forgepilot review` sur le commit
`82a356a` (verdict `FAIL`, huit constats), plus les vérifications faites par
l'orchestrateur en rejouant le lot lui-même.

---

## Ce qui est acquis et ne doit pas être retouché

Le fond du lot est bon et vérifié indépendamment. **Ne refais pas ce travail.**

- `_claude_argv` sans prompt, transport par `Invocation.prompt`, stdin jusqu'à
  `run_command` : correct. Mesuré sur un diff réel de `1253092` octets — plus
  grand élément d'argv `24` octets, et le prompt de `611570` jetons est
  réellement parvenu à l'API. La panne `[Errno 7]` d'origine ne se reproduit
  plus.
- `existing_worktree` + sous-commande `iterate` : corrects, éprouvés à la main
  dans les deux branches (worktree présent / absent).
- Le filtre `startswith("--")` de `format_invocation` et la rédaction du prompt
  dans `persist_result` : deux pièges bien désamorcés, on n'y touche pas.
- Les six tests d'origine (amendement 001) : intacts, aucune ligne supprimée.
  **Ils restent intacts.**
- `process.py` non modifié, invocation Cursor inchangée, README correct.

---

## Les six points à corriger

### P1 — SC5 n'est mesuré qu'à moitié (majeur, c'est le motif du FAIL)

Le brief exige que `harness/tests/` soit rejoué et rapporté avec le nombre de
tests collectés, les neuf échecs Unity préexistants étant rapportés séparément.
Aucune trace de cette commande dans `generator-log.md`.

Mesure déjà faite par l'orchestrateur, **à confirmer par toi, pas à recopier** :

```bash
.venv/bin/python -m pytest harness/tests/ -q
```

Baseline sur `origin/master` avant le lot (voir `amendment-001`, § 4) :
`9 failed, 355 passed`. Rejoue la commande depuis la racine du worktree avec le
venv du worktree, colle la ligne de résultat dans `generator-log.md`, et dis
explicitement si l'écart avec la baseline est nul.

### P2 — `measure_022.py` réécrit le manifeste par défaut (F4)

`--write-manifest` vaut `True` par défaut. Conséquence : l'Évaluateur, à qui la
rubrique demande de tout rejouer, salit un livrable committé en le faisant, et
peut y écrire silencieusement les compteurs de **son** environnement.

Inverse le défaut : l'écriture du manifeste devient une option explicite
(`--write-manifest`), et sans elle le script ne fait qu'imprimer. Supprime
`--no-write-manifest`, devenu inutile.

### P3 — `tests_rouges_avant_correction` s'auto-atteste (F5)

Le compteur est aujourd'hui obtenu en cherchant deux noms de test et le mot
`FAIL` dans `generator-log.md` — c'est-à-dire dans le document même qu'il est
censé corroborer. Règle durement acquise n° `3` : une mesure ne se dérive pas de
la déclaration qu'elle vérifie.

Dérive-le vraiment : fabrique une copie jetable hors du dépôt, restaure-y
`control-plane/forgepilot/workflow.py` et `cli.py` depuis `origin/master` en
gardant les tests neufs, lance la suite, et compte les échecs.

Fait mesuré par l'orchestrateur avec cette méthode, **à retrouver toi-même** :
`5` des `6` tests neufs sont rouges sur le code d'avant, et les `6` tests
d'origine restent verts. Le compteur déclaré (`2`) est donc juste mais
sous-évalué. Rapporte le nombre réel, avec le total de tests ajoutés pour
dénominateur.

### P4 — Le sixième test n'a jamais été vu rouge (F2)

`test_format_invocation_hides_prompt_keeps_output_format` passe déjà sur le code
d'avant : l'ancien `format_invocation` masquait lui aussi l'élément suivant `-p`.
Il ne prouve donc rien du changement — mais il garde réellement quelque chose :
retire le filtre `startswith("--")` du nouveau code, et il rougit.

Deux issues acceptables, au choix :

1. Consigner dans `generator-log.md` le rouge obtenu sur la variante « nouveau
   code **sans** le filtre », avec la sortie recopiée ; ou
2. Déclarer explicitement ce test comme **garde de non-régression** et non comme
   preuve rouge, en le disant dans `generator-log.md`.

Traite aussi son assertion finale (`assertNotEqual("<prompt>", payload["argv"][of_index])`),
qui est tautologique : `of_index` est par construction l'index de
`--output-format`. Remplace-la par une assertion qui a un pouvoir de réfutation,
ou retire-la.

### P5 — `measure_suite_verte` dépend de quel python le lance (F3)

Le sous-processus lance `sys.executable -m unittest discover -s control-plane/tests`
depuis la racine, sans `PYTHONPATH`. Le paquet `forgepilot` est alors résolu par
l'installation editable du venv qui exécute le script.

Attention, la relecture se trompe sur ce point et il ne faut pas la suivre
aveuglément : lancé avec le venv **du worktree**, le script résout bien vers le
`control-plane` du worktree et rend `1` sur `12` tests — vérifié. La fragilité
est réelle, la conclusion chiffrée de la relecture ne l'est pas.

Rends la mesure indépendante de l'environnement : passe un `env` explicite avec
`PYTHONPATH` pointant sur le `control-plane` du dépôt courant, ou lance depuis
`control-plane` avec `-s tests`. Et rapporte dans `generator-log.md` **la
commande réellement jouée**, pas la forme idéale du brief.

### P6 — `sample_size` de `octets_diff_du_test` (F6, mineur)

Il recopie la valeur mesurée au lieu de porter la borne système que SC1 nomme
comme dénominateur. Mets-y la borne lue par `32 * os.sysconf("SC_PAGESIZE")`.

---

## Ce que tu ne fais pas

1. **Ne touche à aucun des six tests d'origine.** Additions seulement.
2. **Ne sors pas du périmètre D4** du brief, augmenté du seul dossier
   `harness/queue/briefs/022-.../feedback/**` qui porte ce document.
3. **Ne modifie pas** le comportement de `_claude_argv`, `existing_worktree`,
   `iterate`, ni l'invocation de Cursor. Le fond est accepté.
4. **N'ajoute ni fusion, ni push sur `master`, ni cron, ni auto-fusion.**
5. Ne corrige pas les neuf échecs Unity : préexistants, hors sujet.
6. F7 de la relecture (`existing_worktree` ne vérifie pas que le répertoire est
   bien un worktree git) est laissé **ouvert volontairement** : le comportement
   est sûr, seul le message de diagnostic est perfectible. Réserve pour plus
   tard, pas un objectif de cette itération.
