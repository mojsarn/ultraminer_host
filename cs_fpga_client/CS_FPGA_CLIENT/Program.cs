using System;

namespace CS_FPGA_CLIENT
{
    class Program
    {

        public static string appName = "GaggaMiner";
        public static string appVersion = "0.1";
        public static System.IO.StreamWriter logfile = null;

        static void Main(string[] args)
        {
            /*
            uint[] test_w32 = new uint[] { 0x20000000, 0xBA9E63D7, 0xD299031B, 0x6D70DED5, 0x94F2554E, 0xF31C3B49, 0x403F0E2C, 0x001D7B2D, 0x00000000, 0x3BEE91CB, 0x7391EBD9, 0x80B250E2, 0x3EB170B2, 0xB4C02449, 0x5B11A174, 0x0B2006E3, 0x6106F5B4, 0x5B208526, 0x1B62DA70};
            byte[] test = new byte[64];
            ulong[] h = new ulong[] { 0x4903ADFF749C51CE, 0x0D95DE399746DF03, 0x8FD1934127C79BCE, 0x9A255629FF352CB1, 0x5DB62599DF6CA7B0, 0xEABE394CA9D5C3F4, 0x991112C71A75B523, 0xAE18A40B660FCC33, 0xcab2076d98173ec4 };
            ulong[] t = new ulong[] { 64, 0x7000000000000000, 0x7000000000000040 };
            Buffer.BlockCopy(test_w32, 0, test, 0, 64);
            skein_ubi_big(test, h, t);
            h[8] = h[0] ^ h[1] ^ h[2] ^ h[3] ^ h[4] ^ h[5] ^ h[6] ^ h[7] ^ 0x1BD11BDAA9FC1A22;
            */

            try
            {
                try
                {
                    logfile = new System.IO.StreamWriter("log.txt");
                }
                catch (Exception) { }
                Settings.loadSettings();
                Settings.storeSettings();

                if (Settings.minerType.ToLower() == "binfpga")
                {
                    switch (Settings.algo.ToLower())
                    {
                        case "keccakc":
                            startKeccakC();
                            break;
                        case "skein":
                            startSkein();
                            break;
                        case "pascal":
                            startPascal();
                            break;
                        default:
                            Logger("Unkown or unsupported algo:'" + Settings.algo + "' for type:'" + Settings.minerType + "'");
                            break;
                    }
                }
                else if (Settings.minerType.ToLower() == "textfpga")
                {
                    switch (Settings.algo.ToLower())
                    {
                        case "neoscrypt":
                            startTextNeoscrypt();
                            break;
                        default:
                            Logger("Unkown or unsupported algo:'" + Settings.algo + "' for type:'" + Settings.minerType + "'");
                            break;
                    }
                }
                else
                {
                    Logger("Unkown miner type '" + Settings.minerType + "'");
                }

                // Xilinx Zynq
                //PascalTextFPGADevice pd = new PascalTextFPGADevice(0, "COM4");
                //PascalStratum ps = new PascalStratum("mine.pasl.fairpool.xyz", 4009, "3GhhbouHErugNkWu3busmnweQbDNudi5MX65osoUhicFEHH84CYe3L4TddttqF37eCLnwQQ38qmx46vKTayvfe6A2FzRHw7oRNaFki+FP", "x", "fairpool");

                // Polarfire
                //PascalBinaryFPGADevice pd = new PascalBinaryFPGADevice(0, "COM16");
                //PascalStratum ps = new PascalStratum("mine.pasl.fairpool.xyz", 4009, "3GhhbouHErugNkWu3busmnweQbDNudi5MX65osoUhicFEHH84CYe3L4TddttqF37eCLnwQQ38qmx46vKTayvfe6A2FzRHw7oRNaFki+BurningBear2", "x", "fairpool");

                //PascalMiner pm = new PascalMiner(ps, pd, pd);
                //pm.workLoop();                


                //KEccakcTextFPGADevice ktfpga = new KEccakcTextFPGADevice(0, "COM4");                
                //KeccakBinaryFPGADevice ktfpga = new KeccakBinaryFPGADevice(0, "COM7");
                //KeccakCStratum kcs = new KeccakCStratum("us.stratum.rapidpools.com", 3032, "CHEfitQ9wJ84EhxHjTLLvyCUCtZ1c3tbZW.PF2", "x", "creapool");
                //KeccakMiner km = new KeccakMiner(kcs, ktfpga, ktfpga);
                //km.workLoop();

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public static void startKeccakC()
        {
            KeccakBinaryFPGADevice ktfpga = new KeccakBinaryFPGADevice(0, Settings.comPort);
            StandardStratum kcs = new StandardStratum(Settings.poolAddr, Settings.poolPort, Settings.poolUser, Settings.poolPass, Settings.poolName, Settings.algo);
            KeccakMiner km = new KeccakMiner(kcs, ktfpga, ktfpga);
            km.workLoop();
        }

        public static void startSkein()
        {
            SkeinBinaryFPGADevice ktfpga = new SkeinBinaryFPGADevice(0, Settings.comPort);
            StandardStratum sks = new StandardStratum(Settings.poolAddr, Settings.poolPort, Settings.poolUser, Settings.poolPass, Settings.poolName, Settings.algo);
            SkeinMiner skm = new SkeinMiner(sks, ktfpga, ktfpga);
            skm.workLoop();
        }

        public static void startPascal()
        {
            PascalBinaryFPGADevice pd = new PascalBinaryFPGADevice(0, Settings.comPort);
            PascalStratum ps = new PascalStratum(Settings.poolAddr, Settings.poolPort, Settings.poolUser, Settings.poolPass, Settings.poolName);
            PascalMiner pm = new PascalMiner(ps, pd, pd);
            pm.workLoop();
        }

        public static void startTextNeoscrypt()
        {
            NeoScryptTextFPGADevice text_nsrpt = new NeoScryptTextFPGADevice(0, Settings.comPort);
            NeoScryptStratum nss = new NeoScryptStratum(Settings.poolAddr, Settings.poolPort, Settings.poolUser, Settings.poolPass, Settings.poolName);
            NeoScryptMiner nsm = new NeoScryptMiner(nss, text_nsrpt, text_nsrpt);
            nsm.workLoop();
        }

        public static void Logger(string line)
        {
            line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.ffff") + " [" + System.Threading.Thread.CurrentThread.ManagedThreadId + "] " + line;
            Console.WriteLine(line);
            if (logfile != null)
            {
                logfile.WriteLine(line);
                logfile.Flush();
            }
        }

    }
}
