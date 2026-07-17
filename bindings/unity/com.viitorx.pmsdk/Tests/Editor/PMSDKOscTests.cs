using NUnit.Framework;

namespace vxpmsdk.Tests
{
    /// <summary>OSC codec regression tests (build → parse roundtrips + malformed input).</summary>
    public class PMSDKOscTests
    {
        [Test]
        public void Osc_RoundtripIntFloatString()
        {
            byte[] pkt = vxpmsdk.Components.PMSDKOsc.Build("/pmsdk/corner/set", 0.25f, 0.75f);
            var msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.IsNotNull(msg);
            Assert.AreEqual("/pmsdk/corner/set", msg.Address);
            Assert.AreEqual(2, msg.Args.Count);
            Assert.AreEqual(0.25f, msg.GetFloat(0), 1e-6f);
            Assert.AreEqual(0.75f, msg.GetFloat(1), 1e-6f);

            pkt = vxpmsdk.Components.PMSDKOsc.Build("/pmsdk/surface", 3);
            msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.AreEqual(3, msg.GetInt(0));

            pkt = vxpmsdk.Components.PMSDKOsc.Build("/pmsdk/preset/load", "night show");
            msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.AreEqual("night show", msg.GetString(0));
        }

        [Test]
        public void Osc_AddressOnlyMessage()
        {
            byte[] pkt = vxpmsdk.Components.PMSDKOsc.Build("/pmsdk/ab");
            var msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.IsNotNull(msg);
            Assert.AreEqual("/pmsdk/ab", msg.Address);
            Assert.AreEqual(0, msg.Args.Count);
        }

        [Test]
        public void Osc_ArgTypeCoercion()
        {
            // int requested as float and vice versa must coerce, not throw.
            byte[] pkt = vxpmsdk.Components.PMSDKOsc.Build("/x", 2);
            var msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.AreEqual(2f, msg.GetFloat(0), 1e-6f);

            pkt = vxpmsdk.Components.PMSDKOsc.Build("/x", 1.9f);
            msg = vxpmsdk.Components.PMSDKOsc.Parse(pkt, pkt.Length);
            Assert.AreEqual(1, msg.GetInt(0));
        }

        [Test]
        public void Osc_MalformedPacketsReturnNull()
        {
            Assert.IsNull(vxpmsdk.Components.PMSDKOsc.Parse(null, 0));
            Assert.IsNull(vxpmsdk.Components.PMSDKOsc.Parse(new byte[] { 1, 2, 3 }, 3));
            // not starting with '/'
            var junk = System.Text.Encoding.ASCII.GetBytes("#bundle\0\0\0\0\0");
            Assert.IsNull(vxpmsdk.Components.PMSDKOsc.Parse(junk, junk.Length));
            // truncated payload: declares a float but no bytes follow
            byte[] good = vxpmsdk.Components.PMSDKOsc.Build("/x", 1.0f);
            Assert.IsNull(vxpmsdk.Components.PMSDKOsc.Parse(good, good.Length - 4));
        }
    }
}
