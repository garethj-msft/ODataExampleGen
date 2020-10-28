namespace ODataExampleGen
{
    using System;
    using System.Diagnostics;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.OData;
    using ServiceLifetime = Microsoft.Extensions.DependencyInjection.ServiceLifetime;

    public class ContainerBuilder : IContainerBuilder
    {
        private readonly IServiceCollection services = new ServiceCollection();

        public IContainerBuilder AddService(
            Microsoft.OData.ServiceLifetime lifetime,
            Type serviceType,
            Type implementationType)
        {
            Debug.Assert(serviceType != null, "serviceType != null");
            Debug.Assert(implementationType != null, "implementationType != null");

            this.services.Add(new ServiceDescriptor(
                serviceType, implementationType, TranslateServiceLifetime(lifetime)));

            return this;
        }

        public IContainerBuilder AddService(
            Microsoft.OData.ServiceLifetime lifetime,
            Type serviceType,
            Func<IServiceProvider, object> implementationFactory)
        {
            Debug.Assert(serviceType != null, "serviceType != null");
            Debug.Assert(implementationFactory != null, "implementationFactory != null");

            this.services.Add(new ServiceDescriptor(
                serviceType, implementationFactory, TranslateServiceLifetime(lifetime)));

            return this;
        }

        public IServiceProvider BuildContainer()
        {
            return this.services.BuildServiceProvider();
        }

        private static ServiceLifetime TranslateServiceLifetime(
            Microsoft.OData.ServiceLifetime lifetime)
        {
            switch (lifetime)
            {
                case Microsoft.OData.ServiceLifetime.Scoped:
                    return ServiceLifetime.Scoped;
                case Microsoft.OData.ServiceLifetime.Singleton:
                    return ServiceLifetime.Singleton;
                default:
                    return ServiceLifetime.Transient;
            }
        }
    }
}
