using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Radzen;
using Radzen.Blazor;
using Unity.ClusterDisplay.MissionControl.EngineeringUI.Services;
using Unity.ClusterDisplay.MissionControl.MissionControl;

namespace Unity.ClusterDisplay.MissionControl.EngineeringUI.Components
{
    public partial class MissionParameters: IDisposable
    {
        [Parameter]
        public Guid LaunchPadId { get; set; }

        [Inject]
        MissionParametersService MissionParametersService { get; set; } = default!;

        [Inject]
        MissionParametersDesiredValuesService DesiredValuesService { get; set; } = default!;
        [Inject]
        MissionParametersEffectiveValuesService EffectiveValuesService { get; set; } = default!;
        [Inject]
        ILogger<MissionParameters> Logger { get; set; } = default!;

        class Entry
        {
            public Entry(MissionParameters owner, MissionParameter definition)
            {
                m_Owner = owner;
                Definition = definition;
            }

            public MissionParameter Definition { get; }
            public MissionParameterValue? DesiredValue { get; set; }
            public MissionParameterValue? EffectiveValue { get; set; }

            MissionParameterValue? DesiredOrEffective => DesiredValue ?? EffectiveValue;

            public bool DesiredValueAsBoolean
            {
                get
                {
                    try
                    {
                        return DesiredOrEffective!.AsBoolean();
                    }
                    catch
                    {
                        return false;
                    }
                }
                set
                {
                    SetDesiredValueFromToSerialize(v => Convert.ToBoolean(v), value);
                }
            }

            public int DesiredValueAsInt32
            {
                get
                {
                    try
                    {
                        return DesiredOrEffective!.AsInt32();
                    }
                    catch
                    {
                        return 0;
                    }
                }
                set
                {
                    SetDesiredValueFromToSerialize(v => Convert.ToInt32(v), value);
                }
            }

            public float DesiredValueAsSingle
            {
                get
                {
                    try
                    {
                        return DesiredOrEffective!.AsSingle();
                    }
                    catch
                    {
                        return 0;
                    }
                }
                set
                {
                    SetDesiredValueFromToSerialize(v => Convert.ToSingle(v), value);
                }
            }

            public string DesiredValueAsString
            {
                get
                {
                    try
                    {
                        return DesiredOrEffective!.AsString();
                    }
                    catch
                    {
                        return "";
                    }
                }
                set
                {
                    SetDesiredValueFromToSerialize(v => Convert.ToString(v)!, value);
                }
            }

            void SetDesiredValueFromToSerialize(Func<object, object> objectConversion, object value)
            {
                DesiredValue ??= new(Guid.NewGuid()) { ValueIdentifier = Definition.ValueIdentifier };
                try
                {
                    object toSerialize = objectConversion(value);
                    DesiredValue.Value = JsonSerializer.SerializeToElement(toSerialize, Json.SerializerOptions);
                    DesiredValueValid = Definition.Constraint?.Validate(value) ?? true;
                    if (DesiredValueValid)
                    {
                        DesiredValueDirty = true;
                    }
                    m_Owner.HasEntryWithDesiredValueDirty = true;
                }
                catch (Exception)
                {
                    DesiredValueValid = false;
                }
            }
            public bool DesiredValueValid { get; set; } = true;
            public bool DesiredValueDirty { get; set; }

