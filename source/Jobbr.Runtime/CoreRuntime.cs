using System;
using Jobbr.Runtime.Activation;
using Jobbr.Runtime.Execution;
using Microsoft.Extensions.Logging;

namespace Jobbr.Runtime
{
    public class CoreRuntime
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<CoreRuntime> logger;

        private readonly JobActivator jobActivator;

        /// <summary>
        /// Raised immediately after start and indicates that the Runtime is setting up itself
        /// </summary>
        public event EventHandler Initializing;

        /// <summary>
        /// Raised before the runtime activates the job class
        /// </summary>
        public event EventHandler Activating;

        /// <summary>
        /// Raised before the wrapper for the Run() Method is generated
        /// </summary>
        public event EventHandler WiringMethod;

        /// <summary>
        /// Raised before the Run()-Method is executed
        /// </summary>
        public event EventHandler Starting;

        /// <summary>
        /// Raised after the job has come to the end, independent of its success
        /// </summary>
        public event EventHandler<ExecutionEndedEventArgs> Ended;

        /// <summary>
        /// Raised for exceptions in the Core infrastructure that have not been handled
        /// </summary>
        public event EventHandler<InfrastructureExceptionEventArgs> InfrastructureException;

        public CoreRuntime(ILoggerFactory loggerFactory, RuntimeConfiguration runtimeConfiguration)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<CoreRuntime>();
                
            var jobTypeResolver = new JobTypeResolver(loggerFactory, runtimeConfiguration.JobTypeSearchAssemblies);
            var serviceProvider = runtimeConfiguration.ServiceProvider ?? new DefaultServiceProvider();

            this.jobActivator = new JobActivator(loggerFactory, jobTypeResolver, serviceProvider);
        }

        public void Execute(ExecutionMetadata executionMetadata)
        {
            var wasSuccessful = false;
            Exception lastException = null;

            try
            {
                this.OnInitializing();

                var jobTypeName = executionMetadata.JobType;

                var userContext = new UserContext()
                {
                    UserId = executionMetadata.UserId,
                    UserDisplayName = executionMetadata.UserDisplayName
                };

                // Register userContext as RuntimeContext in the DI if available
                this.logger.LogDebug("Trying to register additional dependencies if supported.");
                
                #pragma warning disable 618
                var runtimeContext = new RuntimeContext
                {
                    UserId = userContext.UserId,
                    UserDisplayName = userContext.UserDisplayName
                };
                #pragma warning restore 618

                this.jobActivator.AddDependencies(runtimeContext);

                // Create instance
                this.logger.LogDebug("Create instance of job based on the typename '{jobTypeName}'", jobTypeName);
                this.OnActivating();

                var jobClassInstance = this.jobActivator.CreateInstance(jobTypeName);

                if (jobClassInstance == null)
                {
                    this.logger.LogError("Cannot create activate the job based on the typename {jobTypeName}", jobTypeName);
                    return;
                }

                // Create task as wrapper for calling the Run() Method
                this.logger.LogDebug("Create task as wrapper for calling the Run() Method");
                this.OnWiringMethod();

                var runWrapperFactory = new RunWrapperFactory(this.loggerFactory, jobClassInstance.GetType(), executionMetadata.JobParameter, executionMetadata.InstanceParameter);
                var wrapper = runWrapperFactory.CreateWrapper(jobClassInstance, userContext);

                if (wrapper == null)
                {
                    this.logger.LogError("Unable to create a wrapper for the job");
                    return;
                }

                // Start 
                this.logger.LogDebug("Starting Task to execute the Run()-Method.");
                this.OnStarting();

                wrapper.Start();

                // Wait for completion
                wasSuccessful = wrapper.WaitForCompletion();
                lastException = wrapper.Exception;
            }
            catch (Exception e)
            {
                lastException = e;

                this.logger.LogCritical(e, "Exception in the Jobbr-Runtime. Please see details: ");
                this.OnInfrastructureException(new InfrastructureExceptionEventArgs { Exception = e });
            }
            finally
            {
                this.OnEnded(new ExecutionEndedEventArgs() { Succeeded = wasSuccessful, Exception = lastException});
            }
        }

        #region Event Invocators

        protected virtual void OnInitializing()
        {
            try
            {
                this.Initializing?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnInitializing));
            }
        }

        protected virtual void OnActivating()
        {
            try
            {
                this.Activating?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnActivating));
            }
        }

        protected virtual void OnWiringMethod()
        {
            try
            {
                this.WiringMethod?.Invoke(this, EventArgs.Empty);
            }
            catch(Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnWiringMethod));
            }
        }

        protected virtual void OnStarting()
        {
            try
            {
                this.Starting?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnStarting));
            }
        }

        protected virtual void OnEnded(ExecutionEndedEventArgs e)
        {
            try
            {
                this.Ended?.Invoke(this, e);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnEnded));
            }
        }

        protected virtual void OnInfrastructureException(InfrastructureExceptionEventArgs e)
        {
            try
            {
                this.InfrastructureException?.Invoke(this, e);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Recipient of the event {event} threw an exception", nameof(this.OnInfrastructureException));
            }
        }

        #endregion
    }
}