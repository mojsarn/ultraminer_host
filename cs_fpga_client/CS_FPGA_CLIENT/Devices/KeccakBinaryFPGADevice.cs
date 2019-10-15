using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{

    public class KeccakBinaryFPGADevice : GenericComVerA_BinaryFPGADevice, StandardWorkerI
    {

        private const uint NONCE_SLIP_V01 = 26;           // normal
        private const uint NONCE_SLIP_V02 = 24*2 + 5;     // deep

        private uint NONCE_SLIP = NONCE_SLIP_V02;
        private StandardNonceCallbackI nonceCallback = null;

        public override String GetName() { return "KeccakBinaryFPGA"; }

        public KeccakBinaryFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, COM, 4)
        {
        }

        protected override void MinerInfo(string minerInfo)
        {
            switch(minerInfo)
            {
                case "KC01":
                    NONCE_SLIP = NONCE_SLIP_V01;
                    break;
                case "KC02":
                    NONCE_SLIP = NONCE_SLIP_V02;
                    break;
                default:
                    Program.Logger("Unkown Keccak miner '" + minerInfo + "' defaulting to NS:" + NONCE_SLIP);
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
            SendNewWork(data, 19 * 4);
        }

        protected override void FoundNonce(byte[] d, int dataLength)
        {
            uint nonce = BitConverter.ToUInt32(d, 2);
            nonceCallback.FoundNonce(nonce - NONCE_SLIP);
        }


    }

}
