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

            // ---- v1.8 additions (7 presets) ----

            // 7-cell honeycomb: center hex + 6 neighbors in the six pointy-top
            // directions. Cell radius r=0.11 → adjacent centers offset by (√3·r, 0)
            // horizontally and (√3·r/2, 1.5·r) diagonally, so every hex touches its
            // neighbor edges cleanly. Kept as separate polygon skins so users can
            // assign different content per cell.
            new Preset {
                Id = "honeycomb-7",
                DisplayName = "Honeycomb (7 cells)",
                Description = "Seven hex cells — one center + six neighbors. Assign different content per cell for a hexagonal mosaic wall.",
                Skins = HoneycombSkins(cx: 0.50f, cy: 0.50f, r: 0.11f),
            },

            // Human silhouette — a 21-vertex outline of a head-shoulders-arms-torso-
            // legs figure. First "irregular polygon from an outline" preset; teaches
            // users the many-vertex flow without dragging every point manually.
            // Traversal is CCW (interior on the left of motion) so signed area is
            // positive and the mesh triangulates without flipping normals.
            new Preset {
                Id = "silhouette",
                DisplayName = "Human Silhouette",
                Description = "Head-shoulders-arms-legs outline (21 vertices). Drop over a mannequin, statue, or cardboard cutout and drag vertices to match.",
                Skins = new[] {
                    new SkinDef { Name = "body", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = SilhouetteVertices() },
                },
            },

            // Two side-by-side arches — arcade / colonnade. Reuses the Arch factory
            // from v1.6; distinct from the single Arch preset (#7) which is one
            // opening centered in raster.
            new Preset {
                Id = "arcade-2",
                DisplayName = "Double Arch (Arcade)",
                Description = "Two arches side by side — arcade, colonnade, twin tunnel entrances. Same shape, two positions.",
                Skins = new[] {
                    new SkinDef { Name = "arch-l", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = Arch(cx: 0.30f, baseY: 0.15f, topY: 0.75f, halfWidth: 0.16f, steps: 6) },
                    new SkinDef { Name = "arch-r", Kind = PMSDKSkinShapeKind.Polygon,
                                  Vertices = Arch(cx: 0.70f, baseY: 0.15f, topY: 0.75f, halfWidth: 0.16f, steps: 6) },
                },
            },

            // Ziggurat / staircase — 5 rectangles narrowing as they stack. Real
            // installation surfaces: theatre risers, storefront steps, temple facades.
            new Preset {
                Id = "staircase",
                DisplayName = "Staircase / Ziggurat (5 steps)",
                Description = "Five stacked rectangles narrowing to the top — theatre risers, storefront steps, ziggurat facades.",
                Skins = new[] {
                    new SkinDef { Name = "step-1", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.10f, 0.15f, 0.90f, 0.30f) },
                    new SkinDef { Name = "step-2", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.18f, 0.30f, 0.82f, 0.42f) },
                    new SkinDef { Name = "step-3", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.26f, 0.42f, 0.74f, 0.54f) },
                    new SkinDef { Name = "step-4", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.34f, 0.54f, 0.66f, 0.66f) },
                    new SkinDef { Name = "step-5", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.42f, 0.66f, 0.58f, 0.78f) },
                },
            },

            // Building corner (2 faces meeting at 90°). Two rectangles as
            // foreshortened parallelograms sharing the central vertical edge. Distinct
            // from Box:3-Faces which shows a small box from a corner viewpoint; this
            // covers "one edge of a large building" — outdoor architectural mapping.
            new Preset {
                Id = "corner-2",
                DisplayName = "Building Corner (2 Faces)",
                Description = "Two faces meeting at a vertical edge — outdoor architectural corner-wrap. Both faces are parallelograms sharing the central edge.",
                Skins = new[] {
                    new SkinDef { Name = "face-l", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.10f, 0.20f), new Vector2(0.50f, 0.15f),
                                      new Vector2(0.50f, 0.85f), new Vector2(0.10f, 0.80f),
                                  } },
                    new SkinDef { Name = "face-r", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = new[] {
                                      new Vector2(0.50f, 0.15f), new Vector2(0.90f, 0.20f),
                                      new Vector2(0.90f, 0.80f), new Vector2(0.50f, 0.85f),
                                  } },
                },
            },

            // Column-N — catalog entry defaults to N=6; menu path opens a small
            // dialog to pick N in [3..12] and invokes MakeColumnN(n) directly, so
            // this Preset record is the "just click apply for a sane default" path.
            new Preset {
                Id = "column-n",
                DisplayName = "Column (N strips, default 6)",
                Description = "Parametric column strips — the menu prompts for N in [3..12]. Applying from this catalog entry uses N = 6.",
                Skins = MakeColumnStripSkins(n: 6),
            },

            // Logo wordmark — 4 blocks side by side ("L O G O" placeholders). The
            // pattern users hit at retail activations: cutout letters where each
            // letter needs its own content feed.
            new Preset {
                Id = "logo-wordmark",
                DisplayName = "Logo Wordmark (4 blocks)",
                Description = "Four side-by-side rectangles as letter blocks — retail cutouts, sponsor bays, four-panel wordmarks. Replace or resize blocks to match your brand.",
                Skins = new[] {
                    new SkinDef { Name = "block-1", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.15f, 0.35f, 0.31f, 0.65f) },
                    new SkinDef { Name = "block-2", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.33f, 0.35f, 0.49f, 0.65f) },
                    new SkinDef { Name = "block-3", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.51f, 0.35f, 0.67f, 0.65f) },
                    new SkinDef { Name = "block-4", Kind = PMSDKSkinShapeKind.Rectangle,
                                  Vertices = Rect(0.69f, 0.35f, 0.85f, 0.65f) },
                },
            },
        };

        // -------- v1.8 helpers that produce SkinDef[] --------

        /// <summary>
        /// 21-vertex human-figure outline in raster space. Traversal is CCW so
        /// <see cref="PMSDKSkinMeshBuilder.SignedArea"/> is positive.
        /// </summary>
        private static Vector2[] SilhouetteVertices() => new[]
        {
            new Vector2(0.50f, 0.90f), // top of head
            new Vector2(0.45f, 0.88f), // head left-top
            new Vector2(0.42f, 0.78f), // head left
            new Vector2(0.47f, 0.70f), // neck left
            new Vector2(0.35f, 0.68f), // shoulder left
            new Vector2(0.28f, 0.60f), // upper arm out left
            new Vector2(0.32f, 0.48f), // elbow left
            new Vector2(0.38f, 0.35f), // wrist left
            new Vector2(0.42f, 0.30f), // hip left
            new Vector2(0.44f, 0.20f), // leg outer top left
            new Vector2(0.46f, 0.10f), // foot left
            new Vector2(0.54f, 0.10f), // foot right
            new Vector2(0.56f, 0.20f), // leg outer top right
            new Vector2(0.58f, 0.30f), // hip right
            new Vector2(0.62f, 0.35f), // wrist right
            new Vector2(0.68f, 0.48f), // elbow right
            new Vector2(0.72f, 0.60f), // upper arm out right
            new Vector2(0.65f, 0.68f), // shoulder right
            new Vector2(0.53f, 0.70f), // neck right
            new Vector2(0.58f, 0.78f), // head right
            new Vector2(0.55f, 0.88f), // head right-top
        };

        /// <summary>
        /// One hex Polygon skin per honeycomb cell (1 center + 6 neighbors in the
        /// standard pointy-top offsets).
        /// </summary>
        private static SkinDef[] HoneycombSkins(float cx, float cy, float r)
        {
            float dx = Mathf.Sqrt(3f) * r;
            float dyDiag = 1.5f * r;
            float dxDiag = dx * 0.5f;

            (string name, float x, float y)[] centers =
            {
                ("center", cx,              cy         ),
                ("e",      cx + dx,         cy         ),
                ("w",      cx - dx,         cy         ),
                ("ne",     cx + dxDiag,     cy + dyDiag),
                ("nw",     cx - dxDiag,     cy + dyDiag),
                ("se",     cx + dxDiag,     cy - dyDiag),
                ("sw",     cx - dxDiag,     cy - dyDiag),
            };

            var skins = new SkinDef[centers.Length];
            for (int i = 0; i < centers.Length; i++)
            {
                skins[i] = new SkinDef {
                    Name = centers[i].name,
                    Kind = PMSDKSkinShapeKind.Polygon,
                    Vertices = RegularNGon(centers[i].x, centers[i].y, r, 6, 90f),
                };
            }
            return skins;
        }

        /// <summary>
        /// N contiguous vertical strips spanning x∈[0.20, 0.80], y∈[0.15, 0.85].
        /// Extracted so <see cref="MakeColumnN"/> can call it too.
        /// </summary>
        private static SkinDef[] MakeColumnStripSkins(int n)
        {
            n = Mathf.Clamp(n, 3, 12);
            const float xLo = 0.20f, xHi = 0.80f, yLo = 0.15f, yHi = 0.85f;
            float stripW = (xHi - xLo) / n;
            var skins = new SkinDef[n];
            for (int i = 0; i < n; i++)
            {
                float x0 = xLo + i * stripW;
                float x1 = x0 + stripW;
                skins[i] = new SkinDef {
                    Name = $"col-{i}",
                    Kind = PMSDKSkinShapeKind.Rectangle,
                    Vertices = Rect(x0, yLo, x1, yHi),
                };
            }
            return skins;
        }

        /// <summary>
        /// Parametric N-strip column preset — used by the Editor menu's Column
        /// dialog. Returns a <see cref="Preset"/> with N in [3..12] (clamped).
        /// The preset's Id keeps the base "column-n" so downstream consumers can
        /// still `TryFind("column-n", ...)`; the DisplayName reflects the actual N.
        /// </summary>
        public static Preset MakeColumnN(int n)
        {
            n = Mathf.Clamp(n, 3, 12);
            return new Preset {
                Id = "column-n",
                DisplayName = $"Column ({n} strips)",
                Description = $"Parametric column — {n} vertical strips filling x∈[0.20, 0.80], y∈[0.15, 0.85].",
                Skins = MakeColumnStripSkins(n),
            };
        }

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
