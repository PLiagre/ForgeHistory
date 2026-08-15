# Rubrique d'évaluation — Brief 023 (ForgePilot : modèle et effort par rôle)

**Authored**: 2026-08-15T21:01:00Z
**Author**: forge-planificateur

Écrite **avant** tout code. L'Évaluateur juge contre ce document et le
`brief.md`, jamais contre ce que le Générateur affirme avoir fait.

---

## Échecs disqualifiants

Chacun suffit seul à rendre REJECT, quelle que soit la qualité du reste.

1. **Un champ obligatoire est ajouté à `Settings`.** Les douze tests existants
   construisent `Settings` avec ses dix champs ; un onzième champ sans valeur
   par défaut les casse tous d'un `TypeError`. C'est la contrainte D2.
2. **Un des douze tests existants de `control-plane/tests/test_workflow.py` est
   modifié ou supprimé.** Seules des additions sont recevables.
3. **Un drapeau de lecture seule de Claude Code a disparu ou changé de valeur**
   (`--permission-mode plan`, `--tools`, `--disallowedTools mcp__*`,
   `--safe-mode`).
4. **`--sandbox enabled` absent de `execute` ou d'`iterate`.**
5. **Le prompt réapparaît dans un élément d'argv**, ou fuit dans l'aperçu.
   L'acquis du lot `022` est perdu.
6. **Un `--effort` destiné à Cursor est silencieusement ignoré** au lieu d'être
   refusé. Un réglage qui ne fait rien sans le dire est pire qu'un réglage
   absent.
7. **Le drapeau passé à l'appel ne l'emporte pas** sur `config.toml`. Sans cela
   Hermes ne peut pas décider sans réécrire un fichier, et le lot rate sa raison
   d'être.
8. **Un test ajouté n'a jamais été vu échouer** sur le code d'avant, ou son
   échec n'est pas recopié dans `generator-log.md`. Règle n° `4`.
9. **`tests_rouges_avant_correction` est obtenu en cherchant des mots dans
   `generator-log.md`** au lieu d'être dérivé en rejouant la suite. Règle n° `3`.
10. **Un fichier hors de la liste D8 est modifié.**
11. **Une commande de fusion, de push sur `master`, de cron ou d'auto-merge est
    ajoutée.**
12. **Un compteur est déclaré sans dénominateur**, ou avec un `sample_size` nul
    ou égal à la sentinelle.

## Ce que l'Évaluateur reconstruit lui-même

Ne rien reprendre du manifeste. Rejouer :

- la construction des trois invocations avec un `config.toml` de test déclarant
  **deux modèles Claude différents**, et vérifier soi-même que `plan` et
  `review` portent bien des `--model` distincts ;
- les quatre niveaux de priorité de D3, un par un : drapeau > `[roles.*]` >
  `[tools]` > rien ;
- l'appel `--effort` sur `execute` et sur `iterate`, et lire les deux messages
  d'erreur — vérifier qu'ils **expliquent** que Cursor cuit l'effort dans le nom
  du modèle, au lieu de refuser sans raison ;
- la construction de `Settings` avec exactement les dix champs d'origine ;
- les cinq niveaux d'effort, plus un niveau invalide qui doit être refusé ;
- la suite `control-plane` complète, en comptant les tests et en rapportant la
  commande réellement jouée ;
- la preuve rouge, en restaurant le code d'avant dans une copie jetable **hors
  du dépôt** (jamais un `git stash` dans le worktree) et en comptant les échecs
  parmi les seuls tests ajoutés ;
- `git diff --name-only` contre la base réelle du lot, confronté à la liste D8 ;
- `git diff` de `test_workflow.py`, pour vérifier qu'il ne contient **que** des
  additions.

Chercher activement la faille : saboter une ligne et vérifier que la garde
correspondante rougit ; vérifier qu'aucune valeur de D6 n'est recopiée en dur
là où elle devrait être lue de la configuration.

## Barème par condition de succès

| SC | PASS si | REJECT si |
|---|---|---|
| SC1 | deux rôles Claude portent deux modèles différents ; un rôle inconnu est refusé en nommant les trois valides | les rôles partagent encore un modèle, ou un rôle inconnu passe en silence |
| SC2 | `--effort` atteint les deux rôles Claude ; il est refusé avec explication sur les deux chemins Cursor ; les cinq niveaux valides passent et un invalide est refusé | effort absent côté Claude, ignoré côté Cursor, ou niveau arbitraire accepté |
| SC3 | les quatre niveaux de priorité se comportent comme D3 le dit, y compris le repli et le cas « rien déclaré » | un niveau inversé, ou le comportement d'aujourd'hui perdu quand `[roles.*]` est absent |
| SC4 | drapeaux de lecture seule et sandbox intacts, prompt hors argv, `Settings` rétrocompatible, douze tests non touchés | une garantie perdue |
| SC5 | l'aperçu montre modèle et effort, sans le prompt | choix invisible dans l'aperçu, ou prompt visible |
| SC6 | au moins trois tests vus rouges, sortie recopiée, compteur **dérivé**, suite verte | un test jamais rouge, un compteur auto-attesté, ou une suite en échec |
| SC7 | zéro fichier hors D8, `harness/tests/` sans régression nouvelle | un fichier hors périmètre, ou une régression autre que les neuf échecs Unity connus |
| SC8 | README documente `[roles.*]`, la priorité, et le pourquoi de l'absence d'effort chez Cursor ; instantané pré-édition committé et différent | documentation absente, ou couple `must_differ_from` non déclaré |

## Observations attendues, qui ne sont pas des motifs de rejet

- Les neuf échecs de `harness/tests/test_run_unity.py` sous WSL2 sont
  **préexistants** (binaire Unity absent, `powershell.exe` visible via
  `/mnt/c/`). Vérifier qu'ils le sont en rejouant la baseline, et ne pas les
  compter contre ce lot.
- La base du lot peut être `agent/forgepilot-stdin` ou `master`, selon que la
  PR `#108` a été fusionnée. Les deux sont recevables ; ce qui ne l'est pas,
  c'est une base antérieure au lot `022`.
- Le choix de `reviewer` à effort `low` est **délibéré et documenté** (D6). Ce
  n'est pas une négligence, et l'Évaluateur n'a pas à le corriger. S'il le juge
  discutable, c'est une réserve pour le prochain Planificateur, pas un rejet.
- La forme exacte de la lecture de `[roles.*]` est libre. Seul le résultat
  mesuré compte.
