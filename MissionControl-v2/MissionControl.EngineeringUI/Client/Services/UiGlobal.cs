namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Services
{
    public static class UiGlobalExtensions
    {
        public static void AddUiGlobalService(this IServiceCollection services)
        {
            services.AddSingleton<UiGlobal>();
        }
    }

    /// <summary>
    /// Contains some application wide simple data (UI related, not business logic, this should be in more specialized
    /// services).
    /// </summary>
    public class UiGlobal
    {
        /// <summary>
        /// Is the side bar (with navigation menu) expanded.
        /// </summary>
        public bool SideBarExpanded { get; set; } = true;
    }
}
