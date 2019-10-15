using System;
using System.Collections.Generic;
using System.Text;
using System.IO;


namespace CS_FPGA_CLIENT
{
    public class Settings
    {
        public static string algo = "KeccakC";
        public static string minerType = "binFPGA";
        public static string comPort = "COM13";
        public static string poolAddr = "us.stratum.rapidpools.com";
        public static int poolPort = 3032;
        public static string poolUser = "CHEfitQ9wJ84EhxHjTLLvyCUCtZ1c3tbZW.PF";
        public static string poolPass = "x";
        public static string poolName = "Creapool";


        public static ASCIIEncoding e = new ASCIIEncoding();
        private static string fileName = "settings.cfg";

        public static void loadSettings()
        {
            try
            {
                string[] lines;
                lines = File.ReadAllLines(fileName, e);
                char[] deliminators = { ':' };
                int i, ito = lines.Length;
                for (i = 0; i < ito; i++)
                {
                    string[] strs = lines[i].Split(deliminators);
                    if (strs.Length == 2)
                    {
                        switch (strs[0].ToLower())
                        {
                            case "algo":
                                algo = strs[1];
                                break;
                            case "minertype":
                                minerType = strs[1];
                                break;
                            case "comport":
                                comPort = strs[1];
                                break;
                            case "pooladdr":
                                poolAddr = strs[1];
                                break;
                            case "poolport":
                                poolPort = int.Parse(strs[1]);
                                break;
                            case "pooluser":
                                poolUser = strs[1];
                                break;
                            case "poolpass":
                                poolPass = strs[1];
                                break;
                            case "poolname":
                                poolName = strs[1];
                                break;
                        }
                    }
                }
            }
            catch (IOException) { }
        }


        private static void store(System.IO.FileStream fs, string name, string var)
        {
            byte[] d = e.GetBytes(name + ":" + var + Environment.NewLine);
            fs.Write(d, 0, d.Length);
        }

        public static void storeSettings()
        {
            try
            {
                if (System.IO.File.Exists(fileName))
                    System.IO.File.Delete(fileName);

                using (System.IO.FileStream fs = System.IO.File.Create(fileName, 1024))
                {
                    store(fs, "algo", algo);
                    store(fs, "minerType", minerType);
                    store(fs, "comPort", comPort);
                    store(fs, "poolAddr", poolAddr);
                    store(fs, "poolPort", poolPort.ToString());
                    store(fs, "poolUser", poolUser);
                    store(fs, "poolPass", poolPass);
                    store(fs, "poolName", poolName);

                    fs.Close();
                }

            }
            catch (IOException) { }
        }
    }

}
