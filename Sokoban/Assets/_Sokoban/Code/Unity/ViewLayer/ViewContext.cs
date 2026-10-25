using UnityEngine;
using System.Threading;

namespace Sokoban.Presentation
{
    public sealed class ViewContext
    {
        public Transform Root { get; }
        public IViewProvider Provider { get; }
        public CancellationToken Token { get; }
        public float MoveDurationSeconds { get; }
        public BoxGoalFeedbackOptions GoalFeedback { get; }

        public ViewContext(
            Transform root, 
            IViewProvider provider, 
            CancellationToken token,
            float moveDurationSeconds,
            BoxGoalFeedbackOptions goalFeedback = default)
        {
            Root = root;
            Token = token;
            Provider = provider;
            GoalFeedback = goalFeedback;
            MoveDurationSeconds = moveDurationSeconds;
        }
    }
}
