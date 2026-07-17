using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

namespace vxpmsdk.Components
{
    /// <summary>
    /// OSC remote control for the calibration manager — drive it from QLab, TouchOSC,
    /// Chataigne, a lighting desk, or anything that speaks OSC over UDP.
    ///
    /// Address space (args in parentheses):
    ///   /pmsdk/calibration (i)        1 = enter calibration mode, 0 = exit
    ///   /pmsdk/save                   save calibration now
    ///   /pmsdk/surface (i)            select surface index (0-based)
    ///   /pmsdk/corner (i)             select corner 0=TL 1=TR 2=BR 3=BL
    ///   /pmsdk/corner/nudge (f f)     nudge selected corner (normalized delta)
    ///   /pmsdk/corner/set (f f)       set selected corner (normalized position)
    ///   /pmsdk/blend (f f f f)        set L R T B edge widths on the selected surface
    ///   /pmsdk/blend/gamma (f)        set blend gamma on the selected surface
    ///   /pmsdk/blend/black (f)        set black level on the selected surface
    ///   /pmsdk/testpattern (i)        0/1 selected surface, 2 = toggle all
    ///   /pmsdk/autoalign (i)          0 = selected surface, 1 = all
    ///   /pmsdk/preset/save (s)        save named preset
    ///   /pmsdk/preset/load (s)        load named preset
    ///   /pmsdk/ab                     A/B swap with the pre-preset state
    ///
    /// Packets are received on a background thread and dispatched on the main thread
    /// in Update (Unity API is not thread-safe).
    /// </summary>
    public class PMSDKOscServer : MonoBehaviour
    {
        [Tooltip("UDP port to listen on.")]
        public int Port = 9000;
        [Tooltip("Calibration manager to drive. Auto-found if left empty.")]
        public PMSDKCalibrationManager Manager;
        [Tooltip("Log every handled message.")]
        public bool LogMessages = false;

        public int PacketsReceived { get; private set; }
        public string LastAddress { get; private set; } = "";

        private UdpClient udp;
        private Thread receiveThread;
        private volatile bool running;
        private readonly ConcurrentQueue<byte[]> queue = new ConcurrentQueue<byte[]>();

        private void OnEnable()
        {
            if (Manager == null) Manager = FindFirstObjectByType<PMSDKCalibrationManager>();
            try
            {
                udp = new UdpClient(Port);
                running = true;
                receiveThread = new Thread(ReceiveLoop) { IsBackground = true, Name = "PMSDK OSC" };
                receiveThread.Start();
                Debug.Log($"[PMSDK] OSC server listening on UDP {Port}.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[PMSDK] OSC server failed to bind port {Port}: {e.Message}");
                udp = null;
            }
        }

        private void OnDisable()
        {
            running = false;
            try { udp?.Close(); } catch { }
            udp = null;
            receiveThread = null;
        }

        private void ReceiveLoop()
        {
            var any = new IPEndPoint(IPAddress.Any, 0);
            while (running)
            {
                try
                {
                    byte[] data = udp.Receive(ref any);
                    if (data != null && data.Length > 0) queue.Enqueue(data);
                }
                catch
                {
                    // socket closed / interrupted — loop exits via `running`
                    if (!running) return;
                }
            }
        }

        private void Update()
        {
            while (queue.TryDequeue(out byte[] data))
            {
                var msg = PMSDKOsc.Parse(data, data.Length);
                if (msg == null) continue;
                PacketsReceived++;
                LastAddress = msg.Address;
                if (LogMessages) Debug.Log($"[PMSDK] OSC {msg.Address} ({msg.Args.Count} args)");
                Dispatch(msg);
            }
        }

        /// <summary>Public so the dispatch table is unit/loopback testable without sockets.</summary>
        public void Dispatch(PMSDKOsc.Message msg)
        {
            if (Manager == null) return;
            switch (msg.Address)
            {
                case "/pmsdk/calibration":
                    if (msg.GetInt(0) != 0) Manager.EnterCalibration(); else Manager.ExitCalibration();
                    break;
                case "/pmsdk/save":
                    Manager.SaveNow();
                    break;
                case "/pmsdk/surface":
                    Manager.SetSelectedSurface(msg.GetInt(0));
                    break;
                case "/pmsdk/corner":
                    Manager.SetSelectedCorner(msg.GetInt(0));
                    break;
                case "/pmsdk/corner/nudge":
                    Manager.NudgeSelectedCorner(new Vector2(msg.GetFloat(0), msg.GetFloat(1)));
                    break;
                case "/pmsdk/corner/set":
                    Manager.SetSelectedCornerPosition(new Vector2(msg.GetFloat(0), msg.GetFloat(1)));
                    break;
                case "/pmsdk/blend":
                {
                    var s = Manager.Selected;
                    if (s?.Blend != null)
                    {
                        s.Blend.LeftEdge = Mathf.Clamp01(msg.GetFloat(0));
                        s.Blend.RightEdge = Mathf.Clamp01(msg.GetFloat(1));
                        s.Blend.TopEdge = Mathf.Clamp01(msg.GetFloat(2));
                        s.Blend.BottomEdge = Mathf.Clamp01(msg.GetFloat(3));
                    }
                    break;
                }
                case "/pmsdk/blend/gamma":
                {
                    var s = Manager.Selected;
                    if (s?.Blend != null) s.Blend.Gamma = Mathf.Clamp(msg.GetFloat(0, 2.2f), 0.5f, 4f);
                    break;
                }
                case "/pmsdk/blend/black":
                {
                    var s = Manager.Selected;
                    if (s?.Blend != null) s.Blend.BlackLevel = Mathf.Clamp(msg.GetFloat(0), 0f, 0.5f);
                    break;
                }
                case "/pmsdk/testpattern":
                {
                    int v = msg.GetInt(0);
                    if (v == 2) Manager.ToggleTestPattern(all: true);
                    else
                    {
                        var s = Manager.Selected;
                        if (s?.TestPattern != null) s.TestPattern.enabled = v != 0;
                    }
                    break;
                }
                case "/pmsdk/autoalign":
                    Manager.StartAutoAlign(all: msg.GetInt(0) != 0);
                    break;
                case "/pmsdk/preset/save":
                    Manager.SavePreset(msg.GetString(0, "slot1"));
                    break;
                case "/pmsdk/preset/load":
                    Manager.LoadPreset(msg.GetString(0, "slot1"));
                    break;
                case "/pmsdk/ab":
                    Manager.ToggleAB();
                    break;
            }
        }
    }
}
