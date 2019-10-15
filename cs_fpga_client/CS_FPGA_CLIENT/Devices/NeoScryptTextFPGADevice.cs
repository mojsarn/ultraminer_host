using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{
    public class NeoScryptTextFPGADevice : DeviceUARTText, NeoScryptWorkerI
    {
        private NeoScryptNonceCallbackI nonceCallback = null;

        public override String GetVendor() { return "GAGGA_GALACTIC_SYNDICATE"; }
        public override String GetName() { return "NeoScryptTextFPGA"; }

        public NeoScryptTextFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, tRMode.LINE_MODE, COM)
        {
        }

        public void SetNonceCallback(NeoScryptNonceCallbackI nonceCallback)
        {
            this.nonceCallback = nonceCallback;
        }

        public void NewWork(byte[] data)
        {
            uint[] d = new uint[20];
            Buffer.BlockCopy(data, 0, d, 0, 20 * 4);
            NewWork(d);
        }

        public void NewWork(uint[] data)
        {
            string new_work_line = "NW " + Utilities.hex(data, " ");
            Console.WriteLine(new_work_line);
            WriteLine(new_work_line);
        }

        protected override void COM_line(string line)
        {
            Console.WriteLine("R : " + line);
            string[] parts = line.Split(' ');
            if (parts.Length == 3)
            {
                if (parts[0] == "N")  // Received a new found nonce from the device. 
                {
                    try
                    {
                        nonceCallback.FoundNonce(Convert.ToUInt32(parts[1]), Convert.ToUInt32(parts[2]));
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


