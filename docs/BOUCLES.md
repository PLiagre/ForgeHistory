# Deux boucles, une ligne de cron

`/etc/cron.d/forgeatelier` appartient à root. Tant qu'il portait les
treize réveils, leur environnement et `ATELIER_INVOQUER`, changer de
cadence — ou seulement désarmer — demandait le propriétaire et son mot
de passe. C'était une porte humaine sur un geste qui n'est pas une
décision.

Le crontab n'appelle plus qu'un répartiteur, et ne dit rien d'autre. Le
répartiteur lit le **profil actif** dans `~/.atelier/profil`, un fichier
que `hermes` écrit. Basculer devient l'écriture d'un fichier.

```
/etc/cron.d/forgeatelier   →  crons/repartiteur.sh   →  crons/profils/<actif>.sh
   (root, une fois)             (chaque minute)           (hermes, à volonté)
```

## Les deux profils

**`jour`** — la boucle qui produit des lots. Six rôles, treize réveils,
sur ForgeHistory. Ce sont les mêmes heures qu'avant, déplacées du
crontab vers `crons/profils/jour.sh`.

**`atelier`** — la boucle pour travailler *sur l'atelier lui-même*. Un
cycle de quatre minutes — pilote, coder, relire, briefer — sur un
produit d'épreuve monté par `crons/banc.sh`, jamais sur ForgeHistory.
Un tour complet se voit en minutes au lieu d'une journée.

Elle ne consomme aucun quota, et pas par convention : son `PATH`
commence par les faux agents du banc. Un vrai `agent` n'est pas *choisi
de ne pas être appelé*, il est **hors de portée**. C'est la seule
garantie qui tienne quand on relance cent fois.

Les faux agents obéissent à l'environnement : `FAUX_CODE` (le code de
sortie), `FAUX_DORT` (les secondes — c'est ainsi qu'on rejoue un délai
dépassé), `FAUX_PR`, `FAUX_SANS_PR`, `FAUX_SALIT`, `FAUX_COMMIT`.

## La commande

```bash
atelier-boucle jour      # la boucle qui produit des lots
atelier-boucle atelier   # la boucle courte, sur le produit d'épreuve
atelier-boucle arret     # plus aucun réveil ne démarre
atelier-boucle etat      # quel profil, depuis quand, et le prochain réveil
```

`arret` ne coupe pas un tour en cours. `crons/tour.sh` range sa carte
sur tous ses chemins de sortie ; le tuer serait exactement la façon de
laisser une carte prise et un verrou orphelin. L'arrêt est acquis dès
que le profil est posé — plus aucun réveil ne démarre — puis la commande
attend le tour en cours et dit ce qu'elle attend
(`ATELIER_ARRET_ATTENTE`, 60 s par défaut).

`etat` sait démentir « ça tourne » : un profil posé alors que le crontab
n'appelle pas le répartiteur ne réveille personne, et rien d'autre ne le
dirait.

## L'installation, une fois

Le seul geste qui demande root, et il ne se répète pas :

```bash
sudo cp /opt/ForgeAtelier/crons/crontab-repartiteur /etc/cron.d/forgeatelier
sudo chmod 644 /etc/cron.d/forgeatelier
```

Après quoi la bascule ne demande plus rien à personne.
