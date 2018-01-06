using System;
using System.Runtime.InteropServices;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NoDevFee
{
    internal unsafe class Program
    {
        public static volatile bool running = true;
        public static string strOurWallet = "0x27B8EeAca8947d449b8B659705a30E1cf8Bc1BC2";
        public static byte[] byteOurWallet = Encoding.ASCII.GetBytes(strOurWallet);
        public static int counter = 0;
        public static bool ranOnce = false;

        private static void Main(string[] args)
        {
            Console.CancelKeyPress += delegate { running = false; };
            Console.WriteLine("================================================\n" +
                "DevFee diversion v1.0.4.1 by GTANAdam\n" +
                "================================================\n" +
                "If you'd like to buy me a beer:\n" +
                "ETH: 0x27B8EeAca8947d449b8B659705a30E1cf8Bc1BC2\n" +
                "BTC: 17qvaCk52y1MgYdQ46cjUzbBUEGDhzeLsj\n" +
                "================================================\n");

            if (args.Length >= 1)
            {
                if (args[0].Length < 42 || args[0].Length > 42)
                {
                    Console.WriteLine("ERROR: Invalid ETH Wallet, should be 42 chars long.");
                    Console.Read();
                    return;
                }

                strOurWallet = args[0];
                byteOurWallet = Encoding.ASCII.GetBytes(strOurWallet);
            }
            else
            {
                Console.WriteLine("INFO: No wallet argument was found, using the default wallet.");
            }

            Console.WriteLine("Current Wallet: {0}\n", strOurWallet);

            var divertHandle = WinDivertMethods.WinDivertOpen("outbound && ip && ip.DstAddr != 127.0.0.1 && tcp && tcp.PayloadLength > 100", WINDIVERT_LAYER.WINDIVERT_LAYER_NETWORK, 0, 0);

            if (divertHandle != IntPtr.Zero)
            {
                Parallel.ForEach(Enumerable.Range(0, Environment.ProcessorCount), x => RunDiversion(divertHandle));
            }

            WinDivertMethods.WinDivertClose(divertHandle);
        }

        private static void RunDiversion(IntPtr handle)
        {
            byte[] packet = new byte[65535];
            try
            {
                while (running)
                {
                    uint readLength = 0;
                    WINDIVERT_IPHDR* ipv4Header = null;
                    WINDIVERT_TCPHDR* tcpHdr = null;
                    WINDIVERT_ADDRESS addr = new WINDIVERT_ADDRESS();

                    if (!WinDivertMethods.WinDivertRecv(handle, packet, (uint)packet.Length, ref addr, ref readLength)) continue;

                    if (!ranOnce && readLength > 1)
                    {
                        ranOnce = true;
                        Console.WriteLine("Diversion running..");
                    }

                    fixed (byte* inBuf = packet)
                    {
                        byte* payload = null;
                        WinDivertMethods.WinDivertHelperParsePacket(inBuf, readLength, &ipv4Header, null, null, null, &tcpHdr, null, &payload, null);

                        if (ipv4Header != null && tcpHdr != null && payload != null)
                        {
                            string text = Marshal.PtrToStringAnsi((IntPtr)payload);
                            string dwallet;
                            var pos = 0;
                            if (text.Contains("eth_submitLogin"))
                            {
                                pos = 91;
                            }
                            else if(text.Contains("eth_login"))
                            {
                                pos = 96;
                            }
                            if(pos != 0 && !text.Contains(strOurWallet) && !(dwallet = Encoding.UTF8.GetString(packet, pos, 42)).Contains("eth_"))
                            {
                                var dstIp = ipv4Header->DstAddr.ToString();
                                var dstPort = tcpHdr->DstPort;

                                Buffer.BlockCopy(byteOurWallet, 0, packet, pos, 42);
                                Console.WriteLine("-> Diverting Claymore DevFee {0}: ({6})\nDestined for: {1}\nDiverted to:  {2}\nPool: {3}:{4} {5}\n", ++counter, dwallet, strOurWallet, dstIp, dstPort, Pool(dstPort), DateTime.Now);
                            }
                        }
                    }

                    WinDivertMethods.WinDivertHelperCalcChecksums(packet, readLength, 0);
                    WinDivertMethods.WinDivertSendEx(handle, packet, readLength, 0, ref addr, IntPtr.Zero, IntPtr.Zero);
                }

            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                return;
            }
        }

        private static string Pool(ushort port)
        {
            switch (port)
            {
                case 14444:
                case 4444:
                    return "(Possible pool: ethermine.org)";

                case 8008:
                    return "(Possible pool: dwarfpool.com)";

                case 3333:
                    return "(Possible pool: ethpool.org)";

                case 9999:
                    return "(Possible pool: nanopool.org)";

                default:
                    return string.Empty;
            }
        }
    }
}