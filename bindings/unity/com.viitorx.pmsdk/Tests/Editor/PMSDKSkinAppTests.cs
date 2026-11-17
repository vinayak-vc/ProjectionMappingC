using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using vxpmsdk.Components;

namespace vxpmsdk.Tests
{
    /// <summary>EditMode tests for the Skin App additions: the polygon-tool regression and the
    /// FFmpeg conversion helpers (parsing, command building, locating, scripts).</summary>
    public class PMSDKSkinAppTests
    {
        private GameObject projGO;
        private GameObject ctrlGO;
        private PMSDKSkinEditorController controller;
        private string tempDir;

        [SetUp]
        public void Setup()
        {
            projGO = new GameObject("TestProjector", typeof(Camera));
            projGO.AddComponent<PMSDKProjector>();
            ctrlGO = new GameObject("SkinEditor");
            controller = ctrlGO.AddComponent<PMSDKSkinEditorController>();
            controller.Projector = projGO.GetComponent<PMSDKProjector>();
            controller.SkinLayerName = "TransparentFX";
            controller.enabled = false;
            controller.enabled = true;

            tempDir = Path.Combine(Path.GetTempPath(), "pmsdk_tests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
        }

        [TearDown]
        public void Teardown()
        {
            foreach (PMSDKSkin s in UnityEngine.Object.FindObjectsByType<PMSDKSkin>(FindObjectsSortMode.None)) {
                UnityEngine.Object.DestroyImmediate(s.gameObject);
            }
            if (ctrlGO != null) {
                UnityEngine.Object.DestroyImmediate(ctrlGO);
            }
            if (projGO != null) {
                UnityEngine.Object.DestroyImmediate(projGO);
            }
            if (Directory.Exists(tempDir)) {
                Directory.Delete(tempDir, true);
            }
        }

        // ---- polygon tool regression ----

        private void DrawTriangle(Vector2 offset)
        {
            controller.AppendPolygonVertex(new Vector2(0.2f, 0.2f) + offset);
            controller.AppendPolygonVertex(new Vector2(0.4f, 0.2f) + offset);
            controller.AppendPolygonVertex(new Vector2(0.3f, 0.4f) + offset);
        }

        [Test]
        public void Polygon_SwitchToSelectAndBack_StartsANewPolygon()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            DrawTriangle(Vector2.zero);
            Assert.AreEqual(1, controller.Skins.Count);

            // User leaves the polygon tool (to move points) and returns to draw another.
            controller.PickTool(PMSDKSkinEditorController.Tool.Select);
            Assert.IsFalse(controller.IsDrawingPolygon);
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            DrawTriangle(new Vector2(0.4f, 0.3f));

            Assert.AreEqual(2, controller.Skins.Count, "the second polygon must be a new skin");
            foreach (PMSDKSkin skin in controller.Skins) {
                Assert.AreEqual(3, skin.Vertices.Count);
            }
        }

        [Test]
        public void Polygon_SelectingAnotherSkinMidDraw_StartsANewPolygon()
        {
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            DrawTriangle(Vector2.zero);
            PMSDKSkin first = controller.Selected;
            controller.PickTool(PMSDKSkinEditorController.Tool.Rectangle);
            controller.AddDefaultShape();
            controller.PickTool(PMSDKSkinEditorController.Tool.Polygon);
            controller.AppendPolygonVertex(new Vector2(0.7f, 0.7f));

            Assert.AreEqual(3, controller.Skins.Count);
            Assert.AreEqual(3, first.Vertices.Count, "the first polygon must not receive the new point");
        }

        // ---- media types ----

        [Test]
        public void Media_RecognisesFormatsTheConverterCanFix()
        {
            Assert.IsTrue(PMSDKSkinMedia.IsVideoFile(@"C:\a\clip.MKV"));
            Assert.IsTrue(PMSDKSkinMedia.IsVideoFile("clip.flv"));
            Assert.IsTrue(PMSDKSkinMedia.IsVideoFile("clip.mp4"));
            Assert.IsFalse(PMSDKSkinMedia.IsVideoFile("notes.txt"));
        }

        // ---- ffmpeg parsing ----

        [Test]
        public void Ffmpeg_ParseDuration()
        {
            Assert.AreEqual(83.45, PMSDKFfmpeg.ParseDurationSeconds("  Duration: 00:01:23.45, start: 0.0, bitrate: 900 kb/s").Value, 1e-6);
            Assert.AreEqual(3725.0, PMSDKFfmpeg.ParseDurationSeconds("Duration: 01:02:05.00,").Value, 1e-6);
            Assert.IsNull(PMSDKFfmpeg.ParseDurationSeconds("Stream #0:0: Video: h264"));
            Assert.IsNull(PMSDKFfmpeg.ParseDurationSeconds(null));
        }

