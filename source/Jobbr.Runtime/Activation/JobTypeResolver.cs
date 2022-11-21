using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Logging;

namespace Jobbr.Runtime.Activation
{
    internal class JobTypeResolver
    {
        private readonly ILogger<JobTypeResolver> logger;

        private readonly IList<Assembly> assemblies;

        public JobTypeResolver(ILoggerFactory loggerFactory, IList<Assembly> assemblies)
        {
            this.logger = loggerFactory.CreateLogger<JobTypeResolver>();
            this.assemblies = assemblies;
        }

        internal Type ResolveType(string typeName)
        {
            this.logger.LogDebug("Resolve type using '{typeName}' like a full qualified CLR-Name", typeName);
            var type = Type.GetType(typeName);

            if (type == null && this.assemblies != null)
            {
                foreach (var assembly in this.assemblies)
                {
                    this.logger.LogDebug("Trying to resolve '{typeName}' by the assembly '{fullName}'", typeName, assembly.FullName);
                    type = assembly.GetType(typeName, false, true);
                    if (type != null)
                    {
                        break;
                    }
                }
            }

            if (type == null)
            {
                // Search in all Assemblies
                var allReferenced = Assembly.GetExecutingAssembly().GetReferencedAssemblies();

                this.logger.LogDebug("Trying to resolve type by asking all referenced assemblies ('{assemblies}')", string.Join(", ", allReferenced.Select(a => a.Name)));

                foreach (var assemblyName in allReferenced)
                {
                    var assembly = Assembly.Load(assemblyName);

                    var foundType = assembly.GetType(typeName, false, true);

                    if (foundType != null)
                    {
                        type = foundType;
                    }
                }
            }

            if (type == null)
            {
                this.logger.LogDebug("Still no luck finding '{typeName}' somewhere. Iterating through all types and comparing class-names. Please hold on", typeName);

                // Absolutely no clue
                var matchingTypes = AppDomain.CurrentDomain.GetAssemblies().SelectMany(x => x.GetTypes()).Where(x => string.Equals(x.Name, typeName, StringComparison.Ordinal) && x.IsClass && !x.IsAbstract).ToList();

                if (matchingTypes.Count() == 1)
                {
                    this.logger.LogDebug("Found matching type: '{matchingTypes}'", matchingTypes[0]);
                    type = matchingTypes.First();
                }
                else if (matchingTypes.Count > 1)
                {
                    this.logger.LogWarning("More than one matching type found for '{typeName}'. Matches: {matchingTypes}", typeName, string.Join(", ", matchingTypes.Select(t => t.FullName)));
                }
                else
                {
                    this.logger.LogWarning("No matching type found for '{typeName}'.", typeName);
                }
            }

            return type;
        }
    }
}