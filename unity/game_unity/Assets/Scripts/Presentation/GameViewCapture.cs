using Unity.Entities;
using System;
using System.Collections;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UIElements;

namespace VictoriaGame.Presentation
{
    /// <summary>
    /// Utilitaire de vérification / capture honnête.
    /// Acceptation visuelle ui_002 = framebuffer standalone (source=
    /// <see cref="SourceStandaloneFramebuffer"/>), jamais MapTexture seule.
    /// </summary>
    public static class GameViewCapture
    {
        public const int Width = 1920;
        public const int Height = 1080;
        public const string SourceStandaloneFramebuffer = "standalone framebuffer";
        public const string SourceStandalonePanelRt = "standalone panel RT";
        public const string SourcePanelTargetTexture = "UIDocument.PanelSettings.targetTexture";
        public const string SourceNone = "none";

        /// <summary>Résolution attendue pour la prochaine capture framebuffer (v1_055 multi-res).</summary>
        public static int ExpectedWidth = Width;
        public static int ExpectedHeight = Height;

        public static void SetExpectedResolution(int width, int height)
        {
            ExpectedWidth = width > 0 ? width : Width;
            ExpectedHeight = height > 0 ? height : Height;
        }

        public static void ResetExpectedResolution()
        {
            ExpectedWidth = Width;
            ExpectedHeight = Height;
        }
        /// <summary>
        /// Tente de lire le RT du panneau UI Toolkit. Échoue si non peint —
        /// aucun fallback MapTexture (ui_002).
        /// </summary>
        public static IEnumerator CapturePngCoroutine(string absolutePath, Action<CaptureResult> onDone)
        {
            var hud = InGameHud.Instance;
            var doc = hud != null ? hud.GetComponent<UIDocument>() : null;
            var panelSettings = doc != null ? doc.panelSettings : null;

            Texture2D tex = null;
            string source = null;
            string error = null;

            if (panelSettings != null && doc != null && doc.rootVisualElement != null)
            {
                var prevTarget = panelSettings.targetTexture;
                var prevScale = panelSettings.scaleMode;
                var prevRef = panelSettings.referenceResolution;

                var rt = new RenderTexture(ExpectedWidth, ExpectedHeight, 24, RenderTextureFormat.ARGB32)
                {
                    name = "UiHudCaptureRt",
                    antiAliasing = 1,
                    filterMode = FilterMode.Bilinear
                };
                rt.Create();

                panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
                panelSettings.referenceResolution = new Vector2Int(ExpectedWidth, ExpectedHeight);
                panelSettings.targetTexture = rt;
                doc.rootVisualElement.MarkDirtyRepaint();

                for (var i = 0; i < 5; i++)
                    yield return null;

                try
                {
                    tex = new Texture2D(ExpectedWidth, ExpectedHeight, TextureFormat.RGBA32, false)
                    {
                        filterMode = FilterMode.Point,
                        name = "UiHudCaptureTex"
                    };
                    RenderTexture.active = rt;
                    tex.ReadPixels(new Rect(0, 0, ExpectedWidth, ExpectedHeight), 0, 0);
                    tex.Apply(false, false);
                    RenderTexture.active = null;
                    source = SourcePanelTargetTexture;

                    if (IsNearlyUniform(tex))
                    {
                        UnityEngine.Object.Destroy(tex);
                        tex = null;
                        source = null;
                        error = "panel RT uniforme (UI Toolkit n'a pas peint — pas de fallback map-only)";
                    }
                    else if (!TextureHasHudChrome(tex) || !TextureHasMapContent(tex))
                    {
                        UnityEngine.Object.Destroy(tex);
                        tex = null;
                        source = null;
                        error = "panel RT sans chrome HUD + carte (refus map-only / UI absente)";
                    }
                }
                catch (Exception ex)
                {
                    error = "panel: " + ex.Message;
                    if (tex != null)
                    {
                        UnityEngine.Object.Destroy(tex);
                        tex = null;
                    }
                }
                finally
                {
                    panelSettings.targetTexture = prevTarget;
                    panelSettings.scaleMode = prevScale;
                    panelSettings.referenceResolution = prevRef;
                    rt.Release();
                    UnityEngine.Object.Destroy(rt);
                }
            }
            else
            {
                error = "UIDocument/PanelSettings indisponible";
            }

            if (tex == null)
            {
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = error ?? "HUD non rasterisé (aucun fallback MapTexture)",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            yield return WritePngAndFinish(tex, absolutePath, source, onDone);
        }

        /// <summary>
        /// Rasterise le UIDocument hors écran à ExactWidth×ExactHeight dans le player.
        /// Utilisé quand l'afficheur ne peut pas atteindre 1440p (v1_055) — toujours
        /// UI Toolkit réel peint dans le standalone, jamais MapTexture ni compositeur.
        /// </summary>
        public static IEnumerator CaptureStandalonePanelRtPngCoroutine(
            string absolutePath, Action<CaptureResult> onDone)
        {
            var hud = InGameHud.Instance;
            var doc = hud != null ? hud.GetComponent<UIDocument>() : null;
            var panelSettings = doc != null ? doc.panelSettings : null;
            if (panelSettings == null || doc == null || doc.rootVisualElement == null)
            {
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = "UIDocument/PanelSettings indisponible",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            var root = doc.rootVisualElement;
            var prevTarget = panelSettings.targetTexture;
            var prevScale = panelSettings.scaleMode;
            var prevRef = panelSettings.referenceResolution;
            var prevW = root.style.width;
            var prevH = root.style.height;

            var rt = new RenderTexture(ExpectedWidth, ExpectedHeight, 24, RenderTextureFormat.ARGB32)
            {
                name = "UiResponsiveCaptureRt",
                antiAliasing = 1,
                filterMode = FilterMode.Bilinear
            };
            rt.Create();

            panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
            panelSettings.referenceResolution = new Vector2Int(ExpectedWidth, ExpectedHeight);
            panelSettings.targetTexture = rt;
            root.style.width = ExpectedWidth;
            root.style.height = ExpectedHeight;
            hud.ApplyResponsiveClasses(ExpectedWidth, ExpectedHeight);
            root.MarkDirtyRepaint();

            for (var i = 0; i < 12; i++)
                yield return null;

            Texture2D tex = null;
            string error = null;
            try
            {
                tex = new Texture2D(ExpectedWidth, ExpectedHeight, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Point,
                    name = "UiResponsiveCaptureTex"
                };
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, ExpectedWidth, ExpectedHeight), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = null;

                if (IsNearlyUniform(tex))
                {
                    error = "panel RT uniforme (UI Toolkit n'a pas peint)";
                    UnityEngine.Object.Destroy(tex);
                    tex = null;
                }
                else if (!TextureHasHudChrome(tex) || !TextureHasMapContent(tex))
                {
                    error = "panel RT sans chrome HUD + carte";
                    try
                    {
                        var rejectPath = absolutePath + ".reject.png";
                        Directory.CreateDirectory(Path.GetDirectoryName(rejectPath) ?? ".");
                        File.WriteAllBytes(rejectPath, ImageConversion.EncodeToPNG(tex));
                    }
                    catch (Exception)
                    {
                        // ignore
                    }

                    UnityEngine.Object.Destroy(tex);
                    tex = null;
                }
            }
            catch (Exception ex)
            {
                error = "panel RT: " + ex.Message;
                if (tex != null)
                {
                    UnityEngine.Object.Destroy(tex);
                    tex = null;
                }
            }
            finally
            {
                panelSettings.targetTexture = prevTarget;
                panelSettings.scaleMode = prevScale;
                panelSettings.referenceResolution = prevRef;
                root.style.width = prevW;
                root.style.height = prevH;
                rt.Release();
                UnityEngine.Object.Destroy(rt);
            }

            if (tex == null)
            {
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = error ?? "panel RT capture failed",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            yield return WritePngAndFinish(tex, absolutePath, SourceStandalonePanelRt, onDone);
        }

        /// <summary>
        /// Capture le framebuffer réel (player standalone). Requiert WaitForEndOfFrame.
        /// </summary>
        public static IEnumerator CaptureFramebufferPngCoroutine(
            string absolutePath, Action<CaptureResult> onDone)
        {
            yield return new WaitForEndOfFrame();

            Texture2D tex = null;
            string error = null;
            try
            {
                tex = ScreenCapture.CaptureScreenshotAsTexture();
            }
            catch (Exception ex)
            {
                error = "ScreenCapture: " + ex.Message;
            }

            if (tex == null)
            {
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = error ?? "CaptureScreenshotAsTexture a renvoyé null",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            if (tex.width != ExpectedWidth || tex.height != ExpectedHeight)
            {
                var msg = $"résolution {tex.width}x{tex.height} ≠ {ExpectedWidth}x{ExpectedHeight}";
                UnityEngine.Object.Destroy(tex);
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = msg,
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            if (IsNearlyUniform(tex))
            {
                UnityEngine.Object.Destroy(tex);
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = "framebuffer uniforme",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone
                });
                yield break;
            }

            var hasHud = TextureHasHudChrome(tex);
            var hasMap = TextureHasMapContent(tex);
            if (!hasHud || !hasMap)
            {
                // Conserver l'image rejetée pour diagnostic (suffixe .reject.png).
                try
                {
                    var rejectPath = absolutePath + ".reject.png";
                    Directory.CreateDirectory(Path.GetDirectoryName(rejectPath) ?? ".");
                    File.WriteAllBytes(rejectPath, ImageConversion.EncodeToPNG(tex));
                }
                catch (Exception)
                {
                    // ignore diagnostic write
                }

                UnityEngine.Object.Destroy(tex);
                onDone?.Invoke(new CaptureResult
                {
                    Success = false,
                    Path = absolutePath,
                    Error = $"framebuffer sans chrome HUD+carte (hud={hasHud} map={hasMap})",
                    Width = Screen.width,
                    Height = Screen.height,
                    Source = SourceNone,
                    HasHudChrome = hasHud,
                    HasMapContent = hasMap
                });
                yield break;
            }

            yield return WritePngAndFinish(tex, absolutePath, SourceStandaloneFramebuffer, onDone);
        }

        static IEnumerator WritePngAndFinish(
            Texture2D tex, string absolutePath, string source, Action<CaptureResult> onDone)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath) ?? ".");
            var png = ImageConversion.EncodeToPNG(tex);
            File.WriteAllBytes(absolutePath, png);
            var sha = Sha256Hex(png);
            var w = tex.width;
            var h = tex.height;
            var hasHud = TextureHasHudChrome(tex);
            var hasMap = TextureHasMapContent(tex);
            UnityEngine.Object.Destroy(tex);

