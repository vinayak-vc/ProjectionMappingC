using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// IMGUI operator console for the Skin Editor scene (Display 1). Extended in Phase B
    /// to include:
    ///   - Tool toolbar (top): buttons for each tool + Undo/Redo + Save/Load.
    ///   - Skin list (left): thumbnails-less list; click to select, mute glyph if muted.
    ///   - Skin inspector (right): name, kind, opacity, soft-mask, z-order, delete.
    ///   - Status line (bottom).
    ///   - F1 help overlay.
    ///
    /// This is deliberately IMGUI (no Canvas / TextMeshPro dependency). Adequate for
    /// the on-site authoring workflow — a Canvas-based polish pass can slot in as a
    /// separate component later without touching the controller.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorHUD : MonoBehaviour
    {
        public PMSDKSkinEditorController Controller;
        public bool ShowHelp = false;
        public KeyCode HelpKey = KeyCode.F1;

        [Tooltip("HUD text scale — bump on high-DPI displays.")]
        public float TextScale = 1.0f;

        private GUIStyle statusStyle, labelStyle, helpStyle, toolButtonStyle, listButtonStyle;

        private void Update()
        {
            if (Input.GetKeyDown(HelpKey)) ShowHelp = !ShowHelp;
        }

        private void OnGUI()
        {
            if (Controller == null) return;
            EnsureStyles();

            DrawToolbar();
            DrawSkinList();
            DrawSkinInspector();
            DrawStatusLine();
            if (ShowHelp) DrawHelpPanel();
        }

        // -------- toolbar (top center) --------

        private void DrawToolbar()
        {
            float toolbarH = 32 * TextScale;
            float pad = 8;
            float btnW = 88 * TextScale;
            var tools = new (string label, PMSDKSkinEditorController.Tool tool)[]
            {
                ("Select [Esc]", PMSDKSkinEditorController.Tool.Select),
                ("Rect [R]",     PMSDKSkinEditorController.Tool.Rectangle),
                ("Ellipse [E]",  PMSDKSkinEditorController.Tool.Ellipse),
                ("Polygon [P]",  PMSDKSkinEditorController.Tool.Polygon),
                ("Freeform [F]", PMSDKSkinEditorController.Tool.Freeform),
            };
            float totalW = tools.Length * btnW + (tools.Length + 4) * pad + 4 * btnW;
            float x = (Screen.width - totalW) * 0.5f;
            float y = pad;

            var bg = new Rect(x - pad, y - 2, totalW + 2 * pad, toolbarH + 4);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            foreach (var t in tools)
            {
                bool active = Controller.CurrentTool == t.tool;
                var rect = new Rect(x, y, btnW, toolbarH);
                GUI.color = active ? new Color(1f, 0.85f, 0.05f, 1f) : Color.white;
                if (GUI.Button(rect, t.label, toolButtonStyle))
                    Controller.PickTool(t.tool);
                x += btnW + pad;
            }

            GUI.color = Color.white;
            if (GUI.Button(new Rect(x, y, btnW, toolbarH), "Undo [Ctrl+Z]", toolButtonStyle))
                Controller.UndoLast();
            x += btnW + pad;
            if (GUI.Button(new Rect(x, y, btnW, toolbarH), "Redo [Ctrl+Y]", toolButtonStyle))
                Controller.RedoLast();
            x += btnW + pad;
            if (GUI.Button(new Rect(x, y, btnW, toolbarH), "Save [Ctrl+S]", toolButtonStyle))
                Controller.SaveDefault();
            x += btnW + pad;
            if (GUI.Button(new Rect(x, y, btnW, toolbarH), "Load [Ctrl+L]", toolButtonStyle))
                Controller.LoadDefault();

            // Snap toggle (M-Sk4). Reserved snap steps: 0 = off, 0.05 = 5% grid, 0.1 = 10%.
            // Clicking cycles OFF → 0.05 → 0.1 → OFF.
            x += btnW + pad;
            string snapLabel = Controller.SnapGridStep <= 0f
                ? "Snap: OFF"
                : $"Snap: {Controller.SnapGridStep:F2}";
            GUI.color = Controller.SnapGridStep > 0f ? new Color(0.3f, 0.9f, 1f, 1f) : Color.white;
            if (GUI.Button(new Rect(x, y, btnW, toolbarH), snapLabel, toolButtonStyle))
            {
                if      (Controller.SnapGridStep <= 0f)     Controller.SnapGridStep = 0.05f;
                else if (Controller.SnapGridStep < 0.075f)  Controller.SnapGridStep = 0.10f;
                else                                        Controller.SnapGridStep = 0f;
            }
            GUI.color = Color.white;
        }

        // -------- skin list (left) --------

        private void DrawSkinList()
        {
            float w = 220 * TextScale;
            float x = 8;
            float y = 60 * TextScale;
            float h = Screen.height - y - 60;

            var bg = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 8, y + 4, w - 16, 24), $"Skins ({Controller.Skins.Count})", labelStyle);

            float rowH = 26 * TextScale;
            float rowY = y + 28;
            for (int i = 0; i < Controller.Skins.Count; i++)
            {
                var s = Controller.Skins[i];
                if (s == null) continue;
                bool sel = i == Controller.SelectedSkinIndex;
                bool multi = Controller.MultiSelectedIndices.Contains(i);

                // Row rect and inline mini-buttons: [Label ...] [up] [down] [eye].
                float miniW = 20 * TextScale;
                var labelRect = new Rect(x + 6, rowY, w - 12 - 3 * miniW - 6, rowH - 2);
                GUI.color = sel ? new Color(1f, 0.85f, 0.05f, 1f)
                          : (multi ? new Color(0.3f, 0.8f, 1f, 1f)
                                  : new Color(0.9f, 0.9f, 0.9f, 1f));
                string mutedGlyph = Controller.IsMuted(s) ? " (muted)" : "";
                bool visible = Controller.IsVisible(s);
                string hiddenGlyph = visible ? "" : " (hidden)";
                string label = $"{i + 1}. {s.DisplayName}  <{s.Kind}>{mutedGlyph}{hiddenGlyph}";
                if (GUI.Button(labelRect, label, listButtonStyle))
                {
                    // Ctrl+click here would be nice for multi-select, but IMGUI buttons
                    // don't expose modifier state — pointer-based multi-select is on the
                    // projector canvas via the Input layer. This click always sets the primary.
                    Controller.SelectedSkinIndex = i;
                    Controller.SelectedCornerIndex = 0;
                }

                // Z-order up / down / visibility toggle.
                GUI.color = Color.white;
                float bx = labelRect.xMax + 2;
                if (GUI.Button(new Rect(bx, rowY, miniW, rowH - 2), "^", toolButtonStyle))
                { Controller.SelectedSkinIndex = i; Controller.MoveSelectedZOrder(+1); }
                if (GUI.Button(new Rect(bx + miniW + 1, rowY, miniW, rowH - 2), "v", toolButtonStyle))
                { Controller.SelectedSkinIndex = i; Controller.MoveSelectedZOrder(-1); }
                if (GUI.Button(new Rect(bx + 2 * miniW + 2, rowY, miniW, rowH - 2),
                    visible ? "o" : "-", toolButtonStyle))
                { Controller.SetSkinVisibility(s, !visible); }

                rowY += rowH;
                if (rowY + rowH > y + h - 8) break;
            }
        }

        // -------- inspector (right) --------

        private void DrawSkinInspector()
        {
            var s = Controller.Selected;
            float w = 260 * TextScale;
            float x = Screen.width - w - 8;
            float y = 60 * TextScale;
            float h = 260 * TextScale;

            var bg = new Rect(x, y, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(bg, Texture2D.whiteTexture);
            GUI.color = Color.white;

            GUI.Label(new Rect(x + 8, y + 4, w - 16, 24), "Skin Inspector", labelStyle);

            if (s == null)
            {
                GUI.Label(new Rect(x + 8, y + 30, w - 16, 24), "(no skin selected)", labelStyle);
                return;
            }

            float row = y + 30;
            GUI.Label(new Rect(x + 8, row, 60, 22), "Name:", labelStyle);
            s.DisplayName = GUI.TextField(new Rect(x + 76, row, w - 84, 22), s.DisplayName ?? "");
            row += 26;

            GUI.Label(new Rect(x + 8, row, w - 16, 22), $"Kind: {s.Kind}", labelStyle);
            row += 22;
            GUI.Label(new Rect(x + 8, row, w - 16, 22), $"Verts: {s.Vertices?.Count ?? 0}", labelStyle);
            row += 26;

            GUI.Label(new Rect(x + 8, row, 90, 22), $"Opacity: {s.Opacity:F2}", labelStyle);
            s.Opacity = GUI.HorizontalSlider(new Rect(x + 100, row + 6, w - 108, 12), s.Opacity, 0f, 1f);
            row += 24;

            GUI.Label(new Rect(x + 8, row, 90, 22), $"Feather: {s.SoftMask:F2}", labelStyle);
            s.SoftMask = GUI.HorizontalSlider(new Rect(x + 100, row + 6, w - 108, 12), s.SoftMask, 0f, 0.5f);
            row += 24;

            GUI.Label(new Rect(x + 8, row, 100, 22), $"Z: {s.ZOrder}", labelStyle);
            if (GUI.Button(new Rect(x + 108, row, 60, 22), "back [", toolButtonStyle))
                Controller.MoveSelectedZOrder(-1);
            if (GUI.Button(new Rect(x + 172, row, 60, 22), "fwd ]", toolButtonStyle))
                Controller.MoveSelectedZOrder(+1);
            row += 30;

            if (GUI.Button(new Rect(x + 8, row, 100, 24), "Duplicate", toolButtonStyle))
                Controller.DuplicateSelected();
            if (GUI.Button(new Rect(x + 116, row, 100, 24), "Solo [X]", toolButtonStyle))
                Controller.ToggleSoloSelected();
            row += 28;

            if (GUI.Button(new Rect(x + 8, row, 100, 24), "Pattern >", toolButtonStyle))
                Controller.CycleTestPatternSelected(+1);
            if (GUI.Button(new Rect(x + 116, row, 100, 24), "Delete", toolButtonStyle))
                Controller.DeleteSelected();
        }

        // -------- status line (bottom) + F1 hint --------

        private void DrawStatusLine()
        {
            var content = new GUIContent(Controller.StatusLine ?? "");
            var size = statusStyle.CalcSize(content);
            var rect = new Rect(12, Screen.height - size.y - 18, size.x + 20, size.y + 8);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, rect.width - 20, rect.height - 8), content, statusStyle);

            int groupN = Controller.SelectionGroupCount;
            string selBadge = groupN > 1 ? $"  |  Selected: {groupN} (group)" : "";
            string snapBadge = Controller.SnapGridStep > 0f
                ? $"  |  Snap grid: {Controller.SnapGridStep:F3}" : "";
            var hint = $"F1: help  |  Undo depth: {Controller.Undo.UndoDepth}/{100}  Redo: {Controller.Undo.RedoDepth}{selBadge}{snapBadge}";
            var hintSize = helpStyle.CalcSize(new GUIContent(hint));
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(rect.x, rect.y - hintSize.y - 4, hintSize.x + 20, hintSize.y + 4), hint, helpStyle);
        }

        private void DrawHelpPanel()
        {
            string[] lines =
            {
                "SKIN EDITOR — full key map",
                "",
                "Tools:  R Rect   E Ellipse   P Polygon   F Freeform   Esc Select",
                "",
                "Drawing:",
                "  Drag on canvas (R/E) — draw new rect/ellipse.",
                "  Click (P) — append polygon vertex. Enter or click near start — close.",
                "  Drag (F) — trace freeform stroke. Release — close.",
                "  N — add default shape at canvas centre.",
                "",
                "Editing:",
                "  Drag corner handle — move that vertex.",
                "  Drag edge midpoint (Rect/Polygon/Freeform) — move both adjacent vertices.",
                "  + / - — insert / remove polygon vertex.",
                "  Ctrl+D — duplicate.  [ / ] — z-order back / forward.",
                "  X / Shift+X — solo / unmute all.  Y / Shift+Y — pattern cycle.",
                "  Del / Backspace — delete selected.",
                "",
                "Navigation & nudge:",
                "  PgUp / PgDn — cycle skins.  Tab / Shift+Tab — cycle corners.",
                "  Arrows — nudge (Shift = fine, Ctrl = coarse).",
                "",
                "History & files:",
                "  Ctrl+Z / Ctrl+Y — undo / redo.",
                "  Ctrl+S — save to default path.  Ctrl+L — load from default path.",
                "",
                "Selection / snap (M-Sk4):",
                "  Ctrl or Shift + click a skin — toggle in multi-select group.",
                "  Arrows with a group — nudge the same-index corner across every selected skin.",
                "  Snap toolbar button — cycle grid off/0.05/0.10.",
                "  Alignment: dragging a corner near another skin's vertex X/Y snaps + shows a dashed guide.",
                "  List row buttons: ^/v change z-order, o/- toggle visibility.",
                "",
                "Save location: " + Controller.DefaultSavePath,
            };

            float w = 720, h = 22 * lines.Length + 20;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.9f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = Color.white;

            float y = panel.y + 12;
            foreach (var line in lines)
            {
                GUI.Label(new Rect(panel.x + 16, y, panel.width - 32, 22), line, helpStyle);
                y += 22;
            }
        }

        private void EnsureStyles()
        {
            if (statusStyle == null)
                statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(14 * TextScale),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                };
            if (labelStyle == null)
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(13 * TextScale),
                    normal = { textColor = new Color(0.95f, 0.95f, 0.95f, 1f) },
                };
            if (helpStyle == null)
                helpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(13 * TextScale),
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) },
                };
            if (toolButtonStyle == null)
                toolButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    fontSize = Mathf.RoundToInt(12 * TextScale),
                };
            if (listButtonStyle == null)
                listButtonStyle = new GUIStyle(GUI.skin.button)
                {
                    alignment = TextAnchor.MiddleLeft,
                    fontSize = Mathf.RoundToInt(12 * TextScale),
                };
        }
    }
}
