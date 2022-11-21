using System;
using Microsoft.Extensions.Logging;

namespace Jobbr.Runtime.Activation
{
    internal class JobActivator
    {
        private readonly ILogger<JobActivator> logger;

        private readonly JobTypeResolver jobTypeResolver;
        private readonly IServiceProvider serviceProvider;

        internal JobActivator(ILoggerFactory loggerFactory, JobTypeResolver jobTypeResolver, IServiceProvider serviceProvider)
        {
            this.logger = loggerFactory.CreateLogger<JobActivator>();
            this.jobTypeResolver = jobTypeResolver;
            this.serviceProvider = serviceProvider;
        }

        internal object CreateInstance(string jobTypeName)
        {
            // Resolve Type
            this.logger.LogDebug("Trying to resolve the specified type '{jobTypeName}'...", jobTypeName);

            var type = this.jobTypeResolver.ResolveType(jobTypeName);

            if (type == null)
            {
                this.logger.LogError("Unable to resolve the type '{jobTypeName}'!", jobTypeName);
                return null;
            }

            // Activation
            this.logger.LogDebug("Type '{jobTypeName}' has been resolved to '{type}'. Activating now.", jobTypeName, type);

            object jobClassInstance;

            try
            {
                jobClassInstance = this.serviceProvider.GetService(type);
            }
            catch (Exception exception)
            {
                this.logger.LogError(exception, "Exception while activating type '{type}'. See Exception for details!", type);
                return null;
            }

            if (jobClassInstance == null)
            {
                this.logger.LogError("Unable to create an instance ot the type '{type}'!", type);
            }

            return jobClassInstance;
        }

        internal void AddDependencies(params object[] additionalDependencies)
        {
            var registrator = this.serviceProvider as IConfigurableServiceProvider;

            try
            {
                foreach (var dep in additionalDependencies)
                {
                    registrator?.RegisterInstance(dep);
                }

            }
            catch (Exception e)
            {
                this.logger.LogWarning(e, "Unable to register additional dependencies on {registrator}!", registrator);
            }
        }
    }
}