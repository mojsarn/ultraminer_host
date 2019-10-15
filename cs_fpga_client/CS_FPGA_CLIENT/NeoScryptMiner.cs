using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CS_FPGA_CLIENT
{

    public interface NeoScryptNonceCallbackI
    {
        void FoundNonce(uint nonce, uint hash);
    }
    public interface NeoScryptWorkerI
    {
        void SetNonceCallback(NeoScryptNonceCallbackI nonceCallback);
        void NewWork(byte[] data);
    }

    public class NeoScryptMiner : NeoScryptNonceCallbackI
    {
        private NeoScryptStratum nscs;
        private Device device;
        private NeoScryptWorkerI neoScryptWorker;
        NeoScryptStratum.Work curWork = null; /* We need to check nonces comming back against previus work as well */
        private bool stopped = false;

        public NeoScryptMiner(NeoScryptStratum nscs, Device device, NeoScryptWorkerI neoScryptWorker)
        {
            this.nscs = nscs;
            this.neoScryptWorker = neoScryptWorker;
            this.device = device;
            neoScryptWorker.SetNonceCallback(this);
        }

        public void workLoop()
        {

            // Wait for the first NeoScrypt to arrive.
            int elapsedTime = 0;
            while ((nscs.GetJob() == null) && !stopped)
            {
                System.Threading.Thread.Sleep(100);
                elapsedTime += 100;
                if (elapsedTime >= 5000)
                {
                    Program.Logger("Waiting for job from pool...\n");
                    elapsedTime = 0;
                }
            }



            while (!stopped)
            {
                // Each time pascalWork.Blob gives a different blob for the same work, so this is how we get more 32bit nonce search areas... 
                // Just have to rerun this loop at a higher rate then we run out of work nonces... 
                // For now let's make sure we call this at least every 2s, that means we can support hash speeds up to 2GH/s . 
                // Callback will be asyncrhonus, so have to lock cur and pre work while updating them. 
                lock (this)
                {
                    curWork = nscs.GetWork();                        // Get a new work item (can be for the same job). 
                    neoScryptWorker.NewWork(curWork.Blob);
                }

                // let's do 20000 loops, 10ms delay each loop, should be less then 400s.  Then bail out if we get new orders from pool ofcourse. 
                for (int i = 0; i < 20000; i++)
                {
                    if (stopped || (nscs.GetJob().Equals(curWork.Job) == false))
                        break;
                    System.Threading.Thread.Sleep(10);
                }

            }
        }
        
        public void FoundNonce(uint nonce, uint hash)
        {
            if (curWork == null)
                return;
            
            UInt32 target = (UInt32)((double)0xffff0000U / (nscs.Difficulty * 65536)); ;

            if (hash <= target)
            {
                nscs.Submit(device, curWork, nonce);
                Program.Logger("submit hash:" + String.Format("0x{0:X8}", hash) + " t32:" + String.Format("0x{0:X8}", target));
                return;
            }

            Program.Logger("hash:" + String.Format("0x{0:X8}", hash) + " t32:" + String.Format("0x{0:X8}", target));
        }


    }


}
