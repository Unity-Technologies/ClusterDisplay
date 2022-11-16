using System;
using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// A set of files, with customizable parameters, that can be launched inside mission control.  This set of files 
    /// was produced by some tool and has added to MissionControl list of assets.  Each of theses assets can then be 
    /// selected and configured to be launched.
    /// </summary>
    /// <remarks><see cref="Asset"/>s are the root of a group of multiple classes that all play together to represent 
    /// something that can be launched.  <see cref="Asset"/>s are kept in a list in mission control and stay in that
    /// list until someone deletes it.
    /// <br/><br/>Each <see cref="Asset"/> is composed of a list of <see cref="Launchable"/>s that represents something
    /// that can be launched on a launchpad.  So a single <see cref="Asset"/> can have multiple 
    /// <see cref="Launchable"/>s that can be executed on different types of launchpad.  For example, one
    /// <see cref="Launchable"/> can be composed of the files and parameters necessary to launch a cluster node part of
    /// the cluster display while another <see cref="Launchable"/> (of the same <see cref="Asset"/>) can be composed of
    /// the files and parameters necessary to launch the Unity editor that will connect to the cluster allowing to
    /// modify the content of the scene while it is running.
    /// <br/><br/>Each <see cref="Launchable"/> defines the list of files it needs through a list of
    /// <see cref="Payload"/>s (each <see cref="Payload"/> can be referenced by multiple <see cref="Launchable"/> of the
    /// same <see cref="Asset"/>).
    /// <br/><br/>A <see cref="Payload"/> is simply a named (through a string in the LaunchCatalog.json or through a
    /// <see cref="Guid"/> in MissionControl) list of <see cref="PayloadFile"/>.
    /// <br/><br/>A <see cref="PayloadFile"/> is a set of metadata about a file that is necessary to be present on the
    /// launchpad to be able to start a <see cref="Launchable"/>.  A key part of that metadata is the file blob
    /// identifier.
    /// <br/><br/>File blobs represents the bytes of a <see cref="PayloadFile"/> and are shared by all the
    /// <see cref="PayloadFile"/>s, <see cref="Payload"/>s, <see cref="Launchable"/>s and <see cref="Asset"/>s within 
    /// mission control.  So if two different assets have the same binary file then it will shared and will take space
    /// only once in the mission control storage.  As a consequence, file blobs are only removed when all the
    /// <see cref="Asset"/>s using it are removed from mission control.  As an effort to minimize storage requirements
    /// of mission control and speed up sending of data to the hangar bays working for the launchpads the file blobs
    /// are stored as compressed files (gzip).
    /// </remarks>
    public class Asset: IncrementalCollectionObject, IAssetBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object.</param>
        public Asset(Guid id): base(id)
        {
        }

        /// <summary>
        /// Short descriptive name of the asset.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Detailed description of the asset.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// <see cref="Launchable"/>s as found in LaunchCatalog.yaml (but with payload ids as GUID as opposed to
        /// arbitrary strings as in the launch catalog).
        /// </summary>
        public IEnumerable<Launchable> Launchables { get; set; } = Enumerable.Empty<Launchable>();

        /// <summary>
        /// Number of bytes used by all the files of this asset in the MissionControl storage.
        /// </summary>
        /// <remarks>Compressed size that does not take into account if file blobs are shared by multiple assets.
        /// </remarks>
        public long StorageSize { get; set; }

        public override IncrementalCollectionObject NewOfTypeWithId()
        {
            return new Asset(Id);
        }

        protected override void DeepCopyImp(IncrementalCollectionObject fromObject)
        {
            var from = (Asset)fromObject;
            Name = from.Name;
            Description = from.Description;
            // Cloning Launchables could imply a whole set of new methods to clone launchables and everything it
            // references.  However we shouldn't need to create / modify assets that often and it quite light in term of
            // processing, so let's go with the lazy serialize / deserialize trick.
            Launchables = JsonSerializer.Deserialize<IEnumerable<Launchable>>(
                JsonSerializer.Serialize(from.Launchables, Json.SerializerOptions), Json.SerializerOptions)!;
            StorageSize = from.StorageSize;
        }
    }
}
