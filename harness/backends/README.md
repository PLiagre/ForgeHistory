# Backends facultatifs du harnais

Ce dossier contient des wrappers historiques ou pratiques pour appeler un outil
externe. Leur usage n'est jamais obligatoire et ne confère aucun rôle réservé
au fournisseur choisi.

Le wrapper Cursor accepte un dossier contenant `brief.md` et, s'il existe,
`eval-rubric.md`. Il demande à l'outil de réaliser la tâche et de consigner les
fichiers ou mesures utiles dans `deliverables/`. Le même contributeur peut
ensuite corriger, relire ou documenter le résultat.

```bash
bash harness/backends/run_cursor_generator.sh <dossier_du_brief>
```

Le wrapper exige seulement le binaire et l'authentification du service qu'il
appelle. Ce besoin technique ne s'applique pas au reste du dépôt : n'importe
quel autre outil ou travail direct peut être utilisé à la place.
