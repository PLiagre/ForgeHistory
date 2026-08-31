---
name: forgehistory-suivi
description: >
  Aide facultative pour mesurer l'état de ForgeHistory, choisir une tâche,
  modifier le dépôt et vérifier le résultat.
---

# Suivi de ForgeHistory

Cette aide applique `AGENTS.md`. Elle n'attribue aucun rôle et n'impose aucun
outil, modèle, fournisseur, relecteur ou propriétaire de document.

## Démarrage

1. Lire `git status --short --branch` et le dernier commit.
2. Lire `ROADMAP.md` et les fichiers directement concernés par la tâche.
3. Lancer `python3 -m sim --ticks 0 --json` si le moteur est concerné.
4. Choisir une tâche dans la roadmap ou un brief existant.

## Réalisation

Le même contributeur ou agent peut planifier, écrire, tester, documenter et
relire. Modifier tous les fichiers nécessaires, sans toucher aux changements
locaux sans rapport. Les outils ForgePilot, les sous-agents et la veille locale
peuvent être utilisés s'ils apportent une aide concrète ; ils restent
facultatifs.

## Vérification

Lancer les tests pertinents, examiner le diff et vérifier que les fichiers
fonctionnels hors périmètre sont inchangés. Ne jamais affaiblir un test métier
pour obtenir du vert. Mettre à jour `ROADMAP.md`, `sim/MODELE.md` ou toute autre
documentation factuelle lorsqu'elle est réellement devenue fausse.

## Livraison

Rendre compte des fichiers modifiés, des comportements conservés, des tests
exécutés et des limites. Ouvrir une PR ou livrer directement selon le contexte
et les droits disponibles.
