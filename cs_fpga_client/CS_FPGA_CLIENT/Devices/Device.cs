using System;
using System.Collections.Generic;
using System.Text;

namespace CS_FPGA_CLIENT
{

    public class Device : IDisposable
    {
        private int mDeviceIndex;
        private int mAcceptedShares;
        private int mRejectedShares;

        public virtual String GetVendor() { return ""; }
        public virtual String GetName() { return ""; }

        public int DeviceIndex { get { return mDeviceIndex; } }
        public int AcceptedShares { get { return mAcceptedShares; } }
        public int RejectedShares { get { return mRejectedShares; } }


        public Device(int aDeviceIndex)
        {
            mDeviceIndex = aDeviceIndex;
            mAcceptedShares = 0;
            mRejectedShares = 0;
        }

        public void Dispose()
        {
            // Dispose of unmanaged resources.
            Dispose(true);
            // Suppress finalization.
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        public int IncrementAcceptedShares()
        {
            return ++mAcceptedShares;
        }

        public int IncrementRejectedShares()
        {
            return ++mRejectedShares;
        }

        public void ClearShares()
        {
            mAcceptedShares = 0;
            mRejectedShares = 0;
        }

        
    }


}