            onDone?.Invoke(new CaptureResult
            {
                Success = true,
                Path = absolutePath,
                Sha256 = sha,
                ByteLength = png.Length,
                Width = w,
                Height = h,
                ScreenWidth = Screen.width,
                ScreenHeight = Screen.height,
                Source = source ?? "unknown",
                HasHudChrome = hasHud,
                HasMapContent = hasMap
            });
            yield break;
        }

        public static bool IsNearlyUniform(Texture2D tex)
        {
            if (tex == null) return true;
            return PixelsAreNearlyUniform(tex.GetPixels32());
        }

        public static bool PixelsAreNearlyUniform(Color32[] pixels)
        {
            if (pixels == null || pixels.Length == 0)
                return true;
            var c0 = pixels[pixels.Length / 2];
            var step = Math.Max(1, pixels.Length / 128);
            var same = 0;
            var n = 0;
            for (var i = 0; i < pixels.Length; i += step)
            {
                n++;
                var c = pixels[i];
                if (Math.Abs(c.r - c0.r) < 4 && Math.Abs(c.g - c0.g) < 4 && Math.Abs(c.b - c0.b) < 4)
                    same++;
            }

            return n > 0 && same * 100 / n > 98;
        }

        /// <summary>
        /// Chrome HUD DA : bandeau fer (#211F1B) / laiton / parchemin en haut d'écran.
        /// Absent des captures map-only ui_001.
        /// </summary>
        public static bool TextureHasHudChrome(Texture2D tex)
        {
            if (tex == null) return false;
            return PixelsHaveHudChrome(tex.GetPixels32(), tex.width, tex.height);
        }

