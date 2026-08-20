using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// Minimal IMGUI HUD for the Skin Editor scene: status line + a one-page help
    /// overlay (F1). Sits on Display 1 by default (IMGUI renders on the active
    /// display); the projector output on Display 2+ shows the drawing canvas + overlay.
    ///
    /// Phase A only — a proper Canvas-based skin list + inspector lands in M-Sk2 phase B.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKSkinEditorHUD : MonoBehaviour
    {
        public PMSDKSkinEditorController Controller;
        public bool ShowHelp = false;
        public KeyCode HelpKey = KeyCode.F1;

        [Tooltip("HUD text scale — bump on high-DPI displays.")]
        public float TextScale = 1.0f;

        private GUIStyle statusStyle, helpStyle;

        private void Update()
        {
            if (Input.GetKeyDown(HelpKey)) ShowHelp = !ShowHelp;
        }

        private void OnGUI()
        {
            if (Controller == null) return;
            EnsureStyles();

            // Status line, top-left of Display 1.
            var status = Controller.StatusLine;
            var content = new GUIContent(status);
            var size = statusStyle.CalcSize(content);
            var rect = new Rect(12, 12, size.x + 20, size.y + 8);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(rect.x + 10, rect.y + 4, rect.width - 20, rect.height - 8), content, statusStyle);

            // Hint line about F1.
            var hint = "F1: help";
            var hintSize = helpStyle.CalcSize(new GUIContent(hint));
            GUI.color = new Color(1f, 1f, 1f, 0.8f);
            GUI.Label(new Rect(rect.x, rect.y + rect.height + 4, hintSize.x + 20, hintSize.y + 4), hint, helpStyle);

            if (ShowHelp) DrawHelpPanel();
        }

        private void DrawHelpPanel()
        {
            string[] lines =
            {
                "SKIN EDITOR — key map (phase A)",
                "",
                "Tools:",
                "  R  Rectangle    E  Ellipse    P  Polygon    F  Freeform    Esc  Select",
                "",
                "Draw & edit:",
                "  Drag on empty canvas (R/E tools) — draw a new rectangle / ellipse.",
                "  Drag a corner handle — move that vertex.",
                "  Drag an edge midpoint (Rect/Polygon) — move both adjacent vertices.",
                "  N — add a default-sized shape of the current tool at canvas centre.",
                "  Del / Backspace — delete the selected skin.",
                "",
                "Navigation & nudge:",
                "  PgUp / PgDn — cycle skins.",
                "  Tab / Shift+Tab — cycle corners within the selected skin.",
                "  Arrows — nudge selected corner (Shift = fine, Ctrl = coarse).",
                "",
                "Save (phase C):",
                "  Ctrl+S — save skin set (not yet implemented — coming in M-Sk2.5)."
            };

            float w = 620, h = 24 * lines.Length + 20;
            var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
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
            {
                statusStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(14 * TextScale),
                    fontStyle = FontStyle.Bold,
                    normal = { textColor = Color.white },
                };
            }
            if (helpStyle == null)
            {
                helpStyle = new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(13 * TextScale),
                    normal = { textColor = new Color(0.9f, 0.9f, 0.9f, 1f) },
                };
            }
        }
    }
}
