using System.Text.Json;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Value of a <see cref="MissionParameter"/>.
    /// </summary>
    public class MissionParameterValue: IIncrementalCollectionObject, IWithMissionParameterValueIdentifier,
        IEquatable<MissionParameterValue>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="id">Identifier of the object</param>
        public MissionParameterValue(Guid id)
        {
            Id = id;
        }

        /// <inheritdoc/>
        public Guid Id { get; }

        /// <summary>
        /// Case sensitive string used to find the corresponding <see cref="MissionParameter"/>.
        /// </summary>
        /// <remarks>No two MissionParameterValue should have the same valueIdentifier within
        /// currentMission/parametersDesiredValue or currentMission/parametersEffectiveValue.</remarks>
        public string ValueIdentifier { get; set; } = "";

        /// <summary>
        /// The value.
        /// </summary>
        /// <remarks>The value type will depend on the type of the corresponding <see cref="MissionParameter"/>.
        /// Because of that we cannot really parse it to a native type yet.  However this is not really a big problem
        /// as the actual value is not really manipulated by MissionControl itself, it just carries it around and make
        /// it available to whoever ask for it.<br/><br/>
        /// Can be <c>null</c> if the type of the parameter supports it.</remarks>
        public JsonElement? Value { get; set; }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="bool"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public bool AsBoolean()
        {
            return Value!.Value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new InvalidCastException($"JsonElement type invalid, expecting a Boolean but was {Value.Value.ValueKind}")
            };
        }

        /// <summary>
        /// Parse <see cref="Value"/> as an <see cref="Guid"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public Guid AsGuid()
        {
            return new Guid(AsString());
        }

        /// <summary>
        /// Parse <see cref="Value"/> as an <see cref="int"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public int AsInt32()
        {
            if (Value!.Value.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidCastException($"JsonElement type invalid, expecting a Number but was {Value.Value.ValueKind}");
            }
            return Value.Value.GetInt32();
        }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="float"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public float AsSingle()
        {
            if (Value!.Value.ValueKind != JsonValueKind.Number)
            {
                throw new InvalidCastException($"JsonElement type invalid, expecting a Number but was {Value.Value.ValueKind}");
            }
            return Value.Value.GetSingle();
        }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="string"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public string AsString()
        {
            if (Value!.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidCastException($"JToken type invalid, expecting an String but was {Value.Value.ValueKind}");
            }
            return Value.Value.GetString()!;
        }

        /// <summary>
        /// Returns if Value is actually set to null or a json that contains only null.
        /// </summary>
        public bool IsNull => Value == null || Value.Value.ValueKind == JsonValueKind.Null;

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (MissionParameterValue)fromObject;
            ValueIdentifier = from.ValueIdentifier;
            Value = from.Value;
        }

        public bool Equals(MissionParameterValue? other)
        {
            // Easy and quick part of the comparison
            if (other == null ||
                Id != other.Id ||
                ValueIdentifier != other.ValueIdentifier)
            {
                return false;
            }

            // Now the heavier part, comparing the value.  The easiest way is to compare the serialized version of both
            // values.  Might not be the fastest but it shouldn't be a bottle neck, so let's use the simple way for now.
            using MemoryStream thisStream = new();
            using Utf8JsonWriter thisJsonWriter = new(thisStream);
            using MemoryStream otherStream = new();
            using Utf8JsonWriter otherJsonWriter = new(otherStream);
            if (Value.HasValue)
            {
                Value.Value.WriteTo(thisJsonWriter);
                thisJsonWriter.Flush();
            }
            if (other.Value.HasValue)
            {
                other.Value.Value.WriteTo(otherJsonWriter);
                otherJsonWriter.Flush();
            }
            if (thisStream.Length != otherStream.Length)
            {
                return false;
            }
            return thisStream.ToArray().SequenceEqual(otherStream.ToArray());
        }
    }
}
