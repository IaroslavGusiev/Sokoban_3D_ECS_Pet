using System;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    internal static class UiElements
    {
        public static T Require<T>(VisualElement scope, string name) where T : VisualElement
        {
            VisualElement element = scope.Q(name);
            
            if (element is T match)
            {
                return match;
            }

            string actual = element == null 
                ? "missing" 
                : element.GetType().Name;
            
            throw new InvalidOperationException($"UI element '{name}' must be {typeof(T).Name}; found {actual} in '{scope.name}'.");
        }

        public static void SetVisible(VisualElement element, bool visible) =>
            element.EnableInClassList(UiIds.Classes.Hidden, visible == false);
    }
}
