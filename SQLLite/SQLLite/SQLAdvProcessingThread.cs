﻿/*
 * Copyright © 2021 robbyxp1 @ github.com & EDDiscovery Team
 *
 * Licensed under the Apache License, Version 2.0 (the "License"); you may not use this
 * file except in compliance with the License. You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software distributed under
 * the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF
 * ANY KIND, either express or implied. See the License for the specific language
 * governing permissions and limitations under the License.
 * 
 * EDDiscovery is not affiliated with Frontier Developments plc.
 */

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace SQLLiteExtensions
{
    // allows multithreaded access to a SQL database, sequencing the reads/writes
 
    public abstract class SQLAdvProcessingThread<ConnectionType> where ConnectionType : IDisposable
    {
        #region Public control 
        public int Threads { get { return runningThreads; } }
        public int MaxThreads { get; set; } = 8;                       // maximum to create when MultiThreaded = true, 1 or more
        public int MinThreads { get; set; } = 3;                       // maximum to create when MultiThreaded = true, 1 or more

        public string Name { get; set; } = "SQLAdvProcessingThread";   // thread name

        public bool MultiThreaded { get { return multithreaded; } set { SetMultithreaded(value); } }    // default is not

        protected abstract ConnectionType CreateConnection();           // override in derived class to make the connection

        // Execute SQL with the database in a thread.  Must indicate direction by name

        public T DBRead<T>(Func<ConnectionType, T> func, uint warnthreshold = 500, string jobname = "")
        {
            return Execute(() => func.Invoke(connection.Value), false, warnthreshold, jobname);
        }

        public void DBRead(Action<ConnectionType> action, uint warnthreshold = 500, string jobname = "")
        {
            Execute<object>(() => { action.Invoke(connection.Value); return null; }, false, warnthreshold, jobname);
        }

        public void DBWrite(Action<ConnectionType> action, uint warnthreshold = 500, string jobname = "")
        {
            Execute<object>(() => { action.Invoke(connection.Value); return null; }, true, warnthreshold, jobname);
        }

        public T DBWrite<T>(Func<ConnectionType, T> func, uint warnthreshold = 500, string jobname = "")
        {
            return Execute(() => func.Invoke(connection.Value), true, warnthreshold, jobname);
        }

        // clear connections, and restart minimum number of connections
        // you should do this when you've changed the tables around. SQL does not like using an existing connection over a table reorg
        public void ClearDownRestart()      
        {
            SetMultithreaded(multithreaded);                            // this has the effect of clearing the threads and restarting
            System.Data.SQLite.SQLiteConnection.ClearAllPools();        // SQLite caches connections, so if we want to clean up completely, we need to clear pools
        }

        // clear connections and leave DB without any connections active.  Can restart with the next execute
        public void ClearDown()
        {
            StopAllThreads();                                           // all threads stopped
            System.Data.SQLite.SQLiteConnection.ClearAllPools();        // SQLite caches connections, so if we want to clean up completely, we need to clear pools
            stopCreatingNewThreads = false;                             // and we can restart
        }

        // stop dead for good - no recovery
        public void Stop()
        {
            StopAllThreads();   
            System.Data.SQLite.SQLiteConnection.ClearAllPools();        // SQLite caches connections, so if we want to clean up completely, we need to clear pools
        }

        #endregion

        #region Privates

        protected ThreadLocal<ConnectionType> connection = new ThreadLocal<ConnectionType>(true);       // connection per thread

        private ConcurrentQueue<Job> jobQueue = new ConcurrentQueue<Job>();
        private AutoResetEvent jobQueuedEvent = new AutoResetEvent(false);      // first thread waiting resets it back

        private int createdThreads = 0;                 // used to track how many threads we asked for - will be different to RunningThread due to creation delay
        private int runningThreadsAvailable = 0;        // incremented when thread created runs, decremented when closed and during job execution
        private int runningThreads = 0;                 // incremented in thread when its runs, decremented when it exits

        private ManualResetEvent stopRequestedEvent = new ManualResetEvent(false);      // manual reset, multiple threads can be waiting on this one
        private ManualResetEvent stoppedAllThreads = new ManualResetEvent(true);        // Set to true as there are no running ones, cleared on thread start

        private ReaderWriterLock rwLock = new ReaderWriterLock();       // used to prevent writes when readers are running in MT scenarios

        private bool multithreaded = false;             // if MT
        private bool stopCreatingNewThreads = false;    // halt thread creation during stop

        private object locker = new object();  // used to lock the MT change

        private int checkRWLock = 0;        // used to double check reader/writer lock and to provide debug output for number of active items

        #endregion

        #region Processing Thread
        private void SqlThreadProc()    // SQL process thread
        {
            int recursiondepth = 0;

            Interlocked.Increment(ref runningThreadsAvailable);
            Interlocked.Increment(ref runningThreads);
            stoppedAllThreads.Reset();

            try
            {
                System.Diagnostics.Debug.WriteLine($"SQL {Name} Start thread {Thread.CurrentThread.Name}");

                using (connection.Value = CreateConnection())   // hold connection over whole period.
                {
                    while (true)
                    {
                        // multiple threads can be waiting on this.. 

                        switch (WaitHandle.WaitAny(new WaitHandle[] { stopRequestedEvent, jobQueuedEvent }))    // wait for event
                        {
                            case 1:     // JobQueuedEvent
                                try
                                {
                                    Interlocked.Decrement(ref runningThreadsAvailable);        // one less thread ready for use
                                    //System.Diagnostics.Debug.WriteLine($"SQL {Name} Thread state ta {runningThreadsAvailable} rt {runningThreads} ct {createdThreads} mt {MultiThreaded} stop {stopCreatingNewThreads}");

                                    while (jobQueue.Count != 0 )
                                    {
                                        if ( stopRequestedEvent.WaitOne(0))             // if signalled a stop, break the loop   
                                            break;

                                        while (jobQueue.TryDequeue(out Job job))                    // and get the job
                                        {
                                            System.Diagnostics.Debug.Assert(recursiondepth++ == 0); // we must not have a call to Job.Exec() calling back. Should never happen but check
                                            
                                            if ( !MultiThreaded )       // if not multithreaded mode, we can just execute
                                            {
                                                //System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} non mt execute job from {job.jobname} write {job.write}");
                                                job.Exec();
                                                //System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} non mt finish job from {job.jobname} write {job.write}");
                                            }
                                            else if (job.write)
                                            {
                                                while (true)
                                                {
                                                    try
                                                    {
                                                        rwLock.AcquireWriterLock(30*1000);      // 30 seconds - try and gain a lock. This is plenty for most situations. Will except if not

                                                        int active = Interlocked.Increment(ref checkRWLock);
                                                        System.Diagnostics.Debug.Assert(active == 1);
                                                        //System.Diagnostics.Debug.WriteLine($"SQL I{Name} On thread {Thread.CurrentThread.Name} execute write job from {job.jobname} active {active}");

                                                        job.Exec();

                                                        active = Interlocked.Decrement(ref checkRWLock);
                                                        //System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} finish write job from {job.jobname} active {active}");

                                                        rwLock.ReleaseWriterLock();
                                                        break;
                                                    }
                                                    catch
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} from {job.jobname} write failed to gain lock, retrying");
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                while (true)
                                                {
                                                    try
                                                    {
                                                        rwLock.AcquireReaderLock(30 * 1000);

                                                        int active = Interlocked.Increment(ref checkRWLock);
                                                        //System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} execute read job from {job.jobname} active {active}");

                                                        job.Exec();

                                                        active = Interlocked.Decrement(ref checkRWLock);
                                                        //System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} finish read job from {job.jobname} active {active}");

                                                        rwLock.ReleaseReaderLock();
                                                        break;
                                                    }
                                                    catch
                                                    {
                                                        System.Diagnostics.Debug.WriteLine($"SQL {Name} On thread {Thread.CurrentThread.Name} from {job.jobname} read failed to gain lock, retrying");
                                                    }
                                                }
                                            }

                                            recursiondepth--;       // decrease recursion depth
                                        }
                                    }
                                }
                                finally
                                {
                                    Interlocked.Increment(ref runningThreadsAvailable);
                                }
                                break;

                            case 0:     // stoprequested event.. go to finally
                                return;
                        }
                    }
                }
            }
            finally
            {
                System.Diagnostics.Debug.WriteLine($"SQL {Name} stop thread {Thread.CurrentThread.Name}");

                Interlocked.Decrement(ref runningThreadsAvailable);            // stopping threads.. decr count, if 0, say all stopped

                if (Interlocked.Decrement(ref runningThreads) == 0)
                {
                    stoppedAllThreads.Set();
                    System.Diagnostics.Debug.WriteLine($"SQL {Name} All threads stopped");
                }
            }
        }

        #endregion

        #region Execute 

        protected T Execute<T>(Func<T> func, bool write, uint warnthreshold, string jobname)  // in caller thread, queue to job queue, wait for complete
        {
            using (var job = new Job<T>(func, write, Thread.CurrentThread.Name + jobname))       // make a new job
            {
                if (Thread.CurrentThread.Name != null && Thread.CurrentThread.Name.StartsWith(Name))            // we should not be calling this from a thread made by us
                { 
                    System.Diagnostics.Trace.WriteLine($"SQL {Name} Database Re-entrancy\n{new System.Diagnostics.StackTrace(0, true).ToString()}");
                    job.Exec();
                    return job.Wait();
                }
                else
                {
                    //System.Diagnostics.Debug.WriteLine($"SQL {Name} {(write?"Write":"Read")} job, ta {runningThreadsAvailable} rt {runningThreads} ct {createdThreads} mt {MultiThreaded} stop {stopCreatingNewThreads}");
                    //System.Diagnostics.Debug.WriteLine($"... {new System.Diagnostics.StackTrace(2, true)}");

                    if (!stopCreatingNewThreads)   // if we can create new threads..
                    {
                        int tno = Interlocked.Increment(ref createdThreads);        // test how many running, interlocked

                        // if tno == 1, there are no threads created, we must make one
                        // else if MT, not write, and none available, and not exceeding MaxThreads, make another. No point making threads for write

                        if (tno == 1 || (runningThreadsAvailable == 0 && MultiThreaded && !write && tno <= MaxThreads))                      
                        {
                            StartThread(tno);
                        }
                        else
                        {
                            Interlocked.Decrement(ref createdThreads);      // need to decrease it back
                        }
                    }

                    jobQueue.Enqueue(job);
                    jobQueuedEvent.Set();  // kick one of the threads and execute it.

                    T ret = job.Wait();     // must be infinite - can't release the caller thread until the job finished. 

                    if ( job.executiontime >= warnthreshold)
                    {
                        System.Diagnostics.Debug.WriteLine($"SQL {Name} {(write ? "Write" : "Read")} job exceeded warning threshold {warnthreshold} time {job.executiontime}\r\n... {new System.Diagnostics.StackTrace(2, true)}");
                    }

                    job.Dispose();

                    return ret;
                }
            }
        }

        protected void Execute(Action action, bool write, uint warnthreshold, string jobname )
        {
            Execute<object>(() => { action(); return null; }, write, warnthreshold, jobname);
        }

        private void SetMultithreaded(bool mt)
        {
            if (stopCreatingNewThreads)     // if stopped, and we are trying to change this, throw
                throw new Exception();

            lock (locker)
            {
                StopAllThreads();       // stop everything
                stopCreatingNewThreads = false;                  // Reset so we can start making threads again
                multithreaded = mt;     // set state
                for (int i = 0; i < (multithreaded ? MinThreads : 1); i++)      // 1 thread for non MT, else MinThreads
                {
                    int tno = Interlocked.Increment(ref createdThreads);        // get next tno
                    StartThread(tno);      
                }
            }
        }

        private void StartThread(int tno)
        {
            var thread = new Thread(SqlThreadProc);
            thread.Name = $"{Name}-" + tno;
            thread.IsBackground = true;
            System.Diagnostics.Debug.WriteLine($"SQL {Name} Create Thread {thread.Name} ta {runningThreadsAvailable} rt {runningThreads} ct {createdThreads} mt {MultiThreaded}");
            thread.Start();
        }

        private void StopAllThreads()                       // leaves stopCreatingNewThreads in true state
        {
            stopCreatingNewThreads = true;                  // just stop the threads creating new readers

            stopRequestedEvent.Set();                       // stop the threads - all of them
            Interlocked.MemoryBarrier();                    // ??
            stoppedAllThreads.WaitOne();                    // until the last one indicates its finished

            System.Diagnostics.Debug.Assert(runningThreadsAvailable == 0 && runningThreads == 0);  // ensure the counters are right
            System.Diagnostics.Debug.WriteLine($"SQL {Name} stop all threads indicated stopped");

            createdThreads = 0;
            stoppedAllThreads = new ManualResetEvent(true);   // all threads are stopped
            stopRequestedEvent = new ManualResetEvent(false);
        }

        #endregion
    }

    internal interface Job
    {
        void Exec();
        string jobname { get; set; }
        bool write { get; set; }
    }
    internal class Job<T> : Job, IDisposable
    {
        public string jobname { get; set; }
        public bool write { get; set; }
        public uint executiontime { get; set; }   // set after wait for the amount of time between creation and finish execution

        private Func<T> func;           // this is the code to call to execute the job
        private T result;               // passed back result of the job
        private ManualResetEvent waithandle;    // set when job finished
        private Exception exception;

        public Job(Func<T> func, bool write, string jobname)       // in calller thread, set the job up
        {
            this.func = func;
            this.write = write;
            this.jobname = jobname;
            this.waithandle = new ManualResetEvent(false);
            this.executiontime = (uint)Environment.TickCount;
        }

        public void Exec()     // in SQL thread, do the job
        {
            try
            {
                result = func.Invoke();
            }
            catch (Exception ex)
            {
                this.exception = ex;
            }
            finally
            {
                executiontime = (uint)Environment.TickCount - executiontime;        // we use tickcount for this, accurate enough, low overhead
                waithandle.Set();
            }
        }

        public T Wait()     // in caller thread, wait for the job to complete.
        {
            waithandle.WaitOne();

            if (exception != null)
            {
                throw new SQLProcessingThreadException(exception);
            }
            else
            {
                return result;
            }
        }

        public void Dispose()
        {
            this.waithandle?.Dispose();
        }
    }

    public class SQLProcessingThreadException : Exception
    {
        public SQLProcessingThreadException(Exception innerexception) : base(innerexception.Message, innerexception)
        {
        }
    }
}