            public bool? EffectiveAsBoolean
            {
                get
                {
                    try
                    {
                        return EffectiveValue?.AsBoolean();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public int? EffectiveAsInt32
            {
                get
                {
                    try
                    {
                        return EffectiveValue?.AsInt32();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public float? EffectiveAsSingle
            {
                get
                {
                    try
                    {
                        return EffectiveValue?.AsSingle();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            public string? EffectiveAsString
            {
                get
                {
                    try
                    {
                        return EffectiveValue?.AsString();
                    }
                    catch
                    {
                        return null;
                    }
                }
            }

            readonly MissionParameters m_Owner;
        }

        bool HasEntryWithDesiredValueDirty { get; set; }

        List<Entry> Entries { get; } = new();

        RadzenDataGrid<Entry>? m_EntriesGrid;

        protected override void OnInitialized()
        {
            MissionParametersService.Collection.SomethingChanged += DesiredValuesChanged;
            DesiredValuesService.Collection.SomethingChanged += DesiredValuesChanged;
            EffectiveValuesService.Collection.SomethingChanged += EffectiveValuesChanged;

            RefreshEntries();

            _ = ProcessDirtyDesiredValues();
        }

        public void Dispose()
        {
            MissionParametersService.Collection.SomethingChanged -= DesiredValuesChanged;
            DesiredValuesService.Collection.SomethingChanged -= DesiredValuesChanged;
            EffectiveValuesService.Collection.SomethingChanged -= EffectiveValuesChanged;
            m_ProcessDirtyDesiredValuesTimer?.Dispose();
        }

        void DesiredValuesChanged(IReadOnlyIncrementalCollection obj)
        {
            RefreshEntries();
        }

        void EffectiveValuesChanged(IReadOnlyIncrementalCollection obj)
        {
            RefreshEntries();
        }

        void RefreshEntries()
        {
            // Index current entries
            Dictionary<string, Entry> currentEntries = new();
            foreach (var entry in Entries)
            {
                currentEntries[entry.Definition.ValueIdentifier] = entry;
            }

            // Ensure all MissionParameter have a corresponding entry
            // We don't show commands in the MissionParameters component. See MissionCommandComponent.
            var parameters = MissionParametersService.Collection.Values.Where(p =>
                p.Type != MissionParameterType.Command && p.Group == LaunchPadId.ToString());
            var desiredValues = IndexParameterValues(DesiredValuesService.Collection.Values);
            var effectiveValues = IndexParameterValues(EffectiveValuesService.Collection.Values);
            foreach (var parameter in parameters)
            {
                if (!currentEntries.TryGetValue(parameter.ValueIdentifier, out var entry))
                {
                    entry = new(this, parameter);
                    currentEntries.Add(parameter.ValueIdentifier, entry);
                }

                if (entry is {DesiredValueDirty: false, DesiredValueValid: true})
                {
                    desiredValues.TryGetValue(parameter.ValueIdentifier, out var desiredValue);
                    entry.DesiredValue = desiredValue;
                    entry.DesiredValueDirty = false;
                }

                effectiveValues.TryGetValue(parameter.ValueIdentifier, out var effectiveValue);
                entry.EffectiveValue = effectiveValue;
            }

            // Discard old entries
            HashSet<string> newParameters = new ();
            foreach (var newParameter in parameters)
            {
                newParameters.Add(newParameter.ValueIdentifier);
            }
            foreach (var entry in currentEntries.Values.ToList())
            {
                if (!newParameters.Contains(entry.Definition.ValueIdentifier))
                {
                    currentEntries.Remove(entry.Definition.ValueIdentifier);
                }
            }

            // Update the UI
            Entries.Clear();
            Entries.AddRange(currentEntries.Values);
            m_EntriesGrid?.Reload();
            StateHasChanged();
        }

        Dictionary<string, MissionParameterValue> IndexParameterValues(IEnumerable<MissionParameterValue> values)
        {
            Dictionary<string, MissionParameterValue> ret = new();
            foreach (var value in values)
            {
                ret[value.ValueIdentifier] = value;
            }
            return ret;
        }

        async Task ProcessDirtyDesiredValues()
        {
            try
            {
                m_ProcessDirtyDesiredValuesTimer = new(TimeSpan.FromMilliseconds(250));
                while (await m_ProcessDirtyDesiredValuesTimer.WaitForNextTickAsync())
                {
                    if (!HasEntryWithDesiredValueDirty)
                    {
                        continue;
                    }

                    foreach (var entry in Entries.ToArray())
                    {
                        if (!entry.DesiredValueDirty || entry.DesiredValue == null)
                        {
                            continue;
                        }

                        try
                        {
                            var toPut = entry.DesiredValue.DeepClone();
                            var putRet = await DesiredValuesService.PutAsync(toPut);
                            putRet.EnsureSuccessStatusCode();
                            if (toPut.Equals(entry.DesiredValue))
                            {
                                entry.DesiredValueDirty = false;
                                entry.DesiredValueValid = true;

                                DesiredValuesService.Collection.TryGetValue(toPut.Id, out var desiredValue);
                                entry.DesiredValue = desiredValue;
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogError(e, "Sending new value to MissionControl failed");
                        }
                    }

                    HasEntryWithDesiredValueDirty = Entries.Any(e => e.DesiredValueDirty);
                }
            }
            catch (Exception e)
            {
                Logger.LogError(e, "Unexpected exception sending updates");
            }
        }
        PeriodicTimer? m_ProcessDirtyDesiredValuesTimer;
    }
}
