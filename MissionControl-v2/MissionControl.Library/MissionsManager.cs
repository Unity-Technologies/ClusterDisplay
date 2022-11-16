using System;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Information about a mission in the <see cref="MissionsManager"/>.
    /// </summary>
    /// <remarks>Not present yet in the class but it will eventually also contain the list of runtime parameters (and
    /// their values) and the list of panels.</remarks>
    public class MissionDetails: ISavedMissionSummary, IEquatable<MissionDetails>
    {
        /// <summary>
        /// Mission details identifier
        /// </summary>
        public Guid Identifier { get; set; }

        /// <summary>
        /// Short description of the saved mission.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the saved mission.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Launch configuration of the mission
        /// </summary>
        public LaunchConfiguration LaunchConfiguration { get; set; } = new();

        /// <summary>
        /// Creates a new <see cref="SavedMissionSummary"/> from this detailed mission description.
        /// </summary>
        /// <returns>The new <see cref="SavedMissionSummary"/>.</returns>
        public SavedMissionSummary NewSummary(DateTime saveTime)
        {
            SavedMissionSummary ret = new(Identifier);
            ret.CopySavedMissionSummaryProperties(this);
            ret.SaveTime = saveTime;
            ret.AssetId = LaunchConfiguration.AssetId;
            return ret;
        }

        /// <summary>
        /// Returns a complete independent copy of this (no data is be shared between the original and the clone).
        /// </summary>
        /// <returns></returns>
        public MissionDetails DeepClone()
        {
            MissionDetails ret = new();
            ret.Identifier = Identifier;
            ret.CopySavedMissionSummaryProperties(this);
            ret.LaunchConfiguration = LaunchConfiguration.DeepClone();
            return ret;
        }

        public bool Equals(MissionDetails? other)
        {
            if (other == null || other.GetType() != typeof(MissionDetails))
            {
                return false;
            }

            return Identifier == other.Identifier &&
                Name == other.Name &&
                Description == other.Description &&
                LaunchConfiguration.Equals(other.LaunchConfiguration);
        }
    }

    /// <summary>
    /// Object acting as the storage of all the saved missions and keeping a list of <see cref="SavedMissionSummary"/>.
    /// </summary>
    public class MissionsManager: IncrementalCollectionManager<SavedMissionSummary>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        public MissionsManager(ILogger logger): base (logger)
        {
        }

        /// <summary>
        /// Add / modify a saved mission.
        /// </summary>
        /// <param name="mission">New / updated <see cref="MissionDetails"/>.</param>
        /// <remarks>Add or modify depending on if <see cref="MissionDetails.Identifier"/> is a new identifier or one
        /// already present in the manager.</remarks>
        public async Task StoreAsync(MissionDetails mission)
        {
            using (var writeLock = await GetWriteLockAsync())
            {
                // Update the storage
                if (m_DetailsStorage.TryGetValue(mission.Identifier, out var previousSavedMission) &&
                    mission.Equals(previousSavedMission))
                {
                    // Nothing changed...
                    return;
                }
                m_DetailsStorage[mission.Identifier] = mission.DeepClone();

                // Update the external facing inventory catalog
                var missionSummary = mission.NewSummary(DateTime.Now);
                writeLock.Collection[mission.Identifier] = missionSummary;
            }
        }

        /// <summary>
        /// Returns all the information about the requested saved mission.
        /// </summary>
        /// <param name="identifier">Saved mission identifier (used when calling the <see cref="SaveAsync"/> method).</param>
        /// <exception cref="KeyNotFoundException">If no saved mission with that identifier is found.</exception>
        public async Task<MissionDetails> GetDetailsAsync(Guid identifier)
        {
            using (await GetLockedReadOnlyAsync())
            {
                return m_DetailsStorage[identifier].DeepClone();
            }
        }

        /// <summary>
        /// Delete the information about the requested saved mission.
        /// </summary>
        /// <param name="identifier">Identifier passed to the <see cref="SaveAsync"/> method.</param>
        /// <returns><c>true</c> if remove succeeded or <c>false</c> if there was no saved mission with that identifier.
        /// </returns>
        public async Task<bool> DeleteAsync(Guid identifier)
        {
            using (var writeLock = await GetWriteLockAsync())
            {
                if (m_DetailsStorage.Remove(identifier))
                {
                    writeLock.Collection.Remove(identifier);
                    return true;
                }
                else
                {
                    Debug.Assert(!writeLock.Collection.ContainsKey(identifier));
                    return false;
                }
            }
        }

        /// <summary>
        /// Save the state of the <see cref="MissionsManager"/> to the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="saveTo"><see cref="Stream"/> to save to.</param>
        public new async Task SaveAsync(Stream saveTo)
        {
            await using MemoryStream inMemorySerialized = new();

            using (var readlock = await GetLockedReadOnlyAsync())
            {
                SerializeData toSave = new();
                toSave.Details = m_DetailsStorage.Values.ToList();
                foreach (var details in toSave.Details)
                {
                    toSave.DetailsComplement.Add(new (readlock.Value[details.Identifier]));
                }
                await JsonSerializer.SerializeAsync(inMemorySerialized, toSave, Json.SerializerOptions);
            }
            inMemorySerialized.Position = 0;

            await inMemorySerialized.CopyToAsync(saveTo);
        }

        /// <summary>
        /// Loads the state of the <see cref="MissionsManager"/> from the given <see cref="Stream"/>.
        /// </summary>
        /// <param name="loadFrom"><see cref="Stream"/> to load from.</param>
        /// <exception cref="InvalidOperationException">If trying to load into a non empty manager.</exception>
        public new void Load(Stream loadFrom)
        {
            var saved = JsonSerializer.Deserialize<SerializeData>(loadFrom, Json.SerializerOptions);
            if (saved == null)
            {
                throw new NullReferenceException("Parsing missions manager serialized data resulted in a null object.");
            }
            if (saved.Details.Count != saved.DetailsComplement.Count)
            {
                throw new ArgumentException("Received data to load from is invalid: Details.Count != DetailsComplement.Count");
            }

            using (var writeLock = GetWriteLockAsync().Result)
            {
                if (m_DetailsStorage.Any())
                {
                    throw new InvalidOperationException($"Can only call Load on an empty {nameof(MissionsManager)}.");
                }

                List<SavedMissionSummary> catalogUpdateList = new(saved.Details.Count);
                for (int savedIndex = 0; savedIndex < saved.Details.Count; ++savedIndex)
                {
                    var missionDetails = saved.Details[savedIndex];
                    var complement = saved.DetailsComplement[savedIndex];
                    m_DetailsStorage[missionDetails.Identifier] = missionDetails;

                    SavedMissionSummary missionSummary = missionDetails.NewSummary(complement.SaveTime);
                    catalogUpdateList.Add(missionSummary);
                }

                // Prepare an incremental update (this is the fastest way to add everything as a single transaction into
                // m_Collection).
                IncrementalCollectionUpdate<SavedMissionSummary> incrementalUpdate = new();
                incrementalUpdate.UpdatedObjects = catalogUpdateList;
                writeLock.Collection.ApplyDelta(incrementalUpdate);
            }
        }

        /// <summary>
        /// Returns if an <see cref="Asset"/> with the given identifier is in use by at least one mission.
        /// </summary>
        /// <param name="assetId"><see cref="Asset"/>'s identifier.</param>
        /// <remarks>Not optimized (algorithmic complexity is O(n)), so method shouldn't be called too often or in time
        /// critical sections of the code.</remarks>
        public async Task<bool> IsAssetInUseAsync(Guid assetId)
        {
            using (await GetLockedReadOnlyAsync())
            {
                foreach (var mission in m_DetailsStorage.Values)
                {
                    if (mission.LaunchConfiguration.AssetId == assetId)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// Additional data saved for every MissionDetails when serializing / deserializing content of this class.
        /// </summary>
        /// <remarks>Creating this class for a single property might look like overkill but it is in case we need some
        /// new information in the future (making support for old versions of the persisted file much easier).</remarks>
        class MissionDetailsComplement
        {
            // ReSharper disable once UnusedMember.Local -> Used by JsonSerializer
            public MissionDetailsComplement() { }

            public MissionDetailsComplement(SavedMissionSummary savedSummary)
            {
                SaveTime = savedSummary.SaveTime;
            }

            public DateTime SaveTime { get; set; }
        }

        /// <summary>
        /// Data used for serializing / deserializing content of this class.
        /// </summary>
        class SerializeData
        {
            public List<MissionDetails> Details { get; set; } = new();
            public List<MissionDetailsComplement> DetailsComplement { get; set; } = new();
        }

        /// <summary>
        /// Storage of all the missions managed by the <see cref="MissionsManager"/>.
        /// </summary>
        readonly Dictionary<Guid, MissionDetails> m_DetailsStorage = new();
    }
}
