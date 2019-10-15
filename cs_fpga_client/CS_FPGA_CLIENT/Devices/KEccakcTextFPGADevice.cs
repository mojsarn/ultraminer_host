using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{

    public class KEccakcTextFPGADevice : DeviceUARTText, StandardWorkerI
    {

        private StandardNonceCallbackI nonceCallback = null;

        public override String GetVendor() { return "GAGGA_GALACTIC_SYNDICATE"; }
        public override String GetName() { return "KeccakTextFPGA"; }

        public KEccakcTextFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, tRMode.LINE_MODE, COM)
        {
        }

        public void SetNonceCallback(StandardNonceCallbackI nonceCallback)
        {
            this.nonceCallback = nonceCallback;
        }

        public void NewWork(byte[] data)
        {
            uint[] d = new uint[19];
            Buffer.BlockCopy(data, 0, d, 0, 19 * 4);

            // TESTING
            //for (int i = 0; i < 19; i++)
            //    d[i] = Utilities.swap32(d[i]);

            string new_work_line = "NW " + Utilities.hex(d, " ");
            Console.WriteLine(new_work_line);
            WriteLine(new_work_line);
        }

        protected override void COM_line(string line)
        {
            Console.WriteLine("R : " + line);
            string[] parts = line.Split(' ');
            if (parts.Length == 2)
            {
                if (parts[0] == "N")  // Received a new found nonce from the device. 
                {
                    try
                    {
                        nonceCallback.FoundNonce(Convert.ToUInt32(parts[1]));
                    }
                    catch (Exception e)
                    {
                        Program.Logger(e.ToString());
                    }
                }
            }
        }

    }
}
