# Le harnais

Ce qui reste après ADR-0018 : **la porte mécanique**, et rien d'autre.

Le harnais n'orchestre plus personne. Hermes écrit les briefs, Cursor les
exécute, le propriétaire fusionne. Le harnais sert à une seule chose :
refuser un compte-rendu qui se contredit lui-même.

## Contenu

| chemin | à quoi ça sert |
|---|---|
| `verdict_audit.py` | la porte mécanique — relit un dossier de brief et refuse un compte-rendu incohérent |
| `bare_python.py` | la reconnaissance des `python` nus, partagée avec le hook `.claude/hooks/no_bare_python.py` |
| `queue/briefs/` | les dossiers de brief, un par lot |
| `demo/fake_brief_001/` | la preuve que la porte refuse un faux compte-rendu — rejouée en CI |
| `demo/honest_brief_001/` | le contrôle inverse, un compte-rendu honnête accepté |
| `backends/run_cursor_generator.sh` | déléguer l'exécution d'un brief à Cursor CLI |
| `tests/` | les tests de la porte et des hooks |

## La règle de fond

**Celui qui produit ne prononce pas la recevabilité de son propre travail.**

C'est tout ce qui subsiste des trois rôles d'ADR-0001. Elle est tenue par
deux choses : la porte mécanique ci-dessus, et la relecture Cursor dans une
invocation neuve.

## Une seule source d'instruction

Exactement un document dit ce qu'un agent doit faire pour un lot : le
fichier `brief.md` du lot. Tout autre document peut y renvoyer ; aucun ne
peut le paraphraser. Vérifié par
`tests/test_single_source_of_instruction.py`.

## Commandes

```bash
python harness/verdict_audit.py <dossier_du_brief>   # la porte
python -m pytest harness/tests/ -v                    # les tests de la porte
python harness/demo/fake_brief_001/run_demo.py        # un faux compte-rendu est refusé
bash harness/backends/run_cursor_generator.sh <dossier_du_brief>
```

## Ce qui a été supprimé, et pourquoi

Le pipeline full-auto (jamais sorti du `mode: manual`), la machine d'états
d'audit, le budget d'exécution, le comptage de jetons, le bot de fusion,
l'aiguillage de risque et de tests, les trois agents Claude et le backend
Codex. Voir
[ADR-0018](../docs/adr/0018-degraissage-trois-acteurs-et-carte-figee.md).
