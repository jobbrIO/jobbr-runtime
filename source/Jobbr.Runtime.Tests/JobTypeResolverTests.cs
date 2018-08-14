using System.Collections.Generic;
using System.Reflection;
using Jobbr.Runtime.Activation;
using Jobbr.SampleTaskLibrary;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Jobbr.Runtime.Tests
{
    [TestClass]
    public class JobTypeResolverTests
    {
        public class JobInExecutingAssembly
        {
        }

        [TestMethod]
        public void TypeFullName_SearchAssemblySet_TypeFound()
        {
            var jobType = typeof(JobInExecutingAssembly).FullName;
            var anotherJobType = typeof(AnotherJobTask);
            var jobTypeSearchAssemblies = new List<Assembly> { Assembly.GetExecutingAssembly(), anotherJobType.Assembly };
            var jobTypeResolver = new JobTypeResolver(jobTypeSearchAssemblies);

            var result = jobTypeResolver.ResolveType(jobType);

            Assert.IsNotNull(result);
            Assert.AreEqual(typeof(JobInExecutingAssembly), result);

            result = jobTypeResolver.ResolveType(anotherJobType.FullName);

            Assert.IsNotNull(result);
            Assert.AreEqual(anotherJobType, result);
        }

        [TestMethod]
        public void AssemblyFullqualifiedName_NoSearchAssemblySet_TypeFound()
        {
            var jobType = typeof(JobInExecutingAssembly).AssemblyQualifiedName;

            var jobTypeResolver = new JobTypeResolver(null);

            var result = jobTypeResolver.ResolveType(jobType);

            Assert.IsNotNull(result);
            Assert.AreEqual(typeof(JobInExecutingAssembly), result);
        }
    }
}
