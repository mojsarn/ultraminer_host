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
    class OpenEthereumPoolEthashStratum : EthashStratum
    {
        public new class Work
        {
            readonly private Job mJob;

            public Job CurrentJob { get { return mJob; } }

            public Work(Job aJob)
            {
                mJob = aJob;
            }
        }

        public new class Job : EthashStratum.Job
        {
            private byte mExtranonce = 0;

            public byte Extranonce { get { return mExtranonce; } }

            public Job(Stratum aStratum, string aID, string aSeedhash, string aHeaderhash)
                : base(aStratum, aID, aSeedhash, aHeaderhash)
            {
            }
        }

        Thread mPingThread;
        int mJsonRPCMessageID = 1;
        HashSet<string> mShareIDs = new HashSet<string> { };
        private Mutex mMutex = new Mutex();
        private bool mDwarfpoolMode = false;

        protected override void ProcessLine(String line)
        {
            Dictionary<String, Object> response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(line);
            if (response.ContainsKey("result")
                && response["result"] == null
                && response.ContainsKey("error") && response["error"].GetType() == typeof(String))
            {
                Program.Logger("Stratum server responded: " + (String)response["error"]);
            }
            else if (response.ContainsKey("result")
                && response["result"] == null
                && response.ContainsKey("error") && response["error"].GetType() == typeof(Newtonsoft.Json.Linq.JObject))
            {
                Program.Logger("Stratum server responded: " + ((JContainer)response["error"])["message"]);
            }
            else if (response.ContainsKey("result")
                    && response["result"] == null
                    && mShareIDs.Contains(response["id"].ToString()))
            {
                ReportRejectedShare();
            }
            else if (response.ContainsKey("result")
                && response["result"] != null
                && response["result"].GetType() == typeof(bool)
                && mShareIDs.Contains(response["id"].ToString()))
            {
                if ((bool)response["result"])
                {
                    ReportAcceptedShare();
                }
                else if (response.ContainsKey("error") && response["error"].GetType() == typeof(String))
                {
                    ReportRejectedShare((String)response["error"]);
                }
                else if (response.ContainsKey("error") && response["error"].GetType() == typeof(JArray))
                {
                    ReportRejectedShare((string)(((JArray)response["error"])["message"]));
                }
                else if (!(bool)response["result"])
                {
                    ReportRejectedShare();
                }
                else 
                {
                    //Program.Logger("Unknown JSON message: " + line);
                }
            }
            else if (response.ContainsKey("result")
                        && response["result"] != null
                        && response["result"].GetType() == typeof(JArray))
            {
                //var ID = response["id"];
                JArray result = (JArray)response["result"];
                var oldJob = mJob;
                if (mDwarfpoolMode && oldJob != null && ("0x" + oldJob.ID) == (string)result[0])
                    throw new Exception("Stratum server sent duplicate job.");
                if (oldJob == null || ("0x" + oldJob.ID) != (string)result[0])
                {
                    try  { mMutex.WaitOne(5000); } catch (Exception) { }
                    System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex(@"^0x");
                    mJob = (EthashStratum.Job)(new Job(this,
                        regex.Replace((string)result[0], ""), // Use headerhash as job ID.
                        regex.Replace((string)result[1], ""),
                        regex.Replace((string)result[0], "")));
                    regex = new System.Text.RegularExpressions.Regex(@"^0x(.*)................................................$"); // I don't know about this one...
                    mDifficulty = (double)0xffff0000U / (double)Convert.ToUInt64(regex.Replace((string)result[2], "$1"), 16);
                    try  { mMutex.ReleaseMutex(); } catch (Exception) { }
                    if (!SilentMode) Program.Logger("Received new job: " + (string)result[0]);
                }
            }
            else
            {
                //Program.Logger("Unknown JSON message: " + line);
            }
        }
        
        // This is for DwarfPool.
        private void PingThread()
        {
            System.Threading.Thread.Sleep(5000);

            while (!Stopped)
            {
                if (mDwarfpoolMode)
                {
                    try
                    {
                        try { mMutex.WaitOne(5000); }
                        catch (Exception) { }
                        /*
                        WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                            { "id", mJsonRPCMessageID++ },
                            { "jsonrpc", "2.0" },
                            { "method", "eth_getWork" }
                        }));
                        */
                        WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                        { "id", mJsonRPCMessageID++ },
                        { "jsonrpc", "2.0" },
                        { "method", "eth_submitLogin" },
                        { "params", new List<string> {
                            Username
                    }}}));
                        try { mMutex.ReleaseMutex(); }
                        catch (Exception) { }
                    }
                    catch (Exception ex)
                    {
                        Program.Logger("Exception in ping thread: " + ex.Message + ex.StackTrace);
                    }
                }

                System.Threading.Thread.Sleep(5000);
            }
        }

        override protected void Authorize()
        {
            try  { mMutex.WaitOne(5000); } catch (Exception) { }
            WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "jsonrpc", "2.0" },
                { "method", "eth_submitLogin" },
                { "params", new List<string> {
                    Username
            }}}));
            try { mMutex.ReleaseMutex(); } catch (Exception) { }

            var response = JsonConvert.DeserializeObject<Dictionary<string, Object>>(ReadLine());
            if (response["result"] == null)
                throw (UnrecoverableException = new AuthorizationFailedException());

            try { mMutex.WaitOne(5000); } catch (Exception) { }
            WriteLine(Newtonsoft.Json.JsonConvert.SerializeObject(new Dictionary<string, Object> {
                { "id", mJsonRPCMessageID++ },
                { "jsonrpc", "2.0" },
                { "method", "eth_getWork" }
            }));
            try  { mMutex.ReleaseMutex(); } catch (Exception) { }

            mPingThread = new Thread(new ThreadStart(PingThread));
            mPingThread.IsBackground = true;
            mPingThread.Start();
        }

        override public void Submit(Device aDevice, EthashStratum.Job job, UInt64 output)
        {
            if (Stopped)
                return;

            try  { mMutex.WaitOne(5000); } catch (Exception) { }
            ReportSubmittedShare(aDevice);
            try
            {
                String stringNonce
                      = String.Format("{7:x2}{6:x2}{5:x2}{4:x2}{3:x2}{2:x2}{1:x2}{0:x2}",
                                      ((output >> 0) & 0xff),
                                      ((output >> 8) & 0xff),
                                      ((output >> 16) & 0xff),
                                      ((output >> 24) & 0xff),
                                      ((output >> 32) & 0xff),
                                      ((output >> 40) & 0xff),
                                      ((output >> 48) & 0xff),
                                      ((output >> 56) & 0xff));
                mShareIDs.Add(mJsonRPCMessageID.ToString());
                String message = JsonConvert.SerializeObject(new Dictionary<string, Object> {
                    { "id", mJsonRPCMessageID },
                    { "jsonrpc", "2.0" },
                    { "method", "eth_submitWork" },
                    { "params", new List<string> {
                        "0x" + stringNonce,
                        "0x" + job.Headerhash, // The header's pow-hash (256 bits)
                        "0x" + job.GetMixHash(output) // mix digest
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

        public OpenEthereumPoolEthashStratum(String aServerAddress, int aServerPort, String aUsername, String aPassword, String aPoolName, bool aDwalfpoolMode = false)
            : base(aServerAddress, aServerPort, aUsername, aPassword, aPoolName)
        {
            mDwarfpoolMode = aDwalfpoolMode;
        }
    }
}
