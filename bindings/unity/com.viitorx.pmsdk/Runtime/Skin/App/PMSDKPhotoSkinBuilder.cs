using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>How a photo part becomes a skin.</summary>
    public enum PMSDKPhotoSkinMode
    {
        /// <summary>Rectangle skin (corner pin) cut by the photo silhouette: pixel-accurate, keeps holes
        /// and thin features. The most accurate option.</summary>
        QuadWithMask = 0,
        /// <summary>Polygon skin from the outline; every hole gets a "hole" skin that shows the output
        /// background colour on top, so the hole looks like the wall around it.</summary>
        PolygonFillHoles = 1,
        /// <summary>Polygon skin from the outline only (holes are covered by the shape).</summary>
        PolygonOutline = 2,
    }

    /// <summary>
    /// Turns the parts extracted from a photo into skins in the Skin Editor. Pure Unity-side
    /// placement and asset writing: the photo is fitted into the projector raster (aspect kept) and
    /// can then be fine-tuned with the normal corner handles.
    /// </summary>
    public static class PMSDKPhotoSkinBuilder
    {
        /// <summary>Fraction of the raster the photo fills when placed.</summary>
        public const float DefaultFill = 0.9f;

        /// <summary>Raster aspect (the skin rig is 16:9, see PMSDKSkin).</summary>
        public const float RasterAspect = 16f / 9f;

        /// <summary>Map a normalised photo point (y up, 0..1) into raster space: aspect preserved,
        /// centred, filling <paramref name="fill"/> of the raster in the limiting direction.</summary>
        public static Vector2 PlaceInRaster(Vector2 photoPoint, int imageWidth, int imageHeight, float fill)
        {
            float photoAspect = (float)imageWidth / Mathf.Max(1, imageHeight);
            float width;
            float height;
            if (photoAspect >= RasterAspect) {
                width = fill;
                height = fill * RasterAspect / photoAspect;
            } else {
                height = fill;
                width = fill * photoAspect / RasterAspect;
            }
            return new Vector2(0.5f + (photoPoint.x - 0.5f) * width, 0.5f + (photoPoint.y - 0.5f) * height);
        }

        /// <summary>Create the skins for every part. Returns them in part order (largest first); the
        /// first one ends up selected. The whole operation is one undo step.</summary>
        public static List<PMSDKSkin> CreateSkins(PMSDKSkinEditorController controller, PMSDKPhotoExtraction extraction,
            PMSDKPhotoSkinMode mode, string baseName)
        {
            List<PMSDKSkin> created = new List<PMSDKSkin>();
            if (controller == null || extraction == null) {
                return created;
            }
            controller.Undo.Record(controller, "photo-skins");
            string label = string.IsNullOrWhiteSpace(baseName) ? "Photo" : baseName;

            for (int i = 0; i < extraction.Parts.Count; i++) {
                PMSDKPhotoPart part = extraction.Parts[i];
                string partName = extraction.Parts.Count > 1 ? label + " " + (i + 1) : label;
                PMSDKSkin skin;
                if (mode == PMSDKPhotoSkinMode.QuadWithMask) {
                    skin = CreateMaskSkin(controller, extraction, part, partName);
                } else {
                    skin = CreatePolygonSkin(controller, extraction, part, partName);
                }
                if (skin == null) {
                    continue;
                }
                skin.ZOrder = i * 2;
                created.Add(skin);

                if (mode == PMSDKPhotoSkinMode.PolygonFillHoles) {
                    CreateHoleSkins(controller, extraction, part, skin, partName);
                }
            }

            controller.RefreshSkinList();
            if (created.Count > 0) {
                controller.SelectedSkinIndex = controller.Skins.IndexOf(created[0]);
                controller.SelectedCornerIndex = 0;
            }
            return created;
        }

        private static PMSDKSkin CreateMaskSkin(PMSDKSkinEditorController controller, PMSDKPhotoExtraction extraction,
            PMSDKPhotoPart part, string name)
        {
            if (part.MaskWidth <= 0 || part.MaskHeight <= 0 || part.Mask == null
                || part.Mask.Length != part.MaskWidth * part.MaskHeight) {
                return null;
            }
            List<Vector2> quad = new List<Vector2>();
            for (int q = 0; q < 4; q++) {
                quad.Add(PlaceInRaster(part.Quad[q], extraction.ImageWidth, extraction.ImageHeight, DefaultFill));
            }
            string id = Guid.NewGuid().ToString("N").Substring(0, 8);
            PMSDKSkin skin = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Rectangle, quad, id, name);
            if (skin == null) {
                return null;
            }
            string fileName = id + ".png";
            WriteMaskPng(PMSDKSkinStore.ResolveMaskPath(fileName), part);
            skin.AlphaMaskPath = fileName;
            return skin;
        }

        private static PMSDKSkin CreatePolygonSkin(PMSDKSkinEditorController controller, PMSDKPhotoExtraction extraction,
            PMSDKPhotoPart part, string name)
        {
            if (part.Polygon == null || part.Polygon.Count < 3) {
                return null;
            }
            List<Vector2> points = PlacePoints(part.Polygon, extraction);
            PMSDKSkinShapeKind kind = points.Count <= PMSDKSkinMeshBuilder.MaxPolygonVertices
                ? PMSDKSkinShapeKind.Polygon : PMSDKSkinShapeKind.Freeform;
            if (points.Count > PMSDKSkinMeshBuilder.MaxFreeformVertices) {
                points.RemoveRange(PMSDKSkinMeshBuilder.MaxFreeformVertices, points.Count - PMSDKSkinMeshBuilder.MaxFreeformVertices);
            }
            return controller.SpawnSkinFromSnapshot(kind, points, Guid.NewGuid().ToString("N").Substring(0, 8), name);
        }

        private static void CreateHoleSkins(PMSDKSkinEditorController controller, PMSDKPhotoExtraction extraction,
            PMSDKPhotoPart part, PMSDKSkin owner, string ownerName)
        {
            int holeNumber = 0;
            for (int c = 0; c < part.Contours.Count; c++) {
                PMSDKPhotoContour contour = part.Contours[c];
                if (!contour.IsHole || contour.Points.Count < 3) {
                    continue;
                }
                holeNumber++;
                List<Vector2> points = PlacePoints(contour.Points, extraction);
                // Holes arrive clockwise; skins want counter-clockwise polygons.
                points.Reverse();
                if (points.Count > PMSDKSkinMeshBuilder.MaxPolygonVertices) {
                    points.RemoveRange(PMSDKSkinMeshBuilder.MaxPolygonVertices, points.Count - PMSDKSkinMeshBuilder.MaxPolygonVertices);
                }
                PMSDKSkin hole = controller.SpawnSkinFromSnapshot(PMSDKSkinShapeKind.Polygon, points,
                    Guid.NewGuid().ToString("N").Substring(0, 8), ownerName + " hole " + holeNumber);
                if (hole == null) {
                    continue;
                }
                hole.ZOrder = owner.ZOrder + 1;
                PMSDKSkinContent content = hole.GetComponent<PMSDKSkinContent>();
                if (content != null) {
                    content.Mode = PMSDKSkinContentMode.Background;
                }
            }
        }

        private static List<Vector2> PlacePoints(List<Vector2> source, PMSDKPhotoExtraction extraction)
        {
            List<Vector2> placed = new List<Vector2>(source.Count);
            for (int i = 0; i < source.Count; i++) {
                placed.Add(PlaceInRaster(source[i], extraction.ImageWidth, extraction.ImageHeight, DefaultFill));
            }
            return placed;
        }

        /// <summary>Write the cropped silhouette as a grayscale PNG. The native mask is top-to-bottom,
        /// Unity textures are bottom-to-top, so rows are flipped.</summary>
        public static void WriteMaskPng(string path, PMSDKPhotoPart part)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) {
                Directory.CreateDirectory(directory);
            }
            Texture2D texture = new Texture2D(part.MaskWidth, part.MaskHeight, TextureFormat.RGBA32, false, true);
            try {
                Color32[] pixels = new Color32[part.MaskWidth * part.MaskHeight];
                for (int y = 0; y < part.MaskHeight; y++) {
                    int sourceRow = part.MaskHeight - 1 - y;
                    for (int x = 0; x < part.MaskWidth; x++) {
                        byte v = part.Mask[sourceRow * part.MaskWidth + x];
                        pixels[y * part.MaskWidth + x] = new Color32(v, v, v, 255);
                    }
                }
                texture.SetPixels32(pixels);
                texture.Apply(false);
                File.WriteAllBytes(path, texture.EncodeToPNG());
            } finally {
                if (Application.isPlaying) {
                    UnityEngine.Object.Destroy(texture);
                } else {
                    UnityEngine.Object.DestroyImmediate(texture);
                }
            }
        }
    }
}
