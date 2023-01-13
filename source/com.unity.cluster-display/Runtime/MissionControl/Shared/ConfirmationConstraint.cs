using System;
using JetBrains.Annotations;

namespace Unity.ClusterDisplay.MissionControl
{
    /// <summary>
    /// Type of confirmation.
    /// </summary>
    public enum ConfirmationType
    {
        /// <summary>
        /// This is an informative warning, either way consequences are minor.
        /// </summary>
        Informative,
        /// <summary>
        /// This is a confirmation about an operation that can have some negative impact.
        /// </summary>
        Warning,
        /// <summary>
        /// This is a confirmation about something that could definitely have critical consequences, user must know
        /// what he's doing...
        /// </summary>
        Danger
    }

    /// <summary>
    /// Instruct the user interface that a confirmation must be done before sending the command.
    /// </summary>
    /// <remarks>Simplified version of standalone MissionControl project's class of the same name but designed to be
    /// compiled and working in Unity.  Only include the properties needed by ClusterDisplay's MissionControl
    /// integration.</remarks>
    public class ConfirmationConstraint : Constraint, IEquatable<ConfirmationConstraint>
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ConfirmationConstraint()
        {
            Type = ConstraintType.Confirmation;
        }

        /// <summary>
        /// Degree of danger of the operation we are trying to confirm.
        /// </summary>
        public ConfirmationType ConfirmationType { get; set; } = ConfirmationType.Warning;

        /// <summary>
        /// Title of the confirmation window.
        /// </summary>
        [CanBeNull]
        public string Title { get; set; }

        /// <summary>
        /// Full text of the confirmation question / message.
        /// </summary>
        public string FullText { get; set; } = "";

        /// <summary>
        /// Message displayed in the confirm button.
        /// </summary>
        [CanBeNull]
        public string ConfirmText { get; set; }

        /// <summary>
        /// Message displayed in the abort (in the sense of the user changed its mind and they do not want to execute
        /// the command) button.
        /// </summary>
        [CanBeNull]
        public string AbortText { get; set; }

        /// <inheritdoc/>
        public override Constraint DeepClone()
        {
            ConfirmationConstraint ret = new();
            ret.ConfirmationType = ConfirmationType;
            ret.Title = Title;
            ret.FullText = FullText;
            ret.ConfirmText = ConfirmText;
            ret.AbortText = AbortText;
            return ret;
        }

        public bool Equals(ConfirmationConstraint other)
        {
            return other != null &&
                other.GetType() == typeof(ConfirmationConstraint) &&
                other.ConfirmationType == ConfirmationType &&
                other.Title == Title &&
                other.FullText == FullText &&
                other.ConfirmText == ConfirmText &&
                other.AbortText == AbortText;
        }

        protected override bool EqualsOfSameType(Constraint other)
        {
            return Equals((ConfirmationConstraint)other);
        }
    }
}
