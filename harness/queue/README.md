# La file des briefs

Un dossier par lot : `briefs/NNN-<slug>/`, contenant au moins `brief.md`.

**Hermes écrit les briefs** (ADR-0018). Cursor les exécute. Le `brief.md`
est la seule source d'instruction du lot : aucun autre document ne le
paraphrase.

La file est vide : les 33 briefs terminés (001 à 032) ont été archivés avec
le dégraissage. Pour en relire un :

```bash
git show archive/2026-08:harness/queue/briefs/011-sim-monde-vivant-amorcage/brief.md
git checkout archive/2026-08 -- harness/queue/briefs/   # les récupérer tous
```

Les deux briefs de démonstration restent en place, dans `harness/demo/` :
un faux compte-rendu que la porte refuse, un honnête qu'elle accepte.
