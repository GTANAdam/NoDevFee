using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using WinDivert;

namespace NoDevFee
{
    internal class Program
    {
        private static string strOurWallet = "0x0000000000000000000000000000000000000000";
        private static string poolAddress = "eu1.ethermine.org";
        private static string poolPort = "4444";

        private static byte[] byteOurWallet = Encoding.ASCII.GetBytes(strOurWallet);
        private static int counter = 0;
        private static IntPtr DivertHandle;
        private static bool running = true;

        private static void Main(string[] args)
        {
            Console.WriteLine("Init..");

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

            InstallWinDivert();

            var hosts = Dns.GetHostAddresses(poolAddress);

            // Create filter
            var filter = $"!loopback and outbound && ip && tcp && tcp.PayloadLength > 0 && ip.DstAddr == {hosts[0]} && tcp.DstPort == {poolPort}";

            // Check filter 
            var ret = WinDivertNative.WinDivertHelperCompileFilter(filter, WinDivertNative.WinDivertLayer.Network, IntPtr.Zero, 0, out IntPtr errStrPtr, out uint errPos);
            if (!ret)
            {
                var errStr = Marshal.PtrToStringAnsi(errStrPtr);
                throw new Exception($"Filter string is invalid at position {errPos}\n{errStr}");
            }

            // Open new handle
            DivertHandle = WinDivertNative.WinDivertOpen(filter, WinDivertNative.WinDivertLayer.Network, 0, 0);

            // Check handle is null
            if (DivertHandle == IntPtr.Zero) return;

            Console.CancelKeyPress += delegate { running = false; };

            Console.WriteLine("Listening..");
            Divert();

            WinDivertNative.WinDivertClose(DivertHandle);
        }

        private unsafe static void Divert()
        {
            // Allocate buffer
            var buffer = new byte[4096];
            try
            {
                fixed (byte* p = buffer)
                {
                    var ptr = new IntPtr(p);

                    while (running)
                    {
                        // Receive data
                        WinDivertNative.WinDivertRecv(DivertHandle, ptr, (uint)buffer.Length, out uint readLen, out WinDivertNative.Address addr);

                        // Process captured packet
                        var changed = ProcessPacket(buffer, readLen);

                        // Recalculate checksum
                        if (changed) WinDivertNative.WinDivertHelperCalcChecksums(ptr, readLen, 0);
                        WinDivertNative.WinDivertSend(DivertHandle, ptr, readLen, out var pSendLen, ref addr);
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.ToString());
                Console.ReadLine();
                return;
            }
        }

        // Returns true if the packet was altered with
        private static bool ProcessPacket(byte[] buffer, uint length)
        {
            var content = Encoding.ASCII.GetString(buffer, 0, (int)length);

            string dwallet;
            var pos = 0;

            if (content.Contains("eth_submitLogin"))
            {
                pos = 91;
            }
            else if (content.Contains("eth_login"))
            {
                pos = 96;
            }

            if (pos != 0 && !content.Contains(strOurWallet) && !(dwallet = Encoding.UTF8.GetString(buffer, pos, 42)).Contains("eth_"))
            {
                Buffer.BlockCopy(byteOurWallet, 0, buffer, pos, 42);
                Console.WriteLine("-> Diverting Claymore DevFee {0}: ({6})\nDestined for: {1}\n", ++counter, dwallet, strOurWallet, DateTime.Now);
                return true;
            }

            return false;
        }

        private static void InstallWinDivert()
        {
            var path = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
            var version = "2.2.0";
            var arch = IntPtr.Size == 8 ? "x64" : "x86";
            var driverPath = Path.Combine(path, $"WinDivert-{version}-A\\" + arch);

            // Download driver if not already there
            if (!File.Exists(driverPath + "/WinDivert.dll"))
            {
                Console.WriteLine("Installing driver..");

                var zipFile = Path.Combine(path, "windivert.zip");
                using (var client = new WebClient())
                {
                    ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                    client.DownloadFile($"https://github.com/basil00/Divert/releases/download/v{version}/WinDivert-{version}-A.zip", zipFile);
                }
                ZipFile.ExtractToDirectory(zipFile, path);
            }

            // Patch PATH env
            var oldPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            string newPath = oldPath + Path.PathSeparator + driverPath;
            Environment.SetEnvironmentVariable("PATH", newPath);
        }
    }
}