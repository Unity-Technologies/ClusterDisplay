using System;
using JetBrains.Annotations;
using Newtonsoft.Json.Linq;

namespace Unity.ClusterDisplay.MissionControl.MissionControl
{
    /// <summary>
    /// Value of a <see cref="MissionParameter"/>.
    /// </summary>
    public class MissionParameterValue : IIncrementalCollectionObject, IEquatable<MissionParameterValue>
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
        [CanBeNull]
        public JToken Value { get; set; }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="bool"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public bool AsBoolean()
        {
            if (Value!.Type != JTokenType.Boolean)
            {
                throw new InvalidCastException($"JToken type invalid, expecting a Boolean but was {Value!.Type}");
            }
            return ((JValue)Value)!.Value<bool>();
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
            if (Value!.Type != JTokenType.Integer)
            {
                throw new InvalidCastException($"JToken type invalid, expecting an Integer but was {Value!.Type}");
            }
            return ((JValue)Value)!.Value<int>();
        }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="float"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public float AsSingle()
        {
            if (Value!.Type is not (JTokenType.Float or JTokenType.Integer))
            {
                throw new InvalidCastException($"JToken type invalid, expecting a Float but was {Value!.Type}");
            }
            return ((JValue)Value)!.Value<float>();
        }

        /// <summary>
        /// Parse <see cref="Value"/> as a <see cref="string"/>.
        /// </summary>
        /// <remarks>Will throw an exception if conversion fails.</remarks>
        public string AsString()
        {
            if (Value!.Type != JTokenType.String)
            {
                throw new InvalidCastException($"JToken type invalid, expecting an String but was {Value!.Type}");
            }
            return ((JValue)Value)!.Value<string>();
        }

        /// <summary>
        /// Returns if Value is actually set to null or a json that contains only null.
        /// </summary>
        public bool IsNull => Value == null || Value.Type == JTokenType.Null;

        /// <inheritdoc/>
        public void DeepCopyFrom(IIncrementalCollectionObject fromObject)
        {
            var from = (MissionParameterValue)fromObject;
            ValueIdentifier = from.ValueIdentifier;
            Value = from.Value;
        }

        public bool Equals(MissionParameterValue other)
        {
            return other != null &&
                Id == other.Id &&
                ValueIdentifier == other.ValueIdentifier &&
                JToken.DeepEquals(Value, other.Value);
        }
    }
}
