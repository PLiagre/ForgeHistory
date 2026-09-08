"""Le compte rendu : où en est le travail, en une page.

Il ne calcule aucun état. Les états viennent du registre, les raisons de
blocage viennent de la décision de l'intégration — la même fonction que
celle qui fusionne. Une page qui recalculerait pour son affichage
finirait par montrer autre chose que ce que la machine fait, et c'est
exactement le mode de défaillance n°4 du dépôt : la présentation qui
réimplémente la simulation.

Ce module rend du texte. Il ne lit ni GitHub, ni le disque.
"""

from __future__ import annotations

from dataclasses import dataclass
from html import escape

from . import palier

# Ce qu'une couche est, pour l'œil. Les noms vivent dans VISION.md ; ils
# sont repris ici parce qu'une page qui dirait « couche 4 » sans dire
# « armées » demanderait au lecteur d'aller chercher ailleurs. Si les deux
# divergent un jour, VISION.md a raison.
NOMS = {
    "1": "Monde vivant",
    "2": "Villes",
    "3": "États",
    "4": "Armées",
    "5": "Batailles tactiques",
}

COULEURS = {
    "livre": "#2f7d32",
    "archive": "#6b7280",
    "pret": "#1d63b8",
    "a-briefer": "#a8620a",
    "idee": "#7a7a7a",
    "abandonne": "#8a2b2b",
}


@dataclass(frozen=True)
class LignePR:
    """Une PR ouverte, et ce que l'intégration en dit."""

    numero: int
    branche: str
    action: str
    raison: str


def _puce(etat: str) -> str:
    couleur = COULEURS.get(etat, "#7a7a7a")
    return f'<span class="etat" style="--c:{couleur}">{escape(etat)}</span>'


def _fiche(fiche) -> str:
    deps = ", ".join(fiche.depend_de) or "—"
    prs = ", ".join(str(p) for p in fiche.prs) or "—"
    return (
        f'<tr><td class="num">{escape(fiche.numero)}</td>'
        f"<td>{escape(fiche.titre)}</td>"
        f"<td>{_puce(fiche.etat)}</td>"
        f'<td class="fin">{escape(deps)}</td>'
        f'<td class="fin">{escape(prs)}</td></tr>'
    )


def _couche(etape, fiches) -> str:
    nom = NOMS.get(etape.couche, "")
    de_la_couche = [f for f in fiches if f.couche == etape.couche]
    finis = [f for f in de_la_couche if f.etat in palier.FINIS]
    part = round(100 * len(finis) / len(de_la_couche)) if de_la_couche else 0
    if etape.due:
        mot, ton = "palier dû", "du"
    elif etape.finie:
        mot, ton = "finie", "ok"
    else:
        mot, ton = f"{len(etape.en_cours)} lot(s) en cours", "encours"
    lignes = "".join(_fiche(f) for f in de_la_couche)
    return f"""<section class="couche">
  <h3>Couche {escape(etape.couche)} — {escape(nom)} <span class="badge {ton}">{escape(mot)}</span></h3>
  <div class="jauge"><i style="width:{part}%"></i></div>
  <p class="sous">{len(finis)} lot(s) sur {len(de_la_couche)} ne demandent plus rien.</p>
  <table>{lignes}</table>
</section>"""


def _pr(ligne: LignePR) -> str:
    ton = {"fusionner": "ok", "rebaser": "encours"}.get(ligne.action, "du")
    return (
        f'<tr><td class="num">#{ligne.numero}</td>'
        f"<td><code>{escape(ligne.branche)}</code></td>"
        f'<td><span class="badge {ton}">{escape(ligne.action)}</span></td>'
        f"<td>{escape(ligne.raison)}</td></tr>"
    )


