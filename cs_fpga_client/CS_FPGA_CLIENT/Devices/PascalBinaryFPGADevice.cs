using System;
using System.Collections.Generic;
using System.Text;

namespace CS_FPGA_CLIENT
{

    public class PascalBinaryFPGADevice : GenericComVerA_BinaryFPGADevice, PascalWorkerI
    {

        private uint NONCE_SLIP = (2 * 64 + 1);
        private PascNonceCallbackI nonceCallback = null;

        public override String GetName() { return "PascalBinaryFPGA"; }

        public PascalBinaryFPGADevice(int aDeviceIndex, string COM)
            : base(aDeviceIndex, COM, 4)
        {
        }

        public void SetNonceCallback(PascNonceCallbackI nonceCallback)
        {
            this.nonceCallback = nonceCallback;
        }

        public void NewWork(uint[] data)
        {
            SendNewWork(Utilities.UintArrSwap32ToByteArr(data), data.Length*4);
        }

        protected override void FoundNonce(byte[] d, int dataLength)
        {
            uint nonce = BitConverter.ToUInt32(d, 2);
            nonceCallback.FoundNonce(nonce - NONCE_SLIP);
        }
        

    }


}
