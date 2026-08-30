# Grille d'évaluation — Brief 047

**Authored**: 2026-08-30T13:40:00Z
**Author**: Claude

Cette grille est écrite avant tout livrable. Elle ne remplace pas le brief et
n'ajoute aucune instruction d'exécution.

| condition | preuve attendue | échec si |
|---|---|---|
| dépendance | lot 044 fusionné au démarrage, vérifié et consigné dans le `generator-log.md` ; aucun ordre imposé vis-à-vis de 046 | lot lancé avant 044 ; part non agricole fabriquée dans ce lot pour contourner le blocage ; lot séquencé derrière 046 |
| SC1 — la vue se recalcule | deux appels égaux, monde inchangé, empreinte `to_dict()` identique avant/après ; rouge cité (la vue n'existe pas sur la base) | vue qui mute une cellule, ajoute un attribut, ou dont deux appels diffèrent |
| SC2 — aucune seconde clé spatiale | parcours syntaxique, dénominateur = classes de données découvertes ; rouge prouvé sur une entité d'épreuve portant un champ interdit | contrôle qui ne peut pas rougir ; garde d'exécution de `model.py` étendue au lieu d'un contrôle dans les tests ; `bourg_id` ou équivalent sur une entité |
| SC3 — le tick ne consulte pas la vue | `engine.py` n'importe ni ne référence l'agrégation ; dénominateur dérivé du répertoire | vue lue par le tick, même indirectement ; la vue devient un acteur |
| SC4 — échantillon non vide | `cellules_avec_bourg` strictement positif, confronté au nombre de cellules à gisement dérivé de la carte | zéro rapporté comme un fait au lieu de faire échouer ; dépendance contournée |
| SC5 — ordre et unicité | ordre strict majeure > notable > mineure, classes dérivées de la carte ; une seule définition de la part non agricole et un seul jeu de facteurs de richesse dans `sim/` | formule de 044 recopiée dans l'agrégation ; classe manquante sautée au lieu de faire échouer ; second jeu de facteurs |
| SC6 — somme exacte | bourg + champs = population pour chaque cellule, sans tolérance ; écart nul mesuré | tolérance introduite ; habitant perdu ou inventé par l'arrondi ; report de fraction ajouté à une vue |
| SC7 — aucun nombre du monde ne change | sortie CLI identique octet pour octet à la base rejouée, empreintes SHA-256 comparées sur deux fichiers réellement produits | sortie modifiée ; identité affirmée sans comparaison ; clé de manifeste inverse inventée alors que la porte n'en a pas |
| SC8 — la suite reste verte | suite jouée deux fois, avant et après ; liste des échecs vide dans les deux cas, comparée et non supposée | suite déclarée verte sans être jouée ; échec introduit ; test existant modifié pour le faire disparaître |
| fidélité | niveau 2 déclaré pour la part du bourg ; limite « la campagne nourrit son bourg sans transport » consignée dans le `generator-log.md` | niveau absent ; limite présentée comme un mécanisme à écrire ; anomalie de niveau 2 traitée comme un défaut |
| absence de paramètre | aucune constante, aucun seuil, aucun réglage introduit par ce lot | seuil d'habitants qui « fait » un bourg ; constante de part urbaine ; nombre magique |
| périmètre | diff limité à `sim/aggregation.py` et à des ajouts dans `sim/tests/test_province.py` | modification de `engine.py`, `model.py`, `constants.py`, `world.py`, `snapshot_export.py`, `__main__.py`, d'un autre fichier de test, de la carte, du visualiseur, ou des briefs 044 et 046 |
| motif de la vue | enregistrement immuable hors de `sim.model`, indexé par `cell_id` seul, pur, ordre stable par `cell_id` croissant, garde `_NoBadSpatialField` héritée | vue déclarée dans `sim.model` ; second index ; ordre de sortie instable ; fonction impure |
| compteurs | manifeste et mesureur reconstruisent numérateurs et dénominateurs depuis les données et les exécutions | nombre écrit à la main ; dénominateur fixe ; zéro mesuré traité comme « non calculé » ; sentinelle prise pour une mesure |
| indépendance | compte-rendu final produit hors de l'invocation qui a écrit le code, et accepté par la porte mécanique | exécutant qui prononce sa propre recevabilité ou écrit le verdict |
