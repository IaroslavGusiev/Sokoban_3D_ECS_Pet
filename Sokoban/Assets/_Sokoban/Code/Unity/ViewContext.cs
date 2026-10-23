using UnityEngine;
using System.Threading;

namespace Sokoban.Presentation
{
    public sealed class ViewContext
    {
        public Transform Root { get; }
        public IViewProvider Provider { get; }
        public CancellationToken Token { get; }

        public ViewContext(
            Transform root, 
            IViewProvider provider, 
            CancellationToken token)
        {
            Root = root;
            Token = token;
            Provider = provider;
        }
    }
}
