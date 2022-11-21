using System;
using System.Security.Principal;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Jobbr.Runtime.Execution
{
    internal class JobWrapper
    {
        private readonly ILogger<JobWrapper> logger;

        private readonly Thread thread;

        internal JobWrapper(ILoggerFactory loggerFactory, Action action, UserContext runtimeContext)
        {
            this.logger = loggerFactory.CreateLogger<JobWrapper>();
            
            this.thread = new Thread(() =>
            {
                var previousPrincipal = Thread.CurrentPrincipal;

                try
                {
                    if (!string.IsNullOrWhiteSpace(runtimeContext.UserId))
                    {
                        Thread.CurrentPrincipal = new GenericPrincipal(new GenericIdentity(runtimeContext.UserId, "JobbrIdentity"), new string[0]);
                    }

                    action();
                }
                catch (Exception e)
                {
                    this.Exception = e;
                }
                finally
                {
                    Thread.CurrentPrincipal = previousPrincipal;
                }
            });
        }

        internal void Start()
        {
            this.thread.Start();
        }

        public Exception Exception { get; private set; }

        internal bool WaitForCompletion()
        {
            try
            {
                this.thread.Join();
            }
            catch (Exception e)
            {
                this.logger.LogError(e, "Exception while waiting for completion of job");
                this.Exception = e;
                return false;
            }

            if (this.Exception != null)
            {
                this.logger.LogError(this.Exception, "The execution of the job has faulted. See Exception for details.");
                return false;
            }

            return true;
        }
    }
}