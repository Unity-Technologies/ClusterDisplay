using System;
using System.Diagnostics.CodeAnalysis;

namespace Unity.ClusterDisplay.Utils
{
    /// <summary>
    /// A method for controlling global access to a non-Singleton non-static class.
    /// </summary>
    /// <remarks>
    /// Allows you to register an object as a globally-accessible service provider.
    /// Avoids some of the issues with static and Singleton classes by giving you more fine-grained control.
    /// Tip: use an interface as the type argument to maximally decouple the service implementation
    /// from the service consumers.
    /// </remarks>
    static class ServiceLocator
    {
        /// <summary>
        /// Gets the service provider for type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The service type.</typeparam>
        /// <returns>The object providing the service.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if no service provider was specified for type <typeparamref name="T"/> (using
        /// the <see cref="Provide{T}"/> method).
        /// </exception>
        public static T Get<T>() where T : class => ServiceProvider<T>.Service ??
            throw new InvalidOperationException($"A service of type {typeof(T)} has not been provided");

        /// <summary>
        /// Tries to get the service provider for type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="service"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public static bool TryGet<T>([NotNullWhen(true)] out T service) where T : class =>
            (service = ServiceProvider<T>.Service) != null;

        /// <summary>
        /// Specify the service provider for type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="provider">Object providing the service. Can be <see langword="null"/> to clear the provider.</param>
        /// <typeparam name="T">The service type.</typeparam>
        public static void Provide<T>(T provider) where T : class => ServiceProvider<T>.Service = provider;

        public static void Withdraw<T>() where T : class => ServiceProvider<T>.Service = null;

        static class ServiceProvider<T> where T : class
        {
            public static T Service { get; set; }
        }
    }
}
