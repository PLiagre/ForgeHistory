# La file des briefs

Un dossier par lot : `briefs/NNN-<slug>/`, contenant au moins `brief.md`.

Le propriétaire fournit les briefs ; il peut les écrire avec Claude utilisé
manuellement (ADR-0021). Hermes ne lance jamais Claude. Il fait relire les
briefs reçus et les lance ; Cursor les exécute. Le `brief.md` est
la seule source d'instruction du lot : aucun autre document ne le
paraphrase, et son auteur n'est jamais son relecteur.

Les 33 briefs terminés (001 à 032) ont été archivés avec le dégraissage, et
le 033 est fusionné. Pour en relire un :

```bash
git show da1596d:harness/queue/briefs/011-sim-monde-vivant-amorcage/brief.md
git checkout da1596d -- harness/queue/briefs/   # les récupérer tous
git ls-tree --name-only da1596d:harness/queue/briefs   # voir les 33 noms
```

`da1596d` est le commit du lot D. Le tag `archive/2026-08` n'existe pas sur
`origin` — voir « Les archives » dans `AGENTS.md`.

Les deux briefs de démonstration restent en place, dans `harness/demo/` :
un faux compte-rendu que la porte refuse, un honnête qu'elle accepte.
