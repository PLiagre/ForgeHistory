# Rubrique d'évaluation — Brief 022 (ForgePilot : relecture par stdin, commande d'itération)

**Authored**: 2026-08-15T12:31:00Z
**Author**: forge-planificateur

Écrite **avant** tout code. L'Évaluateur juge contre ce document et le
`brief.md`, jamais contre ce que le Générateur affirme avoir fait.

---

## Échecs disqualifiants

Chacun suffit seul à rendre REJECT, quelle que soit la qualité du reste.

1. **Un élément d'argv de la relecture dépasse encore la limite système.**
   C'est le défaut que ce lot existe pour corriger.
2. **Un des quatre tests existants de `control-plane/tests/test_workflow.py` est
   modifié ou supprimé.** Ils encodent les garanties d'ADR-0013. Seules des
   additions sont recevables.
3. **Un drapeau de lecture seule de Claude Code a disparu ou changé de valeur**
   (`--permission-mode plan`, `--tools`, `--disallowedTools mcp__*`,
   `--safe-mode`).
4. **`--sandbox enabled` absent de l'invocation d'`iterate`.**
5. **`iterate` crée un worktree.** Sa raison d'être est de réutiliser l'existant.
6. **Un test ajouté n'a jamais été vu échouer** sur le code d'avant, ou son échec
   n'est pas recopié dans `generator-log.md`. Règle n° 4.
7. **Le prompt apparaît dans un élément d'argv**, ou fuit dans l'aperçu
   `format_invocation`.
8. **Un fichier hors de la liste D4 est modifié.**
9. **Une commande de fusion, de push sur `master`, de cron ou d'auto-merge est
   ajoutée.**
10. **Un compteur est déclaré sans dénominateur**, ou avec un `sample_size` nul
    ou égal à la sentinelle.

## Ce que l'Évaluateur reconstruit lui-même

Ne rien reprendre du manifeste. Rejouer :

- la construction de l'invocation de relecture sur un diff de plus de `131072`
  octets, et mesurer soi-même la longueur du plus grand argv ;
- la borne système, par `32 * os.sysconf("SC_PAGESIZE")` — vérifier qu'elle n'est
  pas recopiée en dur dans le code ni dans le test ;
- `python3 -m unittest discover -s control-plane/tests`, et compter les tests ;
- `git diff origin/master...HEAD --name-only`, confronté à la liste D4 ;
- `git diff origin/master...HEAD -- control-plane/tests/test_workflow.py`, pour
  vérifier que le diff ne contient **que** des additions ;
- l'appel `iterate` sans `--run` sur un worktree existant, puis sur un worktree
  absent, et lire les deux sorties.

## Barème par condition de succès

| SC | PASS si | REJECT si |
|---|---|---|
| SC1 | plus grand argv < borne système, sur un diff qui la dépasse | argv encore au-dessus, ou diff de test trop petit pour prouver quoi que ce soit |
| SC2 | drapeaux identiques, quatre tests intacts, prompt non imprimé | un drapeau perdu, un test touché, un prompt visible |
| SC3 | `iterate` réutilise, refuse proprement sans worktree, porte le sandbox, n'agit pas sans `--run` | crée un worktree, plante au lieu de refuser, perd le sandbox |
| SC4 | au moins deux tests vus rouges avant correction, sortie recopiée, suite verte | un test jamais rouge, ou une suite en échec |
| SC5 | zéro fichier hors périmètre, `harness/tests/` sans régression nouvelle | un fichier hors D4, ou une régression autre que les neuf échecs Unity connus |
| SC6 | README documente `iterate` et le pourquoi de stdin, instantané pré-édition committé et différent | documentation absente, ou couple `must_differ_from` non déclaré |

## Observations attendues, qui ne sont pas des motifs de rejet

- Les neuf échecs de `harness/tests/test_run_unity.py` sous WSL2 sont
  **préexistants** (binaire Unity absent, `powershell.exe` visible via `/mnt/c/`).
  L'Évaluateur doit vérifier qu'ils sont bien préexistants en rejouant la
  baseline, et ne pas les compter contre ce lot.
- Le choix d'implémentation pour transporter le prompt jusqu'à `run_command`
  (champ sur `Invocation`, couple retourné, autre) est libre : seul le résultat
  mesuré compte.
