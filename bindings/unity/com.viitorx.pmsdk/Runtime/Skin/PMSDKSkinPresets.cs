using System.Collections.Generic;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// A catalog of iconic projection-mapping target shapes — pre-built skin layouts
    /// users can drop into the Skin Editor and stretch to fit their physical object.
    /// The intent is to accelerate authoring for common targets (cubes, columns,
    /// facades, arches, domes) and to teach the plugin's UX: click a preset, drag its
    /// corners onto the physical prop, assign content.
    ///
    /// Each preset is a set of raster-space skin definitions (kind + vertex list in
    /// [0..1] y-UP). Applied via <see cref="PMSDKSkinEditorController.ApplyPreset"/>.
    ///
    /// See docs/multi-skin-design.md for the shape/warp routing rules.
    /// </summary>
    public static class PMSDKSkinPresets
    {
        [System.Serializable]
        public struct SkinDef
        {
            public string Name;
            public PMSDKSkinShapeKind Kind;
            public Vector2[] Vertices;
        }

        [System.Serializable]
        public struct Preset
        {
            public string Id;
            public string DisplayName;
            public string Description;
            public SkinDef[] Skins;
        }

        // -------- factory helpers --------

        private static Vector2[] Rect(float x0, float y0, float x1, float y1) => new[]
        {
            new Vector2(x0, y0), new Vector2(x1, y0),
            new Vector2(x1, y1), new Vector2(x0, y1),
        };

        private static Vector2[] Star5(float cx, float cy, float rOuter, float rInner)
        {
            var v = new Vector2[10];
            for (int k = 0; k < 10; k++)
            {
                float ang = Mathf.PI / 2f + k * Mathf.PI / 5f;
                float r = (k % 2 == 0) ? rOuter : rInner;
                v[k] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            return v;
        }

        /// <summary>
        /// Regular n-gon centered at (cx, cy), radius r, first vertex at
        /// <paramref name="startAngleDeg"/> (measured CCW from +X, y-UP). Used by
        /// the Hexagon and Honeycomb presets; kept general so future regular n-gon
        /// presets (pentagon, octagon) can reuse it.
        /// </summary>
        private static Vector2[] RegularNGon(float cx, float cy, float r, int n, float startAngleDeg = 90f)
        {
            var v = new Vector2[n];
            float a0 = startAngleDeg * Mathf.Deg2Rad;
            float step = 2f * Mathf.PI / n;
            for (int k = 0; k < n; k++)
            {
                float ang = a0 + k * step;
                v[k] = new Vector2(cx + Mathf.Cos(ang) * r, cy + Mathf.Sin(ang) * r);
            }
            return v;
        }

        private static Vector2[] Arch(float cx, float baseY, float topY, float halfWidth, int steps)
        {
            // Rectangle base + a semi-arc across the top. Vertices ordered CCW.
            var list = new List<Vector2>();
            list.Add(new Vector2(cx - halfWidth, baseY));                 // BL
            list.Add(new Vector2(cx + halfWidth, baseY));                 // BR
            list.Add(new Vector2(cx + halfWidth, (baseY + topY) * 0.5f)); // right shoulder
            // Semi-arc from right shoulder to left shoulder, sweeping through topY at center.
            float ay = (baseY + topY) * 0.5f;
            float radius = (topY - baseY) * 0.5f;
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps; // 0..1
                float ang = t * Mathf.PI;   // 0..π sweep from right to left along the arc
                float x = cx + Mathf.Cos(ang) * halfWidth;
                float y = ay + Mathf.Sin(ang) * radius;
                list.Add(new Vector2(x, y));
            }
            list.Add(new Vector2(cx - halfWidth, ay)); // left shoulder
            return list.ToArray();
        }

        // -------- the catalog --------

        /// <summary>Every shipping preset, in the order intended for menus / UI.</summary>
        public static readonly Preset[] All = new Preset[]
        {
            new Preset {
                Id = "single-rect",
                DisplayName = "Single Rectangle",
                Description = "One flat rectangle — beginner starter. Best for a poster, monitor, or single flat panel.",
                Skins = new[] {
                    new SkinDef { Name = "screen", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.2f, 0.2f, 0.8f, 0.8f) },
                },
            },

            // ★ The user-asked-for one: box viewed from a corner (3 visible faces).
            new Preset {
                Id = "box-iso-3",
                DisplayName = "Box: 3 Faces Isometric",
                Description = "Cube viewed from a corner — front, right, top. Drag each face's corners onto the physical box.",
                Skins = new[] {
                    // Front — plain rectangle bottom-left of raster.
                    new SkinDef { Name = "front", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.20f, 0.20f), new Vector2(0.55f, 0.20f),
                                      new Vector2(0.55f, 0.60f), new Vector2(0.20f, 0.60f),
                                  } },
                    // Right — foreshortened parallelogram sharing front's right edge.
                    new SkinDef { Name = "right", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.55f, 0.20f), new Vector2(0.75f, 0.30f),
                                      new Vector2(0.75f, 0.70f), new Vector2(0.55f, 0.60f),
                                  } },
                    // Top — foreshortened parallelogram sharing front's top and right's top-left edges.
                    new SkinDef { Name = "top", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.20f, 0.60f), new Vector2(0.55f, 0.60f),
                                      new Vector2(0.75f, 0.70f), new Vector2(0.40f, 0.70f),
                                  } },
                },
            },

            new Preset {
                Id = "box-2",
                DisplayName = "Box: 2 Faces (Top+Front)",
                Description = "Box viewed slightly from above — top and front only.",
                Skins = new[] {
                    new SkinDef { Name = "front", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.20f, 0.15f, 0.80f, 0.55f) },
                    // Top: perspective-foreshortened trapezoid via 4-corner drag.
                    new SkinDef { Name = "top", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.20f, 0.55f), new Vector2(0.80f, 0.55f),
                                      new Vector2(0.75f, 0.85f), new Vector2(0.25f, 0.85f),
                                  } },
                },
            },

            new Preset {
                Id = "column-3",
                DisplayName = "Column / Cylinder (3 strips)",
                Description = "Cylinder or column as three vertical strips — bend the sides to match curvature after applying.",
                Skins = new[] {
                    new SkinDef { Name = "left",   Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.25f, 0.20f, 0.42f, 0.80f) },
                    new SkinDef { Name = "center", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.42f, 0.20f, 0.58f, 0.80f) },
                    new SkinDef { Name = "right",  Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.58f, 0.20f, 0.75f, 0.80f) },
                },
            },

            new Preset {
                Id = "prism-2",
                DisplayName = "Triangular Prism (2 faces)",
                Description = "Two triangular faces of a prism / wedge — polygon skins with three vertices each.",
                Skins = new[] {
                    new SkinDef { Name = "left",  Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = new[] {
                                      new Vector2(0.20f, 0.30f), new Vector2(0.50f, 0.20f),
                                      new Vector2(0.50f, 0.80f),
                                  } },
                    new SkinDef { Name = "right", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = new[] {
                                      new Vector2(0.50f, 0.20f), new Vector2(0.80f, 0.30f),
                                      new Vector2(0.50f, 0.80f),
                                  } },
                },
            },

            new Preset {
                Id = "facade-3x2",
                DisplayName = "Facade Grid 3x2",
                Description = "Six rectangles arranged as a 3x2 grid — for building facades, window arrays, panel walls.",
                Skins = new[] {
                    // Top row (rendered above the bottom row visually in raster space; y=high is up).
                    new SkinDef { Name = "top-1", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.15f, 0.55f, 0.40f, 0.85f) },
                    new SkinDef { Name = "top-2", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.42f, 0.55f, 0.67f, 0.85f) },
                    new SkinDef { Name = "top-3", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.69f, 0.55f, 0.94f, 0.85f) },
                    // Bottom row.
                    new SkinDef { Name = "bot-1", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.15f, 0.15f, 0.40f, 0.45f) },
                    new SkinDef { Name = "bot-2", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.42f, 0.15f, 0.67f, 0.45f) },
                    new SkinDef { Name = "bot-3", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.69f, 0.15f, 0.94f, 0.45f) },
                },
            },

            new Preset {
                Id = "arch",
                DisplayName = "Arch / Tunnel",
                Description = "11-vertex polygon tracing an arch — for archways, tunnel openings, alcoves.",
                Skins = new[] {
                    new SkinDef { Name = "arch", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = Arch(cx: 0.50f, baseY: 0.15f, topY: 0.85f,
                                                  halfWidth: 0.25f, steps: 8) },
                },
            },

            new Preset {
                Id = "dome",
                DisplayName = "Dome / Sphere",
                Description = "Ellipse skin — for domes, orbs, circular signage. Uses the shader mask.",
                Skins = new[] {
                    new SkinDef { Name = "sphere", Kind = PMSDKSkinShapeKind.Ellipse,
                                  Vertices = Rect(0.25f, 0.25f, 0.75f, 0.75f) },
                },
            },

            new Preset {
                Id = "star",
                DisplayName = "Star (5-point)",
                Description = "10-vertex polygon forming a five-pointed star — decorative logo / signage cutout.",
                Skins = new[] {
                    new SkinDef { Name = "star", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = Star5(0.50f, 0.50f, rOuter: 0.30f, rInner: 0.12f) },
                },
            },

            new Preset {
                Id = "diorama",
                DisplayName = "Diorama Stage (Frame + Stage)",
                Description = "Outer frame + inner stage rectangles — for proscenium arches, framed pictures, dioramas.",
                Skins = new[] {
                    new SkinDef { Name = "frame", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.10f, 0.10f, 0.90f, 0.90f) },
                    new SkinDef { Name = "stage", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.25f, 0.25f, 0.75f, 0.75f) },
                },
            },

            // v1.7 — first regular n-gon in the catalog. Cheap on vertices (6) and
            // maps directly onto hex signage, hex stools, single-cell honeycomb tiles.
            new Preset {
                Id = "hexagon",
                DisplayName = "Hexagon",
                Description = "Regular six-sided polygon — hex signage, honeycomb cells, hex-stool tops. Pointy-top orientation.",
                Skins = new[] {
                    new SkinDef { Name = "hex", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = RegularNGon(cx: 0.50f, cy: 0.50f, r: 0.30f, n: 6, startAngleDeg: 90f) },
                },
            },
        };

        // ---- lookup ----

        public static bool TryFind(string id, out Preset preset)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Id == id) { preset = All[i]; return true; }
            }
            preset = default;
            return false;
        }

        public static string[] DisplayNames()
        {
            var names = new string[All.Length];
            for (int i = 0; i < All.Length; i++) names[i] = All[i].DisplayName;
            return names;
        }
    }
}
