// v1_095 — Carte politique rendue par le GPU.
//
// POURQUOI CE FICHIER EXISTE : jusqu'ici la carte était rastérisée pixel par
// pixel sur le CPU (MapSnapshotExporter, ~4200 lignes), puis poussée dans un
// Texture2D. Conséquence directe : chaque déplacement ou zoom coûtait une
// reconstruction complète, donc la carte ne pouvait pas glisser sous la souris.
//
// ICI, TOUT EST UNE LECTURE DE TEXTURE :
//   _CellIds   texture d'identifiants, id = R + (G << 8), 0 = vide.
//              Produite hors ligne par sandbox/geo/ — Unity ne calcule
//              AUCUNE géométrie, il lit un PNG commité.
//   _Palette   table cellule → couleur. UNE ligne de pixels, indexée par
//              (id - _IdBase). C'est le seul objet qui change quand une
//              province est conquise : repeindre la carte = réécrire N octets.
//   _Owners    même indexation, couleur = identifiant de propriétaire encodé.
//              Sert à distinguer frontière D'ÉTAT (épaisse) de limite interne
//              (fine) sans qu'aucune frontière ne soit une donnée stockée.
//   _Hillshade ombrage du relief, appliqué en multiplicatif sur la terre.
//
// LE ZOOM EST UNE FENÊTRE UV (_Window), pas une reconstruction : déplacer la
// carte revient à changer quatre flottants.
Shader "Victoria/MapPolitical"
{
    Properties
    {
        _CellIds ("Cell ids (R+G)", 2D) = "black" {}
        _Palette ("Palette cellule→couleur", 2D) = "white" {}
        _Owners ("Palette cellule→propriétaire", 2D) = "black" {}
        _Hillshade ("Ombrage relief", 2D) = "white" {}
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _CellIds;
            sampler2D _Palette;
            sampler2D _Owners;
            sampler2D _Hillshade;

            // xy = coin bas-gauche de la fenêtre en UV, zw = taille de la fenêtre.
            float4 _Window;
            // xy = taille en texels de _CellIds (pour le pas de voisinage).
            float4 _IdsTexelSize;
            // Nombre de cellules indexées dans _Palette / _Owners.
            float _PaletteWidth;
            // Premier identifiant de cellule terrestre (1164 dans le pilote).
            float _IdBase;
            // Identifiant au-delà duquel on est en mer (5000 dans le pilote).
            float _SeaIdMin;

            float4 _SeaColor;
            float4 _BorderCountryColor;
            float4 _BorderCellColor;
            // x = épaisseur frontière d'état (texels), y = épaisseur limite interne.
            float4 _BorderWidth;
            // Force de l'ombrage : 0 = plat, 1 = plein relief.
            float _HillshadeStrength;

            // Survol et sélection, en identifiants de cellule (-1 = aucun).
            float _HoverId;
            float _SelectedId;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata_img v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.texcoord;
                return o;
            }

            // Décodage exact de la convention du pipeline : id = R + (G << 8).
            // Les canaux arrivent en [0,1] : on repasse en octets avant de composer.
            float DecodeId(float2 uv)
            {
                float4 c = tex2D(_CellIds, uv);
                float r = floor(c.r * 255.0 + 0.5);
                float g = floor(c.g * 255.0 + 0.5);
                return r + g * 256.0;
            }

            // Couleur d'une cellule, lue dans la palette. Hors table ⇒ mer.
            float4 PaletteAt(float id)
            {
                float index = id - _IdBase;
                if (index < 0.0 || index >= _PaletteWidth)
                    return _SeaColor;
                float u = (index + 0.5) / _PaletteWidth;
                return tex2D(_Palette, float2(u, 0.5));
            }

            float4 OwnerAt(float id)
            {
                float index = id - _IdBase;
                if (index < 0.0 || index >= _PaletteWidth)
                    return float4(0, 0, 0, 0);
                float u = (index + 0.5) / _PaletteWidth;
                return tex2D(_Owners, float2(u, 0.5));
            }

            bool SameOwner(float4 a, float4 b)
            {
                // Comparaison en octets : deux propriétaires distincts diffèrent
                // d'au moins 1/255, très au-dessus du seuil.
                return all(abs(a - b) < 0.002);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 uv = _Window.xy + i.uv * _Window.zw;

                float id = DecodeId(uv);

                // Mer, lac, ou pixel hors de toute cellule.
                if (id < 0.5 || id >= _SeaIdMin)
                    return _SeaColor;

                float4 col = PaletteAt(id);

                // Relief : multiplicatif, uniquement sur la terre, et dosable.
                float shade = tex2D(_Hillshade, uv).r;
                float k = lerp(1.0, shade * 2.0, _HillshadeStrength);
                col.rgb *= saturate(k);

                // ---- Frontières dérivées, jamais stockées ----
                // On regarde les quatre voisins à l'épaisseur demandée. Un voisin
                // d'identifiant différent fait une limite ; un voisin de
                // PROPRIÉTAIRE différent fait une frontière d'état.
                float2 tsCountry = _IdsTexelSize.xy * _BorderWidth.x;
                float2 tsCell = _IdsTexelSize.xy * _BorderWidth.y;

                float4 myOwner = OwnerAt(id);

                float idL = DecodeId(uv - float2(tsCountry.x, 0));
                float idR = DecodeId(uv + float2(tsCountry.x, 0));
                float idD = DecodeId(uv - float2(0, tsCountry.y));
                float idU = DecodeId(uv + float2(0, tsCountry.y));

                bool countryEdge =
                    !SameOwner(myOwner, OwnerAt(idL)) ||
                    !SameOwner(myOwner, OwnerAt(idR)) ||
                    !SameOwner(myOwner, OwnerAt(idD)) ||
                    !SameOwner(myOwner, OwnerAt(idU));

                float2 c2 = tsCell;
                bool cellEdge =
                    DecodeId(uv - float2(c2.x, 0)) != id ||
                    DecodeId(uv + float2(c2.x, 0)) != id ||
                    DecodeId(uv - float2(0, c2.y)) != id ||
                    DecodeId(uv + float2(0, c2.y)) != id;

                // Ordre volontaire : la frontière d'état l'emporte sur la limite
                // interne. Sans cet ordre, une limite fine mangerait un tracé
                // d'État là où les deux coïncident — c'est-à-dire partout.
                if (countryEdge)
                    col.rgb = lerp(col.rgb, _BorderCountryColor.rgb, _BorderCountryColor.a);
                else if (cellEdge)
                    col.rgb = lerp(col.rgb, _BorderCellColor.rgb, _BorderCellColor.a);

                // ---- Survol et sélection ----
                if (_SelectedId >= 0.0 && abs(id - _SelectedId) < 0.5)
                    col.rgb = saturate(col.rgb * 1.35 + 0.10);
                else if (_HoverId >= 0.0 && abs(id - _HoverId) < 0.5)
                    col.rgb = saturate(col.rgb * 1.18 + 0.04);

                col.a = 1.0;
                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}
