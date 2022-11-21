using System;
using System.Linq;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Jobbr.Runtime.Execution
{
    internal class RunWrapperFactory
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly ILogger<RunWrapperFactory> logger;
        private readonly Type jobType;
        private readonly object jobParameter;
        private readonly object instanceParameter;

        public RunWrapperFactory(ILoggerFactory loggerFactory, Type jobType, object jobParameter, object instanceParameter)
        {
            this.loggerFactory = loggerFactory;
            this.logger = loggerFactory.CreateLogger<RunWrapperFactory>();
            this.jobType = jobType;
            this.jobParameter = jobParameter;
            this.instanceParameter = instanceParameter;
        }

        internal object GetCastedParameterValue(string parameterName, Type targetType, string jobbrParamName, object value)
        {
            object castedValue;

            this.logger.LogInformation("Casting {jobbrParamName}-parameter to its target value '{targetType}' based on the Run()-Parameter {parameterName}", jobbrParamName, targetType, parameterName);

            // Try to cast them to specific types
            if (value == null)
            {
                this.logger.LogDebug("The {jobbrParamName}-parameter is null - no cast needed.", jobbrParamName);
                castedValue = null;
            }
            else if (targetType == typeof(object))
            {
                this.logger.LogDebug("The {jobbrParamName}-parameter is of type 'object' - no cast needed.", jobbrParamName);
                castedValue = value;
            }
            else
            {
                this.logger.LogDebug("The {jobbrParamName}-parameter '{parameterName}' is from type '{targetType}'. Casting this value to '{targetType}'", jobbrParamName, parameterName, targetType, targetType);
                castedValue = JsonSerializer.Deserialize(value.ToString(), targetType);
            }

            return castedValue;
        }

        internal JobWrapper CreateWrapper(object jobClassInstance, UserContext runtimeContext)
        {
            var runMethods = this.jobType.GetMethods().Where(m => string.Equals(m.Name, "Run", StringComparison.Ordinal) && m.IsPublic).ToList();

            if (!runMethods.Any())
            {
                this.logger.LogError("Unable to find an entrypoint to call your job. Is there at least a public Run()-Method?");
                return null;
            }

            Action runMethodWrapper = null;

            // Try to use the method with 2 concrete parameters
            var parameterizedMethod = runMethods.FirstOrDefault(m => m.GetParameters().Length == 2);
            if (parameterizedMethod != null)
            {
                var jobParamValue = this.jobParameter ?? "<null>";
                var instanceParamValue = this.instanceParameter ?? "<null>";

                var jobParamJsonString = jobParamValue.ToString();
                var instanceParamJsonString = instanceParamValue.ToString();

                // Note: We cannot use string interpolation here, because LibLog is using string.format again and will fail if there are { } chars in the string, even if there is no formatting needed.
                this.logger.LogDebug("Decided to use parameterized method '{parameterizedMethod}' with JobParameter '{jobParameter}' and InstanceParameters '{instanceParameters}'.", parameterizedMethod, jobParamJsonString, instanceParamJsonString);
                var allParams = parameterizedMethod.GetParameters().OrderBy(p => p.Position).ToList();

                var param1Type = allParams[0].ParameterType;
                var param2Type = allParams[1].ParameterType;

                var param1Name = allParams[0].Name;
                var param2Name = allParams[1].Name;

                // Casting in the most preferrable type
                var jobParameterValue = this.GetCastedParameterValue(param1Name, param1Type, "job", this.jobParameter);
                var instanceParameterValue = this.GetCastedParameterValue(param2Name, param2Type, "instance", this.instanceParameter);

                runMethodWrapper = () => { parameterizedMethod.Invoke(jobClassInstance, new[] {jobParameterValue, instanceParameterValue}); };
            }
            else
            {
                var fallBackMethod = runMethods.FirstOrDefault(m => !m.GetParameters().Any());

                if (fallBackMethod != null)
                {
                    this.logger.LogDebug("Decided to use parameterless method '{fallBackMethod}'", fallBackMethod);
                    runMethodWrapper = () => fallBackMethod.Invoke(jobClassInstance, null);
                }
            }

            if (runMethodWrapper == null)
            {
                this.logger.LogError("None of your Run()-Methods are compatible with Jobbr. Please see documentation");
                return null;
            }

            this.logger.LogDebug("Initializing task for JobRun");

            return new JobWrapper(this.loggerFactory, runMethodWrapper, runtimeContext);
        }
    }
}