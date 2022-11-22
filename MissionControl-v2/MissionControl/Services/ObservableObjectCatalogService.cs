using System.Diagnostics.CodeAnalysis;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Services
{
    public static class ObservableObjectCatalogServiceExtension
    {
        public static void AddObservableObjectCatalogService(this IServiceCollection services)
        {
            services.AddSingleton<ObservableObjectCatalogService>();
        }
    }

    /// <summary>
    /// Service that has for objective to get <see cref="ObservableObjectServiceBase"/> accessible through their name
    /// instead of their types.
    /// </summary>
    public class ObservableObjectCatalogService
    {
        public ObservableObjectCatalogService(ILogger<ObservableObjectCatalogService> logger)
        {
            m_Logger = logger;
        }

        public void AddObservableObject(ObservableObjectServiceBase toAdd)
        {
            m_Dictionary.Add(toAdd.Name, toAdd);
        }

        public bool TryGetValue(string name, [MaybeNullWhen(false)] out ObservableObjectServiceBase observableObject)
        {
            return m_Dictionary.TryGetValue(name, out observableObject);
        }

        // ReSharper disable once NotAccessedField.Local
        readonly ILogger m_Logger;
        readonly Dictionary<string, ObservableObjectServiceBase> m_Dictionary = new();
    }
}