        public static bool PixelsHaveHudChrome(Color32[] pixels, int width, int height)
        {
            if (pixels == null || width < 64 || height < 64)
                return false;

            // GetPixels32 est bottom-up : y=height-1 = haut visuel (bandeau HUD).
            var barH = Math.Min(80, height / 8);
            var hits = 0;
            var samples = 0;
            var stepX = Math.Max(1, width / 64);
            var stepY = Math.Max(1, barH / 8);
            for (var dy = 2; dy < barH; dy += stepY)
            {
                var y = height - 1 - dy;
                for (var x = 8; x < width - 8; x += stepX)
                {
                    samples++;
                    if (IsHudChromeColor(pixels[y * width + x]))
                        hits++;
                }
            }

            return samples > 0 && hits * 100 / samples >= 12;
        }

        /// <summary>Zone carte (hors bandeau) avec diversité de couleurs politiques.</summary>
        public static bool TextureHasMapContent(Texture2D tex)
        {
            if (tex == null) return false;
            return PixelsHaveMapContent(tex.GetPixels32(), tex.width, tex.height);
        }

        public static bool PixelsHaveMapContent(Color32[] pixels, int width, int height)
        {
            if (pixels == null || width < 64 || height < 64)
                return false;

            // Bottom-up : exclure bandeau haut (array y élevés) et barre bas (array y bas).
            var yTopExclude = Math.Min(100, height / 8);
            var yBotExclude = Math.Min(80, height / 10);
            var y0 = yBotExclude;
            var y1 = height - yTopExclude;
            if (y1 <= y0 + 8)
                return false;

            var buckets = new int[64];
            var stepX = Math.Max(1, width / 48);
            var stepY = Math.Max(1, (y1 - y0) / 24);
            var n = 0;
            for (var y = y0; y < y1; y += stepY)
            {
                for (var x = 40; x < width - 40; x += stepX)
                {
                    var c = pixels[y * width + x];
                    var key = ((c.r >> 6) << 4) | ((c.g >> 6) << 2) | (c.b >> 6);
                    buckets[key & 63]++;
                    n++;
                }
            }

            if (n < 16)
                return false;

            var distinct = 0;
            for (var i = 0; i < buckets.Length; i++)
            {
                if (buckets[i] > 0)
                    distinct++;
            }

            return distinct >= 6;
        }