        [Test]
        public void Ffmpeg_ParseProgressSeconds()
        {
            Assert.AreEqual(2.5, PMSDKFfmpeg.ParseProgressSeconds("out_time_us=2500000").Value, 1e-9);
            Assert.AreEqual(1.5, PMSDKFfmpeg.ParseProgressSeconds("out_time_ms=1500000").Value, 1e-9);
            Assert.IsNull(PMSDKFfmpeg.ParseProgressSeconds("out_time_us=N/A"));
            Assert.IsNull(PMSDKFfmpeg.ParseProgressSeconds("frame=12"));
        }

        [Test]
        public void Ffmpeg_ParseInstallerMarkers()
        {
            int percent;
            string message;
            Assert.IsTrue(PMSDKFfmpeg.TryParseProgressMarker("PMSDK_PROGRESS 42 Downloading FFmpeg 40 of 109 MB", out percent, out message));
            Assert.AreEqual(42, percent);
            Assert.AreEqual("Downloading FFmpeg 40 of 109 MB", message);
            Assert.IsTrue(PMSDKFfmpeg.TryParseProgressMarker("PMSDK_PROGRESS 250", out percent, out message));
            Assert.AreEqual(100, percent, "clamped");
            Assert.IsFalse(PMSDKFfmpeg.TryParseProgressMarker("PMSDK_PROGRESS abc", out percent, out message));
            Assert.IsFalse(PMSDKFfmpeg.TryParseProgressMarker("something else", out percent, out message));

            Assert.AreEqual(42, PMSDKFfmpeg.ParsePercent("  Downloading   42%"));
            Assert.AreEqual(-1, PMSDKFfmpeg.ParsePercent("version 1.5"));
            Assert.AreEqual(-1, PMSDKFfmpeg.ParsePercent("150%"));
        }

        // ---- ffmpeg command / files ----

        [Test]
        public void Ffmpeg_ConvertArguments_KeepPathsAsSingleArguments()
        {
            string input = @"C:\My Videos\clip one.mkv";
            string output = @"C:\My Videos\clip one_pmsdk.mp4";
            List<string> hd = PMSDKFfmpeg.BuildConvertArguments(input, output, PMSDKConvertPreset.Standard);
            Assert.Contains(input, hd);
            Assert.AreEqual(output, hd[hd.Count - 1]);
            Assert.Contains("libx264", hd);
            Assert.Contains("scale='min(1920,iw)':-2", hd);

            List<string> light = PMSDKFfmpeg.BuildConvertArguments(input, output, PMSDKConvertPreset.Light);
            Assert.Contains("scale='min(1280,iw)':-2", light);
        }

        [Test]
        public void Ffmpeg_OutputPath_NeverOverwritesAndNeverEqualsTheSource()
        {
            string input = Path.Combine(tempDir, "my video.mkv");
            File.WriteAllText(input, "x");
            string first = PMSDKFfmpeg.BuildOutputPath(input);
            Assert.AreEqual(Path.Combine(tempDir, "my video_pmsdk.mp4"), first);

            File.WriteAllText(first, "x");
            string second = PMSDKFfmpeg.BuildOutputPath(input);
            Assert.AreEqual(Path.Combine(tempDir, "my video_pmsdk_2.mp4"), second);
        }

        [Test]
        public void Ffmpeg_FindExecutable_FindsFileAndSkipsBadEntries()
        {
            string bin = Path.Combine(tempDir, "bin");
            Directory.CreateDirectory(bin);
            string exe = Path.Combine(bin, "ffmpeg.exe");
            File.WriteAllText(exe, "x");

            List<string> dirs = new List<string> { null, "", "   ", "<>|invalid", Path.Combine(tempDir, "missing"), bin };
            Assert.AreEqual(exe, PMSDKFfmpeg.FindExecutable(dirs, "ffmpeg.exe"));
            Assert.IsNull(PMSDKFfmpeg.FindExecutable(new List<string> { Path.Combine(tempDir, "missing") }, "ffmpeg.exe"));
        }

        [Test]
        public void Ffmpeg_InstallScripts_AreWrittenWithProgressAndChecksum()
        {
            string bat = PMSDKFfmpeg.WriteInstallScripts(tempDir);
            Assert.IsTrue(File.Exists(bat));
            string batText = File.ReadAllText(bat);
            Assert.IsTrue(batText.Contains("winget install --id Gyan.FFmpeg"));
            Assert.IsTrue(batText.Contains("PMSDK_PROGRESS"));
            Assert.IsTrue(batText.Contains("--dry-run"));

            string ps1 = File.ReadAllText(Path.Combine(tempDir, "PMSDK_install_ffmpeg.ps1"));
            Assert.IsTrue(ps1.Contains("Get-FileHash"), "the download is verified");
            Assert.IsTrue(ps1.Contains("SetEnvironmentVariable('Path'"), "PATH is set for the user");
            Assert.IsTrue(ps1.Contains("'User'"), "user scope only, no admin");
            Assert.IsTrue(ps1.StartsWith("param([switch]$DryRun)"));
        }
    }
}
