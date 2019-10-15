using System;
using System.Collections.Generic;
using System.Text;

namespace CS_FPGA_CLIENT
{

    public class PascalTextFPGADevice : DeviceUARTText, PascalWorkerI
    {

        private PascNonceCallbackI nonceCallback = null;

        public override String GetVendor() { return "GAGGA_GALACTIC_SYNDICATE"; }
        public override String GetName() { return "PascalTextFPGA"; }

        public PascalTextFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, tRMode.LINE_MODE, COM)
        {
        }

        public void SetNonceCallback(PascNonceCallbackI nonceCallback)
        {
            this.nonceCallback = nonceCallback;
        }

        public void NewWork(uint[] data)
        {
            string new_work_line = "NW " + Utilities.hex(data, " ");
            //Console.WriteLine(new_work_line);
            WriteLine(new_work_line);
        }

        protected override void COM_line(string line)
        {
            Console.WriteLine("R : " + line);
            string[] parts = line.Split(' ');
            if(parts.Length == 2)
            {
                if(parts[0] == "N")  // Received a new found nonce from the device. 
                {
                    try
                    {
                        nonceCallback.FoundNonce(Convert.ToUInt32(parts[1]));
                    }catch (Exception e)
                    {
                        Program.Logger(e.ToString());
                    }
                }
            }
        }

    }


}