def rendre(fiches, lignes_pr, moment: str, depot: str = "") -> str:
    """La page entière. Un registre vide est une erreur, pas une page vide."""
    if not fiches:
        raise ValueError("registre sans fiche : il n'y a rien à montrer")
    etapes = palier.etapes(fiches)
    hors_couche = [f for f in fiches if f.couche is None]
    corps = "".join(_couche(e, fiches) for e in etapes)
    if hors_couche:
        corps += (
            '<section class="couche"><h3>Hors couche</h3><table>'
            + "".join(_fiche(f) for f in hors_couche)
            + "</table></section>"
        )
    if lignes_pr:
        travaux = "".join(_pr(l) for l in lignes_pr)
        attente = (
            '<section class="couche"><h3>Ce qui attend d\'entrer</h3>'
            f"<table>{travaux}</table></section>"
        )
    else:
        attente = (
            '<section class="couche"><h3>Ce qui attend d\'entrer</h3>'
            "<p class=\"sous\">Aucune proposition ouverte.</p></section>"
        )
    lien = (
        f'<a href="https://github.com/{escape(depot)}/issues/new?template=nouveau-lot.yml">'
        "Demander un nouveau lot</a>"
        if depot
        else ""
    )
    return f"""<!doctype html>
<html lang="fr"><head>
<meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>ForgeHistory — où en est le travail</title>
<style>
:root {{ color-scheme: light dark;
  --fond:#fbfaf8; --encre:#1b1a17; --gris:#6b6862; --trait:#e2ded6; --carte:#fff; }}
@media (prefers-color-scheme:dark) {{ :root {{
  --fond:#161513; --encre:#eceae6; --gris:#9b968d; --trait:#2c2a26; --carte:#1e1d1a; }} }}
* {{ box-sizing:border-box }}
body {{ margin:0; background:var(--fond); color:var(--encre);
  font:15px/1.55 ui-sans-serif,system-ui,-apple-system,"Segoe UI",sans-serif; }}
main {{ max-width:56rem; margin:0 auto; padding:2.5rem 1.25rem 4rem }}
h1 {{ font-size:1.55rem; margin:0 0 .2rem; letter-spacing:-.01em }}
h3 {{ font-size:1.02rem; margin:0 0 .6rem; display:flex; align-items:center; gap:.6rem }}
.chapeau {{ color:var(--gris); margin:0 0 2rem }}
.chapeau a {{ color:inherit }}
.couche {{ background:var(--carte); border:1px solid var(--trait);
  border-radius:10px; padding:1.1rem 1.2rem; margin-bottom:1rem }}
.sous {{ color:var(--gris); font-size:.87rem; margin:.5rem 0 .8rem }}
.jauge {{ height:5px; background:var(--trait); border-radius:3px; overflow:hidden }}
.jauge i {{ display:block; height:100%; background:#2f7d32 }}
table {{ width:100%; border-collapse:collapse; font-size:.9rem }}
td {{ padding:.34rem .5rem; border-top:1px solid var(--trait); vertical-align:top }}
tr:first-child td {{ border-top:0 }}
.num {{ font-variant-numeric:tabular-nums; color:var(--gris); white-space:nowrap; width:3.4rem }}
.fin {{ color:var(--gris); white-space:nowrap; text-align:right }}
code {{ font-size:.85em }}
.etat {{ color:var(--c); font-weight:600; white-space:nowrap }}
.badge {{ font-size:.74rem; font-weight:600; padding:.1rem .5rem; border-radius:99px;
  border:1px solid currentColor; white-space:nowrap }}
.badge.ok {{ color:#2f7d32 }} .badge.encours {{ color:#a8620a }} .badge.du {{ color:#8a2b2b }}
footer {{ color:var(--gris); font-size:.82rem; margin-top:2rem }}
</style></head><body><main>
<h1>Où en est le travail</h1>
<p class="chapeau">Cette page est réécrite à chaque tour de l'intégration. Elle ne
décide rien : elle montre le registre des lots et ce que l'intégration dit de
chaque proposition ouverte. {lien}</p>
{corps}
{attente}
<footer>Écrite le {escape(moment)}. Si elle contredit le registre, c'est le registre qui a raison.</footer>
</main></body></html>"""
