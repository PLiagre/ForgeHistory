using System.Collections.Generic;
using UnityEngine;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// v1_095 — RENDU DE LA CARTE PAR LE GPU.
    ///
    /// CE QUE CETTE CLASSE REMPLACE, ET POURQUOI : le rendu de carte était une
    /// rastérisation CPU (MapSnapshotExporter) qui repeignait chaque pixel à
    /// chaque changement de fenêtre. Un déplacement de souris coûtait donc une
    /// image entière recalculée sur le thread principal — d'où une carte qui ne
    /// glisse pas, et des paliers de zoom au lieu d'une caméra.
    ///
    /// ICI, LA CARTE EST UNE LECTURE DE TEXTURE :
    ///   — la géométrie ne bouge jamais (cell_ids_lodX.png, produit hors ligne) ;
    ///   — le zoom est une fenêtre UV passée au shader ;
    ///   — repeindre une conquête, c'est réécrire quelques octets de palette.
    ///
    /// CE QUE CETTE CLASSE NE FAIT PAS, ET C'EST VOULU : elle ne dessine ni
    /// étiquettes, ni villes, ni liserés de front. Ces couches restent CPU et se
    /// composent PAR-DESSUS. Les mélanger ici rendrait le fond dépendant de
    /// données qui changent au tick, et ferait perdre le seul avantage du GPU :
    /// un fond qu'on peut redessiner soixante fois par seconde sans rien recalculer.
    ///
    /// ISOLATION : lecture seule du monde. Cette classe ne reçoit que des vues
    /// déjà construites (MapSnapshotExporter.ProvinceView) — jamais un EntityManager.
    /// </summary>
    public static class MapGpuRenderer
    {
        const string ShaderResourcePath = "Shaders/MapPolitical";

        static Shader _shader;
        static Material _material;
        static Texture2D _palette;
        static Texture2D _owners;
        static RenderTexture _target;

        /// <summary>Dernier échec de préparation, pour que le diagnostic ne soit pas muet.</summary>
        public static string LastUnavailableReason { get; private set; } = "(jamais tenté)";

        /// <summary>Nombre de Blit effectués — sert de contrôle de vie dans les mesures.</summary>
        public static int BlitCount { get; private set; }

        /// <summary>Nombre de reconstructions de palette (≈ nombre de changements de monde).</summary>
        public static int PaletteRebuilds { get; private set; }

        /// <summary>Largeur de palette courante = nombre de cellules indexées.</summary>
        public static int PaletteWidth { get; private set; }

        // ---- Réglages d'apparence, dosables sans recompiler le shader ----

        /// <summary>Frontière d'état : épaisseur en texels de la texture d'identifiants.</summary>
        public static float CountryBorderTexels { get; set; } = 1.5f;

        /// <summary>Limite interne (cellule à cellule) : épaisseur en texels.</summary>
        public static float CellBorderTexels { get; set; } = 1f;

        public static Color CountryBorderColor { get; set; } = new Color(0.04f, 0.04f, 0.06f, 0.92f);

        public static Color CellBorderColor { get; set; } = new Color(0.20f, 0.22f, 0.26f, 0.40f);

        /// <summary>0 = carte plate, 1 = relief pleinement multiplié.</summary>
        public static float HillshadeStrength { get; set; } = 0.65f;

        public static bool IsAvailable => EnsureMaterial();

        static bool EnsureMaterial()
        {
            if (_material != null)
                return true;

            if (_shader == null)
                _shader = Resources.Load<Shader>(ShaderResourcePath);
            if (_shader == null)
                _shader = Shader.Find("Victoria/MapPolitical");

            if (_shader == null)
            {
                LastUnavailableReason =
                    "shader introuvable (Resources/" + ShaderResourcePath + ")";
                return false;
            }

            if (!_shader.isSupported)
            {
                LastUnavailableReason = "shader non supporté sur ce matériel";
                return false;
            }

            _material = new Material(_shader) { hideFlags = HideFlags.HideAndDontSave };
            LastUnavailableReason = "";
            return true;
        }

        /// <summary>
        /// Reconstruit les deux tables indexées par cellule : couleur de remplissage
        /// et identité du propriétaire. C'est le SEUL endroit où le monde joué entre
        /// dans le rendu GPU — et il n'y entre que sous forme de couleurs.
        ///
        /// L'index est <c>cell_id - IdBase</c>. Il est valide parce que les
        /// identifiants du pipeline sont contigus ; le contrôle est fait ici plutôt
        /// que supposé, et un trou fait échouer proprement au lieu de décaler
        /// silencieusement toute la carte d'une cellule.
        /// </summary>
        public static bool BuildPalette(
            List<MapSnapshotExporter.ProvinceView> views, out string error)
        {
            error = "";
            if (views == null || views.Count == 0)
            {
                error = "aucune vue";
                return false;
            }

            var idBase = PilotMapProvider.IdBase;
            var width = views.Count;

            if (_palette == null || _palette.width != width)
            {
                if (_palette != null)
                    Object.DestroyImmediate(_palette);
                _palette = NewPaletteTexture(width);
            }

            if (_owners == null || _owners.width != width)
            {
                if (_owners != null)
                    Object.DestroyImmediate(_owners);
                _owners = NewPaletteTexture(width);
            }

            var fills = new Color32[width];
            var owners = new Color32[width];
            var seen = new bool[width];

            for (var i = 0; i < views.Count; i++)
            {
                var v = views[i];
                var index = v.Id - idBase;
                if (index < 0 || index >= width)
                {
                    error = "identifiant hors table : cell " + v.Id +
                            " → index " + index + " (largeur " + width + ")";
                    return false;
                }

                if (seen[index])
                {
                    error = "index dupliqué : cell " + v.Id;
                    return false;
                }

                seen[index] = true;

                // Occupée : c'est le CONTRÔLEUR qu'on voit, pas le propriétaire.
                // Même choix qu'ApplyOccupationHatch côté CPU, en aplat.
                fills[index] = v.Occupied ? v.ControllerColor : v.Fill;
                owners[index] = EncodeOwner(v);
            }

            for (var i = 0; i < width; i++)
            {
                if (seen[i])
                    continue;
                error = "trou dans la table d'identifiants à l'index " + i;
                return false;
            }

            _palette.SetPixels32(fills);
            _palette.Apply(false, false);
            _owners.SetPixels32(owners);
            _owners.Apply(false, false);

            PaletteWidth = width;
            PaletteRebuilds++;
            return true;
        }

        static Texture2D NewPaletteTexture(int width)
        {
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false, true)
            {
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            return tex;
        }

        /// <summary>
        /// Identité du propriétaire encodée en couleur, pour que le shader puisse
        /// décider « frontière d'état » sans connaître un seul tag.
        /// Une cellule sans propriétaire reçoit une identité PROPRE à elle-même :
        /// sinon toutes les cellules sans maître seraient « du même pays » et leurs
        /// limites internes disparaîtraient.
        /// </summary>
        static Color32 EncodeOwner(MapSnapshotExporter.ProvinceView v)
        {
            var controllingTag = v.Occupied ? null : v.OwnerTag;
            if (v.Occupied)
            {
                // Sous occupation, le tracé qui compte est celui du contrôleur :
                // c'est ce que le joueur lit comme « ce qui est tenu par qui ».
                var h = v.ControllerColor;
                return new Color32(h.r, h.g, h.b, 255);
            }

            if (string.IsNullOrEmpty(controllingTag))
            {
                var id = v.Id;
                return new Color32(
                    (byte)(id & 0xFF), (byte)((id >> 8) & 0xFF), 0, 0);
            }

            var hash = 2166136261u;
            for (var i = 0; i < controllingTag.Length; i++)
            {
                hash ^= controllingTag[i];
                hash *= 16777619u;
            }

            return new Color32(
                (byte)(hash & 0xFF),
                (byte)((hash >> 8) & 0xFF),
                (byte)((hash >> 16) & 0xFF),
                255);
        }

        /// <summary>
        /// Dessine la carte dans une RenderTexture et la renvoie. La texture est
        /// réutilisée d'un appel à l'autre : c'est ce qui rend le déplacement gratuit.
        /// </summary>
        public static RenderTexture Render(
            int width, int height,
            float minX, float maxX, float minY, float maxY,
            int lod,
            Color sea,
            int hoverCellId,
            int selectedCellId)
        {
            if (!EnsureMaterial())
                return null;
            if (_palette == null || _owners == null)
            {
                LastUnavailableReason = "palette non construite";
                return null;
            }

            var ids = PilotMapProvider.IdsTextureFor(lod);
            var hillshade = PilotMapProvider.HillshadeTextureFor(lod);
            if (ids == null)
            {
                LastUnavailableReason = "texture d'identifiants absente (lod " + lod + ")";
                return null;
            }

            EnsureTarget(width, height);

            var uv = PilotMapProvider.WorldWindowToUv(minX, maxX, minY, maxY);

            _material.SetTexture("_CellIds", ids);
            _material.SetTexture("_Palette", _palette);
            _material.SetTexture("_Owners", _owners);
            _material.SetTexture("_Hillshade", hillshade != null ? hillshade : Texture2D.whiteTexture);
            _material.SetVector("_Window", uv);
            _material.SetVector(
                "_IdsTexelSize",
                new Vector4(1f / ids.width, 1f / ids.height, ids.width, ids.height));
            _material.SetFloat("_PaletteWidth", PaletteWidth);
            _material.SetFloat("_IdBase", PilotMapProvider.IdBase);
            _material.SetFloat("_SeaIdMin", PilotMapProvider.SeaIdMin);
            _material.SetColor("_SeaColor", sea);
            _material.SetColor("_BorderCountryColor", CountryBorderColor);
            _material.SetColor("_BorderCellColor", CellBorderColor);
            _material.SetVector(
                "_BorderWidth",
                new Vector4(CountryBorderTexels, CellBorderTexels, 0, 0));
            _material.SetFloat("_HillshadeStrength", HillshadeStrength);
            _material.SetFloat("_HoverId", hoverCellId);
            _material.SetFloat("_SelectedId", selectedCellId);

            var prev = RenderTexture.active;
            Graphics.Blit(null, _target, _material);
            RenderTexture.active = prev;
            BlitCount++;
            return _target;
        }

        static void EnsureTarget(int width, int height)
        {
            if (_target != null && _target.width == width && _target.height == height)
                return;
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
            }

            _target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGB32)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                useMipMap = false,
                hideFlags = HideFlags.HideAndDontSave
            };
            _target.Create();
        }

        /// <summary>
        /// Lecture CPU du dernier rendu — mesures et captures uniquement.
        ///
        /// CONVENTION : renvoie un buffer NORD EN RANGÉE 0, comme tous les autres
        /// buffers du dépôt (cf. MapSnapshotExporter.WriteMapBufferPng, dont le
        /// paramètre s'appelle « northAtRow0 »). Aucun retournement n'est fait ici :
        /// c'est le shader qui produit déjà cette orientation, via le pas en v
        /// négatif de PilotMapProvider.WorldWindowToUv. Retourner ici EN PLUS
        /// remettrait l'image tête-bêche.
        ///
        /// ⚠️ COÛTEUX (~18 ms en 960×720, mesuré) : ReadPixels est une synchronisation
        /// avec le GPU. Réservé aux mesures et aux captures. Le jeu, lui, présente
        /// la RenderTexture telle quelle — c'est tout l'intérêt.
        /// </summary>
        public static Color32[] ReadbackLastFrame(int width, int height)
        {
            if (_target == null)
                return null;
            var prev = RenderTexture.active;
            RenderTexture.active = _target;
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false, false);
            RenderTexture.active = prev;
            var pixels = tex.GetPixels32();
            Object.DestroyImmediate(tex);
            return pixels;
        }

        /// <summary>Libère tout — appelé par les tests entre deux mesures.</summary>
        public static void Release()
        {
            if (_target != null)
            {
                _target.Release();
                Object.DestroyImmediate(_target);
                _target = null;
            }

            if (_palette != null)
            {
                Object.DestroyImmediate(_palette);
                _palette = null;
            }

            if (_owners != null)
            {
                Object.DestroyImmediate(_owners);
                _owners = null;
            }

            if (_material != null)
            {
                Object.DestroyImmediate(_material);
                _material = null;
            }

            PaletteWidth = 0;
            BlitCount = 0;
            PaletteRebuilds = 0;
        }
    }
}
