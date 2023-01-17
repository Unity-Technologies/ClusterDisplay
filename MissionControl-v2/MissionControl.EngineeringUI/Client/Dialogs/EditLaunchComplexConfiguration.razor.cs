using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Dialogs
{
    public partial class EditLaunchComplexConfiguration
    {
        [Parameter]
        public Asset Asset { get; set; } = new(Guid.Empty);
        [Parameter]
        public LaunchComplex Complex { get; set; } = new (Guid.Empty);
        [Parameter]
        public LaunchComplexConfiguration ToEdit { get; set; } = new ();

        [Inject]
        DialogService DialogService { get; set; } = default!;

        protected override void OnInitialized()
        {
            Edited = ToEdit.DeepClone();
        }

        LaunchComplexConfiguration Edited { get; set; } = new ();
        MissionControl.LaunchPad? SelectedLaunchPad => m_SelectedLaunchPads?.FirstOrDefault();

        RadzenDataGrid<MissionControl.LaunchPad> m_LaunchpadsGrid = default!;
        IList<MissionControl.LaunchPad>? m_SelectedLaunchPads;

        IEnumerable<LaunchCatalog.LaunchParameter> LaunchParameters
        {
            get
            {
                HashSet<string> launchableNames = new();
                foreach (var launchPad in Complex.LaunchPads)
                {
                    var configuration = GetConfigurationForLaunchPad(launchPad);
                    var launchable = configuration.GetEffectiveLaunchable(Asset, launchPad);
                    if (launchable != null)
                    {
                        launchableNames.Add(launchable.Name);
                    }
                }
                var sortedLaunchableNames = launchableNames.OrderBy(n => n).ToList();
                if (!sortedLaunchableNames.SequenceEqual(m_LaunchablesNames))
                {
                    List<LaunchCatalog.LaunchParameter> launchableParameters = new();
                    foreach (var name in sortedLaunchableNames)
                    {
                        var launchable = Asset.Launchables.FirstOrDefault(l => l.Name == name);
                        if (launchable != null)
                        {
                            launchableParameters.AddRange(launchable.LaunchComplexParameters);
                        }
                    }
                    m_LaunchablesLaunchParameters = launchableParameters;
                }
                return m_LaunchablesLaunchParameters;
            }
        }
        List<LaunchParameterValue> LaunchParametersValue { get; set; } = new();

        async Task ConfigureLaunchPad()
        {
            var selected = SelectedLaunchPad;
            if (selected == null)
            {
                return;
            }

            var toEdit = GetConfigurationForLaunchPad(selected);

            await DialogService.OpenAsync<EditLaunchPadConfiguration>($"Configure {selected.Name}",
               new Dictionary<string, object>{ {"Asset", Asset}, {"LaunchPad", selected}, {"ToEdit", toEdit} },
               new DialogOptions() { Width = "50%", Height = "50%", Resizable = true, Draggable = true });
        }

        void OnOk()
        {
            ToEdit.DeepCopyFrom(Edited);
            DialogService.Close(true);
        }

        void OnCancel()
        {
            DialogService.Close(false);
        }

        /// <summary>
        /// Returns the <see cref="LaunchPadConfiguration"/> for the requested <see cref="MissionControl.LaunchPad"/>.
        /// </summary>
        /// <param name="launchPad">The <see cref="MissionControl.LaunchPad"/>.</param>
        LaunchPadConfiguration GetConfigurationForLaunchPad(MissionControl.LaunchPad launchPad)
        {
            var cfg = Edited.LaunchPads.FirstOrDefault(c => c.Identifier == launchPad.Identifier);
            if (cfg == null)
            {
                cfg = new() {Identifier = launchPad.Identifier};
                cfg.LaunchableName = launchPad.GetCompatibleLaunchables(Asset).Select(l => l.Name).FirstOrDefault("");
                Edited.LaunchPads = Edited.LaunchPads.Append(cfg).ToList();
            }
            return cfg;
        }

        /// <summary>
        /// Name of the <see cref="Launchable"/> of the <see cref="LaunchPad"/>s.
        /// </summary>
        IEnumerable<string> m_LaunchablesNames = Enumerable.Empty<string>();
        /// <summary>
        /// <see cref="LaunchCatalog.LaunchParameter"/> of the <see cref="Launchable"/> of the <see cref="LaunchPad"/>s.
        /// </summary>
        IEnumerable<LaunchCatalog.LaunchParameter> m_LaunchablesLaunchParameters = Enumerable.Empty<LaunchCatalog.LaunchParameter>();
    }
}
