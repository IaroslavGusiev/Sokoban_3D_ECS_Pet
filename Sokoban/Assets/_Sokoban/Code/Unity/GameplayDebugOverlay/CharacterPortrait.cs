using UnityEngine;
using UnityEngine.UIElements;

namespace Sokoban.Presentation
{
    [UxmlElement]
    public partial class CharacterPortrait : VisualElement
    {
        [UxmlAttribute]
        public SkinId Skin { get; set; }

        [UxmlAttribute]
        public Texture2D Portrait { get; set; }
    }
}
