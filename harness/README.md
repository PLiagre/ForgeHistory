# Le harnais facultatif

`harness/` conserve les briefs, leurs livrables et des vérificateurs
déterministes. Aucun de ces éléments n'est requis pour modifier le dépôt,
relire un changement, ouvrir une PR ou le livrer.

| chemin | contenu |
|---|---|
| `verdict_audit.py` | vérification informative de cohérence d'un dossier historique |
| `bare_python.py` | détection de l'alias `python` problématique sous Windows |
| `queue/briefs/` | descriptions de lots et livrables conservés |
| `demo/` | exemples honnête et incohérent du format historique |
| `backends/` | wrappers facultatifs d'outils externes |
| `tests/` | tests de ces utilitaires |

Le vérificateur contrôle uniquement des faits internes : fichiers déclarés,
échantillons non vides, nombres traçables, captures comparables et références
Git lisibles. L'identité des auteurs n'influence jamais son résultat.

```bash
python3 harness/verdict_audit.py <dossier_du_brief>
.venv/bin/python -m pytest harness/tests/ -q
```

La sortie du vérificateur est un diagnostic. Elle ne constitue pas un verdict
de recevabilité et n'est pas une porte de gouvernance.
