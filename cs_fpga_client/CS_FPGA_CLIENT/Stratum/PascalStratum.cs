using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
//using HashLib;



namespace CS_FPGA_CLIENT
{
    public class PascalStratum : Stratum
    {
        public new class Work : Stratum.Work
        {
            readonly private Job mJob;

            public Job Job {
                get { return mJob; }
            }

            public Work(Job aJob)
                : base(aJob)
            {
                mJob = aJob;
            }


            public byte[] Blob
            {
                get
                {
                    byte[] blob = new byte[196];
                    byte[] coinbase = Utilities.StringToByteArray(
                          Job.Coinbase1
                        + Job.Stratum.PoolExtranonce
                        + LocalExtranonceString
                        + Job.Coinbase2);
                    Buffer.BlockCopy(coinbase, 0, blob, 0, coinbase.Length);
                    var array = Utilities.StringToByteArray(Job.NTime);
                    blob[192] = array[3];
                    blob[193] = array[2];
                    blob[194] = array[1];
                    blob[195] = array[0];

                    return blob;
                }
            }
        }

        public new class Job : Stratum.Job
        {
            private string mID;
            private string mCoinbase1;
            private string mCoinbase2;
            private string mNTime;
            private PascalStratum mStratum;

            public String ID { get { return mID; } }
            public String Coinbase1 { get { return mCoinbase1; } }
            public String Coinbase2 { get { return mCoinbase2; } }
            public String NTime { get { return mNTime; } }
            public new PascalStratum Stratum { get { return mStratum; } }

            public Job(PascalStratum aStratum, string aID, string aCoinbase1, string aCoinbase2, string aNTime)
                : base(aStratum)
            {
                mStratum = aStratum;
                mID = aID;
                mCoinbase1 = aCoinbase1;
                mCoinbase2 = aCoinbase2;
                mNTime = aNTime;
            }

            public bool Equals(Job aJob)
            {
                return mID == aJob.mID 
                    && mCoinbase1 == aJob.Coinbase1 
                    && mCoinbase2 == aJob.mCoinbase2 
                    && mNTime == aJob.mNTime;
            }
        }

        private int mJsonRPCMessageID = 1;
        private Job mJob = null;
        private Mutex mMutex = new Mutex();

        public Job GetJob()
        {
            return mJob;
        }

        public new Work GetWork()
        {
            return new Work(mJob);
        }

        protected override void ProcessLine(String line)
        {
            //Program.Logger("line: " + line);
            Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(line);
            if (response.ContainsKey("method") && response.ContainsKey("params"))
            {
                string method = (string)response["method"];
                JArray parameters = (JArray)response["params"];
                if (method.Equals("mining.set_difficulty"))
                {
                    try  { mMutex.WaitOne(5000); } catch (Exception) { }
                    mDifficulty = (double)parameters[0];
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    Program.Logger("Difficulty set to " + (double)parameters[0] + ".");
                }
                else if (method.Equals("mining.notify"))
                {
                    bool jobChanged = (mJob == null || mJob.ID != (string)parameters[0]);
                    try { mMutex.WaitOne(5000); } catch (Exception) { }
                    mJob = (new Job(this, (string)parameters[0], (string)parameters[2], (string)parameters[3], (string)parameters[7]));
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    if (!SilentMode && jobChanged) Program.Logger("Received new job: " + parameters[0]);
                }
                else if (method.Equals("mining.set_extranonce"))
                {
                    try  { mMutex.WaitOne(5000); } catch (Exception) { }
                    mPoolExtranonce = (String)parameters[0];
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    Program.Logger("Received new extranonce: " + parameters[0]);
                }
                else if (method.Equals("client.reconnect"))
                {
                    throw new Exception("client.reconnect");
                }
                else
                {
                    Program.Logger("Unknown stratum method: " + line);
                }
            }   
            else if (response.ContainsKey("id") && response.ContainsKey("result"))
            {
                var ID = response["id"].ToString();
                bool result = (response["result"] == null) ? false : (bool)response["result"];

                if (ID == "3" && !result)
                {
                    throw (UnrecoverableException = new UnrecoverableException("Authorization failed."));
                }
                else if ((ID != "1" && ID != "2" && ID != "3") && result)
                {
                    ReportAcceptedShare();
                }
                else if ((ID != "1" && ID != "2" && ID != "3") && !result)
                {
                    ReportRejectedShare((String)(((JArray)response["error"])[1]));
                }
            }
            else
            {
                Program.Logger("Unknown JSON message: " + line);
            }
        }

        protected override void Authorize()
        {
            try  { mMutex.WaitOne(5000); } catch (Exception) { }

            mJsonRPCMessageID = 1;

            WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.subscribe" },
                { "params", new List<string> {
                    Program.appName + "/" + Program.appVersion
            }}}));

            try {
                Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(ReadLine());
                //mSubsciptionID = (string)(((JArray)(((JArray)(response["result"]))[0]))[1]);
                mPoolExtranonce = (string)(((JArray)(response["result"]))[1]);
                LocalExtranonceSize = (int)(((JArray)(response["result"]))[2]);
                //Program.Logger("mLocalExtranonceSize: " + mLocalExtranonceSize);
            } catch (Exception) {
                throw this.UnrecoverableException = new AuthorizationFailedException();
            }
            
            // mining.extranonce.subscribe
            WriteLine(JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.extranonce.subscribe" },
                { "params", new List<string> {
            }}}));

            WriteLine(JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "method", "mining.authorize" },
                { "params", new List<string> {
                    Username,
                    Password
            }}}));

            try  { mMutex.ReleaseMutex(); } catch (Exception) { }
        }
        
        public void Submit(Device aDevice, PascalStratum.Work work, UInt32 aNonce)
        {
            if (Stopped)
                return;
            
            try  { mMutex.WaitOne(5000); } catch (Exception) { }

            ReportSubmittedShare(aDevice);
            try
            {
                String stringNonce = (String.Format("{3:x2}{2:x2}{1:x2}{0:x2}", ((aNonce >> 0) & 0xff), ((aNonce >> 8) & 0xff), ((aNonce >> 16) & 0xff), ((aNonce >> 24) & 0xff)));
                String message = JsonConvert.SerializeObject(new Dictionary<string, Object> {
                    { "id", mJsonRPCMessageID },
                    { "method", "mining.submit" },
                    { "params", new List<string> {
                        Username,
                        work.Job.ID,
                        work.LocalExtranonceString,
                        work.Job.NTime,
                        stringNonce
                }}});
                WriteLine(message);
                ++mJsonRPCMessageID;
            }
            catch (Exception ex) {
                Program.Logger("Failed to submit share: " + ex.Message + "\nReconnecting to the server...");
                Reconnect();
            }

            try  { mMutex.ReleaseMutex(); } catch (Exception) { }
        }

        public PascalStratum(String aServerAddress, int aServerPort, String aUsername, String aPassword, String aPoolName)
            : base(aServerAddress, aServerPort, aUsername, aPassword, aPoolName, "pascal")
        {
        }
    }
}
