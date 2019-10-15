using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{

    class SkeinBinaryFPGADevice : GenericComVerA_BinaryFPGADevice, StandardWorkerI
    {

        private const uint NONCE_SLIP_V00 = 201;

        private uint NONCE_SLIP = NONCE_SLIP_V00;
        private StandardNonceCallbackI nonceCallback = null;

        public override String GetName() { return "SkeinBinaryFPGA"; }

        public SkeinBinaryFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, COM, 4)
        {
        }

        protected override void MinerInfo(string minerInfo)
        {
            switch (minerInfo)
            {
                case "SK00":
                    NONCE_SLIP = NONCE_SLIP_V00;
                    break;
                default:
                    Program.Logger("Unkown Skein miner '" + minerInfo + "' defaulting to NS:" + NONCE_SLIP);
                    break;
            }
        }

        public void SetNonceCallback(StandardNonceCallbackI nonceCallback)
        {
            this.nonceCallback = nonceCallback;
        }

        public void NewWork(byte[] data)
        {
            Console.WriteLine("--- NW ---");
            SendNewWork(data, data.Length);
        }

        protected override void FoundNonce(byte[] d, int dataLength)
        {
            uint nonce = BitConverter.ToUInt32(d, 2);
            nonceCallback.FoundNonce(nonce - NONCE_SLIP);
        }


    }
}
