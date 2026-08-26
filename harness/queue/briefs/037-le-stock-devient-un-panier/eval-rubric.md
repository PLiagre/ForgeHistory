# Grille d'évaluation — Brief 037

**Authored**: 2026-08-26T09:30:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| SC1 — le champ a disparu | contrôle sur l'arbre syntaxique des modules de `sim/` hors tests, nombre de modules dérivé ; rouge cité sur le SHA de base | parcours vide accepté, rouge non prouvé, champ conservé « pour compatibilité » |
| SC2 — identité au bit près | trois références archivées avant édition et rejouées : CLI 20 ticks, CLI 365 ticks, snapshot ; comparaison champ par champ | une seule différence, comparaison contre une copie fabriquée après coup, version de schéma modifiée |
| SC3 — sentinelle | marchandise absente à `-1.0`, marchandise à zéro à `0.0`, les deux distinguées | absence traduite en zéro, zéro traité comme « non calculé » |
| SC4 — accès nommés | aucun module hors `sim/model.py` n'indexe le panier directement | indexation directe, accès dupliqué, deuxième chemin d'écriture |
| SC5 — deuxième marchandise | écriture puis relecture d'une marchandise inconnue, sans effet sur la nourriture et sans code nouveau ; `to_dict` porte le panier | panier codé pour une seule marchandise, collision entre deux marchandises, `to_dict` gelé aux anciennes clés |
| SC6 — invariants | suites `sim/` et `viewer/` vertes ; couverture d'écriture verte ; tests collectés non réduits ; aucun `global` | contrôle supprimé au passage, champ sans lecteur, littéral numérique dans une fonction |
| substitution | règle de substitution appliquée ligne à ligne au diff des tests, compte de violations nul | une valeur attendue, un seuil, un nom de test ou une assertion modifiés |
| périmètre | diff limité aux modules de `sim/` nommés et à la substitution dans les cinq fichiers de test | modification de `aggregation.py`, de `viewer/`, de la carte ou de l'outil de carte |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main, dénominateur fixe, sentinelle prise pour une mesure, référence avant nulle |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
