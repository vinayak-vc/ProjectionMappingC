using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace vxpmsdk.Components
{
    /// <summary>
    /// "This video can't be played" flow. Listens for <see cref="PMSDKSkinContent.VideoProblem"/>,
    /// asks the user whether to convert the file with FFmpeg, finds FFmpeg (offering to install
    /// it with a progress bar when missing), converts to a standard H.264 MP4 next to the original
    /// and points the skin at the converted copy. The original file is never changed.
    /// Created automatically by the Skin Workspace and the Skin Player.
    /// </summary>
    [DisallowMultipleComponent]
    public class PMSDKVideoConvertFlow : MonoBehaviour
    {
        /// <summary>Raised after a skin was switched to its converted copy (hosts save the set).</summary>
        public static event Action<PMSDKSkinContent> VideoReplaced;

        private enum Stage
        {
            Hidden,
            Prompt,
            AskInstall,
            Installing,
            Converting,
            Done,
            Error,
        }

        private const int MaxLogLines = 5;

        private Canvas canvas;
        private Text titleText;
        private Text bodyText;
        private Text detailText;
        private Text percentText;
        private GameObject progressRoot;
        private RectTransform progressFill;
        private readonly Button[] buttons = new Button[3];
        private readonly Text[] buttonLabels = new Text[3];

        private Stage stage = Stage.Hidden;
        private PMSDKSkinContent target;
        private string sourcePath;
        private string reason;
        private PMSDKConvertPreset preset;
        private string ffmpegPath;
        private string outputPath;
        private double durationSeconds;
        private float progress;
        private float doneAt;
        private PMSDKFfmpegJob job;
        private readonly Queue<string> logLines = new Queue<string>();
        private readonly HashSet<string> declined = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<PMSDKSkinEditorInput> pausedInputs = new List<PMSDKSkinEditorInput>();
        private Action[] buttonActions = new Action[3];

        // ------------------------------------------------------------------ lifecycle

        private void OnEnable()
        {
            PMSDKSkinContent.VideoProblem += OnVideoProblem;
            PMSDKUi.EnsureEventSystem();
            BuildUi();
            SetVisible(false);
        }

        private void OnDisable()
        {
            PMSDKSkinContent.VideoProblem -= OnVideoProblem;
            if (job != null) {
                job.Dispose();
                job = null;
            }
            ResumeInputs();
            if (canvas != null) {
                Destroy(canvas.gameObject);
                canvas = null;
            }
            stage = Stage.Hidden;
        }

        // ------------------------------------------------------------------ entry

        private void OnVideoProblem(PMSDKSkinContent content, string message)
        {
            if (stage != Stage.Hidden || content == null) {
                return;
            }
            string path = content.VideoUrl;
            if (string.IsNullOrEmpty(path) || declined.Contains(path)) {
                return;
            }
            if (path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || !File.Exists(path)) {
                return; // Streams cannot be converted; a missing file is shown by the media panel.
            }
            target = content;
            sourcePath = path;
            // Unity's message repeats the file path; keep only the reason part.
            int colon = string.IsNullOrEmpty(message) ? -1 : message.IndexOf(" : ", StringComparison.Ordinal);
            reason = colon > 0 ? message.Substring(0, colon) : message;
            ShowPrompt();
        }

        // ------------------------------------------------------------------ stages

        private void ShowPrompt()
        {
            stage = Stage.Prompt;
            string name = Path.GetFileName(sourcePath);
            Present("This video can't be played here",
                name + "\n\nThe player could not decode this file (" + Shorten(reason, 90) + ").\n\n"
                + "Recommended: convert it to a standard H.264 MP4 with FFmpeg. Your original file is not changed.",
                -1f, "",
                "Convert (HD)", delegate { BeginConvert(PMSDKConvertPreset.Standard); },
                "Convert (720p, lighter)", delegate { BeginConvert(PMSDKConvertPreset.Light); },
                "Not now", Decline);
        }

        private void BeginConvert(PMSDKConvertPreset chosen)
        {
            preset = chosen;
            ffmpegPath = PMSDKFfmpeg.Locate();
            if (ffmpegPath != null) {
                StartConversion();
                return;
            }
            if (!PMSDKFfmpeg.IsWindows) {
                stage = Stage.Error;
                Present("FFmpeg is not installed",
                    "Install FFmpeg with your package manager (for example 'brew install ffmpeg' or 'apt install ffmpeg'), then choose the video again.",
                    -1f, "", "OK", Close, null, null, null, null);
                return;
            }
            ShowAskInstall();
        }

        private void ShowAskInstall()
        {
            stage = Stage.AskInstall;
            Present("FFmpeg is needed",
                "FFmpeg is not installed on this computer.\n\nInstall it now? It will be downloaded (about 110 MB, checked for integrity) and added to your user PATH, so other programs can use it too. No administrator rights are needed.",
                -1f, "",
                "Install FFmpeg", StartInstall,
                "Cancel", Decline,
                null, null);
        }

        private void StartInstall()
        {
            string bat;
            try {
                bat = PMSDKFfmpeg.WriteInstallScripts(Path.Combine(Application.persistentDataPath, "ffmpeg_install"));
            } catch (Exception ex) {
                Fail("Could not prepare the installer: " + ex.Message);
                return;
            }
            logLines.Clear();
            progress = 0f;
            job = PMSDKFfmpegJob.Start(bat, new List<string>());
            if (job.StartError != null) {
                Fail("Could not start the installer: " + job.StartError);
                return;
            }
            stage = Stage.Installing;
            Present("Installing FFmpeg", "Please wait. This can take a few minutes.", 0f, "Starting...",
                "Cancel", CancelJob, null, null, null, null);
        }

        private void StartConversion()
        {
            try {
                outputPath = PMSDKFfmpeg.BuildOutputPath(sourcePath);
            } catch (Exception ex) {
                Fail("Could not choose an output file: " + ex.Message);
                return;
            }
            logLines.Clear();
            durationSeconds = 0.0;
            progress = 0f;
            job = PMSDKFfmpegJob.Start(ffmpegPath, PMSDKFfmpeg.BuildConvertArguments(sourcePath, outputPath, preset));
            if (job.StartError != null) {
                Fail("Could not start FFmpeg: " + job.StartError);
                return;
            }
            stage = Stage.Converting;
            Present("Converting video", Path.GetFileName(sourcePath) + "\n->  " + Path.GetFileName(outputPath),
                0f, "Starting...", "Cancel", CancelJob, null, null, null, null);
        }

        private void Finish()
        {
            if (target != null) {
                PMSDKSkinMedia.AssignVideo(target, outputPath);
                Action<PMSDKSkinContent> handler = VideoReplaced;
                if (handler != null) {
                    handler(target);
                }
            }
            stage = Stage.Done;
            doneAt = Time.realtimeSinceStartup;
            Present("Converted", "Now playing the converted copy:\n" + outputPath, 1f, "",
                "OK", Close, null, null, null, null);
        }

        private void Fail(string message)
        {
            stage = Stage.Error;
            if (job != null) {
                job.Dispose();
                job = null;
            }
            string tail = string.Join("\n", logLines.ToArray());
            Present("Something went wrong", message + (tail.Length > 0 ? "\n\n" + tail : ""),
                -1f, "", "Close", Close, null, null, null, null);
        }

        private void Decline()
        {
            if (!string.IsNullOrEmpty(sourcePath)) {
                declined.Add(sourcePath);
            }
            Close();
        }

        private void CancelJob()
        {
            if (job != null) {
                job.Cancel();
                job.Dispose();
                job = null;
            }
            if (stage == Stage.Converting) {
                TryDeletePartialOutput();
            }
            Close();
        }

        private void Close()
        {
            stage = Stage.Hidden;
            target = null;
            SetVisible(false);
            ResumeInputs();
        }

        private void TryDeletePartialOutput()
        {
            try {
                if (!string.IsNullOrEmpty(outputPath) && File.Exists(outputPath)) {
                    File.Delete(outputPath);
                }
            } catch (Exception ex) {
                Debug.LogWarning("PMSDKVideoConvertFlow: could not remove the partial file - " + ex.Message);
            }
        }

        // ------------------------------------------------------------------ per frame

        private void Update()
        {
            if (stage == Stage.Installing) {
                PumpInstall();
            } else if (stage == Stage.Converting) {
                PumpConversion();
            } else if (stage == Stage.Done && Time.realtimeSinceStartup - doneAt > 6f) {
                Close();
            }
            if (stage != Stage.Hidden) {
                SetProgress(progress);
            }
        }

        private void PumpInstall()
        {
            string line;
            while (job != null && job.TryDequeue(out line)) {
                int percent;
                string message;
                if (PMSDKFfmpeg.TryParseProgressMarker(line, out percent, out message)) {
                    progress = percent / 100f;
                    detailText.text = message;
                    continue;
                }
                int loose = PMSDKFfmpeg.ParsePercent(line);
                if (loose >= 0) {
                    progress = Mathf.Max(progress, 0.08f + 0.87f * (loose / 100f));
                }
                AddLog(line);
            }
            if (job != null && job.IsFinished) {
                bool ok = job.Succeeded;
                job.Dispose();
                job = null;
                ffmpegPath = PMSDKFfmpeg.Locate();
                if (ok && ffmpegPath != null) {
                    StartConversion();
                } else if (ok) {
                    Fail("FFmpeg was installed, but could not be found yet. Restart this app and try again.");
                } else {
                    Fail("The FFmpeg installation did not finish.");
                }
            }
        }

        private void PumpConversion()
        {
            string line;
            while (job != null && job.TryDequeue(out line)) {
                double? duration = PMSDKFfmpeg.ParseDurationSeconds(line);
                if (duration.HasValue && durationSeconds <= 0.0) {
                    durationSeconds = duration.Value;
                }
                double? seconds = PMSDKFfmpeg.ParseProgressSeconds(line);
                if (seconds.HasValue && durationSeconds > 0.0) {
                    progress = Mathf.Clamp01((float)(seconds.Value / durationSeconds));
                    detailText.text = Mathf.RoundToInt(progress * 100f) + "%  -  " + FormatTime(seconds.Value) + " of " + FormatTime(durationSeconds);
                } else if (!seconds.HasValue) {
                    AddLog(line);
                }
            }
            if (job != null && job.IsFinished) {
                bool ok = job.Succeeded;
                job.Dispose();
                job = null;
                if (ok && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0) {
                    Finish();
                } else {
                    TryDeletePartialOutput();
                    Fail("FFmpeg could not convert this file.");
                }
            }
        }

        private void AddLog(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) {
                return;
            }
            logLines.Enqueue(Shorten(line.Trim(), 110));
            while (logLines.Count > MaxLogLines) {
                logLines.Dequeue();
            }
            if (stage == Stage.Installing || stage == Stage.Converting) {
                if (detailText != null && !line.StartsWith("frame=", StringComparison.Ordinal)) {
                    detailText.text = string.Join("\n", logLines.ToArray());
                }
            }
        }

        // ------------------------------------------------------------------ UI

        private void BuildUi()
        {
            canvas = PMSDKUi.CreateCanvas("PMSDK Video Convert", 0, 4000);
            RectTransform root = (RectTransform)canvas.transform;

            RectTransform shade = PMSDKUi.NewRect("Shade", root);
            PMSDKUi.AddImage(shade.gameObject, new Color(0f, 0f, 0f, 0.65f));
            PMSDKUi.Stretch(shade, 0f, 0f, 0f, 0f);

            RectTransform panel = PMSDKUi.NewRect("Dialog", shade);
            PMSDKUi.AddImage(panel.gameObject, PMSDKUi.Panel);
            panel.anchorMin = new Vector2(0.5f, 0.5f);
            panel.anchorMax = new Vector2(0.5f, 0.5f);
            panel.pivot = new Vector2(0.5f, 0.5f);
            panel.sizeDelta = new Vector2(660f, 380f);
            VerticalLayoutGroup vlg = PMSDKUi.AddVertical(panel.gameObject, 20, 8f);
            vlg.childControlHeight = true;

            titleText = PMSDKUi.AddText(panel, "", 20, PMSDKUi.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            PMSDKUi.SetLayout(titleText.gameObject, -1f, 34f, 1f, -1f);

            bodyText = PMSDKUi.AddText(panel, "", 14, PMSDKUi.TextMain, TextAnchor.UpperLeft, FontStyle.Normal);
            bodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PMSDKUi.SetLayout(bodyText.gameObject, -1f, 130f, 1f, -1f);

            RectTransform progressRow = PMSDKUi.NewRect("Progress Row", panel);
            progressRoot = progressRow.gameObject;
            PMSDKUi.SetLayout(progressRoot, -1f, 20f, 1f, -1f);
            RectTransform barBack = PMSDKUi.NewRect("Bar", progressRow);
            PMSDKUi.AddImage(barBack.gameObject, PMSDKUi.Field);
            PMSDKUi.Place(barBack, new Vector2(0f, 0.5f), new Vector2(1f, 0.5f), new Vector2(0f, -7f), new Vector2(-56f, 7f));
            progressFill = PMSDKUi.NewRect("Fill", barBack);
            PMSDKUi.AddImage(progressFill.gameObject, PMSDKUi.Accent);
            progressFill.anchorMin = Vector2.zero;
            progressFill.anchorMax = new Vector2(0f, 1f);
            progressFill.offsetMin = Vector2.zero;
            progressFill.offsetMax = Vector2.zero;
            percentText = PMSDKUi.AddText(progressRow, "0%", 13, PMSDKUi.TextMain, TextAnchor.MiddleRight, FontStyle.Normal);
            PMSDKUi.Place(percentText.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(-52f, 0f), Vector2.zero);

            detailText = PMSDKUi.AddText(panel, "", 12, PMSDKUi.TextDim, TextAnchor.UpperLeft, FontStyle.Normal);
            detailText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PMSDKUi.SetLayout(detailText.gameObject, -1f, 70f, 1f, -1f);

            RectTransform buttonRow = PMSDKUi.NewRect("Buttons", panel);
            PMSDKUi.SetLayout(buttonRow.gameObject, -1f, 38f, 1f, -1f);
            HorizontalLayoutGroup hlg = PMSDKUi.AddHorizontal(buttonRow.gameObject, 0, 8f);
            hlg.childForceExpandWidth = true;
            for (int i = 0; i < buttons.Length; i++) {
                int index = i;
                buttons[i] = PMSDKUi.AddButton(buttonRow, "", delegate { OnButton(index); });
                PMSDKUi.SetLayout(buttons[i].gameObject, -1f, 38f, 1f, -1f);
                buttonLabels[i] = buttons[i].GetComponentInChildren<Text>();
            }
        }

        private void Present(string title, string body, float progressValue, string detail,
            string label0, Action action0, string label1, Action action1, string label2, Action action2)
        {
            SetVisible(true);
            titleText.text = title;
            bodyText.text = body;
            progressRoot.SetActive(progressValue >= 0f);
            progress = Mathf.Max(0f, progressValue);
            SetProgress(progress);
            detailText.text = detail;
            SetButton(0, label0, action0);
            SetButton(1, label1, action1);
            SetButton(2, label2, action2);
            PMSDKUi.SetButtonActive(buttons[0], action0 != null);
        }

        private void SetButton(int index, string label, Action action)
        {
            buttons[index].gameObject.SetActive(action != null);
            buttonActions[index] = action;
            if (label != null) {
                buttonLabels[index].text = label;
            }
        }

        private void OnButton(int index)
        {
            Action action = buttonActions[index];
            if (action != null) {
                action();
            }
        }

        private void SetProgress(float value)
        {
            if (progressFill == null) {
                return;
            }
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(value), 1f);
            percentText.text = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f) + "%";
        }

        private void SetVisible(bool visible)
        {
            if (canvas != null) {
                canvas.enabled = visible;
            }
            if (visible) {
                PauseInputs();
            }
        }

        // Block the skin editor's canvas/keyboard handling while the dialog is open.
        private void PauseInputs()
        {
            if (pausedInputs.Count > 0) {
                return;
            }
            PMSDKSkinEditorInput[] inputs = FindObjectsByType<PMSDKSkinEditorInput>(FindObjectsSortMode.None);
            for (int i = 0; i < inputs.Length; i++) {
                if (inputs[i].InputEnabled) {
                    inputs[i].InputEnabled = false;
                    pausedInputs.Add(inputs[i]);
                }
            }
        }

        private void ResumeInputs()
        {
            for (int i = 0; i < pausedInputs.Count; i++) {
                if (pausedInputs[i] != null) {
                    pausedInputs[i].InputEnabled = true;
                }
            }
            pausedInputs.Clear();
        }

        private static string Shorten(string text, int max)
        {
            if (string.IsNullOrEmpty(text)) {
                return "unknown error";
            }
            string single = text.Replace('\r', ' ').Replace('\n', ' ');
            return single.Length <= max ? single : single.Substring(0, max - 1) + "...";
        }

        private static string FormatTime(double seconds)
        {
            TimeSpan t = TimeSpan.FromSeconds(Math.Max(0.0, seconds));
            return t.TotalHours >= 1.0 ? t.ToString(@"h\:mm\:ss") : t.ToString(@"m\:ss");
        }
    }
}
