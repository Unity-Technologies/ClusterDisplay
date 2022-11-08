using System;
using Microsoft.Extensions.Logging;

namespace Unity.ClusterDisplay.MissionControl.MissionControl.Library
{
    /// <summary>
    /// Class of the object responsible for managing <see cref="LaunchComplex"/>.
    /// </summary>
    /// <remarks>I must admit, this class does not do much and could probably have been done all in the service, however
    /// having it split in a different class like this one allow reusing functionality of
    /// <see cref="IncrementalCollectionManager{T}"/>, is easier to test and ready to do more work in the future if
    /// <see cref="LaunchComplex"/> management requires more work to be done.</remarks>
    public class ComplexesManager: IncrementalCollectionManager<LaunchComplex>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="logger">Object used to send logging messages.</param>
        public ComplexesManager(ILogger logger) : base(logger)
        {
        }

        /// <summary>
        /// Add / modify a <see cref="LaunchComplex"/>.
        /// </summary>
        /// <param name="complex">New / updated <see cref="LaunchComplex"/>.</param>
        /// <exception cref="ArgumentException">Something bad found in <paramref name="complex"/>.</exception>
        public async Task PutAsync(LaunchComplex complex)
        {
            if (complex.HangarBay.Identifier != complex.Id)
            {
                throw new ArgumentException("complex.HangarBay.Identifier != complex.Id");
            }

            HashSet<Guid> identifierSet = new();
            HashSet<Uri> endpointSet = new();
            identifierSet.Add(complex.Id);
            endpointSet.Add(complex.HangarBay.Endpoint);
            foreach (var launchPad in complex.LaunchPads)
            {
                if (!identifierSet.Add(launchPad.Identifier))
                {
                    throw new ArgumentException($"{nameof(LaunchPad)} {launchPad.Identifier} has the same identifier " +
                        $"than another {nameof(LaunchPad)} or the {nameof(HangarBay)}.");
                }
                if (!endpointSet.Add(launchPad.Endpoint))
                {
                    throw new ArgumentException($"{nameof(LaunchPad)} {launchPad.Identifier} has the same endpoint " +
                        $"({launchPad.Endpoint}) than another {nameof(LaunchPad)} or the {nameof(HangarBay)}.");
                }
            }

            using var writeLock = await GetWriteLockAsync();

            // Before doing the modification, each complex added should be unique and point to a resource that is not
            // already present in the list...
            foreach (var currentComplex in writeLock.Collection.Values)
            {
                if (currentComplex.Id == complex.Id)
                {
                    continue;
                }

                if (identifierSet.Contains(currentComplex.HangarBay.Identifier))
                {
                    throw new ArgumentException($"Identifier {currentComplex.HangarBay.Identifier} is already " +
                        $"used by another {nameof(LaunchComplex)}.");
                }
                if (endpointSet.Contains(currentComplex.HangarBay.Endpoint))
                {
                    throw new ArgumentException($"Endpoint {currentComplex.HangarBay.Endpoint} is already used " +
                        $"by another {nameof(LaunchComplex)}.");
                }

                foreach (var launchpad in currentComplex.LaunchPads)
                {
                    if (identifierSet.Contains(launchpad.Identifier))
                    {
                        throw new ArgumentException($"Identifier {launchpad.Identifier} is already used by " +
                            $"another {nameof(LaunchComplex)}.");
                    }
                    if (endpointSet.Contains(launchpad.Endpoint))
                    {
                        throw new ArgumentException($"Endpoint {launchpad.Endpoint} is already used by another " +
                            $"{nameof(LaunchComplex)}.");
                    }
                }
            }

            // Now that we know everything is ok let's do the modifications
            writeLock.Collection[complex.Id] = complex;
        }

        /// <summary>
        /// Remove the <see cref="LaunchComplex"/> with the specified identifier.
        /// </summary>
        /// <param name="identifier"><see cref="LaunchComplex"/>'s identifier.</param>
        /// <returns><c>true</c> if remove succeeded or <c>false</c> if there was no asset with that identifier in the
        /// list of <see cref="Asset"/>s of the <see cref="AssetsManager"/>.  Any other problem removing will throw an
        /// exception.</returns>
        public async Task<bool> RemoveAsync(Guid identifier)
        {
            // First get the asset and remove it from the "known list" so that we do not have to keep m_Lock locked for
            // the whole removal process and so that it appear to be gone ASAP from the outside.
            using var writeLock = await GetWriteLockAsync();
            return writeLock.Collection.Remove(identifier);
        }
    }
}