        static bool IsHudChromeColor(Color32 c)
        {
            if (c.a < 180)
                return false;

            // --hud-iron rgb(33,31,27) / bar-bg
            if (InRange(c.r, 22, 55) && InRange(c.g, 20, 52) && InRange(c.b, 16, 48))
                return true;
            // --hud-iron-raised rgb(48,44,38)
            if (InRange(c.r, 40, 70) && InRange(c.g, 36, 64) && InRange(c.b, 30, 58))
                return true;
            // --hud-brass rgb(154,122,67)
            if (InRange(c.r, 130, 180) && InRange(c.g, 95, 145) && InRange(c.b, 45, 95))
                return true;
            // --hud-parchment / bone text-ish
            if (InRange(c.r, 175, 235) && InRange(c.g, 160, 225) && InRange(c.b, 120, 205))
                return true;
            // --hud-blood (pause badge)
            if (InRange(c.r, 85, 130) && InRange(c.g, 30, 70) && InRange(c.b, 28, 68))
                return true;

            return false;
        }

        static bool InRange(byte v, int lo, int hi) => v >= lo && v <= hi;

        public static string Sha256Hex(byte[] data)
        {
            using var sha = SHA256.Create();
            var hash = sha.ComputeHash(data);
            return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        }

        public static bool TryReadPngValidation(
            string absolutePath, out int width, out int height, out string sha256,
            out long byteLength, out bool uniform, out bool hasHud, out bool hasMap)
        {
            width = height = 0;
            sha256 = "";
            byteLength = 0;
            uniform = true;
            hasHud = hasMap = false;
            if (!File.Exists(absolutePath))
                return false;

            var bytes = File.ReadAllBytes(absolutePath);
            byteLength = bytes.LongLength;
            sha256 = Sha256Hex(bytes);
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!ImageConversion.LoadImage(tex, bytes, false))
            {
                UnityEngine.Object.Destroy(tex);
                return false;
            }

            width = tex.width;
            height = tex.height;
            uniform = IsNearlyUniform(tex);
            hasHud = TextureHasHudChrome(tex);
            hasMap = TextureHasMapContent(tex);
            UnityEngine.Object.Destroy(tex);
            return true;
        }

        public struct CaptureResult
        {
            public bool Success;
            public string Path;
            public string Sha256;
            public string Error;
            public string Source;
            public int ByteLength;
            public int Width;
            public int Height;
            public int ScreenWidth;
            public int ScreenHeight;
            public bool HasHudChrome;
            public bool HasMapContent;
        }

        public static bool TextureHasDiagnosticPanelBg(Texture2D tex)
        {
            if (tex == null) return false;
            return PixelsHaveDiagnosticPanelBg(tex.GetPixels32(), tex.width, tex.height);
        }

        public static bool PixelsHaveDiagnosticPanelBg(Color32[] pixels, int width, int height)
        {
            if (pixels == null || width < 8 || height < 8)
                return false;
            var hits = 0;
            var samples = 0;
            for (var dy = 0; dy < 6; dy++)
            {
                var y = height - 2 - dy;
                if (y < 0) break;
                for (var x = 1; x < 6; x++)
                {
                    samples++;
                    if (IsDiagnosticPanelColor(pixels[y * width + x]))
                        hits++;
                }
            }

            return samples > 0 && hits >= samples * 3 / 4;
        }

        static bool IsDiagnosticPanelColor(Color32 c)
        {
            if (c.a <= 200)
                return false;
            return c.r >= 0x0E && c.r <= 0x16 &&
                   c.g >= 0x10 && c.g <= 0x18 &&
                   c.b >= 0x14 && c.b <= 0x1C;
        }
    }
}
